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

        public void PlayDeathSound()
        {
            SoundManager.PlaySound(SoundType.DEATH);
            _logger?.Log("Death sound played");
        }

        public void PlayLandSound()
        {
            SoundManager.PlaySound(SoundType.LAND);
            _logger?.Log("Land sound played");
        }

        public void PlayGrabSound()
        {
            SoundManager.PlaySound(SoundType.GRAB);
            _logger?.Log("Grab sound played");
        }

        public void PlayEnergyPickupSound()
        {
            SoundManager.PlaySound(SoundType.ENERGY);
            _logger?.Log("Energy pickup sound played");
        }

        public void PlayEmpSound()
        {
            SoundManager.PlaySound(SoundType.EMP);
            _logger?.Log("EMP sound played");
        }

        public void PlayTrueDamageArmSound()
        {
            SoundManager.PlaySound(SoundType.TRUEDAMAGE_ARM);
            _logger?.Log("True damage arm sound played");
        }

        public void PlayTrueDamageConsumeSound()
        {
            SoundManager.PlaySound(SoundType.TRUEDAMAGE_CONSUME);
            _logger?.Log("True damage consume sound played");
        }

        public void PlaySlowMoSound()
        {
            SoundManager.PlaySound(SoundType.SLOWMO);
            _logger?.Log("Slow-mo sound played");
        }

        public void PlayFootstepSound()
        {
            SoundManager.PlaySound(SoundType.FOOTSTEP);
            _logger?.Log("Footstep sound played");
        }

        public void StartWallSlideLoop()
        {
            SoundManager.StartLoop(SoundType.WALLSLIDE);
            _logger?.Log("Wall slide loop started");
        }

        public void StopWallSlideLoop()
        {
            SoundManager.StopLoop();
            _logger?.Log("Wall slide loop stopped");
        }
    }
}