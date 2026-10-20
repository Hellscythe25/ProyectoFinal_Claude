using System;
using TTTXO.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Character selection for Single Player RPS: shows the 3 combatants in Idle,
    /// player picks one (which = their move), AI picks randomly, then goes to VS Screen.
    /// </summary>
    public sealed class RpsCharacterSelectScreenController : IScreenController
    {
        private readonly VisualElement _root;
        private readonly ScreenRouter _router;
        private readonly System.Random _rng = new();

        private readonly VisualElement _caballeroBtn;
        private readonly VisualElement _magaBtn;
        private readonly VisualElement _asesinoBtn;
        private readonly Label _caballeroName;
        private readonly Label _magaName;
        private readonly Label _asesinoName;

        private VSAnimationController _caballeroAnim;
        private VSAnimationController _magaAnim;
        private VSAnimationController _asesinoAnim;
        private RpsCharacterDatabase _database;

        public RpsCharacterSelectScreenController(VisualElement root, ScreenRouter router)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");

            _caballeroBtn = _root.Q<VisualElement>("charselect-caballero");
            _magaBtn = _root.Q<VisualElement>("charselect-maga");
            _asesinoBtn = _root.Q<VisualElement>("charselect-asesino");
            _caballeroName = _root.Q<Label>("charselect-caballero-name");
            _magaName = _root.Q<Label>("charselect-maga-name");
            _asesinoName = _root.Q<Label>("charselect-asesino-name");

            var backButton = _root.Q<Button>("charselect-back-button");
            backButton.text = UiText.Common.BackIcon;
            backButton.clicked += () => _router.Show(ScreenId.RpsModeSelect);

            _caballeroBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Rock));
            _magaBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Paper));
            _asesinoBtn.RegisterCallback<ClickEvent>(_ => OnCharacterPicked(RpsMove.Scissors));

            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            _root.Q<Label>("charselect-title-label").text = UiText.RpsCharacterSelect.Title;
            _root.Q<Label>("charselect-subtitle-label").text = UiText.RpsCharacterSelect.Subtitle;
            _caballeroName.text = "Caballero";
            _magaName.text = "Maga";
            _asesinoName.text = "Asesino";
        }

        public void OnShow()
        {
            // Ensure database is loaded (might not be ready in constructor if Resources not loaded yet)
            if (_database == null)
            {
                _database = Resources.Load<RpsCharacterDatabase>("RpsCharacterDatabase");
            }

            // Start idle animations for all 3 characters
            if (_database != null)
            {
                var cabFrames = _database.GetFrames(RpsMove.Rock);
                var magaFrames = _database.GetFrames(RpsMove.Paper);
                // Asesino has no frames - fallback

                var cabSpriteEl = _caballeroBtn.Q<VisualElement>("charselect-caballero-sprite");
                var magaSpriteEl = _magaBtn.Q<VisualElement>("charselect-maga-sprite");
                var asesinoSpriteEl = _asesinoBtn.Q<VisualElement>("charselect-asesino-sprite");

                if (cabSpriteEl != null && magaSpriteEl != null && asesinoSpriteEl != null)
                {
                    _caballeroAnim = new VSAnimationController(cabSpriteEl, _caballeroName, "Caballero", cabFrames.Idle, cabFrames.Attack, cabFrames.Hurt);
                    _magaAnim = new VSAnimationController(magaSpriteEl, _magaName, "Maga", magaFrames.Idle, magaFrames.Attack, magaFrames.Hurt);
                    _asesinoAnim = new VSAnimationController(asesinoSpriteEl, _asesinoName, "Asesino", Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>());
                }
                else
                {
                    Debug.LogError("Character select: sprite elements not found in UXML!");
                }
            }
            else
            {
                Debug.LogError("RpsCharacterDatabase not found in Resources!");
            }

            _caballeroAnim?.PlayIdle();
            _magaAnim?.PlayIdle();
            _asesinoAnim?.PlayIdle();
        }

        public void OnHide()
        {
            _caballeroAnim?.Dispose();
            _magaAnim?.Dispose();
            _asesinoAnim?.Dispose();
        }

        private void OnCharacterPicked(RpsMove playerMove)
        {
            // AI picks randomly FROM THE OTHER TWO MOVES (not the same as player)
            var otherMoves = new System.Collections.Generic.List<RpsMove> { RpsMove.Rock, RpsMove.Paper, RpsMove.Scissors };
            otherMoves.Remove(playerMove);
            RpsMove aiMove = otherMoves[_rng.Next(otherMoves.Count)];

            // Navigate to VS Screen
            _router.RpsVsScreen.SetUp(playerMove, aiMove, RpsMatchMode.SinglePlayer, RpsPlayer.One);
            _router.Show(ScreenId.RpsVs);
        }

        public void RefreshLocalizedText() => ApplyLocalizedText();
    }
}