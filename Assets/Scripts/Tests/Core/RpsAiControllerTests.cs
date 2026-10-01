using System.Collections.Generic;
using NUnit.Framework;
using TTTXO.Core;

namespace TTTXO.Core.Tests
{
    /// <summary>
    /// The RPS AI draws from <see cref="RpsAiParams"/> weights and never sees the human's move.
    /// These tests pin the two properties the rest of the game relies on: the same seed replays the
    /// same match (so an AI match is reproducible), and a difficulty is only a weighting, never a
    /// guarantee of beating a uniform-random opponent.
    /// </summary>
    public class RpsAiControllerTests
    {
        [Test]
        public void ChooseMove_SameSeed_ReplaysTheSameSequence()
        {
            var first = new RpsAiController(1234);
            var second = new RpsAiController(1234);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(
                    first.ChooseMove(RpsAiParams.ForDifficulty(RpsAiDifficulty.Hard)),
                    second.ChooseMove(RpsAiParams.ForDifficulty(RpsAiDifficulty.Hard)),
                    $"Move {i} diverged between two identically seeded controllers.");
            }
        }

        [Test]
        public void ChooseMove_Easy_PlaysEveryMoveAndIsNotBiased()
        {
            var counts = SampleMany(seed: 99, difficulty: RpsAiDifficulty.Easy, rounds: 3000);

            Assert.IsTrue(counts[(int)RpsMove.Rock] > 0, "Easy must be able to play Rock.");
            Assert.IsTrue(counts[(int)RpsMove.Paper] > 0, "Easy must be able to play Paper.");
            Assert.IsTrue(counts[(int)RpsMove.Scissors] > 0, "Easy must be able to play Scissors.");

            // Uniform weights must land near a third each, well outside that range otherwise.
            foreach (var count in counts.Values)
            {
                Assert.That(count, Is.InRange(800, 1200), "Easy should be close to uniform.");
            }
        }

        [Test]
        public void ChooseMove_Hard_LeansOnTheCounterOfTheHumanBias()
        {
            var counts = SampleMany(seed: 7, difficulty: RpsAiDifficulty.Hard, rounds: 3000);

            // Paper beats Rock, which is the move humans over-play, so Hard must favour Paper.
            Assert.Greater(counts[(int)RpsMove.Paper], counts[(int)RpsMove.Rock]);
            Assert.Greater(counts[(int)RpsMove.Paper], counts[(int)RpsMove.Scissors]);
        }

        [Test]
        public void ChooseMove_NullParams_Throws()
        {
            var ai = new RpsAiController(1);

            Assert.Throws<System.ArgumentNullException>(() => ai.ChooseMove(null));
        }

        [Test]
        public void AiParams_NonPositiveWeight_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RpsAiParams(0, 1, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RpsAiParams(1, -1, 1));
        }

        [Test]
        public void Hard_IsNeverBeatenByAUniformRandomOpponentOnAverage()
        {
            // Simultaneous moves have no dominant strategy: the point of the weighting is a small
            // edge against a biased human, not a win rate above 1/3 against randomness. This test
            // fails loudly if someone reintroduces a "counter the opponent's move" cheat, which
            // would take the AI close to 100%.
            var ai = new RpsAiController(4242);
            var opponent = new RpsAiController(2424);
            var aiParams = RpsAiParams.ForDifficulty(RpsAiDifficulty.Hard);
            var opponentParams = RpsAiParams.ForDifficulty(RpsAiDifficulty.Easy);

            int wins = 0;
            const int rounds = 4000;
            for (int i = 0; i < rounds; i++)
            {
                var aiMove = ai.ChooseMove(aiParams);
                var opponentMove = opponent.ChooseMove(opponentParams);
                var outcome = RpsRulesEngine.Evaluate(aiMove, opponentMove);

                if (!outcome.IsDraw && outcome.Winner == aiMove)
                {
                    wins++;
                }
            }

            double winRate = (double)wins / rounds;
            Assert.That(winRate, Is.LessThan(0.50), "A cheat-proof AI cannot approach a 50% win rate here.");
            Assert.That(winRate, Is.GreaterThan(0.25), "The weighting should still beat uniform noise.");
        }

        private static Dictionary<int, int> SampleMany(int seed, RpsAiDifficulty difficulty, int rounds)
        {
            var ai = new RpsAiController(seed);
            var aiParams = RpsAiParams.ForDifficulty(difficulty);
            var counts = new Dictionary<int, int>
            {
                { (int)RpsMove.Rock, 0 },
                { (int)RpsMove.Paper, 0 },
                { (int)RpsMove.Scissors, 0 }
            };

            for (int i = 0; i < rounds; i++)
            {
                counts[(int)ai.ChooseMove(aiParams)]++;
            }

            return counts;
        }
    }
}
