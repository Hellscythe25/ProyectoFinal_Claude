using TTTXO.Core;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors mode picker. Both modes start a best-of-five series and go to Character
    /// Select; the difference is who provides Player Two's move (AI or the second local player).
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
            backButton.clicked += () =>
            {
                _router.RpsSeries.End();
                _router.Show(ScreenId.RpsMenu);
            };

            root.Q<VisualElement>("rps-mode-single-card").RegisterCallback<ClickEvent>(_ => Start(RpsMatchMode.SinglePlayer));
            root.Q<VisualElement>("rps-mode-local-card").RegisterCallback<ClickEvent>(_ => Start(RpsMatchMode.LocalTwoPlayer));

            ApplyLocalizedText();
        }

        private void Start(RpsMatchMode mode)
        {
            _router.RpsSeries.Start(mode, RpsAiDifficulty.Medium);
            _router.Show(ScreenId.RpsCharacterSelect);
        }

        private void ApplyLocalizedText()
        {
            _root.Q<Label>("rps-mode-title-label").text = UiText.RpsModeSelect.Title;
            _root.Q<Label>("rps-mode-single-title-label").text = UiText.RpsModeSelect.SinglePlayerTitle;
            _root.Q<Label>("rps-mode-single-description-label").text = UiText.RpsModeSelect.SinglePlayerDescription;
            _root.Q<Label>("rps-mode-local-title-label").text = UiText.RpsModeSelect.LocalMultiplayerTitle;
            _root.Q<Label>("rps-mode-local-description-label").text = UiText.RpsModeSelect.LocalMultiplayerDescription;
        }

        public void OnShow()
        {
        }

        public void OnHide()
        {
        }

        public void RefreshLocalizedText() => ApplyLocalizedText();
    }
}
