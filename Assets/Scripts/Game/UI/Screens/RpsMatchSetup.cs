using TTTXO.Core;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>Which way a Rock-Paper-Scissors match is being played.</summary>
    public enum RpsMatchMode
    {
        /// <summary>The human plays the local AI, best of five.</summary>
        SinglePlayer,

        /// <summary>Two humans share the device, passing it after each hidden move.</summary>
        LocalTwoPlayer,

        /// <summary>Two UGS players via Matchmaker + Cloud Code (Milestone pending - see RpsModeSelectScreenController).</summary>
        Online
    }

    /// <summary>
    /// Selection that lands a specific RPS match on the game screen. Produced by
    /// <see cref="RpsModeSelectScreenController"/> and consumed by <see cref="RpsGameScreenController"/>
    /// on every <c>SetUp</c>, so the two screens stay decoupled and a rematch can be started from the
    /// same configuration without re-picking the mode.
    /// </summary>
    public sealed class RpsMatchSetup
    {
        public RpsMatchSetup(RpsMatchMode mode, RpsAiDifficulty difficulty)
        {
            Mode = mode;
            Difficulty = difficulty;
        }

        public RpsMatchMode Mode { get; }

        /// <summary>Only meaningful in <see cref="RpsMatchMode.SinglePlayer"/>.</summary>
        public RpsAiDifficulty Difficulty { get; }
    }
}