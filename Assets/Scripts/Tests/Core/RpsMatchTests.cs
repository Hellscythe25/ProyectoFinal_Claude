using System;
using NUnit.Framework;
using TTTXO.Core;

namespace TTTXO.Core.Tests
{
    /// <summary>
    /// Rock-Paper-Scissors rules, the hidden-move round model and the series scoring. The core has
    /// no public mutators beyond <c>SubmitMove</c>/<c>ResolveRound</c>, so matches are driven through
    /// the same call sequence production code uses - including the 2P handoff order (One plays,
    /// device is handed over, Two plays) - and then inspected through the public surface.
    /// </summary>
    public class RpsMatchTests
    {
        [Test]
        public void Evaluate_AllNineCombinations_ProducesTheExpectedResult()
        {
            AssertDraw(RpsMove.Rock, RpsMove.Rock);
            AssertDraw(RpsMove.Paper, RpsMove.Paper);
            AssertDraw(RpsMove.Scissors, RpsMove.Scissors);

            AssertBeats(RpsMove.Rock, RpsMove.Scissors, RpsMove.Rock);
            AssertBeats(RpsMove.Rock, RpsMove.Paper, RpsMove.Paper);
            AssertBeats(RpsMove.Scissors, RpsMove.Paper, RpsMove.Scissors);

            AssertBeats(RpsMove.Scissors, RpsMove.Rock, RpsMove.Rock);
            AssertBeats(RpsMove.Paper, RpsMove.Rock, RpsMove.Paper);
            AssertBeats(RpsMove.Paper, RpsMove.Scissors, RpsMove.Scissors);
        }

        [Test]
        public void CounterTo_CycleOfThree_ReturnsTheWinningMove()
        {
            Assert.AreEqual(RpsMove.Paper, RpsRulesEngine.CounterTo(RpsMove.Rock));
            Assert.AreEqual(RpsMove.Scissors, RpsRulesEngine.CounterTo(RpsMove.Paper));
            Assert.AreEqual(RpsMove.Rock, RpsRulesEngine.CounterTo(RpsMove.Scissors));
        }

        [Test]
        public void SubmitMove_BeforeSeriesStarts_IsRejected()
        {
            var match = new RpsMatchController(RpsConfig.Default);

            var submission = match.SubmitMove(RpsPlayer.One, RpsMove.Rock);

            Assert.IsFalse(submission.IsValid);
            StringAssert.Contains("not accepting moves", submission.InvalidReason);
        }

        [Test]
        public void SubmitMove_OneSideOnly_KeepsTheRoundUnresolvedAndHidesTheMove()
        {
            var match = StartedMatch();

            match.SubmitMove(RpsPlayer.One, RpsMove.Scissors);

            Assert.IsTrue(match.HasPendingMove(RpsPlayer.One));
            Assert.IsFalse(match.HasPendingMove(RpsPlayer.Two));
            Assert.IsFalse(match.BothMovesSubmitted);
            Assert.Throws<InvalidOperationException>(() => match.ResolveRound());
        }

        [Test]
        public void SubmitMove_SameSideTwice_IsRejectedInsteadOfOverwriting()
        {
            var match = StartedMatch();

            match.SubmitMove(RpsPlayer.One, RpsMove.Rock);
            var second = match.SubmitMove(RpsPlayer.One, RpsMove.Scissors);

            Assert.IsFalse(second.IsValid);
            Assert.AreEqual(RpsMove.Rock, match.PendingMoveFor(RpsPlayer.One));
        }

        [Test]
        public void SubmitMove_AfterSeriesFinished_IsRejected()
        {
            var match = StartedMatch();
            PlayRound(match, RpsMove.Rock, RpsMove.Scissors);
            PlayRound(match, RpsMove.Rock, RpsMove.Scissors);
            PlayRound(match, RpsMove.Rock, RpsMove.Scissors);

            Assert.AreEqual(RpsMatchStatus.Finished, match.Status);

            var submission = match.SubmitMove(RpsPlayer.One, RpsMove.Paper);

            Assert.IsFalse(submission.IsValid);
        }

        [Test]
        public void ResolveRound_BothMovesIn_ScoresTheWinnerAndClearsThePendingMoves()
        {
            var match = StartedMatch();

            var result = PlayRound(match, RpsMove.Rock, RpsMove.Scissors);

            Assert.AreEqual(1, result.RoundNumber);
            Assert.AreEqual(RpsPlayer.One, result.RoundWinner);
            Assert.AreEqual(1, result.ScoreOne);
            Assert.AreEqual(0, result.ScoreTwo);
            Assert.IsFalse(result.IsSeriesOver);
            Assert.IsFalse(match.HasPendingMove(RpsPlayer.One), "The next round must start hidden.");
            Assert.IsFalse(match.HasPendingMove(RpsPlayer.Two), "The next round must start hidden.");
            Assert.AreEqual(RpsMatchStatus.AwaitingMoves, match.Status);
        }

        [Test]
        public void ResolveRound_SameMoves_IsADrawAndScoresNothing()
        {
            var match = StartedMatch();

            var result = PlayRound(match, RpsMove.Paper, RpsMove.Paper);

            Assert.IsTrue(result.Outcome.IsDraw);
            Assert.IsNull(result.RoundWinner);
            Assert.AreEqual(0, result.ScoreOne);
            Assert.AreEqual(0, result.ScoreTwo);
        }

        [Test]
        public void ResolveRound_PlayerTwoWins_ScoresPlayerTwo()
        {
            var match = StartedMatch();

            var result = PlayRound(match, RpsMove.Scissors, RpsMove.Rock);

            Assert.AreEqual(RpsPlayer.Two, result.RoundWinner);
            Assert.AreEqual(0, result.ScoreFor(RpsPlayer.One));
            Assert.AreEqual(1, result.ScoreFor(RpsPlayer.Two));
        }

        [Test]
        public void Series_FirstToThreeWins_EndsOnTheFifthScoringRound()
        {
            var match = StartedMatch();

            var results = new[]
            {
                PlayRound(match, RpsMove.Rock, RpsMove.Scissors),
                PlayRound(match, RpsMove.Rock, RpsMove.Paper),
                PlayRound(match, RpsMove.Paper, RpsMove.Rock),
                PlayRound(match, RpsMove.Scissors, RpsMove.Rock),
            };

            Assert.IsFalse(results[3].IsSeriesOver, "Two wins to two is not a finished best-of-five.");
            Assert.AreEqual(RpsMatchStatus.AwaitingMoves, match.Status);

            var decider = PlayRound(match, RpsMove.Rock, RpsMove.Scissors);

            Assert.IsTrue(decider.IsSeriesOver);
            Assert.AreEqual(RpsPlayer.One, decider.SeriesWinner);
            Assert.AreEqual(3, decider.ScoreOne);
            Assert.AreEqual(RpsMatchStatus.Finished, match.Status);
        }

        [Test]
        public void ResolveRound_WithDrawsBeforeTheWin_StillReachesThreeWins()
        {
            var match = StartedMatch();

            PlayRound(match, RpsMove.Rock, RpsMove.Rock);
            PlayRound(match, RpsMove.Paper, RpsMove.Paper);
            RpsRoundResult last = null;
            for (int i = 0; i < 3; i++)
            {
                last = PlayRound(match, RpsMove.Rock, RpsMove.Scissors);
            }

            Assert.IsTrue(last.IsSeriesOver);
            Assert.AreEqual(3, last.ScoreOne);
            Assert.AreEqual(0, last.ScoreTwo);
        }

        [Test]
        public void StartSeries_AfterAFinishedSeries_ResetsScoreAndRoundCounter()
        {
            var match = StartedMatch();
            for (int i = 0; i < 3; i++)
            {
                PlayRound(match, RpsMove.Rock, RpsMove.Scissors);
            }

            match.StartSeries();

            Assert.AreEqual(RpsMatchStatus.AwaitingMoves, match.Status);
            Assert.AreEqual(0, match.ScoreOne);
            Assert.AreEqual(0, match.ScoreTwo);
            Assert.AreEqual(0, match.RoundNumber);
            Assert.IsNull(match.LastRound);
        }

        [Test]
        public void RpsConfig_ZeroWinsToWin_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RpsConfig(0));
        }

        private static RpsMatchController StartedMatch()
        {
            var match = new RpsMatchController(RpsConfig.Default);
            match.StartSeries();
            return match;
        }

        private static RpsRoundResult PlayRound(RpsMatchController match, RpsMove moveOne, RpsMove moveTwo)
        {
            Assert.IsTrue(match.SubmitMove(RpsPlayer.One, moveOne).IsValid);
            Assert.IsTrue(match.SubmitMove(RpsPlayer.Two, moveTwo).IsValid);
            return match.ResolveRound();
        }

        private static void AssertDraw(RpsMove a, RpsMove b)
        {
            var outcome = RpsRulesEngine.Evaluate(a, b);
            Assert.IsTrue(outcome.IsDraw, $"{a} vs {b} should be a draw.");
        }

        private static void AssertBeats(RpsMove a, RpsMove b, RpsMove expectedWinner)
        {
            var outcome = RpsRulesEngine.Evaluate(a, b);
            Assert.IsFalse(outcome.IsDraw, $"{a} vs {b} should not be a draw.");
            Assert.AreEqual(expectedWinner, outcome.Winner, $"{a} vs {b} resolved wrong.");
            Assert.IsTrue(expectedWinner == a || expectedWinner == b,
                $"{expectedWinner} is not one of the two moves played ({a}, {b}).");
        }
    }
}
