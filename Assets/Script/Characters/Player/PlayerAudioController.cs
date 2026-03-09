using Core.Logging;

namespace Game.Characters.Player
{
    /// <summary>
    /// Handles all player audio using SoundManager (Plain C# - No MonoBehaviour)
    /// </summary>
    public class PlayerAudioController
    {
        private readonly Core.Logging.ILogger _logger;

        public PlayerAudioController(LoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<PlayerAudioController>();
            _logger?.Log("PlayerAudioController initialized");
        }

        public void PlayJumpSound()
        {
            SoundManager.PlaySound(SoundType.JUMP);
            _logger?.Log("Jump sound played");
        }

        public void PlayAttackSound()
        {
            SoundManager.PlaySound(SoundType.SWORD);
            _logger?.Log("Attack sound played");
        }

        public void PlayHurtSound()
        {
            SoundManager.PlaySound(SoundType.HURT);
            _logger?.Log("Hurt sound played");
        }

        public void PlayDashSound()
        {
            SoundManager.PlaySound(SoundType.DASH);
            _logger?.Log("Dash sound played");
        }
    }
}