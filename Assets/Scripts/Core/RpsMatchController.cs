using System;

namespace TTTXO.Core
{
    /// <summary>
    /// Lifecycle of a Rock-Paper-Scissors match (a series of rounds).
    /// </summary>
    public enum RpsMatchStatus
    {
        /// <summary>Created but no series started yet.</summary>
        NotStarted,

        /// <summary>A series is running and at least one side has not submitted its move for the current round.</summary>
        AwaitingMoves,

        /// <summary>Both sides have submitted and the round is scored (or the series is over).</summary>
        Finished
    }

    /// <summary>
    /// Series configuration: how many round wins a side needs to take the series.
    /// </summary>
    public sealed class RpsConfig
    {
        public RpsConfig(int winPointsToWinSeries)
        {
            if (winPointsToWinSeries < 1)
                throw new ArgumentOutOfRangeException(nameof(winPointsToWinSeries), winPointsToWinSeries,
                    "A series must be winnable with at least one round.");

            WinPointsToWinSeries = winPointsToWinSeries;
        }

        public int WinPointsToWinSeries { get; }

        /// <summary>Best of five, the default the GDD asks for.</summary>
        public static RpsConfig Default => new RpsConfig(3);
    }

    /// <summary>
    /// Scored result of one round, including the running series score after that round.
    /// </summary>
    public sealed class RpsRoundResult
    {
        internal RpsRoundResult(
            int roundNumber,
            RpsMove moveOne,
            RpsMove moveTwo,
            RpsPlayer? roundWinner,
            RpsOutcome outcome,
            int scoreOne,
            int scoreTwo,
            bool isSeriesOver)
        {
            RoundNumber = roundNumber;
            MoveOne = moveOne;
            MoveTwo = moveTwo;
            RoundWinner = roundWinner;
            Outcome = outcome;
            ScoreOne = scoreOne;
            ScoreTwo = scoreTwo;
            IsSeriesOver = isSeriesOver;
        }

        /// <summary>1-based index of the round this result closes.</summary>
        public int RoundNumber { get; }

        public RpsMove MoveOne { get; }

        public RpsMove MoveTwo { get; }

        /// <summary>Which side won the round, or null when the round was a draw.</summary>
        public RpsPlayer? RoundWinner { get; }

        public RpsOutcome Outcome { get; }

        /// <summary>Series score after this round, from <see cref="RpsPlayer.One"/>'s perspective.</summary>
        public int ScoreOne { get; }

        /// <summary>Series score after this round, from <see cref="RpsPlayer.Two"/>'s perspective.</summary>
        public int ScoreTwo { get; }

        /// <summary>True when a side reached <see cref="RpsConfig.WinPointsToWinSeries"/> with this round.</summary>
        public bool IsSeriesOver { get; }

        /// <summary>Score of a given side after this round.</summary>
        public int ScoreFor(RpsPlayer player) => player == RpsPlayer.One ? ScoreOne : ScoreTwo;

        /// <summary>Winner of the whole series, or null while it is undecided.</summary>
        public RpsPlayer? SeriesWinner => IsSeriesOver ? SeriesWinnerFromScore() : null;

        private RpsPlayer? SeriesWinnerFromScore()
        {
            if (ScoreOne > ScoreTwo) return RpsPlayer.One;
            if (ScoreTwo > ScoreOne) return RpsPlayer.Two;
            return null;
        }
    }

    /// <summary>
    /// Outcome of a <see cref="RpsMatchController.SubmitMove"/> call.
    /// </summary>
    public sealed class RpsMoveSubmission
    {
        internal RpsMoveSubmission(bool isValid, string invalidReason, RpsMatchStatus statusAfterSubmission)
        {
            IsValid = isValid;
            InvalidReason = invalidReason;
            StatusAfterSubmission = statusAfterSubmission;
        }

        public bool IsValid { get; }

        /// <summary>Why the submission was rejected, or null when it was accepted.</summary>
        public string InvalidReason { get; }

        public RpsMatchStatus StatusAfterSubmission { get; }
    }

    /// <summary>
    /// Pure match logic for Rock-Paper-Scissors: a series of simultaneous-move rounds where each
    /// side's choice stays hidden until both are in. Deliberately free of UnityEngine so Cloud Code
    /// can run the exact same code as the authoritative online host - see Docs/03-Arquitectura-UGS-TicTacToe.md
    /// (server-authoritative rules) and the RPS contract in Docs/05-UI-Pantallas-TicTacToe.md.
    ///
    /// The 2P local flow drives it as: submit for One, hand the device over, submit for Two,
    /// then <see cref="ResolveRound"/>. The online flow does the same with the moves going through
    /// Cloud Code instead of the local UI.
    /// </summary>
    public sealed class RpsMatchController
    {
        private readonly RpsConfig _config;
        private RpsMatchStatus _status;
        private RpsMove? _pendingMoveOne;
        private RpsMove? _pendingMoveTwo;
        private int _scoreOne;
        private int _scoreTwo;
        private int _roundNumber;

        public RpsMatchController(RpsConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _status = RpsMatchStatus.NotStarted;
        }

        public RpsMatchStatus Status => _status;

        /// <summary>Points <see cref="RpsPlayer.One"/> needs to take the series.</summary>
        public int WinPointsToWinSeries => _config.WinPointsToWinSeries;

        /// <summary>1-based number of the round currently being played.</summary>
        public int RoundNumber => _roundNumber;

        public int ScoreOne => _scoreOne;

        public int ScoreTwo => _scoreTwo;

        /// <summary>Series score for a given side.</summary>
        public int ScoreFor(RpsPlayer player) => player == RpsPlayer.One ? _scoreOne : _scoreTwo;

        /// <summary>
        /// The move a side has locked in for the current round, or null when it has not played yet.
        /// In the 2P local flow this is only ever read after the handoff, so the waiting side's
        /// choice stays hidden.
        /// </summary>
        public RpsMove? PendingMoveFor(RpsPlayer player)
        {
            return player == RpsPlayer.One ? _pendingMoveOne : _pendingMoveTwo;
        }

        public bool HasPendingMove(RpsPlayer player) => PendingMoveFor(player).HasValue;

        /// <summary>True once both sides have played and <see cref="ResolveRound"/> can score the round.</summary>
        public bool BothMovesSubmitted => _pendingMoveOne.HasValue && _pendingMoveTwo.HasValue;

        /// <summary>Result of the most recently resolved round, or null before the first one.</summary>
        public RpsRoundResult? LastRound { get; private set; }

        /// <summary>Starts a series and resets the score. Any move locked in from a previous series is dropped.</summary>
        public void StartSeries()
        {
            _scoreOne = 0;
            _scoreTwo = 0;
            _roundNumber = 0;
            _pendingMoveOne = null;
            _pendingMoveTwo = null;
            LastRound = null;
            _status = RpsMatchStatus.AwaitingMoves;
        }

        /// <summary>
        /// Locks in one side's move for the current round. Re-submitting for the same side is
        /// rejected rather than overwriting, so a stray double-tap cannot change a choice that is
        /// already hidden from the other player.
        /// </summary>
        public RpsMoveSubmission SubmitMove(RpsPlayer player, RpsMove move)
        {
            if (_status != RpsMatchStatus.AwaitingMoves)
                return Rejected($"The match is not accepting moves (status {_status}).");

            if (!Enum.IsDefined(typeof(RpsMove), move))
                return Rejected($"Unknown move '{move}'.");

            if (HasPendingMove(player))
                return Rejected($"{player} has already played this round.");

            if (player == RpsPlayer.One)
                _pendingMoveOne = move;
            else
                _pendingMoveTwo = move;

            if (BothMovesSubmitted)
                _status = RpsMatchStatus.Finished;

            return new RpsMoveSubmission(true, null, _status);
        }

        /// <summary>
        /// Scores the current round and opens the next one. Only legal once
        /// <see cref="BothMovesSubmitted"/> is true; the moves are cleared afterwards so the next
        /// round starts hidden. When a side reaches the win target the series ends and further
        /// calls are rejected until <see cref="StartSeries"/> runs again.
        /// </summary>
        public RpsRoundResult ResolveRound()
        {
            if (!BothMovesSubmitted)
                throw new InvalidOperationException("Cannot resolve a round before both sides have played.");

            RpsMove moveOne = _pendingMoveOne.Value;
            RpsMove moveTwo = _pendingMoveTwo.Value;
            RpsOutcome outcome = RpsRulesEngine.Evaluate(moveOne, moveTwo);

            _roundNumber++;
            if (!outcome.IsDraw)
            {
                if (RpsRulesEngine.IsWinningMoveFor(outcome.Winner, moveOne))
                    _scoreOne++;
                else
                    _scoreTwo++;
            }

            RpsPlayer? roundWinner = outcome.IsDraw
                ? (RpsPlayer?)null
                : (RpsRulesEngine.IsWinningMoveFor(outcome.Winner, moveOne) ? RpsPlayer.One : RpsPlayer.Two);

            bool seriesOver = _scoreOne >= WinPointsToWinSeries || _scoreTwo >= WinPointsToWinSeries;

            LastRound = new RpsRoundResult(
                roundNumber: _roundNumber,
                moveOne: moveOne,
                moveTwo: moveTwo,
                roundWinner: roundWinner,
                outcome: outcome,
                scoreOne: _scoreOne,
                scoreTwo: _scoreTwo,
                isSeriesOver: seriesOver);

            _pendingMoveOne = null;
            _pendingMoveTwo = null;
            _status = seriesOver ? RpsMatchStatus.Finished : RpsMatchStatus.AwaitingMoves;

            return LastRound;
        }

        private RpsMoveSubmission Rejected(string reason)
        {
            return new RpsMoveSubmission(false, reason, _status);
        }
    }
}
