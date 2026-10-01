using TTTXO.Core;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors mode picker (the RPS scene's equivalent of TTT Mode Select):
    /// Single Player and Local 2P start a series via <see cref="RpsGameScreenController.SetUp"/>;
    /// Online is wired to a "coming soon" toast until the Milestone contract lands, so it never
    /// half-navigates into a match that cannot run yet (same pattern as the old Online TTT card).
    /// </summary>
    public class RpsModeSelectScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;

        public RpsModeSelectScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root;
            _router = router;

            var backButton = root.Q<Button>("rps-mode-back-button");
            backButton.text = UiText.Common.BackIcon;
            backButton.clicked += () => _router.Show(ScreenId.RpsMenu);

            root.Q<VisualElement>("rps-mode-single-card").RegisterCallback<ClickEvent>(_ => StartMode(RpsMatchMode.SinglePlayer));
            root.Q<VisualElement>("rps-mode-local-card").RegisterCallback<ClickEvent>(_ => StartMode(RpsMatchMode.LocalTwoPlayer));

            // The single-player difficulty has no picker here: the game screen owns the chips (they
            // can change mid-series, and every level stays beatable by design). The mode only chooses
            // a sane Medium default, documented in RpsGameScreenController.
            root.Q<VisualElement>("rps-mode-online-card").RegisterCallback<ClickEvent>(_ =>
            {
                _router.Toast.Show(UiText.RpsModeSelect.OnlineComingSoonMessage);
            });

            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            _root.Q<Label>("rps-mode-title-label").text = UiText.RpsModeSelect.Title;
            _root.Q<Label>("rps-mode-single-title-label").text = UiText.RpsModeSelect.SinglePlayerTitle;
            _root.Q<Label>("rps-mode-single-description-label").text = UiText.RpsModeSelect.SinglePlayerDescription;
            _root.Q<Label>("rps-mode-local-title-label").text = UiText.RpsModeSelect.LocalMultiplayerTitle;
            _root.Q<Label>("rps-mode-local-description-label").text = UiText.RpsModeSelect.LocalMultiplayerDescription;
            _root.Q<Label>("rps-mode-online-title-label").text = UiText.RpsModeSelect.OnlineTitle;
            _root.Q<Label>("rps-mode-online-description-label").text = UiText.RpsModeSelect.OnlineDescription;
        }

        private void StartMode(RpsMatchMode mode)
        {
            _router.RpsGameScreen.SetUp(new RpsMatchSetup(mode, RpsAiDifficulty.Medium));
            _router.Show(ScreenId.RpsGame);
        }

        public void OnShow()
        {
        }

        public void OnHide()
        {
        }

        public void RefreshLocalizedText()
        {
            ApplyLocalizedText();
        }
    }
}