using TTTXO.Game.Bootstrap;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors start menu - the RPS scene's equivalent of Home
    /// (Assets/Scenes/Rps.unity boots the shared shell pointing its splash at <see cref="ScreenId.RpsMenu"/>
    /// instead of Home - see RpsSceneSetup). Big "Play" button opens the mode picker; the
    /// Profile/Store/Settings row reuses the real Main screens; the header back button leaves for
    /// Main's Home (only reachable when the RPS menu was opened from there).
    /// </summary>
    public class RpsMenuScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;

        public RpsMenuScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root;
            _router = router;

            var backButton = root.Q<Button>("rps-menu-back-button");
            backButton.text = UiText.Common.BackIcon;

            // RpsMenu is the hub of the standalone RPS scene (the router's StartScreen is RpsMenu,
            // set by RpsSceneSetup) - a back arrow there would lead nowhere, so it is hidden. The
            // back arrow is only meaningful when the RPS menu was reached from the main scene's Home
            // (StartScreen == Home), where it returns there: never jump into the other game.
            if (_router.StartScreen == ScreenId.RpsMenu)
            {
                backButton.style.display = DisplayStyle.None;
            }
            else
            {
                backButton.clicked += () => _router.Show(ScreenId.Home);
            }

            root.Q<Button>("rps-play-button").clicked += () => _router.Show(ScreenId.RpsModeSelect);

            root.Q<Label>("rps-menu-profile-icon-label").text = UiText.Icons.Profile;
            root.Q<VisualElement>("rps-menu-profile-button").RegisterCallback<ClickEvent>(_ => _router.Show(ScreenId.Profile));

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
            _root.Q<Label>("rps-menu-profile-label").text = UiText.RpsMenu.ProfileLabel;
            _root.Q<Label>("rps-menu-store-label").text = UiText.RpsMenu.StoreLabel;
            _root.Q<Label>("rps-menu-settings-label").text = UiText.RpsMenu.SettingsLabel;
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