using System;
using NUnit.Framework;
using TTTXO.Core;
using TTTXO.Core.Duel;

namespace TTTXO.Core.Tests
{
    /// <summary>
    /// Rules of the async duel: 15 damage per clash won, 0 on a draw, three clashes per round,
    /// 100 HP, and the abandonment window. These run without Unity because Cloud Code will run the
    /// same <see cref="DuelResolver"/> as the authoritative host.
    /// </summary>
    [TestFixture]
    public class DuelResolverTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static DuelStateDto NewDuel(DateTimeOffset? now = null)
        {
            return DuelStateDto.Create("match-1", "p1", "Uno", "p2", "Dos", now ?? Start);
        }

        private static void Lock(DuelStateDto duel, RpsMove[] one, RpsMove[] two)
        {
            Assert.IsTrue(duel.jugador1.TrySetSequence(one, out var errorOne), errorOne);
            Assert.IsTrue(duel.jugador2.TrySetSequence(two, out var errorTwo), errorTwo);
        }

        [Test]
        public void Create_StartsWithFullHp_AndThreeEmptySlots()
        {
            var duel = NewDuel();

            Assert.AreEqual(DuelRules.MaxHp, duel.jugador1.hp);
            Assert.AreEqual(DuelRules.MaxHp, duel.jugador2.hp);
            Assert.AreEqual(1, duel.Round);
            Assert.AreEqual(DuelRules.StateYourTurn, duel.State);
            Assert.AreEqual(3, duel.jugador1.secuencia_actual.Length);
            Assert.AreEqual(0, duel.jugador1.FilledSlots);
            Assert.IsFalse(duel.IsFinished);
        }

        [Test]
        public void Resolve_ChargesFifteenPerClashWon()
        {
            var duel = NewDuel();
            Lock(duel, new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock },
                new[] { RpsMove.Scissors, RpsMove.Scissors, RpsMove.Scissors });

            var result = DuelResolver.Resolve(duel, Start.AddMinutes(1));

            Assert.AreEqual(3, result.Clashes.Count);
            Assert.AreEqual(DuelRules.ClashDamage * 3, result.DamageToPlayerTwo);
            Assert.AreEqual(0, result.DamageToPlayerOne);
            Assert.AreEqual(100, result.PlayerOneHp);
            Assert.AreEqual(55, result.PlayerTwoHp);
            Assert.AreEqual(100, duel.jugador1.hp);
            Assert.AreEqual(55, duel.jugador2.hp);
        }

        [Test]
        public void Resolve_DrawClashesDealNoDamage()
        {
            var duel = NewDuel();
            Lock(duel, new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors },
                new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });

            var result = DuelResolver.Resolve(duel, Start);

            Assert.AreEqual(0, result.DamageToPlayerOne);
            Assert.AreEqual(0, result.DamageToPlayerTwo);
            Assert.IsTrue(result.Clashes[0].IsDraw);
            Assert.AreEqual(100, duel.jugador1.hp);
            Assert.AreEqual(100, duel.jugador2.hp);
        }

        [Test]
        public void Resolve_UsesTheSameRulesAsTheVsScreen()
        {
            // Guerrero (Rock) beats Asesino (Scissors); Maga (Paper) beats Guerrero;
            // Asesino beats Maga. One clash each, one winner each.
            var duel = NewDuel();
            Lock(duel, new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors },
                new[] { RpsMove.Scissors, RpsMove.Rock, RpsMove.Paper });

            var result = DuelResolver.Resolve(duel, Start);

            Assert.AreEqual(RpsPlayer.One, result.Clashes[0].Winner);
            Assert.AreEqual(RpsPlayer.One, result.Clashes[1].Winner);
            Assert.AreEqual(RpsPlayer.One, result.Clashes[2].Winner);
            Assert.AreEqual(DuelRules.ClashDamage * 3, result.DamageToPlayerTwo);
        }

        [Test]
        public void Resolve_ClearsSequencesAndAdvancesTheRound()
        {
            var duel = NewDuel();
            Lock(duel, new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors },
                new[] { RpsMove.Scissors, RpsMove.Rock, RpsMove.Paper });

            DuelResolver.Resolve(duel, Start.AddMinutes(2));

            Assert.AreEqual(2, duel.Round);
            Assert.AreEqual(0, duel.jugador1.FilledSlots);
            Assert.AreEqual(0, duel.jugador2.FilledSlots);
            Assert.AreEqual(DuelRules.StateYourTurn, duel.State);
            Assert.AreEqual(string.Empty, duel.turno_de);
            Assert.AreEqual(DuelTimestamp.ToIso(Start.AddMinutes(2)), duel.timestamp_ultimo_movimiento);
        }

        [Test]
        public void Resolve_RejectsARoundWhereOnlyOneSidePlayed()
        {
            var duel = NewDuel();
            duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock }, out _);

            Assert.IsFalse(DuelResolver.CanResolve(duel));
            Assert.Throws<InvalidOperationException>(() => DuelResolver.Resolve(duel, Start));
        }

        [Test]
        public void Resolve_EndsTheDuelAtZeroHp_WithAwinnersId()
        {
            var duel = NewDuel();
            DuelRoundResult result = null;

            for (int round = 0; round < 3; round++)
            {
                Lock(duel, new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock },
                    new[] { RpsMove.Scissors, RpsMove.Scissors, RpsMove.Scissors });
                result = DuelResolver.Resolve(duel, Start.AddMinutes(round));
            }

            Assert.IsTrue(result.IsFinished);
            Assert.AreEqual("p1", result.WinnerId);
            Assert.AreEqual(DuelRules.EndKo, result.EndReason);
            Assert.AreEqual(0, duel.jugador2.hp);
            Assert.AreEqual(DuelRules.StateFinished, duel.State);
            Assert.IsTrue(duel.IsFinished);
        }

        [Test]
        public void Resolve_StopsAtTheRoundCap_AndDecidesOnHp()
        {
            var duel = NewDuel();

            for (int round = 1; round <= DuelRules.MaxRounds; round++)
            {
                // Mirrored moves = three draws = no damage, so nobody can die.
                Lock(duel,
                    new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors },
                    new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });

                var result = DuelResolver.Resolve(duel, Start.AddMinutes(round));

                if (round < DuelRules.MaxRounds)
                {
                    Assert.IsFalse(result.IsFinished, $"round {round} should not end the duel");
                    continue;
                }

                Assert.IsTrue(result.IsFinished);
                Assert.IsTrue(result.IsDraw);
                Assert.AreEqual(DuelRules.EndDraw, result.EndReason);
            }

            Assert.AreEqual(DuelRules.MaxRounds, duel.Round);
            Assert.IsTrue(duel.IsFinished);
        }

        [Test]
        public void Timeout_UnlocksOnlyAfterTheWindow_WhenTheRivalIsSilent()
        {
            var duel = NewDuel();
            duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors }, out _);
            duel.LastMoveTimestamp = DuelTimestamp.ToIso(Start);

            Assert.IsFalse(DuelTimeout.CanClaimVictory(duel, "p1", Start.AddMinutes(9), TimeSpan.FromMinutes(10)));
            Assert.IsTrue(DuelTimeout.CanClaimVictory(duel, "p1", Start.AddMinutes(10), TimeSpan.FromMinutes(10)));
            Assert.IsTrue(DuelTimeout.CanClaimVictory(duel, "p1", Start.AddMinutes(11), TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void Timeout_IsNotClaimable_WithoutHavingPlayed()
        {
            var duel = NewDuel();
            duel.LastMoveTimestamp = DuelTimestamp.ToIso(Start);

            Assert.IsFalse(DuelTimeout.CanClaimVictory(duel, "p1", Start.AddHours(1), TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void Timeout_IsNotClaimable_WhenTheRivalAlreadyPlayed()
        {
            var duel = NewDuel();
            Lock(duel, new[] { RpsMove.Rock, RpsMove.Rock, RpsMove.Rock },
                new[] { RpsMove.Scissors, RpsMove.Scissors, RpsMove.Scissors });
            duel.LastMoveTimestamp = DuelTimestamp.ToIso(Start);

            Assert.IsFalse(DuelTimeout.CanClaimVictory(duel, "p1", Start.AddHours(1), TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void ClaimVictory_FinishesTheDuelByAbandonment()
        {
            var duel = NewDuel();
            duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors }, out _);
            duel.LastMoveTimestamp = DuelTimestamp.ToIso(Start);

            bool claimed = DuelTimeout.TryClaimVictory(duel, "p1", Start.AddMinutes(11), TimeSpan.FromMinutes(10));

            Assert.IsTrue(claimed);
            Assert.IsTrue(duel.IsFinished);
            Assert.AreEqual("p1", duel.WinnerId);
            Assert.AreEqual(DuelRules.EndAbandon, duel.EndReason);
            Assert.AreEqual(0, duel.jugador1.FilledSlots, "the claimed duel clears the abandoned round");
        }

        [Test]
        public void StateFor_GivesEachPlayerTheirOwnPerspective()
        {
            var duel = NewDuel();
            duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors }, out _);

            Assert.AreEqual(DuelRules.StateWaitingRival, duel.StateFor("p1"));
            Assert.AreEqual(DuelRules.StateYourTurn, duel.StateFor("p2"));
            Assert.IsFalse(duel.CanSubmit("p1"));
            Assert.IsTrue(duel.CanSubmit("p2"));
        }

        [Test]
        public void MoveCodec_AcceptsCanonicalAndAliasCodes()
        {
            Assert.AreEqual("Guerrero", DuelMoveCodec.ToCode(RpsMove.Rock));
            Assert.AreEqual("Maga", DuelMoveCodec.ToCode(RpsMove.Paper));
            Assert.AreEqual("Asesino", DuelMoveCodec.ToCode(RpsMove.Scissors));

            Assert.IsTrue(DuelMoveCodec.TryParse("Guerrero", out var rock));
            Assert.AreEqual(RpsMove.Rock, rock);
            Assert.IsTrue(DuelMoveCodec.TryParse("mago", out var paper));
            Assert.AreEqual(RpsMove.Paper, paper);
            Assert.IsTrue(DuelMoveCodec.TryParse("Scissors", out var scissors));
            Assert.AreEqual(RpsMove.Scissors, scissors);
            Assert.IsFalse(DuelMoveCodec.TryParse("", out _));
            Assert.IsFalse(DuelMoveCodec.TryParse("Piedra de fuego", out _));
        }

        [Test]
        public void Submit_RejectsAWrongSlotCount()
        {
            var duel = NewDuel();

            Assert.IsFalse(duel.jugador1.TrySetSequence(new[] { RpsMove.Rock, RpsMove.Rock }, out var error));
            Assert.IsNotNull(error);
            Assert.AreEqual(0, duel.jugador1.FilledSlots);
        }

        [Test]
        public void Normalize_RepairsADocumentLoadedWithoutSequences()
        {
            var duel = new DuelStateDto { jugador1 = null, jugador2 = null, match_id = null };

            duel.Normalize();

            Assert.IsNotNull(duel.jugador1);
            Assert.IsNotNull(duel.jugador2);
            Assert.AreEqual(DuelRules.SlotsPerRound, duel.jugador1.secuencia_actual.Length);
            Assert.AreEqual(string.Empty, duel.match_id);
            Assert.AreEqual(DuelRules.StateYourTurn, duel.State);
        }
    }
}
