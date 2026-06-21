using UnityEngine;
using VContainer;
using Game.Characters.Player;
using Game.Components.Skills;
using Game.Tutorial;

public class PlayerSkillController : MonoBehaviour
{
    [Header("EMP Blast")]
    [SerializeField] private float empRadius        = 5f;
    [SerializeField] private float empStunDuration  = 3f;
    [SerializeField] private float empCooldown      = 8f;
    [SerializeField] private float empEnergyCost    = 1f;

    [Header("True Damage Dash")]
    [SerializeField] private float trueDamageCooldown   = 5f;
    [SerializeField] private float trueDamageEnergyCost = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private Color empFlashColor           = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private float empFlashDuration        = 0.15f;
    [SerializeField] private Color trueDamageArmedColor    = new Color(1f, 0.5f, 0.1f, 1f);

    // Debug gizmo
    private bool  _drawEmpGizmo;
    private float _empGizmoTimer;
    private float _empGizmoRadius;
    private Vector3 _empGizmoPos;

    private Core.Logging.ILogger _logger;
    private PlayerInputHandler   _inputHandler;
    private PlayerSkillEvents  _bus;
    private IEnergyPool        _energyPool;

    private EmpBlastSkill      _empSkill;
    private TrueDamageDashSkill _trueDashSkill;

    private SpriteRenderer _spriteRenderer;
    private Color          _originalColor;
    private Coroutine      _flashCoroutine;
    private bool           _trueDamageArmedVisual;

    // Alive check — PlayerSkillController sits on the same GO as Player
    private character _character;
    private Player    _player;

    [Inject]
    public void Construct(
        Core.Logging.LoggerFactory loggerFactory,
        PlayerInputHandler inputHandler,
        IPlayerSkillEvents skillEvents,
        IEnergyPool energyPool)
    {
        _logger       = loggerFactory?.CreateLogger<PlayerSkillController>();
        _inputHandler = inputHandler;
        _bus          = (PlayerSkillEvents)skillEvents;
        _energyPool   = energyPool;
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

        if (_drawEmpGizmo)
        {
            _empGizmoTimer -= Time.unscaledDeltaTime;
            if (_empGizmoTimer <= 0f)
                _drawEmpGizmo = false;
        }
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
        _drawEmpGizmo  = true;
        _empGizmoTimer = 0.5f;
        _empGizmoRadius = e.Radius;
        _empGizmoPos    = transform.position;

        FlashColor(empFlashColor, empFlashDuration);
    }

    private void OnTrueDamageArmed()
    {
        _trueDamageArmedVisual = true;
        if (_spriteRenderer != null)
            _spriteRenderer.color = trueDamageArmedColor;
    }

    private void OnTrueDamageConsumed()
    {
        _trueDamageArmedVisual = false;
        RestoreColor();
    }

    private void FlashColor(Color color, float duration)
    {
        if (_spriteRenderer == null) return;
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashCoroutine(color, duration));
    }

    private System.Collections.IEnumerator FlashCoroutine(Color color, float duration)
    {
        _spriteRenderer.color = color;
        yield return new WaitForSecondsRealtime(duration);
        // Restore: keep armed tint if still armed, else go back to original
        RestoreColor();
        _flashCoroutine = null;
    }

    private void RestoreColor()
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.color = _trueDamageArmedVisual ? trueDamageArmedColor : _originalColor;
    }

    // ── Debug Gizmos ───────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!_drawEmpGizmo) return;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawSphere(_empGizmoPos, _empGizmoRadius);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(_empGizmoPos, _empGizmoRadius);
    }
}
