using NUnit.Framework;
using TTTXO.Core;
using TTTXO.Game.UI.Screens;

namespace TTTXO.Game.Tests
{
    /// <summary>
    /// Series-level rules that live above <see cref="RpsMatchController"/>: the AI must be allowed
    /// to tie, a tie must not end the series, and Local 2P must wait for Player Two before resolving.
    /// </summary>
    [TestFixture]
    public class RpsSeriesTests
    {
        [Test]
        public void SinglePlayer_ResolvesImmediately_AndUsesTheSelectedMove()
        {
            var series = new RpsSeries(() => RpsMove.Paper);
            series.Start(RpsMatchMode.SinglePlayer);

            Assert.IsTrue(series.TrySubmitPlayerOne(RpsMove.Rock, out var result));

            Assert.IsNotNull(result);
            Assert.AreEqual(RpsMove.Rock, result.MoveOne);
            Assert.AreEqual(RpsMove.Paper, result.MoveTwo);
            Assert.AreEqual(RpsPlayer.Two, result.RoundWinner);
        }

        [Test]
        public void SinglePlayer_AllowsDraws()
        {
            var series = new RpsSeries(() => RpsMove.Rock);
            series.Start(RpsMatchMode.SinglePlayer);

            Assert.IsTrue(series.TrySubmitPlayerOne(RpsMove.Rock, out var result));

            Assert.IsTrue(result.Outcome.IsDraw);
            Assert.AreEqual(0, result.ScoreOne);
            Assert.AreEqual(0, result.ScoreTwo);
            Assert.AreEqual(1, series.RoundNumber);
            Assert.IsTrue(series.IsActive, "a draw must continue the series");
        }

        [Test]
        public void SinglePlayer_FirstToThree_WinsTheSeries()
        {
            // Rock beats Scissors, so this AI configuration lets Player One win three rounds.
            var series = new RpsSeries(() => RpsMove.Scissors);
            series.Start(RpsMatchMode.SinglePlayer);

            RpsRoundResult result = null;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(series.TrySubmitPlayerOne(RpsMove.Rock, out result));
            }

            Assert.IsTrue(result.IsSeriesOver);
            Assert.AreEqual(3, result.ScoreOne);
            Assert.AreEqual(RpsPlayer.One, result.SeriesWinner);
            Assert.IsFalse(series.TrySubmitPlayerOne(RpsMove.Rock, out _), "a finished series rejects more moves");
        }

        [Test]
        public void LocalTwoPlayer_WaitsForPlayerTwo_ThenResolves()
        {
            var series = new RpsSeries();
            series.Start(RpsMatchMode.LocalTwoPlayer);

            Assert.IsTrue(series.TrySubmitPlayerOne(RpsMove.Rock, out var firstResult));
            Assert.IsNull(firstResult, "Player One's move alone must not resolve the round");
            Assert.IsTrue(series.AwaitingPlayerTwo);

            Assert.IsTrue(series.TrySubmitPlayerTwo(RpsMove.Rock, out var draw));
            Assert.IsTrue(draw.Outcome.IsDraw);
            Assert.IsFalse(series.AwaitingPlayerTwo);
            Assert.AreEqual(1, series.RoundNumber);
        }

        [Test]
        public void LocalTwoPlayer_RejectsPlayerTwoBeforePlayerOne()
        {
            var series = new RpsSeries();
            series.Start(RpsMatchMode.LocalTwoPlayer);

            Assert.IsFalse(series.TrySubmitPlayerTwo(RpsMove.Rock, out _));
            Assert.AreEqual(0, series.RoundNumber);
        }

        [Test]
        public void Start_ResetsScoresAndRound()
        {
            var series = new RpsSeries(() => RpsMove.Scissors);
            series.Start(RpsMatchMode.SinglePlayer);
            series.TrySubmitPlayerOne(RpsMove.Rock, out _);

            series.Start(RpsMatchMode.SinglePlayer);

            Assert.AreEqual(0, series.ScoreOne);
            Assert.AreEqual(0, series.ScoreTwo);
            Assert.AreEqual(0, series.RoundNumber);
            Assert.IsNull(series.LastRound);
            Assert.IsTrue(series.IsActive);
        }

        [Test]
        public void End_StopsAcceptingMoves()
        {
            var series = new RpsSeries(() => RpsMove.Paper);
            series.Start(RpsMatchMode.SinglePlayer);
            series.End();

            Assert.IsFalse(series.TrySubmitPlayerOne(RpsMove.Rock, out _));
        }
    }
}
