using System;
using System.Globalization;
using TTTXO.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// VS screen for Rock-Paper-Scissors. It reads the round already resolved by
    /// <see cref="RpsSeries"/>, shows Player One on the left and Player Two/AI on the right, waits
    /// 5.5 seconds before revealing the opponent (the AI's pick stays hidden), then plays
    /// Attack/Hurt for one second. A draw shows EMPATE and continues the series.
    ///
    /// The screen never resolves a round itself: the series owns the score, so going back to
    /// Character Select for the next round always sees the updated state.
    /// </summary>
    public sealed class RpsVsScreenController : IScreenController
    {
        private const int RevealDelayMs = 5500;
        private const int DrawHoldMs = 3000;
        private const int PostAnimationDelayMs = 700;

        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly RpsCharacterDatabase _database;

        private readonly VisualElement _rightSprite;
        private readonly Label _rightName;
        private readonly Label _roundLabel;
        private readonly Label _scoreLabel;
        private readonly Label _drawLabel;
        private readonly Label _seriesLabel;
        private readonly Button _rematchButton;
        private readonly Button _menuButton;

        private VSAnimationController _leftAnim;
        private VSAnimationController _rightAnim;

        private RpsRoundResult _lastResult;
        private RpsMatchMode _mode;
        private int _pendingAnimations;
        private int _generation;
        private bool _disposed;
        private IVisualElementScheduledItem _revealTask;
        private IVisualElementScheduledItem _drawTask;
        private IVisualElementScheduledItem _navigateTask;

        public RpsVsScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");
            if (_database == null)
            {
                Debug.LogWarning("RpsCharacterDatabase not found in Resources. VS screen will use fallbacks.");
            }

            _rightSprite = _root.Q<VisualElement>("vs-right-sprite");
            _rightName = _root.Q<Label>("vs-right-name");
            _roundLabel = _root.Q<Label>("vs-round-label");
            _scoreLabel = _root.Q<Label>("vs-score-label");
            _drawLabel = _root.Q<Label>("vs-draw-label");
            _seriesLabel = _root.Q<Label>("vs-series-label");
            _rematchButton = _root.Q<Button>("vs-rematch-button");
            _menuButton = _root.Q<Button>("vs-menu-button");

            _drawLabel.style.display = DisplayStyle.None;
            _seriesLabel.style.display = DisplayStyle.None;
            _rematchButton.style.display = DisplayStyle.None;
            _menuButton.style.display = DisplayStyle.None;

            _rematchButton.clicked += OnRematchClicked;
            _menuButton.clicked += OnMenuClicked;

            RefreshLocalizedText();
        }

        public void OnShow()
        {
            _disposed = false;
            _generation++;
            CancelScheduled();

            var series = _router.RpsSeries;
            var result = series.LastRound;
            if (result == null)
            {
                Debug.LogError("RpsVsScreenController: no round result. Returning to the menu.");
                _router.Show(ScreenId.RpsMenu);
                return;
            }

            _lastResult = result;
            _mode = series.Mode;

            _drawLabel.style.display = DisplayStyle.None;
            _seriesLabel.style.display = DisplayStyle.None;
            _rematchButton.style.display = DisplayStyle.None;
            _menuButton.style.display = DisplayStyle.None;

            var leftName = DisplayName(result.MoveOne);
            var rightName = DisplayName(result.MoveTwo);
            var leftFrames = Frames(result.MoveOne);
            var rightFrames = Frames(result.MoveTwo);

            _leftAnim = new VSAnimationController(_root.Q<VisualElement>("vs-left-sprite"), _root.Q<Label>("vs-left-name"),
                leftName, leftFrames.Idle, leftFrames.Attack, leftFrames.Hurt, OnAnimationComplete);
            _rightAnim = new VSAnimationController(_rightSprite, _rightName, rightName,
                rightFrames.Idle, rightFrames.Attack, rightFrames.Hurt, OnAnimationComplete);

            _leftAnim.PlayIdle();

            if (_mode == RpsMatchMode.LocalTwoPlayer)
            {
                _rightSprite.style.visibility = Visibility.Visible;
                _rightName.text = rightName;
            }
            else
            {
                // Keep the AI's fighter hidden until the reveal. The name is a question mark so the
                // player knows the right side is the opponent, not an empty slot.
                _rightSprite.style.visibility = Visibility.Hidden;
                _rightName.text = "?";
            }

            _rightAnim.PlayIdle();

            UpdateHeader(series, result);

            var generation = _generation;
            _revealTask = _root.schedule.Execute(() => Reveal(generation)).StartingIn(RevealDelayMs);
        }

        private void Reveal(int generation)
        {
            if (_disposed || generation != _generation || _lastResult == null)
            {
                return;
            }

            if (_mode == RpsMatchMode.SinglePlayer)
            {
                // Reveal the AI's character
                _rightSprite.style.visibility = Visibility.Visible;
                _rightName.text = DisplayName(_lastResult.MoveTwo);
                _rightAnim?.PlayIdle();
            }

            if (_lastResult.Outcome.IsDraw)
            {
                _drawLabel.text = UiText.RpsGame.DrawLabel;
                _drawLabel.style.display = DisplayStyle.Flex;
                
                // Both characters play Attack animation simultaneously for draw
                _leftAnim?.PlayAttack();
                _rightAnim?.PlayAttack();

                var drawGeneration = _generation;
                _drawTask = _root.schedule.Execute(() => FinishRound(drawGeneration)).StartingIn(DrawHoldMs);
                return;
            }

            _pendingAnimations = 2;
            if (_lastResult.RoundWinner == RpsPlayer.One)
            {
                _leftAnim?.PlayAttack();
                _rightAnim?.PlayHurt();
            }
            else
            {
                _leftAnim?.PlayHurt();
                _rightAnim?.PlayAttack();
            }
        }

        private void OnAnimationComplete()
        {
            if (_disposed)
            {
                return;
            }

            _pendingAnimations--;
            if (_pendingAnimations > 0)
            {
                return;
            }

            var generation = _generation;
            _navigateTask = _root.schedule.Execute(() => FinishRound(generation)).StartingIn(PostAnimationDelayMs);
        }

        private void FinishRound(int generation)
        {
            if (_disposed || generation != _generation || _lastResult == null)
            {
                return;
            }

            if (_lastResult.IsSeriesOver)
            {
                ShowSeriesResult();
                return;
            }

            _router.Show(ScreenId.RpsCharacterSelect);
        }

        private void ShowSeriesResult()
        {
            var winner = _lastResult.SeriesWinner;
            _seriesLabel.text = _mode == RpsMatchMode.SinglePlayer
                ? (winner == RpsPlayer.One ? UiText.RpsGame.YouWon : UiText.RpsGame.YouLost)
                : (winner == RpsPlayer.One ? UiText.RpsGame.Player1Won : UiText.RpsGame.Player2Won);

            _drawLabel.style.display = DisplayStyle.None;
            _seriesLabel.style.display = DisplayStyle.Flex;
            _rematchButton.style.display = DisplayStyle.Flex;
            _menuButton.style.display = DisplayStyle.Flex;
        }

        private void UpdateHeader(RpsSeries series, RpsRoundResult result)
        {
            _roundLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.RpsGame.RoundFormat, result.RoundNumber);

            var oneLabel = _mode == RpsMatchMode.SinglePlayer ? UiText.RpsGame.YouLabel : UiText.RpsGame.Player1Label;
            var twoLabel = _mode == RpsMatchMode.SinglePlayer ? UiText.RpsGame.AiLabel : UiText.RpsGame.Player2Label;
            _scoreLabel.text = string.Format(
                CultureInfo.InvariantCulture,
                UiText.RpsCharacterSelect.ScoreFormat,
                oneLabel,
                series.ScoreOne,
                twoLabel,
                series.ScoreTwo);
        }

        private string DisplayName(RpsMove move)
        {
            if (_database != null)
            {
                return _database.GetDisplayName(move);
            }

            return move switch
            {
                RpsMove.Rock => UiText.RpsCharacterSelect.Guerrero,
                RpsMove.Paper => UiText.RpsCharacterSelect.Maga,
                _ => UiText.RpsCharacterSelect.Asesino
            };
        }

        private (Texture2D[] Idle, Texture2D[] Attack, Texture2D[] Hurt) Frames(RpsMove move)
        {
            if (_database != null)
            {
                return _database.GetFrames(move);
            }

            return (Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>());
        }

        private void OnRematchClicked()
        {
            var mode = _router.RpsSeries.Mode;
            _router.RpsSeries.Start(mode, _router.RpsSeries.Difficulty);
            _router.Show(ScreenId.RpsCharacterSelect);
        }

        private void OnMenuClicked()
        {
            _router.RpsSeries.End();
            _router.Show(ScreenId.RpsMenu);
        }

        private void CancelScheduled()
        {
            _revealTask?.Pause();
            _drawTask?.Pause();
            _navigateTask?.Pause();
            _revealTask = null;
            _drawTask = null;
            _navigateTask = null;
        }

        public void OnHide()
        {
            _disposed = true;
            _generation++;
            CancelScheduled();

            _leftAnim?.Dispose();
            _rightAnim?.Dispose();
            _leftAnim = null;
            _rightAnim = null;
        }

        public void RefreshLocalizedText()
        {
            _rematchButton.text = UiText.RpsGame.RematchButton;
            _menuButton.text = UiText.RpsGame.MenuButton;
            _drawLabel.text = UiText.RpsGame.DrawLabel;

            if (_lastResult != null)
            {
                UpdateHeader(_router.RpsSeries, _lastResult);

                if (_seriesLabel.style.display == DisplayStyle.Flex)
                {
                    ShowSeriesResult();
                }
            }
        }
    }
}
