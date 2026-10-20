using System;
using TTTXO.Core;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>How the current RPS series is played: against the local AI or two players on one device.</summary>
    public enum RpsMatchMode
    {
        SinglePlayer,
        LocalTwoPlayer
    }

    /// <summary>
    /// Session state shared by Character Select and the VS screen. It owns the best-of-five
    /// <see cref="RpsMatchController"/>, picks the AI move in single player, and exposes the latest
    /// round result so the VS screen can animate it without re-resolving anything.
    ///
    /// Keeping this outside the screen controllers is what lets a series survive the round trip
    /// Character Select -> VS -> Character Select. The AI picks from all three moves (including the
    /// player's), so draws are possible and the round is never a guaranteed win.
    /// </summary>
    public sealed class RpsSeries
    {
        private readonly RpsAiController _ai;
        private readonly Func<RpsMove> _aiMoveOverride;
        private RpsMatchController _match;
        private RpsAiDifficulty _difficulty;

        /// <param name="aiMoveOverride">
        /// Test seam: when set, the AI uses this move instead of <see cref="RpsAiController"/>.
        /// Production code leaves it null.
        /// </param>
        public RpsSeries(Func<RpsMove> aiMoveOverride = null, RpsAiController ai = null)
        {
            _aiMoveOverride = aiMoveOverride;
            _ai = ai ?? new RpsAiController();
        }

        public RpsMatchMode Mode { get; private set; }
        public RpsAiDifficulty Difficulty => _difficulty;
        public bool IsActive { get; private set; }

        public RpsMatchController Match => _match;
        public RpsMatchStatus Status => _match?.Status ?? RpsMatchStatus.NotStarted;
        public RpsRoundResult LastRound => _match?.LastRound;
        public int RoundNumber => _match?.RoundNumber ?? 0;
        public int ScoreOne => _match?.ScoreOne ?? 0;
        public int ScoreTwo => _match?.ScoreTwo ?? 0;
        public int WinPointsToWinSeries => _match?.WinPointsToWinSeries ?? RpsConfig.Default.WinPointsToWinSeries;

        /// <summary>True after Player One has submitted in Local 2P and the device should go to Player Two.</summary>
        public bool AwaitingPlayerTwo =>
            IsActive &&
            Mode == RpsMatchMode.LocalTwoPlayer &&
            _match != null &&
            _match.Status == RpsMatchStatus.AwaitingMoves &&
            _match.HasPendingMove(RpsPlayer.One) &&
            !_match.HasPendingMove(RpsPlayer.Two);

        /// <summary>Starts a fresh series. Scores and the last round are reset.</summary>
        public void Start(RpsMatchMode mode, RpsAiDifficulty difficulty = RpsAiDifficulty.Medium)
        {
            Mode = mode;
            _difficulty = difficulty;
            _match = new RpsMatchController(RpsConfig.Default);
            _match.StartSeries();
            IsActive = true;
        }

        /// <summary>Leaves the series without touching the match state. The next Start() resets it.</summary>
        public void End()
        {
            IsActive = false;
        }

        /// <summary>
        /// Submits Player One's move. In single player this also picks and submits the AI move and
        /// resolves the round, returning its result. In Local 2P it returns true with a null result -
        /// the round resolves after <see cref="TrySubmitPlayerTwo"/>.
        /// </summary>
        public bool TrySubmitPlayerOne(RpsMove move, out RpsRoundResult result)
        {
            result = null;

            if (!IsActive || _match == null || _match.Status != RpsMatchStatus.AwaitingMoves)
            {
                return false;
            }

            if (_match.HasPendingMove(RpsPlayer.One))
            {
                return false;
            }

            var submission = _match.SubmitMove(RpsPlayer.One, move);
            if (!submission.IsValid)
            {
                return false;
            }

            if (Mode == RpsMatchMode.LocalTwoPlayer)
            {
                return true;
            }

            var aiMove = _aiMoveOverride != null
                ? _aiMoveOverride()
                : _ai.ChooseMove(RpsAiParams.ForDifficulty(_difficulty));

            var aiSubmission = _match.SubmitMove(RpsPlayer.Two, aiMove);
            if (!aiSubmission.IsValid)
            {
                return false;
            }

            result = _match.ResolveRound();
            return true;
        }

        /// <summary>Submits Player Two's move in Local 2P and resolves the round.</summary>
        public bool TrySubmitPlayerTwo(RpsMove move, out RpsRoundResult result)
        {
            result = null;

            if (!AwaitingPlayerTwo)
            {
                return false;
            }

            var submission = _match.SubmitMove(RpsPlayer.Two, move);
            if (!submission.IsValid)
            {
                return false;
            }

            result = _match.ResolveRound();
            return true;
        }
    }
}
