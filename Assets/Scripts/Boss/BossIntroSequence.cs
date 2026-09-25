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

    [Header("Hammer Climb")]
    [Tooltip("Which handIKTargets entry is the hammer arm — it raises up and over the ledge, then " +
             "slams down onto it instead of reaching up like a claw. -1 makes every hand use the claw motion.")]
    [SerializeField] private int hammerHandIndex = 1;
    [Tooltip("Apex the hammer raises to, relative to its ledge position. The horizontal offset gives the " +
             "arm real angular swing (so the slam reads at full IK reach) and arcs the hammer in from the side.")]
    [SerializeField] private Vector2 hammerApexOffset = new Vector2(3.35f, 10f);
    [SerializeField] private float hammerRaiseDuration = 0.9f;
    [SerializeField] private float hammerSmashDuration = 0.18f;
    [Tooltip("How much higher than the authored ledge spot the hammer stays planted for the rest of the " +
             "intro (body rise + reveal) — intro-only flourish, eased back to the real spot below.")]
    [SerializeField] private float hammerLedgeHoldOffset = 2.5f;
    [Tooltip("How long the hammer takes to ease back down to its real authored spot right before " +
             "BossHammerAttack re-enables — that component captures whatever position it's at as its " +
             "permanent idle/attack anchor, so it must land exactly on the authored spot, unchanged.")]
    [SerializeField] private float hammerSettleDuration = 0.4f;
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
        // Mute the room's ambient bed (and any music) for the silent, rumble-only opening beat.
        // Starting boss music here instead used to race the ambient bed's own fade-in from room
        // entry — two PlayMusicWithFade coroutines stomping each other produced an audible
        // "song starts, cuts after ~1.5s" glitch. RestoreSceneAudio() undoes this once the
        // sequence reaches its reveal beat below.
        SoundManager.SuspendSceneAudio(0f);
        CameraShake.Shake(preShakeIntensity, preHandDelay);

        yield return new WaitForSeconds(preHandDelay);

        // Hands climb up one at a time, from their lowered (root-dragged) position to their
        // authored ledge position.
        for (int i = 0; i < handIKTargets.Length; i++)
        {
            var hand = handIKTargets[i];
            if (hand == null) continue;

            if (i == hammerHandIndex)
            {
                yield return HammerSmashClimb(hand, _authoredHandWorldPos[i]);
            }
            else
            {
                SoundManager.PlaySound(SoundType.BOSS_CLAW_ANTICIPATION);
                yield return LerpWorldPosition(hand, hand.position, _authoredHandWorldPos[i], handRiseDuration);
                SoundManager.PlaySound(SoundType.BOSS_CLAW_IMPACT);
                SpawnHandImpact(_authoredHandWorldPos[i]);
            }
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
            RepinHandsHeld();
            yield return null;
        }
        bossRoot.position = _authoredRootWorldPos;
        RepinHandsHeld();

        _weakPoints?.SetIntroHighlight(true);
        SoundManager.PlaySound(SoundType.BOSS_WEAKPOINT_REVEAL);
        // Reuses Room_Boss's own SceneAudio entry (clip + authored volume) — a bare
        // PlayMusic(MusicType.BOSS) would crossfade in at whatever volume was last set
        // elsewhere (e.g. the title screen's near-zero), landing inaudible.
        SoundManager.RestoreSceneAudio();
        SoundManager.PlayCurrentSceneMusic();
        yield return new WaitForSeconds(weakPointShowDuration);

        // Re-enable Boss so PollDetection() fires PlayerEnteredRange next frame — the health
        // bar's own Show() reveal runs exactly once, no second trigger path needed.
        if (_boss     != null) _boss.enabled     = _bossWasEnabled;
        if (_idleAnim != null) _idleAnim.enabled = _idleAnimWasEnabled;

        yield return new WaitForSeconds(healthBarHoldDuration);
        _weakPoints?.SetIntroHighlight(false);

        // Ease the hammer down from its held-high intro pose to its real authored spot before
        // BossHammerAttack re-enables — its Start() captures whatever position this Transform is
        // at as the permanent idle/attack anchor for the rest of the fight.
        if (hammerHandIndex >= 0 && hammerHandIndex < handIKTargets.Length && handIKTargets[hammerHandIndex] != null)
        {
            Transform hammerHand = handIKTargets[hammerHandIndex];
            yield return LerpWorldPosition(hammerHand, hammerHand.position,
                _authoredHandWorldPos[hammerHandIndex], hammerSettleDuration);
        }

        RestoreAttacks();
        FinishIntro();
    }

    private void RepinHands()
    {
        for (int i = 0; i < handIKTargets.Length; i++)
            if (handIKTargets[i] != null)
                handIKTargets[i].position = _authoredHandWorldPos[i];
    }

    // Same as RepinHands, but keeps the hammer at its held-high intro pose instead of its real
    // authored spot — used while the body rises and during the reveal hold, before the hammer
    // eases back down to the real spot ahead of RestoreAttacks().
    private void RepinHandsHeld()
    {
        for (int i = 0; i < handIKTargets.Length; i++)
        {
            if (handIKTargets[i] == null) continue;
            handIKTargets[i].position = i == hammerHandIndex ? HammerHeldPos(i) : _authoredHandWorldPos[i];
        }
    }

    private Vector3 HammerHeldPos(int i) => _authoredHandWorldPos[i] + new Vector3(0f, hammerLedgeHoldOffset, 0f);

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

    // Hammer arm climbs by raising up and over the ledge, then smashing down and planting higher
    // than its real authored spot for the rest of the intro (see hammerLedgeHoldOffset) —
    // matches BossHammerAttack's swing feel instead of reaching up and sticking like the claw.
    private IEnumerator HammerSmashClimb(Transform hand, Vector3 ledgePos)
    {
        float xDir = bossRoot.lossyScale.x >= 0f ? 1f : -1f;
        Vector3 apex = ledgePos + new Vector3(hammerApexOffset.x * xDir, hammerApexOffset.y, 0f);
        Vector3 heldPos = HammerHeldPos(hammerHandIndex);

        SoundManager.PlaySound(SoundType.BOSS_HAMMER_WINDUP);
        yield return LerpWorldPosition(hand, hand.position, apex, hammerRaiseDuration);

        SoundManager.PlaySound(SoundType.BOSS_HAMMER_SWING);
        yield return SmashDown(hand, apex, heldPos, hammerSmashDuration);

        SoundManager.PlaySound(SoundType.BOSS_HAMMER_IMPACT);
        SpawnHandImpact(heldPos);
    }

    // Arcs from the apex back in over the ledge, then slams straight down — same bezier
    // pattern as BossHammerAttack's swing, with an ease-in so the slam reads as a smash.
    private static IEnumerator SmashDown(Transform t, Vector3 from, Vector3 to, float duration)
    {
        Vector3 control = new Vector3(to.x, from.y, from.z);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            k *= k;
            t.position = QuadraticBezier(from, control, to, k);
            yield return null;
        }
        t.position = to;
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
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

        // Skip landed before the reveal beat that normally undoes SuspendSceneAudio() — do it
        // here instead, or the ambient bed (and all future scene music) stays muted forever.
        SoundManager.RestoreSceneAudio();
        SoundManager.PlayCurrentSceneMusic();
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
        // coroutine's tail ever running — don't strand the persistent player input-locked,
        // and don't leave the scene's ambient bed muted for whatever loads next.
        if (_running)
        {
            PlayerInputGate.Set(true);
            _pauseMenu?.SetPauseInputEnabled(true);
            SoundManager.RestoreSceneAudio();
        }
        DisposeSkip();
    }
}
