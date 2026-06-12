#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDebugCheats : MonoBehaviour
{
    [Header("Cheat Keys")]
    [SerializeField] private Key takeHitKey      = Key.F1;
    [SerializeField] private Key healHitKey      = Key.F2;
    [SerializeField] private Key fullHealKey     = Key.F3;
    [SerializeField] private Key invincibleKey   = Key.F4;
    [SerializeField] private Key reviveKey       = Key.F5;
    [SerializeField] private float invincibilityDuration = 5f;

    private Player _player;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Update()
    {
        if (_player == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[takeHitKey].wasPressedThisFrame)
            _player.TakeDamage(1f);

        if (keyboard[healHitKey].wasPressedThisFrame)
            _player.Heal(1f);

        if (keyboard[fullHealKey].wasPressedThisFrame)
            _player.Heal(_player.GetMaxHits());

        if (keyboard[invincibleKey].wasPressedThisFrame)
            _player.StartCheatInvincibility(invincibilityDuration);

        if (keyboard[reviveKey].wasPressedThisFrame)
            _player.CheatRevive(invincibilityDuration);
    }
}
#endif
