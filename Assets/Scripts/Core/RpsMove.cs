using System;

namespace TTTXO.Core
{
    /// <summary>
    /// A single Rock-Paper-Scissors choice. Moves are simultaneous: neither side sees the other's
    /// choice before the round is revealed, in local play (see the 2P handoff flow) or online
    /// (Cloud Code holds both moves and reveals them together).
    /// </summary>
    public enum RpsMove
    {
        Rock,
        Paper,
        Scissors
    }

    /// <summary>
    /// The two sides of a Rock-Paper-Scissors round. Named One/Two rather than reusing Tic-Tac-Toe's
    /// CellOwner.X/O: RPS has no board and no symbols, and the online contract must not leak
    /// Tic-Tac-Toe semantics into a game that has none.
    /// </summary>
    public enum RpsPlayer
    {
        One,
        Two
    }

    /// <summary>
    /// Result of comparing two moves. <see cref="Winner"/> is the winning <em>move</em>, not the
    /// winning player - map it back to a side with <see cref="RpsRoundResult.RoundWinner"/>.
    /// </summary>
    public sealed class RpsOutcome
    {
        public RpsOutcome(RpsMove winner, bool isDraw, string reason)
        {
            Winner = winner;
            IsDraw = isDraw;
            Reason = reason;
        }

        public RpsOutcome(RpsMove winner, bool isDraw)
            : this(winner, isDraw, null) { }

        public RpsMove Winner { get; }

        public bool IsDraw { get; }

        /// <summary>Human-readable reason, e.g. "Rock beats Scissors".</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Pure rules: given two moves, return the outcome. Exhaustive by construction - the three
    /// pairs above cover all nine combinations of two moves.
    /// </summary>
    public static class RpsRulesEngine
    {
        /// <summary>The move that each move defeats.</summary>
        public static RpsMove CounterTo(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => RpsMove.Paper,
                RpsMove.Paper => RpsMove.Scissors,
                RpsMove.Scissors => RpsMove.Rock,
                _ => throw new ArgumentOutOfRangeException(nameof(move), move, "Unknown RPS move.")
            };
        }

        /// <summary>Compares two moves and returns which one wins, or a draw.</summary>
        public static RpsOutcome Evaluate(RpsMove a, RpsMove b)
        {
            if (a == b)
                return new RpsOutcome(a, isDraw: true);

            if (CounterTo(b) == a)
                return new RpsOutcome(a, isDraw: false, reason: $"{a} beats {b}");

            return new RpsOutcome(b, isDraw: false, reason: $"{b} beats {a}");
        }

        /// <summary>
        /// True when <paramref name="winner"/> is the side that played <paramref name="move"/>.
        /// Used to turn a move-level outcome into a player-level result.
        /// </summary>
        public static bool IsWinningMoveFor(RpsMove winner, RpsMove move)
        {
            return winner == move;
        }
    }
}
