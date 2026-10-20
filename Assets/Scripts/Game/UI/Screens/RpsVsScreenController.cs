using System;
using System.Threading.Tasks;
using TTTXO.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// VS Screen for Rock-Paper-Scissors: shows two animated characters (player left, opponent right)
    /// playing Idle → Attack/Hurt (1s) → Idle, then navigates to result or shows "EMPATE".
    /// Replaces the old RpsGameScreenController for both Single Player and Local 2P.
    /// </summary>
    public sealed class RpsVsScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly RpsCharacterDatabase _database;

        private readonly VisualElement _leftSide;
        private readonly VisualElement _rightSide;
        private readonly VisualElement _leftSprite;
        private readonly VisualElement _rightSprite;
        private readonly Label _leftName;
        private readonly Label _rightName;
        private readonly Label _drawLabel;
        private readonly Button _rematchButton;
        private readonly Button _menuButton;

        private VSAnimationController _leftAnim;
        private VSAnimationController _rightAnim;

        private RpsMove _playerMove;
        private RpsMove _opponentMove;
        private RpsMatchMode _mode;
        private RpsPlayer _playerSide;
        private int _pendingAnimations;
        private bool _disposed;

        public RpsVsScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            // Load database from Resources (place asset at Assets/Resources/RpsCharacterDatabase.asset)
            _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");
            if (_database == null)
            {
                Debug.LogWarning("RpsCharacterDatabase not found in Resources. VS Screen will use fallbacks.");
            }

            _leftSide = _root.Q<VisualElement>("vs-left");
            _rightSide = _root.Q<VisualElement>("vs-right");
            _leftSprite = _root.Q<VisualElement>("vs-left-sprite");
            _rightSprite = _root.Q<VisualElement>("vs-right-sprite");
            _leftName = _root.Q<Label>("vs-left-name");
            _rightName = _root.Q<Label>("vs-right-name");
            _drawLabel = _root.Q<Label>("vs-draw-label");
            _rematchButton = _root.Q<Button>("vs-rematch-button");
            _menuButton = _root.Q<Button>("vs-menu-button");

            _drawLabel.style.display = DisplayStyle.None;

            _rematchButton.clicked += OnRematchClicked;
            _menuButton.clicked += OnMenuClicked;
        }

        /// <summary>
        /// Prepares the VS Screen with the two moves already decided.
        /// Call this BEFORE navigating to the screen.
        /// </summary>
        public void SetUp(RpsMove playerMove, RpsMove opponentMove, RpsMatchMode mode, RpsPlayer playerSide = RpsPlayer.One)
        {
            _playerMove = playerMove;
            _opponentMove = opponentMove;
            _mode = mode;
            _playerSide = playerSide;
            _pendingAnimations = 0;
            _disposed = false;

            // Determine character for each side
            var leftCharName = _database?.GetDisplayName(_playerMove) ?? GetFallbackName(_playerMove);
            var rightCharName = _database?.GetDisplayName(_opponentMove) ?? GetFallbackName(_opponentMove);

            var leftFrames = _database?.GetFrames(_playerMove) ?? (Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>());
            var rightFrames = _database?.GetFrames(_opponentMove) ?? (Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>());

            // Create animation controllers
            _leftAnim = new VSAnimationController(_leftSprite, _leftName, leftCharName, leftFrames.Idle, leftFrames.Attack, leftFrames.Hurt, OnLeftAnimationComplete);
            _rightAnim = new VSAnimationController(_rightSprite, _rightName, rightCharName, rightFrames.Idle, rightFrames.Attack, rightFrames.Hurt, OnRightAnimationComplete);
        }

        private string GetFallbackName(RpsMove move) => move switch
        {
            RpsMove.Rock => "Caballero",
            RpsMove.Paper => "Maga",
            RpsMove.Scissors => "Asesino",
            _ => "?"
        };

        private void OnLeftAnimationComplete() => OnAnimationComplete();
        private void OnRightAnimationComplete() => OnAnimationComplete();

        private void OnAnimationComplete()
        {
            _pendingAnimations--;
            if (_pendingAnimations <= 0)
            {
                _root.schedule.Execute(NavigateAfterResolution).StartingIn(500);
            }
        }

        private void NavigateAfterResolution()
        {
            if (_disposed) return;
            // Go to result screen (RpsGame) or back to menu
            _router.Show(ScreenId.RpsGame);
        }

        private void OnRematchClicked()
        {
            if (_disposed) return;
            // Restart the same mode
            _router.RpsGameScreen.SetUp(new RpsMatchSetup(_mode, RpsAiDifficulty.Medium));
            _router.Show(ScreenId.RpsGame); // Will go through move picking again
        }

        private void OnMenuClicked()
        {
            if (_disposed) return;
            _router.Show(ScreenId.RpsMenu);
        }

        public void OnShow()
        {
            if (_disposed) return;

            _leftAnim?.PlayIdle();
            _rightAnim?.PlayIdle();

            _root.schedule.Execute(StartResolution).StartingIn(5500);
        }

        private void StartResolution()
        {
            if (_disposed) return;

            var outcome = RpsRulesEngine.Evaluate(_playerMove, _opponentMove);

            if (outcome.IsDraw)
            {
                _leftAnim?.ShowDraw();
                _rightAnim?.ShowDraw();
                _drawLabel.style.display = DisplayStyle.Flex;

                _root.schedule.Execute(() => _router.Show(ScreenId.RpsMenu)).StartingIn(3000);
            }
            else
            {
                bool playerWins = RpsRulesEngine.IsWinningMoveFor(outcome.Winner, _playerMove);

                if (playerWins)
                {
                    _pendingAnimations = 2;
                    _leftAnim?.PlayAttack();
                    _rightAnim?.PlayHurt();
                }
                else
                {
                    _pendingAnimations = 2;
                    _leftAnim?.PlayHurt();
                    _rightAnim?.PlayAttack();
                }
            }
        }

        public void OnHide()
        {
            _disposed = true;
            _leftAnim?.Dispose();
            _rightAnim?.Dispose();
            _leftAnim = null;
            _rightAnim = null;
        }

        public void RefreshLocalizedText()
        {
            if (_drawLabel != null && _drawLabel.style.display == DisplayStyle.Flex)
            {
                _drawLabel.text = UiText.RpsGame.DrawLabel;
            }
            _rematchButton.text = UiText.RpsGame.RematchButton;
            _menuButton.text = UiText.RpsGame.MenuButton;
        }
    }
}