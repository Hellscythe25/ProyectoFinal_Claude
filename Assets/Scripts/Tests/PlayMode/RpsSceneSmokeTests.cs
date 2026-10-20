using System.Collections;
using NUnit.Framework;
using TTTXO.Core;
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
