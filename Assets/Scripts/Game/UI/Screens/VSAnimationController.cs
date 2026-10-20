using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TTTXO.Game.UI.Screens
{
    /// <summary>
    /// Controls a single character's sprite animations on the VS Screen.
    /// Uses pre-extracted Texture2D frames (from RpsCharacterDatabase) and swaps
    /// background-image on a VisualElement frame-by-frame via schedule.Execute.
    /// Designed for the RPS VS Screen: Idle loop, Attack (1s), Hurt (1s), Draw text.
    /// </summary>
    public sealed class VSAnimationController : IDisposable
    {
        private readonly VisualElement _spriteElement;
        private readonly Label _nameLabel;
        private readonly string _characterName;

        private readonly Texture2D[] _idleTextures;
        private readonly Texture2D[] _attackTextures;
        private readonly Texture2D[] _hurtTextures;

        private IVisualElementScheduledItem _animationTask;
        private int _currentFrame;
        private AnimationState _state;
        private readonly Action _onAnimationComplete;
        private bool _disposed;

        public enum AnimationState
        {
            Idle,
            Attack,
            Hurt,
            Draw
        }

        /// <param name="spriteElement">VisualElement that will display the sprite (background-image)</param>
        /// <param name="nameLabel">Label for character name</param>
        /// <param name="characterName">Display name (Caballero, Maga, Asesino)</param>
        /// <param name="idleFrames">Idle animation frames in order (pre-extracted Texture2Ds)</param>
        /// <param name="attackFrames">Attack animation frames in order</param>
        /// <param name="hurtFrames">Hurt animation frames in order</param>
        /// <param name="onAnimationComplete">Callback when Attack/Hurt finishes and returns to Idle</param>
        public VSAnimationController(
            VisualElement spriteElement,
            Label nameLabel,
            string characterName,
            Texture2D[] idleFrames,
            Texture2D[] attackFrames,
            Texture2D[] hurtFrames,
            Action onAnimationComplete = null)
        {
            _spriteElement = spriteElement ?? throw new ArgumentNullException(nameof(spriteElement));
            _nameLabel = nameLabel;
            _characterName = characterName;
            _idleTextures = idleFrames ?? Array.Empty<Texture2D>();
            _attackTextures = attackFrames ?? Array.Empty<Texture2D>();
            _hurtTextures = hurtFrames ?? Array.Empty<Texture2D>();
            _onAnimationComplete = onAnimationComplete;

            if (_nameLabel != null) _nameLabel.text = characterName;
        }

        /// <summary>
        /// Starts the Idle loop. Call once on screen enter.
        /// </summary>
        public void PlayIdle()
        {
            if (_disposed) return;
            StopAnimation();
            _state = AnimationState.Idle;
            _currentFrame = 0;
            if (_idleTextures.Length > 0)
            {
                ApplyFrame(_idleTextures[0]);
                // Idle at 12 FPS (~83ms per frame)
                _animationTask = _spriteElement.schedule.Execute(TickIdle).Every(83);
            }
            else
            {
                // Fallback: no frames (e.g., Asesino) - show emoji via USS class
                _spriteElement.style.backgroundImage = StyleKeyword.Null;
                _spriteElement.RemoveFromClassList("vs-sprite--animated");
                _spriteElement.AddToClassList("vs-sprite--fallback");
            }
        }

        /// <summary>
        /// Plays Attack animation once (1 second total), then returns to Idle.
        /// </summary>
        public void PlayAttack()
        {
            if (_disposed) return;
            PlayOnce(_attackTextures, AnimationState.Attack);
        }

        /// <summary>
        /// Plays Hurt animation once (1 second total), then returns to Idle.
        /// </summary>
        public void PlayHurt()
        {
            if (_disposed) return;
            PlayOnce(_hurtTextures, AnimationState.Hurt);
        }

        /// <summary>
        /// Shows "EMPATE" text instead of character.
        /// </summary>
        public void ShowDraw()
        {
            if (_disposed) return;
            StopAnimation();
            _state = AnimationState.Draw;
            _spriteElement.style.backgroundImage = StyleKeyword.Null;
            _spriteElement.AddToClassList("vs-sprite--draw");
            if (_nameLabel != null) _nameLabel.text = "EMPATE";
        }

        /// <summary>
        /// Returns to Idle from Draw state.
        /// </summary>
        public void HideDraw()
        {
            if (_disposed) return;
            _spriteElement.RemoveFromClassList("vs-sprite--draw");
            _spriteElement.RemoveFromClassList("vs-sprite--fallback");
            PlayIdle();
        }

        private void PlayOnce(Texture2D[] frames, AnimationState state)
        {
            if (frames.Length == 0)
            {
                _onAnimationComplete?.Invoke();
                return;
            }

            StopAnimation();
            _state = state;
            _currentFrame = 0;
            ApplyFrame(frames[0]);

            int totalFrames = frames.Length;
            float totalDuration = 1f; // 1 second as specified
            float frameTime = totalDuration / totalFrames;
            int frameMs = Mathf.RoundToInt(frameTime * 1000f);

            _animationTask = _spriteElement.schedule.Execute(() => TickOnce(frames, totalFrames)).Every(frameMs);
        }

        private void TickIdle()
        {
            if (_disposed || _state != AnimationState.Idle) return;
            _currentFrame = (_currentFrame + 1) % _idleTextures.Length;
            ApplyFrame(_idleTextures[_currentFrame]);
        }

        private void TickOnce(Texture2D[] frames, int totalFrames)
        {
            if (_disposed) return;

            _currentFrame++;
            if (_currentFrame >= totalFrames)
            {
                StopAnimation();
                _state = AnimationState.Idle;
                _onAnimationComplete?.Invoke();
                PlayIdle();
                return;
            }

            ApplyFrame(frames[_currentFrame]);
        }

        private void ApplyFrame(Texture2D tex)
        {
            if (tex == null) return;
            _spriteElement.style.backgroundImage = new StyleBackground(tex);
            // Element size is fixed in USS; background-size: contain centers the frame
        }

        private void StopAnimation()
        {
            _animationTask?.Pause();
            _animationTask = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAnimation();
        }
    }
}