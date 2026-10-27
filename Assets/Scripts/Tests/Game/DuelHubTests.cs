using System;
using System.Collections.Generic;
using NUnit.Framework;
using TTTXO.Core;
using TTTXO.Core.Duel;
using TTTXO.Game.Duel;

namespace TTTXO.Game.Tests
{
    /// <summary>
    /// Async loop of the Hub: submit, wait, reopen, resolve, claim. These tests inject the clock and
    /// the store, so the ten-minute window and the "rival answers on next open" behaviour are
    /// deterministic instead of sleeping.
    /// </summary>
    [TestFixture]
    public class DuelHubTests
    {
        private const string LocalId = "p1";

        private DateTimeOffset _now;
        private MemoryStore _store;
        private DuelHub _hub;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            _store = new MemoryStore();
            _hub = new DuelHub(
                _store,
                LocalId,
                "Uno",
                () => TimeSpan.FromMinutes(10),
                () => _now,
                new RpsAiController(7));
        }

        [Test]
        public void CreatePracticeDuel_IsListedAtFullHp()
        {
            var duel = _hub.CreatePracticeDuel("Rival");

            Assert.IsNotNull(duel);
            Assert.AreEqual(LocalId, duel.jugador1.id);
            Assert.AreEqual(DuelRules.MaxHp, duel.jugador1.hp);
            Assert.AreEqual(DuelRules.MaxHp, duel.jugador2.hp);
            Assert.AreEqual(1, _hub.ActiveDuels.Count);
            Assert.IsTrue(_store.Items.ContainsKey(duel.match_id), "the new duel must be persisted");
        }

        [Test]
        public void Submit_LeavesTheDuelWaiting_AndHidesTheRivalSequence()
        {
            var duel = _hub.CreatePracticeDuel("Rival");

            var result = _hub.Submit(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });

            Assert.IsNull(result);
            Assert.IsTrue(_hub.IsWaitingOnRival());
            Assert.AreEqual(DuelRules.StateWaitingRival, duel.State);
            Assert.AreEqual(0, duel.jugador2.FilledSlots, "the rival's moves stay hidden until it answers");
        }

        [Test]
        public void Open_AfterSubmitting_LetsThePracticeRivalAnswerAndResolves()
        {
            var duel = _hub.CreatePracticeDuel("Rival");
            _hub.Submit(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });
            _now = _now.AddMinutes(1);

            bool resolved = _hub.Open(duel.match_id);

            Assert.IsTrue(resolved);
            Assert.IsNotNull(_hub.LastResult);
            Assert.AreEqual(2, duel.Round);
            Assert.AreEqual(0, duel.jugador1.FilledSlots);
            Assert.AreEqual(0, duel.jugador2.FilledSlots);
            Assert.IsFalse(_hub.IsWaitingOnRival());
        }

        [Test]
        public void ClaimVictory_IsRejectedBeforeTheWindow()
        {
            var duel = _hub.CreateIdleRivalDuel("Ausente");
            _hub.Submit(new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock });
            _now = _now.AddMinutes(9);

            Assert.IsFalse(_hub.ClaimVictory());
            Assert.IsFalse(duel.IsFinished);
        }

        [Test]
        public void ClaimVictory_WinsByAbandonmentAfterTheWindow()
        {
            var duel = _hub.CreateIdleRivalDuel("Ausente");
            _hub.Submit(new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock });
            _now = _now.AddMinutes(11);

            Assert.IsTrue(_hub.ClaimVictory());
            Assert.IsTrue(duel.IsFinished);
            Assert.AreEqual(LocalId, duel.WinnerId);
            Assert.AreEqual(DuelRules.EndAbandon, duel.EndReason);
        }

        [Test]
        public void TakeLastResult_ReturnsTheResolutionOnlyOnce()
        {
            var duel = _hub.CreatePracticeDuel("Rival");
            _hub.Submit(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });
            _hub.Open(duel.match_id);

            Assert.IsNotNull(_hub.TakeLastResult());
            Assert.IsNull(_hub.TakeLastResult());
        }

        [Test]
        public void Refresh_DropsFinishedDuelsFromTheHub()
        {
            var duel = _hub.CreateIdleRivalDuel("Ausente");
            _hub.Submit(new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock });
            _now = _now.AddMinutes(11);
            Assert.IsTrue(_hub.ClaimVictory());

            _hub.Refresh();

            Assert.AreEqual(0, _hub.ActiveDuels.Count);
            Assert.IsNull(_hub.Current);
        }

        private sealed class MemoryStore : IDuelStore
        {
            public readonly Dictionary<string, DuelStateDto> Items = new();

            public IReadOnlyList<DuelStateDto> LoadAll()
            {
                return new List<DuelStateDto>(Items.Values);
            }

            public void Save(DuelStateDto state)
            {
                Items[state.match_id] = state;
            }

            public void Delete(string matchId)
            {
                Items.Remove(matchId);
            }
        }
    }
}
