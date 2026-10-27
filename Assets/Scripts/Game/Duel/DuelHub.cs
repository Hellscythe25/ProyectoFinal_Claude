using System;
using System.Collections.Generic;
using TTTXO.Core;
using TTTXO.Core.Duel;
using UnityEngine;

namespace TTTXO.Game.Duel
{
    /// <summary>
    /// The Hub Central's model: the list of active duels plus the one currently open.
    ///
    /// It owns the whole async loop of Phase 3 - load the documents, let the local player lock in
    /// three moves, simulate the rival, resolve with <see cref="DuelResolver"/> and save again -
    /// against an <see cref="IDuelStore"/>, so the same class works with local files today and with
    /// Cloud Save once Phase 4 lands.
    ///
    /// The practice rival is the same <see cref="RpsAiController"/> the VS screen uses. It answers on
    /// the *next* open, not immediately, which is what makes "enviaste tu jugada y te fuiste" a real
    /// state of the document instead of a fake delay.
    /// </summary>
    public sealed class DuelHub
    {
        /// <summary>Rival id used by a practice duel (it answers on the next open).</summary>
        public const string PracticeRivalId = "rival-practica";

        /// <summary>Rival id used by the timeout demo: it never answers, so the claim can unlock.</summary>
        public const string IdleRivalId = "rival-inactivo";

        private readonly IDuelStore _store;
        private readonly RpsAiController _ai;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Func<TimeSpan> _timeoutProvider;
        private readonly List<DuelStateDto> _activeDuels = new();

        public DuelHub(
            IDuelStore store,
            string localPlayerId,
            string localPlayerName,
            Func<TimeSpan> timeoutProvider = null,
            Func<DateTimeOffset> clock = null,
            RpsAiController ai = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                throw new ArgumentException("The hub needs a local player id.", nameof(localPlayerId));
            }

            LocalPlayerId = localPlayerId;
            LocalPlayerName = string.IsNullOrWhiteSpace(localPlayerName) ? localPlayerId : localPlayerName;
            _timeoutProvider = timeoutProvider;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _ai = ai ?? new RpsAiController();
        }

        public string LocalPlayerId { get; }

        public string LocalPlayerName { get; }

        /// <summary>Active (not finished) duels, most recently touched first.</summary>
        public IReadOnlyList<DuelStateDto> ActiveDuels => _activeDuels;

        /// <summary>The duel the combat screen is showing, or null.</summary>
        public DuelStateDto Current { get; private set; }

        /// <summary>Resolution produced by the last <see cref="Open"/> or <see cref="Submit"/> call.</summary>
        public DuelRoundResult LastResult { get; private set; }

        /// <summary>
        /// Returns the pending resolution once and clears it, so re-entering the combat screen does
        /// not replay an animation the player already saw.
        /// </summary>
        public DuelRoundResult TakeLastResult()
        {
            var result = LastResult;
            LastResult = null;
            return result;
        }

        /// <summary>Inactivity window currently in force (10 minutes, or 2 in demo mode).</summary>
        public TimeSpan Timeout => _timeoutProvider?.Invoke() ?? DuelTimeout.Default;

        /// <summary>Reloads every document from the store and keeps the open duel in sync.</summary>
        public void Refresh()
        {
            var openId = Current?.match_id;

            _activeDuels.Clear();
            foreach (var document in _store.LoadAll())
            {
                document.Normalize();
                if (!document.IsFinished)
                {
                    _activeDuels.Add(document);
                }
            }

            // ISO 8601 UTC strings sort lexicographically in chronological order.
            _activeDuels.Sort((a, b) => string.CompareOrdinal(b.timestamp_ultimo_movimiento, a.timestamp_ultimo_movimiento));

            Current = string.IsNullOrEmpty(openId)
                ? null
                : _activeDuels.Find(document => document.match_id == openId);
        }

        /// <summary>Creates a duel against the practice rival and leaves it open.</summary>
        public DuelStateDto CreatePracticeDuel(string rivalName)
        {
            return CreateDuel(PracticeRivalId, rivalName);
        }

        /// <summary>
        /// Creates a duel whose rival never answers. Only used by the editor-only demo button, so the
        /// "Reclamar Victoria" flow can be seen without waiting for a real opponent.
        /// </summary>
        public DuelStateDto CreateIdleRivalDuel(string rivalName)
        {
            return CreateDuel(IdleRivalId, rivalName);
        }

        /// <summary>
        /// Opens a duel by id. If the local player already played and the rival is the practice bot,
        /// the bot answers here and the round is resolved, which is what the combat screen then
        /// animates. Returns true when <see cref="LastResult"/> holds a fresh resolution.
        /// </summary>
        public bool Open(string matchId)
        {
            LastResult = null;

            Current = _activeDuels.Find(document => document.match_id == matchId);
            if (Current == null)
            {
                Debug.LogWarning($"DuelHub: match '{matchId}' is not part of the active list.");
                return false;
            }

            if (Current.IsFinished)
            {
                return false;
            }

            if (IsPracticeRival(Current) && Current.jugador1.HasSubmitted && !Current.jugador2.HasSubmitted)
            {
                var me = Current.PlayerById(LocalPlayerId);
                var rival = Current.OpponentOf(LocalPlayerId);
                if (me != null && rival != null && me.HasSubmitted && !rival.HasSubmitted)
                {
                    SubmitRivalSequence(Current);
                    LastResult = Resolve(Current);
                    return LastResult != null;
                }
            }

            return false;
        }

        /// <summary>
        /// Locks in the local player's three moves. Returns a resolution when that submission also
        /// completed the round (only possible when the rival had already played), otherwise null -
        /// the document is now waiting on the rival.
        /// </summary>
        public DuelRoundResult Submit(IReadOnlyList<RpsMove> moves)
        {
            if (Current == null)
            {
                throw new InvalidOperationException("No duel is open.");
            }

            if (Current.IsFinished)
            {
                return null;
            }

            var me = Current.PlayerById(LocalPlayerId);
            if (me == null)
            {
                Debug.LogError("DuelHub: the open duel does not contain the local player.");
                return null;
            }

            if (!me.TrySetSequence(moves, out var error))
            {
                Debug.LogWarning($"DuelHub: move submission rejected - {error}");
                return null;
            }

            Current.timestamp_ultimo_movimiento = DuelTimestamp.ToIso(_clock());
            Current.estado_partida = DuelRules.StateWaitingRival;
            Current.turno_de = Current.OpponentIdOf(LocalPlayerId);
            Save(Current);

            if (!DuelResolver.CanResolve(Current))
            {
                return null;
            }

            LastResult = Resolve(Current);
            return LastResult;
        }

        /// <summary>
        /// Attempts to win by abandonment. The rule lives in <see cref="DuelTimeout"/> so the future
        /// Cloud Code host can re-validate it with the server clock.
        /// </summary>
        public bool ClaimVictory()
        {
            if (Current == null)
            {
                return false;
            }

            if (!DuelTimeout.TryClaimVictory(Current, LocalPlayerId, _clock(), Timeout))
            {
                return false;
            }

            Save(Current);
            return true;
        }

        /// <summary>True when the open duel is waiting on the rival (the local player already played).</summary>
        public bool IsWaitingOnRival()
        {
            if (Current == null || Current.IsFinished)
            {
                return false;
            }

            var me = Current.PlayerById(LocalPlayerId);
            var rival = Current.OpponentOf(LocalPlayerId);
            return me != null && rival != null && me.HasSubmitted && !rival.HasSubmitted;
        }

        private DuelStateDto CreateDuel(string rivalId, string rivalName)
        {
            var now = _clock();
            var duel = DuelStateDto.Create(
                Guid.NewGuid().ToString("N"),
                LocalPlayerId,
                LocalPlayerName,
                rivalId,
                rivalName,
                now);

            Save(duel);
            Current = duel;
            LastResult = null;

            if (!_activeDuels.Contains(duel))
            {
                _activeDuels.Insert(0, duel);
            }

            return duel;
        }

        private bool IsPracticeRival(DuelStateDto duel)
        {
            return duel.OpponentIdOf(LocalPlayerId) == PracticeRivalId;
        }

        private void SubmitRivalSequence(DuelStateDto duel)
        {
            var rival = duel.OpponentOf(LocalPlayerId);
            if (rival == null || rival.HasSubmitted)
            {
                return;
            }

            var moves = new RpsMove[DuelRules.SlotsPerRound];
            for (int i = 0; i < moves.Length; i++)
            {
                moves[i] = _ai.ChooseMove(RpsAiParams.ForDifficulty(RpsAiDifficulty.Easy));
            }

            rival.TrySetSequence(moves, out _);
        }

        private DuelRoundResult Resolve(DuelStateDto duel)
        {
            try
            {
                var result = DuelResolver.Resolve(duel, _clock());
                Save(duel);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DuelHub: could not resolve match '{duel.match_id}' - {ex.Message}");
                return null;
            }
        }

        private void Save(DuelStateDto duel)
        {
            _store.Save(duel);
        }
    }
}
