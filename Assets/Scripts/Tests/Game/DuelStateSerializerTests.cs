using System;
using NUnit.Framework;
using TTTXO.Core;
using TTTXO.Core.Duel;
using TTTXO.Game.Duel;

namespace TTTXO.Game.Tests
{
    /// <summary>
    /// The document is the contract with Cloud Save, so these tests pin the exact JSON field names
    /// the flight plan asks for and make sure a damaged document is rejected instead of loaded as a
    /// half-valid duel.
    /// </summary>
    [TestFixture]
    public class DuelStateSerializerTests
    {
        private static DuelStateDto NewDuel()
        {
            return DuelStateDto.Create(
                "match-1", "p1", "Uno", "p2", "Dos",
                new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        }

        [Test]
        public void ToJson_EmitsTheContractFieldNames()
        {
            var json = DuelStateSerializer.ToJson(NewDuel());

            StringAssert.Contains("\"match_id\"", json);
            StringAssert.Contains("\"ronda_actual\"", json);
            StringAssert.Contains("\"estado_partida\"", json);
            StringAssert.Contains("\"turno_de\"", json);
            StringAssert.Contains("\"timestamp_ultimo_movimiento\"", json);
            StringAssert.Contains("\"jugador1\"", json);
            StringAssert.Contains("\"jugador2\"", json);
            StringAssert.Contains("\"nombre\"", json);
            StringAssert.Contains("\"hp\"", json);
            StringAssert.Contains("\"secuencia_actual\"", json);
        }

        [Test]
        public void RoundTrip_PreservesHpRoundAndSequences()
        {
            var duel = NewDuel();
            duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors }, out _);
            duel.jugador2.hp = 55;
            duel.ronda_actual = 4;
            duel.estado_partida = DuelRules.StateWaitingRival;

            var json = DuelStateSerializer.ToJson(duel);

            Assert.IsTrue(DuelStateSerializer.TryFromJson(json, out var loaded, out var error), error);
            Assert.AreEqual("match-1", loaded.match_id);
            Assert.AreEqual(4, loaded.Round);
            Assert.AreEqual(55, loaded.jugador2.hp);
            Assert.AreEqual(DuelRules.StateWaitingRival, loaded.State);
            Assert.AreEqual("Guerrero", loaded.jugador1.secuencia_actual[0]);
            Assert.AreEqual("Maga", loaded.jugador1.secuencia_actual[1]);
            Assert.AreEqual("Asesino", loaded.jugador1.secuencia_actual[2]);
        }

        [Test]
        public void TryFromJson_CreatesThreeSlots_WhenTheArrayIsMissing()
        {
            const string json = "{\"match_id\":\"m\",\"jugador1\":{\"id\":\"p1\"},\"jugador2\":{\"id\":\"p2\"}}";

            Assert.IsTrue(DuelStateSerializer.TryFromJson(json, out var duel, out var error), error);
            Assert.AreEqual(DuelRules.SlotsPerRound, duel.jugador1.secuencia_actual.Length);
            Assert.AreEqual(DuelRules.SlotsPerRound, duel.jugador2.secuencia_actual.Length);
            Assert.AreEqual(DuelRules.StateYourTurn, duel.State);
        }

        [Test]
        public void TryFromJson_RejectsADocumentWithoutTwoPlayers()
        {
            const string json = "{\"match_id\":\"m\"}";

            Assert.IsFalse(DuelStateSerializer.TryFromJson(json, out var duel, out var error));
            Assert.IsNull(duel);
            Assert.IsNotNull(error);
        }

        [Test]
        public void TryFromJson_RejectsGarbage()
        {
            Assert.IsFalse(DuelStateSerializer.TryFromJson("not a duel", out _, out var error));
            Assert.IsNotNull(error);
        }
    }
}
