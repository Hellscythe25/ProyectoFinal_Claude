using System;

namespace TTTXO.Core
{
    /// <summary>
    /// Difficulty levels for the Rock-Paper-Scissors AI.
    /// </summary>
    public enum RpsAiDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>
    /// How the AI weights its three choices, which is the whole difficulty model for a
    /// simultaneous-move game.
    ///
    /// RPS moves are chosen at the same time and hidden, so no strategy can beat a uniform-random
    /// opponent - the expected value of any fixed move is 1/3 win, 1/3 loss, 1/3 draw. The only
    /// legitimate edge is to exploit a predictable opponent: a human who plays Rock most of the
    /// time can be met with Paper most of the time. Difficulty is therefore how sharply the AI
    /// weights its choices toward the counter of the human's most common move, and it is a
    /// preference, not a guarantee.
    ///
    /// The previous implementation took the opponent's move and played its counter, which only
    /// makes sense if the AI can see the choice before locking it in - not something local play
    /// or a fair online round allows. It was removed when the hidden-move round model landed.
    /// </summary>
    public sealed class RpsAiParams
    {
        public RpsAiParams(double rockWeight, double paperWeight, double scissorsWeight)
        {
            if (rockWeight <= 0 || paperWeight <= 0 || scissorsWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(rockWeight),
                    "Every move weight must be greater than zero.");

            RockWeight = rockWeight;
            PaperWeight = paperWeight;
            ScissorsWeight = scissorsWeight;
        }

        public double RockWeight { get; }

        public double PaperWeight { get; }

        public double ScissorsWeight { get; }

        /// <summary>
        /// Difficulty presets. Easy is uniform (a coin-flip opponent); Medium and Hard lean on the
        /// well-documented human bias towards Rock and weight Paper - its counter - higher.
        /// </summary>
        public static RpsAiParams ForDifficulty(RpsAiDifficulty difficulty)
        {
            return difficulty switch
            {
                RpsAiDifficulty.Easy => new RpsAiParams(1.0, 1.0, 1.0),
                RpsAiDifficulty.Medium => new RpsAiParams(0.9, 1.25, 0.85),
                RpsAiDifficulty.Hard => new RpsAiParams(0.7, 1.7, 0.6),
                _ => new RpsAiParams(1.0, 1.0, 1.0)
            };
        }
    }

    /// <summary>
    /// Local Rock-Paper-Scissors opponent. Draws from the weights in <see cref="RpsAiParams"/>,
    /// mirroring how AiPlayer uses AiParams for Tic-Tac-Toe, and never sees the human's move.
    /// </summary>
    public sealed class RpsAiController
    {
        private readonly Random _random;

        /// <summary>Constructor with a deterministic seed, for tests.</summary>
        public RpsAiController(int randomSeed)
        {
            _random = new Random(randomSeed);
        }

        /// <summary>Time-seeded constructor, for normal play.</summary>
        public RpsAiController() : this(Environment.TickCount) { }

        /// <summary>
        /// Picks a move from the configured weights. Deterministic for a given seed, which is what
        /// lets a replay or a test reproduce an AI match exactly.
        /// </summary>
        public RpsMove ChooseMove(RpsAiParams rpsAiParams)
        {
            if (rpsAiParams == null)
                throw new ArgumentNullException(nameof(rpsAiParams));

            double total = rpsAiParams.RockWeight + rpsAiParams.PaperWeight + rpsAiParams.ScissorsWeight;
            double roll = _random.NextDouble() * total;

            if (roll < rpsAiParams.RockWeight)
                return RpsMove.Rock;

            if (roll < rpsAiParams.RockWeight + rpsAiParams.PaperWeight)
                return RpsMove.Paper;

            return RpsMove.Scissors;
        }
    }
}
