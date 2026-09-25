using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Rooms;
using Game.UI;

// Place on a mid-platform trigger GameObject in Room_Boss (Collider2D set as trigger).
// One-shot: gates seal (via RoomLockTrigger.Lock()), boss music starts, then the boss climbs
// up from below one hand at a time before the body follows, weak points tell-flash, and the
// health bar reveals — player input is frozen for the whole beat. Esc/gamepad Start skips
// straight to the fight-ready pose behind a screen fade.
//
// Trick that makes this safe: BossHammerAttack/BossClawAttack/BossJunkSmashAttack cache their
// arm idle positions in world space, once, in Start() — with no re-capture path. Start() doesn't
// run until a component is first enabled, so this sequence disables those components in its own
// Awake() (before their Start() can fire) and only re-enables them once the boss is sitting at
// its authored pose. Same trick gates the health bar: disabling Boss stops PollDetection(), so
// BossHealthBarController's PlayerEnteredRange subscription can't fire early; re-enabling it at
// the right beat reveals the bar exactly once, with no second Show() path.
[RequireComponent(typeof(Collider2D))]
public class BossIntroSequence : MonoBehaviour
{
    [Header("References")]
    [Tooltip("===Boss===Last — the boss root. Left null (or already destroyed) means a defeated boss: gates still seal, no cutscene.")]
    [SerializeField] private Transform bossRoot;
    [Tooltip("Hand IK end-effector targets, in the order they should climb up.")]
    [SerializeField] private Transform[] handIKTargets;
    [Tooltip("The arena's RoomLockTrigger — set its own lockOnPlayerEnter to false and let this sequence call Lock() instead.")]
    [SerializeField] private RoomLockTrigger roomLock;

    [Header("Timing")]
    [SerializeField] private float riseDistance = 8f;
    [SerializeField] private float preHandDelay = 1f;
    [SerializeField] private float handRiseDuration = 0.8f;
    [SerializeField] private float betweenHands = 0.5f;
    [SerializeField] private float bodyRiseDuration = 1.5f;
    [SerializeField] private float weakPointShowDuration = 1.2f;
    [SerializeField] private float healthBarHoldDuration = 1.5f;

    [Header("Pre-Reveal Rumble")]
    [Tooltip("Ground rumble for the preHandDelay window, before the first hand appears.")]
    [SerializeField] private float preShakeIntensity = 0.5f;

    [Header("Hand Impact VFX")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float impactVfxRadius = 2f;
    [SerializeField] private float shakeIntensity = 0.6f;
    [SerializeField] private float shakeDuration = 0.3f;

    private bool _fired;
    private bool _running;
    private IRoomLoader _roomLoader;
    private PauseMenuController _pauseMenu;
    private InputAction _skipAction;

    private Vector3   _authoredRootWorldPos;
    private Vector3[] _authoredHandWorldPos;

    private Boss                 _boss;
    private BossIdleAnimation    _idleAnim;
    private BossAttackManager    _attackManager;
    private BossClawAttack       _claw;
    private BossHammerAttack     _hammer;
    private BossGasAttack        _gas;
    private BossJunkSmashAttack  _junk;
    private BossWeakPointManager _weakPoints;

    private bool _bossWasEnabled, _idleAnimWasEnabled, _attackManagerWasEnabled,
                 _clawWasEnabled, _hammerWasEnabled, _gasWasEnabled, _junkWasEnabled;

    private void Awake()
    {
        if (bossRoot == null) return;

        _boss          = bossRoot.GetComponentInChildren<Boss>(true);
        _idleAnim      = bossRoot.GetComponentInChildren<BossIdleAnimation>(true);
        _attackManager = bossRoot.GetComponentInChildren<BossAttackManager>(true);
        _claw          = bossRoot.GetComponentInChildren<BossClawAttack>(true);
        _hammer        = bossRoot.GetComponentInChildren<BossHammerAttack>(true);
        _gas           = bossRoot.GetComponentInChildren<BossGasAttack>(true);
        _junk          = bossRoot.GetComponentInChildren<BossJunkSmashAttack>(true);
        _weakPoints    = bossRoot.GetComponentInChildren<BossWeakPointManager>(true);

        _authoredRootWorldPos = bossRoot.position;
        _authoredHandWorldPos = new Vector3[handIKTargets.Length];
        for (int i = 0; i < handIKTargets.Length; i++)
            if (handIKTargets[i] != null)
                _authoredHandWorldPos[i] = handIKTargets[i].position;

        // Lower the whole boss (hands are descendants, so they drop with it automatically).
        bossRoot.position = _authoredRootWorldPos - new Vector3(0f, riseDistance, 0f);

        // Disable before any of these run Start() — defers their world-space idle capture
        // to the moment we restore the boss to its authored pose.
        if (_boss          != null) { _bossWasEnabled          = _boss.enabled;          _boss.enabled          = false; }
        if (_idleAnim      != null) { _idleAnimWasEnabled      = _idleAnim.enabled;      _idleAnim.enabled      = false; }
        if (_attackManager != null) { _attackManagerWasEnabled = _attackManager.enabled; _attackManager.enabled = false; }
        if (_claw          != null) { _clawWasEnabled          = _claw.enabled;          _claw.enabled          = false; }
        if (_hammer        != null) { _hammerWasEnabled        = _hammer.enabled;        _hammer.enabled        = false; }
        if (_gas           != null) { _gasWasEnabled           = _gas.enabled;           _gas.enabled           = false; }
        if (_junk          != null) { _junkWasEnabled          = _junk.enabled;          _junk.enabled          = false; }
    }

    private void Start()
    {
        _roomLoader = FindFirstObjectByType<RoomManager>();
        _pauseMenu  = FindFirstObjectByType<PauseMenuController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_fired) return;
        if (_roomLoader != null && _roomLoader.IsTransitioning) return;
        // Player collider can live on the Cape layer, not the Player-tagged root — match by
        // component like MusicZoneTrigger does, not CompareTag.
        if (other.GetComponentInParent<Player>() == null) return;

        _fired = true;

        if (bossRoot == null)
        {
            // Boss already defeated (BossPersistence destroyed it) — still seal the room.
            roomLock?.Lock();
            return;
        }

        _running = true;
        PlayerInputGate.Set(false);
        _pauseMenu?.SetPauseInputEnabled(false);
        EnableSkip();

        StartCoroutine(IntroRoutine());
    }

    private void EnableSkip()
    {
        _skipAction = new InputAction("SkipBossIntro", InputActionType.Button);
        _skipAction.AddBinding("<Keyboard>/escape");
        _skipAction.AddBinding("<Gamepad>/start");
        _skipAction.performed += OnSkipPressed;
        _skipAction.Enable();
    }

    private void OnSkipPressed(InputAction.CallbackContext ctx)
    {
        if (!_running) return;
        StopAllCoroutines();
        SnapToFightReady();
        _running = false;

        ScreenFader.Instance?.FadeOut(() =>
        {
            // Boss is already fight-ready under the black screen; just fade back in.
            ScreenFader.Instance?.FadeIn(FinishIntro);
        });

        // No ScreenFader in the scene (shouldn't happen — it's Bootstrap-persistent) — don't
        // strand the player input-locked behind a fade that will never run.
        if (ScreenFader.Instance == null) FinishIntro();
    }

    private IEnumerator IntroRoutine()
    {
        roomLock?.Lock();
        // Reuses Room_Boss's own SceneAudio entry (clip + authored volume) — a bare
        // PlayMusic(MusicType.BOSS) would crossfade in at whatever volume was last set
        // elsewhere (e.g. the title screen's near-zero), landing inaudible.
        SoundManager.PlayCurrentSceneMusic();
        CameraShake.Shake(preShakeIntensity, preHandDelay);

        yield return new WaitForSeconds(preHandDelay);

        // Hands climb up one at a time, from their lowered (root-dragged) position to their
        // authored ledge position.
        for (int i = 0; i < handIKTargets.Length; i++)
        {
            var hand = handIKTargets[i];
            if (hand == null) continue;

            SoundManager.PlaySound(SoundType.BOSS_CLAW_ANTICIPATION);
            yield return LerpWorldPosition(hand, hand.position, _authoredHandWorldPos[i], handRiseDuration);
            SoundManager.PlaySound(SoundType.BOSS_CLAW_IMPACT);
            SpawnHandImpact(_authoredHandWorldPos[i]);
            yield return new WaitForSeconds(betweenHands);
        }

        // Body rises to meet the now-planted hands; re-pin each hand's world position every
        // frame so it stays fixed on the ledge instead of drifting back down with the root.
        Vector3 startRootPos = bossRoot.position;
        float t = 0f;
        while (t < bodyRiseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / bodyRiseDuration);
            bossRoot.position = Vector3.Lerp(startRootPos, _authoredRootWorldPos, k);
            RepinHands();
            yield return null;
        }
        bossRoot.position = _authoredRootWorldPos;
        RepinHands();

        _weakPoints?.SetIntroHighlight(true);
        SoundManager.PlaySound(SoundType.BOSS_WEAKPOINT_REVEAL);
        yield return new WaitForSeconds(weakPointShowDuration);

        // Re-enable Boss so PollDetection() fires PlayerEnteredRange next frame — the health
        // bar's own Show() reveal runs exactly once, no second trigger path needed.
        if (_boss     != null) _boss.enabled     = _bossWasEnabled;
        if (_idleAnim != null) _idleAnim.enabled = _idleAnimWasEnabled;

        yield return new WaitForSeconds(healthBarHoldDuration);
        _weakPoints?.SetIntroHighlight(false);

        RestoreAttacks();
        FinishIntro();
    }

    private void RepinHands()
    {
        for (int i = 0; i < handIKTargets.Length; i++)
            if (handIKTargets[i] != null)
                handIKTargets[i].position = _authoredHandWorldPos[i];
    }

    private void SpawnHandImpact(Vector3 worldPos)
    {
        if (impactVfxPrefab != null)
        {
            var go = Instantiate(impactVfxPrefab, worldPos, Quaternion.identity);
            go.GetComponent<StompImpactVFX>()?.Initialize(impactVfxRadius);
        }
        CameraShake.Shake(shakeIntensity, shakeDuration);
    }

    private static IEnumerator LerpWorldPosition(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            t.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        t.position = to;
    }

    // Used by the skip path — jumps straight to the pose the full sequence ends at.
    private void SnapToFightReady()
    {
        if (bossRoot != null) bossRoot.position = _authoredRootWorldPos;
        RepinHands();
        _weakPoints?.SetIntroHighlight(false);

        if (_boss     != null) _boss.enabled     = _bossWasEnabled;
        if (_idleAnim != null) _idleAnim.enabled = _idleAnimWasEnabled;
        RestoreAttacks();
    }

    private void RestoreAttacks()
    {
        // _attackManager is deliberately left out here — it's re-enabled in FinishIntro(),
        // after the skip fade-in. Enabling it here would let it start attacking the same
        // frame the arms are repinned, before BossClawAttack/BossHammerAttack's Start() has
        // captured idle, yanking the arm off pose right as the screen clears.
        if (_claw   != null) _claw.enabled   = _clawWasEnabled;
        if (_hammer != null) _hammer.enabled = _hammerWasEnabled;
        if (_gas    != null) _gas.enabled    = _gasWasEnabled;
        if (_junk   != null) _junk.enabled   = _junkWasEnabled;
    }

    private void FinishIntro()
    {
        if (_attackManager != null) _attackManager.enabled = _attackManagerWasEnabled;
        _running = false;
        PlayerInputGate.Set(true);
        _pauseMenu?.SetPauseInputEnabled(true);
        DisposeSkip();
    }

    private void DisposeSkip()
    {
        if (_skipAction == null) return;
        _skipAction.performed -= OnSkipPressed;
        _skipAction.Disable();
        _skipAction.Dispose();
        _skipAction = null;
    }

    private void OnDestroy()
    {
        // A mid-intro Room_Boss reload (e.g. death) destroys this GameObject without the
        // coroutine's tail ever running — don't strand the persistent player input-locked.
        if (_running)
        {
            PlayerInputGate.Set(true);
            _pauseMenu?.SetPauseInputEnabled(true);
        }
        DisposeSkip();
    }
}
