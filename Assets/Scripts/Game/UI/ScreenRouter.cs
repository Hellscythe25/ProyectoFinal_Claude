using System;
using System.Collections.Generic;
using TTTXO.Game.Bootstrap;
using TTTXO.Game.UI.Screens;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI
{
    /// <summary>
    /// Single root UIDocument navigation host. Every screen is a sub-tree of the same UXML document
    /// (Assets/UI/Screens/Root.uxml); this router shows exactly one at a time by toggling display
    /// style and forwards lifecycle events to each screen's controller. It also mounts the two
    /// shared overlays (confirm dialog, toast) used across several screens.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScreenRouter : MonoBehaviour
    {
        private static readonly IReadOnlyDictionary<ScreenId, string> ScreenElementNames = new Dictionary<ScreenId, string>
        {
            { ScreenId.RpsMenu, "screen-rps-menu" },
            { ScreenId.RpsModeSelect, "screen-rps-mode-select" },
            { ScreenId.RpsCharacterSelect, "screen-rps-character-select" },
            { ScreenId.RpsVs, "screen-rps-vs" },
            { ScreenId.Store, "screen-store" },
            { ScreenId.Settings, "screen-settings" }
        };

        /// <summary>Where <see cref="Initialize"/> lands after startup.</summary>
        [SerializeField]
        private ScreenId _startScreen = ScreenId.RpsMenu;

        public ScreenId StartScreen => _startScreen;

        private UIDocument _document;
        private readonly Dictionary<ScreenId, VisualElement> _screenRoots = new();
        private readonly Dictionary<ScreenId, IScreenController> _controllers = new();
        private ScreenId? _currentScreen;
        private bool _initialized;

        public RpsVsScreenController RpsVsScreen { get; private set; }
        public RpsCharacterSelectScreenController RpsCharacterSelectScreen { get; private set; }

        /// <summary>The screen currently visible, or null before <see cref="Initialize"/> shows one.</summary>
        public ScreenId? CurrentScreen => _currentScreen;

        /// <summary>
        /// Match state shared by Character Select and the VS screen. It survives the navigation
        /// between rounds and is reset when a new series starts or the player returns to the menu.
        /// </summary>
        public RpsSeries RpsSeries { get; } = new();

        /// <summary>
        /// Navigation history for generic "back" buttons on sub-screens. The rules - and why a
        /// single-level "previous screen" field is not enough - live in <see cref="ScreenHistory"/>,
        /// which holds them without a UIDocument so they can be exercised by a test.
        /// </summary>
        private readonly ScreenHistory _history = new();
        private bool _navigatingBack;

        public ConfirmDialogController ConfirmDialog { get; private set; }
        public ToastController Toast { get; private set; }

        /// <summary>Keeps every screen's content clear of the notch and the home indicator.</summary>
        public SafeAreaController SafeArea { get; private set; }

        /// <summary>Binds every screen root, constructs the per-screen controllers, and shows the menu.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _document = GetComponent<UIDocument>();
            var root = _document.rootVisualElement;

            foreach (var pair in ScreenElementNames)
            {
                var element = root.Q<VisualElement>(pair.Value);
                if (element == null)
                {
                    Debug.LogError($"ScreenRouter: could not find root element '{pair.Value}' for screen {pair.Key}.");
                    continue;
                }

                _screenRoots[pair.Key] = element;
            }

            ConfirmDialog = new ConfirmDialogController(root.Q<VisualElement>("confirm-dialog-overlay"));
            Toast = new ToastController(root.Q<VisualElement>("toast"));

            var gameManager = GameManager.Instance;

            _controllers[ScreenId.Store] = new StoreScreenController(_screenRoots[ScreenId.Store], this, gameManager);
            _controllers[ScreenId.Settings] = new SettingsScreenController(_screenRoots[ScreenId.Settings], this, gameManager);
            _controllers[ScreenId.RpsMenu] = new RpsMenuScreenController(_screenRoots[ScreenId.RpsMenu], this);
            _controllers[ScreenId.RpsModeSelect] = new RpsModeSelectScreenController(_screenRoots[ScreenId.RpsModeSelect], this);
            RpsCharacterSelectScreen = new RpsCharacterSelectScreenController(_screenRoots[ScreenId.RpsCharacterSelect], this);
            RpsVsScreen = new RpsVsScreenController(_screenRoots[ScreenId.RpsVs], this);
            _controllers[ScreenId.RpsCharacterSelect] = RpsCharacterSelectScreen;
            _controllers[ScreenId.RpsVs] = RpsVsScreen;

            foreach (var pair in _screenRoots)
            {
                pair.Value.style.display = DisplayStyle.None;
            }

            // Insets every screen out from under the notch and the home indicator. Constructed after the
            // screen roots are bound and before anything is shown, so the first painted frame is already
            // inside the safe area rather than jumping once the first resize arrives.
            SafeArea = new SafeAreaController(root, new List<VisualElement>(_screenRoots.Values));

            _initialized = true;

            // Every screen's static UiText-sourced labels/buttons are assigned once from each
            // controller's constructor (see IScreenController.RefreshLocalizedText). Re-run that
            // assignment for every constructed screen whenever the player picks a different language
            // on the Settings screen.
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

            // The scene was serialized with the old enum value (Splash/Home) before this build.
            // Falling back to the menu keeps an out-of-date scene playable without a re-save.
            var start = ScreenElementNames.ContainsKey(_startScreen) ? _startScreen : ScreenId.RpsMenu;
            Show(start);
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            // Defensive: a broken screen refresh must never take down the rest of the app or block
            // navigation, so each controller is refreshed independently.
            foreach (var pair in _controllers)
            {
                try
                {
                    pair.Value.RefreshLocalizedText();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ScreenRouter: RefreshLocalizedText failed for {pair.Key} - {ex.Message}");
                }
            }

            ConfirmDialog?.RefreshLocalizedText();
        }

        /// <summary>Hides the current screen (if any) and shows the requested one.</summary>
        public void Show(ScreenId screenId)
        {
            if (!_screenRoots.TryGetValue(screenId, out var nextRoot))
            {
                Debug.LogError($"ScreenRouter: screen {screenId} is not registered.");
                return;
            }

            ScreenId? leavingScreen = null;
            if (_currentScreen.HasValue && _screenRoots.TryGetValue(_currentScreen.Value, out var currentRoot))
            {
                currentRoot.style.display = DisplayStyle.None;
                if (_controllers.TryGetValue(_currentScreen.Value, out var currentController))
                {
                    currentController.OnHide();
                }

                leavingScreen = _currentScreen.Value;
            }

            _history.RecordNavigation(leavingScreen, screenId, _navigatingBack);

            nextRoot.style.display = DisplayStyle.Flex;
            _currentScreen = screenId;

            if (_controllers.TryGetValue(screenId, out var nextController))
            {
                nextController.OnShow();
            }
        }

        /// <summary>
        /// Convenience for sub-screens whose back button should return wherever the player came from
        /// (Store/Settings can be reached from more than one place), falling back to
        /// <paramref name="fallback"/> if there is no recorded history. Pops the history instead of
        /// pushing onto it, so back-and-forth between two screens can never loop.
        /// </summary>
        public void GoBack(ScreenId fallback)
        {
            var target = _history.ResolveBack(fallback);
            _navigatingBack = true;
            try
            {
                Show(target);
            }
            finally
            {
                _navigatingBack = false;
            }
        }
    }
}
