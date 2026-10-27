using System.Collections;
using System.Globalization;
using NUnit.Framework;
using TTTXO.Core;
using TTTXO.Core.Duel;
using TTTXO.Game.UI;
using TTTXO.Game.UI.Screens;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TTTXO.Game.PlayModeTests
{
    /// <summary>
    /// Runtime smoke tests for the only scene in the build. They load Rps.unity, let
    /// AppBootstrap initialize the router, and drive one real single-player round and one real
    /// local draw through the VS screen. This catches the failures EditMode cannot see: missing
    /// UXML elements, a null database, and scheduled reveal/animation callbacks that never fire.
    /// </summary>
    public class RpsSceneSmokeTests
    {
        private ScreenRouter _router;
        private VisualElement _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("UI Toolkit needs a graphics device; run PlayMode tests with the editor window.");
            }

            SceneManager.LoadScene("Rps");
            yield return null;
            yield return null;

            _router = Object.FindFirstObjectByType<ScreenRouter>();
            Assert.IsNotNull(_router, "Rps.unity should contain the AppRoot with a ScreenRouter.");

            var document = _router.GetComponent<UIDocument>();
            Assert.IsNotNull(document, "AppRoot should have a UIDocument.");
            _root = document.rootVisualElement;
            Assert.IsNotNull(_root);

            Assert.AreEqual(ScreenId.RpsMenu, _router.CurrentScreen);
            Assert.AreEqual(DisplayStyle.Flex, _root.Q<VisualElement>("screen-rps-menu").style.display.value);
        }

        [UnityTest]
        public IEnumerator Menu_OpensStoreAndSettings_AndReturns()
        {
            _router.Show(ScreenId.Store);
            yield return null;
            Assert.AreEqual(ScreenId.Store, _router.CurrentScreen);
            Assert.AreEqual(UiText.Store.Title, _root.Q<Label>("store-title-label").text);

            _router.GoBack(ScreenId.RpsMenu);
            yield return null;
            Assert.AreEqual(ScreenId.RpsMenu, _router.CurrentScreen);

            _router.Show(ScreenId.Settings);
            yield return null;
            Assert.AreEqual(ScreenId.Settings, _router.CurrentScreen);
            Assert.AreEqual(UiText.Settings.Title, _root.Q<Label>("settings-title-label").text);

            _router.GoBack(ScreenId.RpsMenu);
            yield return null;
            Assert.AreEqual(ScreenId.RpsMenu, _router.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator Duels_HubAndCombat_RenderTheAsyncFlow()
        {
            DuelStateDto duel = null;
            try
            {
                _router.Show(ScreenId.DuelHub);
                yield return null;

                Assert.AreEqual(ScreenId.DuelHub, _router.CurrentScreen);
                Assert.AreEqual(UiText.Duel.HubTitle, _root.Q<Label>("duel-hub-title-label").text);
                Assert.IsNotNull(_root.Q<Button>("duel-hub-new-button"), "the hub needs its create button");
                Assert.IsNotNull(_root.Q<ScrollView>("duel-hub-list"), "the hub needs its card list");

                duel = _router.Duels.CreatePracticeDuel(UiText.Duel.PracticeRivalName);
                _router.Show(ScreenId.DuelHub);
                yield return null;

                Assert.IsTrue(
                    _root.Q<ScrollView>("duel-hub-list").contentContainer.childCount > 0,
                    "a created duel must appear as a card in the hub");

                _router.Show(ScreenId.DuelCombat);
                yield return null;

                Assert.AreEqual(ScreenId.DuelCombat, _router.CurrentScreen);
                Assert.AreEqual(DuelRules.MaxHp, _router.Duels.Current.jugador1.hp);
                Assert.AreEqual(DuelRules.MaxHp, _router.Duels.Current.jugador2.hp);

                for (int i = 0; i < DuelRules.SlotsPerRound; i++)
                {
                    Assert.IsNotNull(_root.Q<Button>("duel-slot-" + i), "the player needs three slots");
                    Assert.IsNotNull(_root.Q<Label>("duel-rival-slot-" + i), "the rival needs three slots");
                }

                string fullHp = string.Format(CultureInfo.InvariantCulture, UiText.Duel.HpFormat, DuelRules.MaxHp, DuelRules.MaxHp);
                Assert.AreEqual(fullHp, _root.Q<Label>("duel-left-hp-label").text);
                Assert.AreEqual(fullHp, _root.Q<Label>("duel-right-hp-label").text);
                Assert.AreEqual(UiText.Duel.YourTurn, _root.Q<Label>("duel-status-label").text);
                Assert.AreEqual(UiText.Duel.SlotEmpty, _root.Q<Button>("duel-slot-0").text);
                Assert.AreEqual(UiText.Duel.SlotEmpty, _root.Q<Label>("duel-rival-slot-0").text, "the rival's moves stay hidden");
                Assert.IsFalse(_root.Q<Button>("duel-confirm-button").enabledSelf, "an empty draft cannot be sent");
                Assert.IsNotNull(_root.Q<Button>("duel-claim-button"));

                var resolution = _router.Duels.Submit(new[] { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors });
                Assert.IsNull(resolution, "the practice rival answers on the next open");
                Assert.AreEqual(DuelRules.StateWaitingRival, _router.Duels.Current.estado_partida);

                _router.Show(ScreenId.DuelCombat);
                yield return null;

                Assert.AreEqual(UiText.Duel.WaitingRival, _root.Q<Label>("duel-status-label").text);
                Assert.AreEqual(DisplayStyle.None, _root.Q<Button>("duel-confirm-button").style.display.value);
                Assert.AreEqual(DisplayStyle.Flex, _root.Q<Label>("duel-countdown-label").style.display.value);
                Assert.AreEqual(UiText.Duel.SlotEmpty, _root.Q<Label>("duel-rival-slot-1").text);
                Assert.AreEqual(UiText.RpsCharacterSelect.Guerrero, _root.Q<Button>("duel-slot-0").text,
                    "the moves you already sent stay visible while you wait");
            }
            finally
            {
                if (duel != null)
                {
                    _router.DuelStore.Delete(duel.match_id);
                    _router.Duels.Refresh();
                }
            }
        }

        [UnityTest]
        public IEnumerator SinglePlayer_RevealsOpponent_ThenReturnsToCharacterSelect()
        {
            _router.RpsSeries.Start(RpsMatchMode.SinglePlayer, RpsAiDifficulty.Medium);
            Assert.IsTrue(_router.RpsSeries.TrySubmitPlayerOne(RpsMove.Rock, out var result));
            Assert.IsNotNull(result);

            _router.Show(ScreenId.RpsVs);
            yield return null;

            Assert.AreEqual(ScreenId.RpsVs, _router.CurrentScreen);
            Assert.AreEqual("?", _root.Q<Label>("vs-right-name").text, "the AI's fighter must stay hidden before the reveal");

            yield return new WaitForSeconds(6f);
            Assert.AreNotEqual("?", _root.Q<Label>("vs-right-name").text, "the opponent is revealed after 5.5s");

            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(ScreenId.RpsCharacterSelect, _router.CurrentScreen, "a first round must continue the series");
            Assert.AreEqual(1, _router.RpsSeries.RoundNumber);
            Assert.IsTrue(_router.RpsSeries.IsActive);
        }

        [UnityTest]
        public IEnumerator LocalTwoPlayer_Draw_ContinuesTheSeries()
        {
            _router.RpsSeries.Start(RpsMatchMode.LocalTwoPlayer);
            Assert.IsTrue(_router.RpsSeries.TrySubmitPlayerOne(RpsMove.Rock, out _));
            Assert.IsTrue(_router.RpsSeries.TrySubmitPlayerTwo(RpsMove.Rock, out var result));
            Assert.IsTrue(result.Outcome.IsDraw);

            _router.Show(ScreenId.RpsVs);
            yield return null;

            Assert.AreEqual(ScreenId.RpsVs, _router.CurrentScreen);
            Assert.AreEqual(Visibility.Visible, _root.Q<VisualElement>("vs-right-sprite").style.visibility.value);

            yield return new WaitForSeconds(6f);
            var drawLabel = _root.Q<Label>("vs-draw-label");
            Assert.AreEqual(DisplayStyle.Flex, drawLabel.style.display.value);
            Assert.AreEqual(UiText.RpsGame.DrawLabel, drawLabel.text);

            yield return new WaitForSeconds(3f);
            Assert.AreEqual(ScreenId.RpsCharacterSelect, _router.CurrentScreen, "a draw must never end the series");
            Assert.AreEqual(0, _router.RpsSeries.ScoreOne);
            Assert.AreEqual(0, _router.RpsSeries.ScoreTwo);
            Assert.IsTrue(_router.RpsSeries.IsActive);
        }
    }
}
