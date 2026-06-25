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

    [Header("True Damage Dash")]
    [SerializeField] private float trueDamageCooldown   = 5f;
    [SerializeField] private float trueDamageEnergyCost = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private Color trueDamageArmedColor    = new Color(1f, 0.5f, 0.1f, 1f);

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
    }

    private void OnDestroy()
    {
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
        {
            var instance = Instantiate(empVFXPrefab, (Vector3)(Vector2)e.Position, Quaternion.identity);
            Destroy(instance, 4f);
        }
    }

    private void OnTrueDamageArmed()
    {
        _trueDamageArmedVisual = true;
        if (_spriteRenderer != null)
            _spriteRenderer.color = trueDamageArmedColor;
        _audioController?.PlayTrueDamageArmSound();
    }

    private void OnTrueDamageConsumed()
    {
        _trueDamageArmedVisual = false;
        RestoreColor();
        _audioController?.PlayTrueDamageConsumeSound();
    }

    private void RestoreColor()
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.color = _trueDamageArmedVisual ? trueDamageArmedColor : _originalColor;
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
