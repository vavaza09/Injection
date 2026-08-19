using UnityEngine;
using VContainer;
using Game.Characters.Player;
using Game.Components.Skills;
using Game.Tutorial;

public class PlayerSkillController : MonoBehaviour, Game.Components.Skills.ISkillReadinessProvider
{
    [Header("EMP Blast")]
    [SerializeField] private float empRadius        = 5f;
    [SerializeField] private float empStunDuration  = 3f;
    [SerializeField] private float empCooldown      = 8f;
    [SerializeField] private float empEnergyCost    = 1f;
    [SerializeField] private GameObject empVFXPrefab;
    [Tooltip("How long (s) the EMP VFX spawner emits before being stopped — keeps it a single instant burst. Lower = fewer overlapping meshes.")]
    [SerializeField] private float empVfxBurstWindow = 4f;
    [Tooltip("How long (s) the EMP VFX instance lives before being destroyed (lets meshes finish dissolving).")]
    [SerializeField] private float empVfxLifetime = 2f;

    [Header("True Damage Dash")]
    [SerializeField] private float trueDamageCooldown   = 5f;
    [SerializeField] private float trueDamageEnergyCost = 1f;
    [SerializeField] private float trueDamageDashSpeedMultiplier = 1.5f;

    [Header("Visual Feedback")]
    [SerializeField] private Color trueDamageArmedColor    = new Color(0.35f, 0.8f, 1f, 1f);

    private Core.Logging.ILogger _logger;
    private PlayerInputHandler   _inputHandler;
    private PlayerSkillEvents    _bus;
    private IEnergyPool          _energyPool;
    private PlayerAudioController _audioController;

    private EmpBlastSkill      _empSkill;
    private TrueDamageDashSkill _trueDashSkill;

    private SpriteRenderer _spriteRenderer;
    private Color          _originalColor;
    private bool           _trueDamageArmedVisual;
    private PlayerMovementVFX _movementVFX;
    private Game.Components.Movement.MovementComponent _movement;

    // Alive check — PlayerSkillController sits on the same GO as Player
    private character _character;
    private Player    _player;

    [Inject]
    public void Construct(
        Core.Logging.LoggerFactory loggerFactory,
        PlayerInputHandler inputHandler,
        IPlayerSkillEvents skillEvents,
        IEnergyPool energyPool,
        PlayerAudioController audioController)
    {
        _logger          = loggerFactory?.CreateLogger<PlayerSkillController>();
        _inputHandler    = inputHandler;
        _bus             = (PlayerSkillEvents)skillEvents;
        _energyPool      = energyPool;
        _audioController = audioController;
    }

    private void Awake()
    {
        _character      = GetComponent<character>();
        _player         = _character as Player;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
        _movementVFX = GetComponent<PlayerMovementVFX>();
        _movement    = GetComponent<Game.Components.Movement.MovementComponent>();
    }

    private void Start()
    {
        var dashImpact = GetComponent<PlayerDashImpact>();

        _empSkill = new EmpBlastSkill(
            empCooldown,
            empRadius,
            empStunDuration,
            empEnergyCost,
            _bus,
            _energyPool,
            () => (Vector2)transform.position,
            _logger);

        _trueDashSkill = new TrueDamageDashSkill(
            trueDamageCooldown,
            trueDamageEnergyCost,
            _bus,
            _energyPool,
            () => (Game.Components.Skills.ITrueDamageTarget)dashImpact,
            _logger);

        // Wire visual feedback from events
        if (_bus != null)
        {
            _bus.EmpDetonated      += OnEmpDetonated;
            _bus.TrueDamageArmed   += OnTrueDamageArmed;
            _bus.TrueDamageConsumed += OnTrueDamageConsumed;
        }

        // Wire dash impact consumed callback
        if (dashImpact != null)
            dashImpact.TrueDamageConsumed += _trueDashSkill.OnConsumed;

        if (_inputHandler != null)
        {
            _inputHandler.OnSkill1Pressed += UseEmp;
            _inputHandler.OnSkill2Pressed += UseTrueDamage;
        }
    }

    private void Update()
    {
        _empSkill?.Tick(Time.deltaTime);
        _trueDashSkill?.Tick(Time.deltaTime);

        if (_movement != null)
        {
            bool empoweredDashing = _trueDamageArmedVisual && _movement.IsDashing;
            _movement.DashPowerMultiplier = _trueDamageArmedVisual ? trueDamageDashSpeedMultiplier : 1f;
            ApplyTrueDamageTint(empoweredDashing);
            ApplyElectricFX(empoweredDashing);
        }
    }

    private void OnDestroy()
    {
        if (_electricFx != null)
            Destroy(_electricFx);

        if (_inputHandler != null)
        {
            _inputHandler.OnSkill1Pressed -= UseEmp;
            _inputHandler.OnSkill2Pressed -= UseTrueDamage;
        }

        if (_bus != null)
        {
            _bus.EmpDetonated      -= OnEmpDetonated;
            _bus.TrueDamageArmed   -= OnTrueDamageArmed;
            _bus.TrueDamageConsumed -= OnTrueDamageConsumed;
        }

        var dashImpact = GetComponent<PlayerDashImpact>();
        if (dashImpact != null && _trueDashSkill != null)
            dashImpact.TrueDamageConsumed -= _trueDashSkill.OnConsumed;
    }

    private bool IsPlayerAlive() => _character != null && _character.gameObject.activeSelf;

    private void UseEmp()
    {
        if (!IsPlayerAlive()) return;
        if (_player != null && !_player.IsAbilityUnlocked(TutorialAbilities.Skill1)) return;
        _empSkill?.Activate();
    }

    private void UseTrueDamage()
    {
        if (!IsPlayerAlive()) return;
        if (_player != null && !_player.IsAbilityUnlocked(TutorialAbilities.Skill2)) return;
        _trueDashSkill?.Activate();
    }

    // ── Visual Feedback ────────────────────────────────────────

    private void OnEmpDetonated(EmpBlastEvent e)
    {
        _audioController?.PlayEmpSound();
        if (empVFXPrefab != null)
            StartCoroutine(PlayEmpVfxOnce((Vector3)(Vector2)e.Position));
    }

    // Plays the EMP VFX as a single instant burst. The shared VFX graph uses a
    // continuous spawner, so we let it emit for one short window then Stop() the
    // spawners — already-spawned meshes finish their dissolve, but nothing re-bursts.
    private System.Collections.IEnumerator PlayEmpVfxOnce(Vector3 position)
    {
        var instance = Instantiate(empVFXPrefab, position, Quaternion.identity);
        var effects = instance.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>();
        foreach (var v in effects) { v.Reinit(); v.Play(); }

        yield return new WaitForSeconds(empVfxBurstWindow);
        foreach (var v in effects) if (v != null) v.Stop();

        Destroy(instance, empVfxLifetime);
    }

    private void OnTrueDamageArmed()
    {
        _trueDamageArmedVisual = true;
        _audioController?.PlayTrueDamageArmSound();
        _movementVFX?.EnableTrueDamageTrail();
    }

    private void OnTrueDamageConsumed()
    {
        _trueDamageArmedVisual = false;
        _audioController?.PlayTrueDamageConsumeSound();
        _movementVFX?.DisableTrueDamageTrail();
    }

    private bool _tintActive;

    private void ApplyTrueDamageTint(bool active)
    {
        if (_spriteRenderer == null || active == _tintActive) return;
        _tintActive = active;
        _spriteRenderer.color = active ? trueDamageArmedColor : _originalColor;
    }

    private GameObject _electricFx;

    // Reuses the enemy EMP-stun electric look (StunElectricFX) as a player-worn effect
    // for the duration of an empowered dash.
    private void ApplyElectricFX(bool active)
    {
        if (active)
        {
            if (_electricFx != null || _spriteRenderer == null) return;
            _electricFx = StunElectricFX.Attach(transform, _spriteRenderer.bounds);
        }
        else if (_electricFx != null)
        {
            Destroy(_electricFx);
            _electricFx = null;
        }
    }

    // ── ISkillReadinessProvider ────────────────────────────────

    public Game.Components.Skills.SkillReadout Get(Game.Components.Skills.SkillId id)
    {
        switch (id)
        {
            case Game.Components.Skills.SkillId.Emp:
            {
                bool unlocked = _player == null || _player.IsAbilityUnlocked(TutorialAbilities.Skill1);
                bool canCast  = _empSkill != null && _empSkill.CanActivate && unlocked;
                float remaining = _empSkill?.CooldownRemaining ?? 0f;
                return new Game.Components.Skills.SkillReadout(unlocked, canCast, remaining, empCooldown);
            }
            case Game.Components.Skills.SkillId.TrueDamage:
            {
                bool unlocked = _player == null || _player.IsAbilityUnlocked(TutorialAbilities.Skill2);
                bool canCast  = _trueDashSkill != null && _trueDashSkill.CanActivate && unlocked;
                float remaining = _trueDashSkill?.CooldownRemaining ?? 0f;
                return new Game.Components.Skills.SkillReadout(unlocked, canCast, remaining, trueDamageCooldown);
            }
            default:
                return new Game.Components.Skills.SkillReadout(false, false, 0f, 1f);
        }
    }

}
