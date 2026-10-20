using System;
using TTTXO.Core;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors mode picker (the RPS scene's equivalent of TTT Mode Select):
    /// Single Player starts a series and goes directly to VS Screen;
    /// Local 2P goes to the game screen for the handoff flow, then VS Screen after resolution;
    /// Online is wired to a "coming soon" toast until the Milestone contract lands.
    /// </summary>
    public class RpsModeSelectScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly Random _rng = new();

        public RpsModeSelectScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root;
            _router = router;

            var backButton = root.Q<Button>("rps-mode-back-button");
            backButton.text = UiText.Common.BackIcon;
            backButton.clicked += () => _router.Show(ScreenId.RpsMenu);

            root.Q<VisualElement>("rps-mode-single-card").RegisterCallback<ClickEvent>(_ => StartSinglePlayer());
            root.Q<VisualElement>("rps-mode-local-card").RegisterCallback<ClickEvent>(_ => StartLocalTwoPlayer());

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

        private void StartSinglePlayer()
        {
            // Single Player: go to Character Select (player picks combatant = move)
            _router.Show(ScreenId.RpsCharacterSelect);
        }

        private void StartLocalTwoPlayer()
        {
            // 2P uses the game screen for handoff, then VS Screen after resolution
            _router.RpsGameScreen.SetUp(new RpsMatchSetup(RpsMatchMode.LocalTwoPlayer, RpsAiDifficulty.Medium));
            _router.Show(ScreenId.RpsGame);
        }

        public void OnShow() { }
        public void OnHide() { }
        public void RefreshLocalizedText() => ApplyLocalizedText();
    }
}