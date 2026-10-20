using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors start menu and hub of the game. "Play" opens the mode picker; the second
    /// row opens the Store and Settings. There is no back button because this is the root screen.
    /// </summary>
    public class RpsMenuScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;

        public RpsMenuScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root;
            _router = router;

            root.Q<Button>("rps-menu-back-button").style.display = DisplayStyle.None;
            root.Q<Button>("rps-play-button").clicked += () => _router.Show(ScreenId.RpsModeSelect);

            root.Q<Label>("rps-menu-store-icon-label").text = UiText.Icons.Store;
            root.Q<VisualElement>("rps-menu-store-button").RegisterCallback<ClickEvent>(_ => _router.Show(ScreenId.Store));

            root.Q<Label>("rps-menu-settings-icon-label").text = UiText.Icons.Settings;
            root.Q<VisualElement>("rps-menu-settings-button").RegisterCallback<ClickEvent>(_ => _router.Show(ScreenId.Settings));

            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            _root.Q<Label>("rps-menu-title-label").text = UiText.RpsMenu.Title;
            _root.Q<Label>("rps-menu-subtitle-label").text = UiText.RpsMenu.Subtitle;
            _root.Q<Button>("rps-play-button").text = UiText.RpsMenu.PlayButton;
            _root.Q<Label>("rps-menu-store-label").text = UiText.RpsMenu.StoreLabel;
            _root.Q<Label>("rps-menu-settings-label").text = UiText.RpsMenu.SettingsLabel;
        }

        public void OnShow()
        {
            // The menu is the hub: any series still in memory is abandoned when the player comes back.
            _router.RpsSeries.End();
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
