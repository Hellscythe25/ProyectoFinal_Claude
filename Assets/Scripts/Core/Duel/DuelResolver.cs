using System;
using System.Collections.Generic;

namespace TTTXO.Core.Duel
{
    /// <summary>
    /// One of the three simultaneous clashes of a round: player one's move against player two's.
    /// </summary>
    public sealed class DuelClash
    {
        public DuelClash(int slot, RpsMove playerOneMove, RpsMove playerTwoMove, RpsPlayer? winner, int damage)
        {
            Slot = slot;
            PlayerOneMove = playerOneMove;
            PlayerTwoMove = playerTwoMove;
            Winner = winner;
            Damage = damage;
        }

        /// <summary>1-based slot inside the round (1..3).</summary>
        public int Slot { get; }

        public RpsMove PlayerOneMove { get; }

        public RpsMove PlayerTwoMove { get; }

        /// <summary>Winner of this clash, or null when both played the same class (0 damage).</summary>
        public RpsPlayer? Winner { get; }

        /// <summary>HP the loser of this clash loses (0 on a draw).</summary>
        public int Damage { get; }

        public bool IsDraw => Winner == null;
    }

    /// <summary>
    /// Everything the result screen needs after a round was resolved: the three clashes, the damage
    /// each side took, the HP left, and whether the duel ended (and why).
    /// </summary>
    public sealed class DuelRoundResult
    {
        public DuelRoundResult(
            int roundNumber,
            IReadOnlyList<DuelClash> clashes,
            int damageToPlayerOne,
            int damageToPlayerTwo,
            int playerOneHp,
            int playerTwoHp,
            bool isFinished,
            string winnerId,
            string endReason)
        {
            RoundNumber = roundNumber;
            Clashes = clashes ?? Array.Empty<DuelClash>();
            DamageToPlayerOne = damageToPlayerOne;
            DamageToPlayerTwo = damageToPlayerTwo;
            PlayerOneHp = playerOneHp;
            PlayerTwoHp = playerTwoHp;
            IsFinished = isFinished;
            WinnerId = winnerId ?? string.Empty;
            EndReason = endReason ?? string.Empty;
        }

        public int RoundNumber { get; }

        public IReadOnlyList<DuelClash> Clashes { get; }

        public int DamageToPlayerOne { get; }

        public int DamageToPlayerTwo { get; }

        public int PlayerOneHp { get; }

        public int PlayerTwoHp { get; }

        public bool IsFinished { get; }

        /// <summary>Winning player id, or empty when the duel is still running or ended in a draw.</summary>
        public string WinnerId { get; }

        public string EndReason { get; }

        public bool IsDraw => IsFinished && string.IsNullOrEmpty(WinnerId);
    }

    /// <summary>
    /// Resolves a full round (three clashes at once) using the existing
    /// <see cref="RpsRulesEngine"/> - the same rules the local VS screen already uses, so Guerrero
    /// beats Asesino, Asesino beats Maga and Maga beats Guerrero with no second implementation.
    ///
    /// This is the state machine the flight plan asks for: the client that completes the round
    /// downloads the document, calls <see cref="Resolve"/>, and uploads the mutated document. It is
    /// deliberately free of UnityEngine so the exact same call can later run inside Cloud Code as
    /// the authoritative host.
    /// </summary>
    public static class DuelResolver
    {
        /// <summary>True when both sides have locked in their three moves and the round can be scored.</summary>
        public static bool CanResolve(DuelStateDto state)
        {
            return state != null
                && !state.IsFinished
                && state.jugador1 != null
                && state.jugador2 != null
                && state.jugador1.HasFullSequence
                && state.jugador2.HasFullSequence;
        }

        /// <summary>
        /// Scores the round in place: applies clash damage, clears both sequences, advances the round
        /// (unless the duel ended) and stamps the document. Throws when the round is not resolvable,
        /// because silently no-oping here would corrupt a saved match.
        /// </summary>
        public static DuelRoundResult Resolve(DuelStateDto state, DateTimeOffset now)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.Normalize();

            if (state.IsFinished)
            {
                throw new InvalidOperationException("The duel is already finished.");
            }

            if (!CanResolve(state))
            {
                throw new InvalidOperationException(
                    "Both players must lock in three moves before the round can be resolved.");
            }

            int roundNumber = state.ronda_actual;
            var clashes = new List<DuelClash>(DuelRules.SlotsPerRound);
            int damageToOne = 0;
            int damageToTwo = 0;

            for (int slot = 0; slot < DuelRules.SlotsPerRound; slot++)
            {
                var oneMove = DuelMoveCodec.Parse(state.jugador1.secuencia_actual[slot], nameof(state.jugador1.secuencia_actual));
                var twoMove = DuelMoveCodec.Parse(state.jugador2.secuencia_actual[slot], nameof(state.jugador2.secuencia_actual));
                var outcome = RpsRulesEngine.Evaluate(oneMove, twoMove);

                if (outcome.IsDraw)
                {
                    clashes.Add(new DuelClash(slot + 1, oneMove, twoMove, null, 0));
                    continue;
                }

                bool playerOneWon = RpsRulesEngine.IsWinningMoveFor(outcome.Winner, oneMove);
                if (playerOneWon)
                {
                    damageToTwo += DuelRules.ClashDamage;
                    clashes.Add(new DuelClash(slot + 1, oneMove, twoMove, RpsPlayer.One, DuelRules.ClashDamage));
                }
                else
                {
                    damageToOne += DuelRules.ClashDamage;
                    clashes.Add(new DuelClash(slot + 1, oneMove, twoMove, RpsPlayer.Two, DuelRules.ClashDamage));
                }
            }

            state.jugador1.hp = Math.Max(0, state.jugador1.hp - damageToOne);
            state.jugador2.hp = Math.Max(0, state.jugador2.hp - damageToTwo);

            var (winnerId, endReason) = DecideEnd(state, roundNumber);
            bool finished = !string.IsNullOrEmpty(endReason);

            if (finished)
            {
                state.estado_partida = DuelRules.StateFinished;
                state.turno_de = string.Empty;
                state.ganador = winnerId;
                state.motivo_fin = endReason;
            }
            else
            {
                state.ronda_actual = roundNumber + 1;
                state.estado_partida = DuelRules.StateYourTurn;
                state.turno_de = string.Empty;
            }

            state.timestamp_ultimo_movimiento = DuelTimestamp.ToIso(now);
            state.jugador1.ClearSequence();
            state.jugador2.ClearSequence();

            return new DuelRoundResult(
                roundNumber,
                clashes,
                damageToOne,
                damageToTwo,
                state.jugador1.hp,
                state.jugador2.hp,
                finished,
                winnerId,
                endReason);
        }

        /// <summary>
        /// Decides whether the duel is over. A KO wins it; if nobody died, the round cap ends it on
        /// HP (equal HP is a draw). Returns an empty reason while the duel continues.
        /// </summary>
        private static (string WinnerId, string EndReason) DecideEnd(DuelStateDto state, int roundNumber)
        {
            bool oneDown = state.jugador1.hp <= 0;
            bool twoDown = state.jugador2.hp <= 0;

            if (oneDown && twoDown)
            {
                return (string.Empty, DuelRules.EndDraw);
            }

            if (oneDown)
            {
                return (state.jugador2.id, DuelRules.EndKo);
            }

            if (twoDown)
            {
                return (state.jugador1.id, DuelRules.EndKo);
            }

            if (roundNumber >= DuelRules.MaxRounds)
            {
                if (state.jugador1.hp == state.jugador2.hp)
                {
                    return (string.Empty, DuelRules.EndDraw);
                }

                return (state.jugador1.hp > state.jugador2.hp ? state.jugador1.id : state.jugador2.id,
                    DuelRules.EndRoundLimit);
            }

            return (string.Empty, string.Empty);
        }
    }
}
