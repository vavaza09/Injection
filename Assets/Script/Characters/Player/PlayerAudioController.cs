using UnityEngine;
using Core.Logging;

namespace Game.Characters.Player
{
    /// <summary>
    /// Handles all player audio (Plain C# - No MonoBehaviour)
    /// </summary>
    public class PlayerAudioController
    {
        private readonly Core.Logging.ILogger _logger;
        private readonly Transform _playerTransform;
        private readonly AudioClip _jumpSound;
        private readonly AudioClip _attackSound;
        private readonly AudioClip _hurtSound;

        // Constructor - DI via VContainer
        public PlayerAudioController(
            Transform playerTransform,
            AudioClip jumpSound,
            AudioClip attackSound,
            AudioClip hurtSound,
            LoggerFactory loggerFactory)
        {
            _playerTransform = playerTransform;
            _jumpSound = jumpSound;
            _attackSound = attackSound;
            _hurtSound = hurtSound;
            _logger = loggerFactory?.CreateLogger<PlayerAudioController>();

            _logger?.Log("PlayerAudioController initialized");
        }

        public void PlayJumpSound()
        {
            PlaySound(_jumpSound);
            _logger?.Log("Jump sound played");
        }

        public void PlayAttackSound()
        {
            PlaySound(_attackSound);
            _logger?.Log("Attack sound played");
        }

        public void PlayHurtSound()
        {
            PlaySound(_hurtSound);
            _logger?.Log("Hurt sound played");
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _playerTransform != null)
            {
                AudioSource.PlayClipAtPoint(clip, _playerTransform.position);
            }
            else if (clip == null)
            {
                _logger?.LogWarning("AudioClip is null");
            }
        }
    }
}