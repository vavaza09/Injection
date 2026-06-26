using UnityEngine;
using UnityEngine.InputSystem;
using Game.Rooms;

public class PlayerDebugCheats : MonoBehaviour
{
    [Header("Player Cheat Keys (F1–F5)")]
    [SerializeField] private Key takeHitKey      = Key.F1;
    [SerializeField] private Key healHitKey      = Key.F2;
    [SerializeField] private Key fullHealKey     = Key.F3;
    [SerializeField] private Key invincibleKey   = Key.F4;
    [SerializeField] private Key reviveKey       = Key.F5;
    [SerializeField] private float invincibilityDuration = 5f;

    [Header("Kill Boss (F8)")]
    [SerializeField] private Key killBossKey = Key.F8;

    [Header("Room Warp (Ctrl+1–7)")]
    [SerializeField] private RoomCatalog roomCatalog;

    private Player _player;
    private RoomManager _roomManager;

#if UNITY_EDITOR
    private bool _cheatsEnabled = true;
#else
    private bool _cheatsEnabled;
#endif

    private static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7
    };

    private void Awake()
    {
        _player = GetComponent<Player>();
        _roomManager = FindFirstObjectByType<RoomManager>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        HandleToggle(kb);

        if (!_cheatsEnabled) return;

        if (_player != null)
            HandlePlayerCheats(kb);

        HandleWarpCheats(kb);
        HandleKillBoss(kb);
    }

    private void HandleToggle(Keyboard kb)
    {
        bool ctrlHeld   = kb[Key.LeftCtrl].isPressed  || kb[Key.RightCtrl].isPressed;
        bool shiftHeld  = kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;

        if (ctrlHeld && shiftHeld && kb[Key.C].wasPressedThisFrame)
            _cheatsEnabled = !_cheatsEnabled;
    }

    private void HandlePlayerCheats(Keyboard kb)
    {
        if (kb[takeHitKey].wasPressedThisFrame)
            _player.TakeDamage(1f);

        if (kb[healHitKey].wasPressedThisFrame)
            _player.Heal(1f);

        if (kb[fullHealKey].wasPressedThisFrame)
            _player.Heal(_player.GetMaxHits());

        if (kb[invincibleKey].wasPressedThisFrame)
            _player.StartCheatInvincibility(invincibilityDuration);

        if (kb[reviveKey].wasPressedThisFrame)
            _player.CheatRevive(invincibilityDuration);
    }

    private void HandleWarpCheats(Keyboard kb)
    {
        if (_roomManager == null || roomCatalog == null) return;

#if !UNITY_EDITOR
        bool ctrlHeld = kb[Key.LeftCtrl].isPressed || kb[Key.RightCtrl].isPressed;
        if (!ctrlHeld) return;
#endif

        for (int i = 0; i < DigitKeys.Length && i < roomCatalog.Count; i++)
        {
            if (kb[DigitKeys[i]].wasPressedThisFrame)
            {
                var room = roomCatalog.GetByIndex(i);
                if (room != null)
                    _roomManager.LoadRoom(room.roomId, room.sceneName);
                break;
            }
        }
    }

    private void HandleKillBoss(Keyboard kb)
    {
        if (!kb[killBossKey].wasPressedThisFrame) return;

        var boss = FindFirstObjectByType<BossBase>();
        if (boss == null) return;

        boss.TakeDamage(int.MaxValue);
    }
}
