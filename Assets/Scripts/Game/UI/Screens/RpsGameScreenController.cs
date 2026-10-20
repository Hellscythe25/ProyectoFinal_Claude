using System.Collections.Generic;
using System.Globalization;
using TTTXO.Core;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Rock-Paper-Scissors match screen. Drives a <see cref="RpsMatchController"/> (the pure core
    /// model, best of five with hidden simultaneous moves) with two live source flows:
    ///
    /// - SinglePlayer: the human is <see cref="RpsPlayer.One"/>, the local AI answers after a short
    ///   "thinking" beat - its difficulty is chosen live from the chips on this screen, so every
    ///   level stays beatable (see RpsAiController docs).
    /// - LocalTwoPlayer: both share the device. One plays first, a handoff banner tells them to pass
    ///   the device, the options stay visible so Two picks the same three buttons, and only then the
    ///   round resolves. Choices are only ever revealed through <see cref="RpsMatchController.LastRound"/>
    ///   - never peeked between the two submits.
    /// - Online: reserved for the Milestone contract (see RpsModeSelectScreenController) - SetUp
    ///   with <see cref="RpsMatchMode.Online"/> throws on purpose so a half-wired match can never run.
    ///
    /// The series winner is decided purely by <see cref="RpsMatchController"/> - the screen only
    /// renders what the core reports, the same authority Cloud Code will use online.
    /// </summary>
    public class RpsGameScreenController : IScreenController
    {
        private const long AiThinkingDelayMilliseconds = 400;

        private readonly VisualElement _root;
        private readonly ScreenRouter _router;

        private readonly Label _roundLabel;
        private readonly Label _scoreOneLabel;
        private readonly Label _scoreTwoLabel;
        private readonly VisualElement _options;
        private readonly VisualElement _difficultyRow;
        private readonly Label _difficultySectionLabel;
        private readonly VisualElement _difficultyList;
        private readonly VisualElement _grid;
        private readonly Label _moveOneTitleLabel;
        private readonly Label _moveOneValueLabel;
        private readonly Label _moveTwoTitleLabel;
        private readonly Label _moveTwoValueLabel;
        private readonly Label _seriesResultLabel;
        private readonly Label _handoffLabel;
        private readonly Button _rematchButton;
        private readonly Button _menuButton;

        private readonly Dictionary<RpsAiDifficulty, Label> _difficultyChips = new();

        private RpsMatchController _match;
        private RpsAiController _ai;
        private RpsMatchMode _mode;
        private RpsAiDifficulty _difficulty;
        private RpsPlayer? _awaitingPlayer;
        private int _runGeneration;

        public RpsGameScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root;
            _router = router;

            _roundLabel = root.Q<Label>("rps-round-label");
            _scoreOneLabel = root.Q<Label>("rps-score-one-label");
            _scoreTwoLabel = root.Q<Label>("rps-score-two-label");
            _options = root.Q<VisualElement>("rps-options");
            _difficultyRow = root.Q<VisualElement>("rps-difficulty-row");
            _difficultySectionLabel = root.Q<Label>("rps-difficulty-section-label");
            _difficultyList = root.Q<VisualElement>("rps-difficulty-list");
            _grid = root.Q<VisualElement>("rps-grid");
            _moveOneTitleLabel = root.Q<Label>("rps-move-one-title-label");
            _moveOneValueLabel = root.Q<Label>("rps-move-one-value-label");
            _moveTwoTitleLabel = root.Q<Label>("rps-move-two-title-label");
            _moveTwoValueLabel = root.Q<Label>("rps-move-two-value-label");
            _seriesResultLabel = root.Q<Label>("rps-series-result-label");
            _handoffLabel = root.Q<Label>("rps-handoff-label");
            _rematchButton = root.Q<Button>("rps-rematch-button");
            _menuButton = root.Q<Button>("rps-menu-button");

            var backButton = root.Q<Button>("rps-game-back-button");
            backButton.text = UiText.Common.BackIcon;
            backButton.clicked += () => _router.Show(ScreenId.RpsMenu);

            _root.Q<Button>("rps-rock-button").clicked += () => OnMovePicked(RpsMove.Rock);
            _root.Q<Button>("rps-paper-button").clicked += () => OnMovePicked(RpsMove.Paper);
            _root.Q<Button>("rps-scissors-button").clicked += () => OnMovePicked(RpsMove.Scissors);

            _rematchButton.clicked += OnRematchClicked;
            _menuButton.clicked += () => _router.Show(ScreenId.RpsMenu);

            BuildDifficultyChips();
            ApplyLocalizedText();
        }

        /// <summary>
        /// Arms the screen for a fresh series. Called by <see cref="RpsModeSelectScreenController"/>
        /// right before navigating here; calling it again restarts cleanly (e.g. from a future
        /// online flow). <see cref="RpsMatchMode.Online"/> is rejected until the contract exists.
        /// </summary>
        public void SetUp(RpsMatchSetup setup)
        {
            if (setup == null)
            {
                throw new System.ArgumentNullException(nameof(setup));
            }

            if (setup.Mode == RpsMatchMode.Online)
            {
                // No server flow yet - see class docs. The mode screen never sends Online, so this is
                // a hard guard against a half-wired match rather than a user-facing path.
                throw new System.NotSupportedException("Online RPS is not implemented yet (Milestone pending).");
            }

            _mode = setup.Mode;
            _difficulty = setup.Difficulty;
            _match = new RpsMatchController(RpsConfig.Default);
            _match.StartSeries();
            _ai = new RpsAiController();
            _awaitingPlayer = RpsPlayer.One;
            _options.SetEnabled(true);

            _runGeneration++;
            HideResults();
            SelectDifficulty(_difficulty);
            RefreshDifficultyVisibility();
            RefreshRoundUi();
        }

        private void BuildDifficultyChips()
        {
            (RpsAiDifficulty Difficulty, string Label)[] order =
            {
                (RpsAiDifficulty.Easy, UiText.BoardSelect.DifficultyEasy),
                (RpsAiDifficulty.Medium, UiText.BoardSelect.DifficultyMedium),
                (RpsAiDifficulty.Hard, UiText.BoardSelect.DifficultyHard)
            };

            foreach (var entry in order)
            {
                var chip = new Label(entry.Label);
                chip.AddToClassList("difficulty-chip");
                chip.RegisterCallback<ClickEvent>(_ => SelectDifficulty(entry.Difficulty));
                _difficultyList.Add(chip);
                _difficultyChips[entry.Difficulty] = chip;
            }
        }

        private void SelectDifficulty(RpsAiDifficulty difficulty)
        {
            _difficulty = difficulty;
            foreach (var pair in _difficultyChips)
            {
                pair.Value.EnableInClassList("difficulty-chip--selected", pair.Key == difficulty);
            }
        }

        private void RefreshDifficultyVisibility()
        {
            _difficultyRow.style.display = _mode == RpsMatchMode.SinglePlayer ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnMovePicked(RpsMove move)
        {
            if (_match == null || _awaitingPlayer == null || _match.Status != RpsMatchStatus.AwaitingMoves)
            {
                return;
            }

            if (_mode == RpsMatchMode.SinglePlayer)
            {
                if (_awaitingPlayer != RpsPlayer.One)
                {
                    return;
                }

                if (!SubmitOrReject(RpsPlayer.One, move))
                {
                    return;
                }

                _awaitingPlayer = null;
                _options.SetEnabled(false);
                _roundLabel.text = UiText.RpsGame.AiThinking;

                int generation = _runGeneration;
                _root.schedule.Execute(() => RunAiMove(generation)).StartingIn(AiThinkingDelayMilliseconds);
                return;
            }

            // LocalTwoPlayer: submit for whoever is up, then either hand the device over or resolve.
            if (!SubmitOrReject(_awaitingPlayer.Value, move))
            {
                return;
            }

            if (_match.BothMovesSubmitted)
            {
                ResolveAndRender();
            }
            else
            {
                _awaitingPlayer = RpsPlayer.Two;
                RefreshRoundUi();
            }
        }

        private bool SubmitOrReject(RpsPlayer player, RpsMove move)
        {
            var submission = _match.SubmitMove(player, move);
            if (submission.IsValid)
            {
                return true;
            }

            _router.Toast.Show(submission.InvalidReason);
            return false;
        }

        private void RunAiMove(int generation)
        {
            if (generation != _runGeneration || _match == null || _match.Status != RpsMatchStatus.AwaitingMoves)
            {
                return;
            }

            RpsMove aiMove = _ai.ChooseMove(RpsAiParams.ForDifficulty(_difficulty));
            if (!SubmitOrReject(RpsPlayer.Two, aiMove))
            {
                _awaitingPlayer = RpsPlayer.One;
                _options.SetEnabled(true);
                RefreshRoundUi();
                return;
            }

            // Both moves submitted - resolve and navigate to VS Screen
            ResolveAndRender();
        }

        private void ResolveAndRender()
        {
            RpsRoundResult result = _match.ResolveRound();

            // For both modes, navigate to VS Screen after resolution
            NavigateToVsScreen(result.MoveOne, result.MoveTwo);
        }

        private void NavigateToVsScreen(RpsMove playerMove, RpsMove opponentMove)
        {
            _router.RpsVsScreen.SetUp(playerMove, opponentMove, _mode, RpsPlayer.One);
            _router.Show(ScreenId.RpsVs);
        }

        private void ShowSeriesEnded(RpsRoundResult result)
        {
            _seriesResultLabel.text = result.SeriesWinner == RpsPlayer.One
                ? _mode == RpsMatchMode.SinglePlayer ? UiText.RpsGame.YouWon : UiText.RpsGame.Player1Won
                : _mode == RpsMatchMode.SinglePlayer ? UiText.RpsGame.YouLost : UiText.RpsGame.Player2Won;

            _options.style.display = DisplayStyle.None;
            _handoffLabel.style.display = DisplayStyle.None;
            _seriesResultLabel.style.display = DisplayStyle.Flex;
            _rematchButton.style.display = DisplayStyle.Flex;
            _roundLabel.style.display = DisplayStyle.None;
        }

        private void RefreshRoundUi()
        {
            if (_match == null || _awaitingPlayer == null)
            {
                return;
            }

            _scoreOneLabel.text = ScoreChipText(PlayerNameFor(RpsPlayer.One), _match.ScoreFor(RpsPlayer.One));
            _scoreTwoLabel.text = ScoreChipText(PlayerNameFor(RpsPlayer.Two), _match.ScoreFor(RpsPlayer.Two));

            string playerName = PlayerNameFor(_awaitingPlayer.Value);
            if (_mode == RpsMatchMode.SinglePlayer)
            {
                // Single player always waits on the human; the AI beat is handled by AiThinking.
                _roundLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.RpsGame.RoundYourMove, _match.RoundNumber + 1);
                _handoffLabel.style.display = DisplayStyle.None;
                return;
            }

            _roundLabel.text = string.Format(
                CultureInfo.InvariantCulture, UiText.RpsGame.RoundWaiting, _match.RoundNumber + 1, playerName);

            if (_awaitingPlayer == RpsPlayer.Two)
            {
                // Player 1 just locked a move - show the pass-the-device banner before Player 2 picks.
                _handoffLabel.text = string.Format(CultureInfo.InvariantCulture, UiText.RpsGame.HandoffLabel, playerName);
                _handoffLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _handoffLabel.style.display = DisplayStyle.None;
            }
        }

        private void RevealLastRound()
        {
            var last = _match.LastRound;
            if (last == null)
            {
                return;
            }

            _moveOneTitleLabel.text = PlayerNameFor(RpsPlayer.One);
            _moveOneValueLabel.text = MoveIconFor(last.MoveOne);
            _moveTwoTitleLabel.text = PlayerNameFor(RpsPlayer.Two);
            _moveTwoValueLabel.text = MoveIconFor(last.MoveTwo);
            _grid.style.display = DisplayStyle.Flex;
        }

        private void HideResults()
        {
            _grid.style.display = DisplayStyle.None;
            _seriesResultLabel.style.display = DisplayStyle.None;
            _rematchButton.style.display = DisplayStyle.None;
            _roundLabel.style.display = DisplayStyle.Flex;
        }

        private void OnRematchClicked()
        {
            _match.StartSeries();
            _awaitingPlayer = RpsPlayer.One;
            _options.style.display = DisplayStyle.Flex;
            _options.SetEnabled(true);
            _handoffLabel.style.display = DisplayStyle.None;
            HideResults();
            RefreshRoundUi();
        }

        private string PlayerNameFor(RpsPlayer player)
        {
            if (_mode == RpsMatchMode.SinglePlayer)
            {
                return player == RpsPlayer.One ? UiText.RpsGame.YouLabel : UiText.RpsGame.AiLabel;
            }

            return player == RpsPlayer.One ? UiText.RpsGame.Player1Label : UiText.RpsGame.Player2Label;
        }

        private string ScoreChipText(string playerName, int score)
        {
            string winPoints = string.Format(CultureInfo.InvariantCulture, UiText.RpsGame.WinPointsFormat, score, _match.WinPointsToWinSeries);
            return string.Format(CultureInfo.InvariantCulture, UiText.RpsGame.ScoreChipFormat, playerName, winPoints);
        }

        private static string MoveIconFor(RpsMove move)
        {
            return move switch
            {
                RpsMove.Rock => UiText.Icons.RpsRock,
                RpsMove.Paper => UiText.Icons.RpsPaper,
                RpsMove.Scissors => UiText.Icons.RpsScissors,
                _ => string.Empty
            };
        }

        private void ApplyLocalizedText()
        {
            ApplyMoveText("rps-rock-button", "rps-rock-icon-label", "rps-rock-label", RpsMove.Rock, UiText.RpsGame.Rock);
            ApplyMoveText("rps-paper-button", "rps-paper-icon-label", "rps-paper-label", RpsMove.Paper, UiText.RpsGame.Paper);
            ApplyMoveText("rps-scissors-button", "rps-scissors-icon-label", "rps-scissors-label", RpsMove.Scissors, UiText.RpsGame.Scissors);

            _difficultySectionLabel.text = UiText.BoardSelect.DifficultyLabel;
            _rematchButton.text = UiText.RpsGame.RematchButton;
            _menuButton.text = UiText.RpsGame.MenuButton;
        }

        private void ApplyMoveText(string buttonName, string iconLabelName, string textLabelName, RpsMove move, string text)
        {
            var button = _root.Q<Button>(buttonName);
            button.Q<Label>(iconLabelName).text = MoveIconFor(move);
            button.Q<Label>(textLabelName).text = text;
        }

        public void OnShow()
        {
            // Idempotent re-render: covers coming back mid-series or after a locale change. Never
            // mutates the core model.
            if (_match == null)
            {
                return;
            }

            RefreshDifficultyVisibility();
            RefreshRoundUi();
        }

        public void OnHide()
        {
        }

        public void RefreshLocalizedText()
        {
            ApplyLocalizedText();
            if (_match == null)
            {
                return;
            }

            RefreshRoundUi();
        }
    }
}