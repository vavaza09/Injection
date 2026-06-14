using UnityEngine;
using Game.Components.Movement;

/// <summary>
/// Manages all player movement particle effects: wind streaks, run dust, jump/land puffs,
/// dash burst + afterimages, and wall slide/wall-jump particles.
/// Attach to the Player root GameObject. Call Initialize() from Player.Start().
/// All ParticleSystems are built in code at runtime — no prefab authoring needed.
/// </summary>
public class PlayerMovementVFX : MonoBehaviour
{
    #region Serialized Fields

    [Header("Wind Streaks")]
    [SerializeField, Range(0f, 1f)] private float windSpeedThreshold = 0.5f;
    [SerializeField] private float windRateMin = 12f;
    [SerializeField] private float windRateMax = 35f;
    [SerializeField] private float windLengthScaleMin = 2.5f;
    [SerializeField] private float windLengthScaleMax = 5f;
    [SerializeField] private float windBackOffset = 0.4f;
    [SerializeField, Range(0f, 1f)] private float windAlphaMin = 0.35f;
    [SerializeField, Range(0f, 1f)] private float windAlphaMax = 0.80f;
    [SerializeField] private Color windColor = new Color(0.7f, 0.85f, 1f, 0.7f);

    [Header("Run Dust")]
    [SerializeField] private float runDustMinSpeed = 1.5f;
    [SerializeField] private float runDustMaxRate = 10f;
    [SerializeField] private Color dustColor = new Color(0.92f, 0.92f, 0.88f, 0.45f);

    [Header("Jump / Land")]
    [SerializeField] private int jumpPuffCount = 6;
    [SerializeField] private float landMinFallSpeed = 4f;
    [SerializeField] private float landMaxFallSpeed = 20f;
    [SerializeField] private int landBurstMin = 6;
    [SerializeField] private int landBurstMax = 18;

    [Header("Dash")]
    [SerializeField] private int dashBurstCount = 14;
    [SerializeField] private float dashStreakLengthScale = 12f;
    [SerializeField] private Color dashColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private float ghostInterval = 0.04f;
    [SerializeField] private float ghostFadeDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float ghostStartAlpha = 0.45f;
    [SerializeField] private Color ghostTint = Color.white;

    [Header("Wall")]
    [SerializeField] private float wallDustRate = 8f;
    [SerializeField] private int wallJumpBurstCount = 8;

    [Header("Sorting")]
    [SerializeField] private int behindPlayerOrder = 5;
    [SerializeField] private int frontOfPlayerOrder = 10;

    #endregion

    #region Private State

    private MovementComponent _movement;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;

    private Transform _vfxRoot;

    // Looping systems
    private ParticleSystem _windStreaks;
    private ParticleSystem _runDust;
    private ParticleSystem _wallDust;

    // Burst systems (triggered via Emit / Play)
    private ParticleSystem _jumpPuff;
    private ParticleSystem _landBurst;
    private ParticleSystem _dashBurst;

    // Afterimage pool
    private SpriteAfterimage[] _ghostPool;
    private int _ghostIndex;
    private float _lastGhostTime;

    // Landing detection
    private bool _wasGrounded;
    private Vector2 _lastAirborneVelocity;

    // Wall side cache (valid even after leaving wall)
    private int _lastWallSide = 1;

    // Dash edge detection for wind streak clear
    private bool _wasDashing;

    // Static texture cache
    private static Texture2D _softCircleTex;

    #endregion

    #region Initialization

    public void Initialize(MovementComponent movement)
    {
        _movement = movement;
        _wasGrounded = movement.IsGrounded();
    }

    private void Awake()
    {
        // Self-init fallback: Initialize() from Player.Start() will override if called
        _movement = GetComponent<MovementComponent>();

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();

        _vfxRoot = new GameObject("MovementVFX").transform;
        _vfxRoot.SetParent(transform, worldPositionStays: false);
        _vfxRoot.localPosition = Vector3.zero;

        BuildWindStreaks();
        BuildRunDust();
        BuildWallDust();
        BuildJumpPuff();
        BuildLandBurst();
        BuildDashBurst();
        BuildGhostPool();
    }

    #endregion

    #region Update

    private void Update()
    {
        if (_movement == null) return;

        Vector2 vel = _movement.GetVelocity();
        float speedFactor = Mathf.Clamp01(vel.magnitude / Mathf.Max(0.01f, _movement.MaxSpeed));

        UpdateWindStreaks(vel, speedFactor);
        UpdateRunDust(vel, speedFactor);
        UpdateWallDust();
        UpdateLanding(vel);
        UpdateAfterimages();
    }

    private void LateUpdate()
    {
        if (_vfxRoot == null) return;
        // Counter-flip: keep VFX root in world scale even when player flips localScale.x
        float sx = transform.localScale.x;
        _vfxRoot.localScale = new Vector3(Mathf.Sign(sx != 0f ? sx : 1f), 1f, 1f);
    }

    #endregion

    #region Continuous Effect Updates

    private void UpdateWindStreaks(Vector2 vel, float speedFactor)
    {
        if (_windStreaks == null) return;
        var emission = _windStreaks.emission;

        if (_movement.IsDashing)
        {
            if (!_wasDashing)
                _windStreaks.gameObject.SetActive(false);
            _wasDashing = true;
            return;
        }

        if (_wasDashing)
        {
            _windStreaks.gameObject.SetActive(true);
            _windStreaks.Play();
        }
        _wasDashing = false;

        if (speedFactor < windSpeedThreshold)
        {
            emission.rateOverTime = 0f;
            return;
        }

        float t = Mathf.Clamp01((speedFactor - windSpeedThreshold) / (1f - windSpeedThreshold));
        emission.rateOverTime = Mathf.Lerp(windRateMin, windRateMax, t);

        var psr = _windStreaks.GetComponent<ParticleSystemRenderer>();
        psr.lengthScale = Mathf.Lerp(windLengthScaleMin, windLengthScaleMax, t);

        var main = _windStreaks.main;
        Color c = windColor;
        c.a = Mathf.Lerp(windAlphaMin, windAlphaMax, t);
        main.startColor = c;

        // World-space velocity opposite to movement — particles stay in XY plane, visible in 2D
        if (vel.sqrMagnitude > 0.01f)
        {
            float windSpeed = Mathf.Lerp(4f, 8f, t);
            Vector2 windDir = -vel.normalized * windSpeed;
            var vol = _windStreaks.velocityOverLifetime;
            // All axes Constant mode → no MinMaxCurve mode mismatch
            vol.x = new ParticleSystem.MinMaxCurve(windDir.x);
            vol.y = new ParticleSystem.MinMaxCurve(windDir.y);

            // Push spawn point backward so the symmetric stretch tip lands on the body, not past the front
            Vector3 back = (Vector3)(-vel.normalized * windBackOffset);
            _windStreaks.transform.position = transform.position + back;
        }
    }

    private void UpdateRunDust(Vector2 vel, float speedFactor)
    {
        if (_runDust == null) return;
        var emission = _runDust.emission;
        bool shouldEmit = _movement.IsGrounded() && Mathf.Abs(vel.x) > runDustMinSpeed;
        if (!shouldEmit)
        {
            emission.rateOverTime = 0f;
            return;
        }

        emission.rateOverTime = Mathf.Lerp(0f, runDustMaxRate, speedFactor);

        // Position at feet
        if (_collider != null)
        {
            Vector3 feetPos = new Vector3(_collider.bounds.center.x, _collider.bounds.min.y, 0f);
            _runDust.transform.position = feetPos;
        }
    }

    private void UpdateWallDust()
    {
        if (_wallDust == null) return;
        var emission = _wallDust.emission;

        if (_movement.IsWallSliding)
        {
            _lastWallSide = _movement.WallSideSign;
            emission.rateOverTime = wallDustRate;

            // Position at wall contact side
            if (_collider != null)
            {
                float xOffset = _lastWallSide * (_collider.bounds.extents.x + 0.05f);
                Vector3 wallPos = new Vector3(
                    _collider.bounds.center.x + xOffset,
                    _collider.bounds.center.y,
                    0f);
                _wallDust.transform.position = wallPos;
            }
        }
        else
        {
            emission.rateOverTime = 0f;
        }
    }

    private void UpdateLanding(Vector2 vel)
    {
        if (_movement == null) return;
        bool isGrounded = _movement.IsGrounded();

        if (!isGrounded)
        {
            _lastAirborneVelocity = vel;
        }

        if (!_wasGrounded && isGrounded)
        {
            float fall = Mathf.Abs(_lastAirborneVelocity.y);
            if (fall > landMinFallSpeed && _landBurst != null)
            {
                float t = Mathf.InverseLerp(landMinFallSpeed, landMaxFallSpeed, fall);
                int count = Mathf.RoundToInt(Mathf.Lerp(landBurstMin, landBurstMax, t));
                EmitLandBurst(count);
            }
        }

        _wasGrounded = isGrounded;
    }

    private void UpdateAfterimages()
    {
        if (!_movement.IsDashing) return;
        if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;
        if (Time.unscaledTime - _lastGhostTime < ghostInterval) return;

        _lastGhostTime = Time.unscaledTime;

        SpriteAfterimage ghost = _ghostPool[_ghostIndex % _ghostPool.Length];
        _ghostIndex++;
        ghost.Show(
            _spriteRenderer.sprite,
            _spriteRenderer.transform.position,
            _spriteRenderer.transform.rotation,
            transform.lossyScale,
            ghostTint,
            ghostStartAlpha,
            ghostFadeDuration);
    }

    #endregion

    #region One-Shot Methods (called from Player.OnStateEnter)

    public void PlayJumpPuff()
    {
        if (_jumpPuff == null) return;
        PositionAtFeet(_jumpPuff.transform);
        var ep = new ParticleSystem.EmitParams();
        _jumpPuff.Emit(ep, jumpPuffCount);
    }

    public void PlayWallJumpBurst()
    {
        if (_jumpPuff == null) return;
        PositionAtFeet(_jumpPuff.transform);
        // Emit with horizontal velocity pushing away from the wall
        var ep = new ParticleSystem.EmitParams();
        ep.velocity = new Vector3(-_lastWallSide * 3f, 2f, 0f);
        _jumpPuff.Emit(ep, wallJumpBurstCount);
    }

    public void PlayDashBurst(Vector2 dir)
    {
        if (_dashBurst == null) return;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        _dashBurst.transform.position = transform.position;

        // Inject XY-plane velocity via EmitParams — avoids local-Z emission direction bug
        float spread = 18f * Mathf.Deg2Rad;
        for (int i = 0; i < dashBurstCount; i++)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) + Random.Range(-spread, spread);
            float speed = Random.Range(6f, 14f);
            var ep = new ParticleSystem.EmitParams();
            ep.velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);
            _dashBurst.Emit(ep, 1);
        }
    }

    #endregion

    #region Particle System Builders

    private void BuildWindStreaks()
    {
        var go = new GameObject("WindStreaks");
        go.transform.SetParent(_vfxRoot, false);
        _windStreaks = go.AddComponent<ParticleSystem>();
        _windStreaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _windStreaks.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        // startSpeed = 0 — direction comes from velocityOverLifetime (world XY), not local Z
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = windColor;
        main.maxParticles = 80;
        main.useUnscaledTime = false;

        var emission = _windStreaks.emission;
        emission.rateOverTime = 0f;

        var shape = _windStreaks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.2f, 1.0f, 0.01f);

        // World-space velocity; UpdateWindStreaks sets x/y per frame to trail opposite movement
        var vol = _windStreaks.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(-6f);
        vol.y = new ParticleSystem.MinMaxCurve(0f);
        vol.z = new ParticleSystem.MinMaxCurve(0f);

        var col = _windStreaks.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(windColor);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AlphaBlendMat(windColor);
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0.06f;
        psr.lengthScale = windLengthScaleMin;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = behindPlayerOrder;

        _windStreaks.Play();
    }

    private void BuildRunDust()
    {
        var go = new GameObject("RunDust");
        go.transform.SetParent(_vfxRoot, false);
        _runDust = go.AddComponent<ParticleSystem>();
        _runDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _runDust.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = 0f;  // velocityOverLifetime handles Y-rise; no Z leak
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = dustColor;
        main.gravityModifier = 0f;
        main.maxParticles = 40;
        main.useUnscaledTime = false;

        var emission = _runDust.emission;
        emission.rateOverTime = 0f;

        var shape = _runDust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.4f, 0.05f, 0.1f);

        var vel = _runDust.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // All axes must share the same MinMaxCurve mode — use TwoConstants for all three
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = _runDust.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(dustColor);

        var sizeOL = _runDust.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve sizeGrow = new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.6f));
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeGrow);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AlphaBlendMat(dustColor);
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        SetSoftCircleTexture(psr);
        psr.sortingLayerName = "Default";
        psr.sortingOrder = behindPlayerOrder;

        _runDust.Play();
    }

    private void BuildWallDust()
    {
        var go = new GameObject("WallDust");
        go.transform.SetParent(_vfxRoot, false);
        _wallDust = go.AddComponent<ParticleSystem>();
        _wallDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _wallDust.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = 0f;  // particles fall via gravity; no Z leak
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = dustColor;
        main.gravityModifier = 0.5f;
        main.maxParticles = 30;
        main.useUnscaledTime = false;

        var emission = _wallDust.emission;
        emission.rateOverTime = 0f;

        var shape = _wallDust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.05f, 0.6f, 0.1f);

        var col = _wallDust.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(dustColor);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AlphaBlendMat(dustColor);
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        SetSoftCircleTexture(psr);
        psr.sortingLayerName = "Default";
        psr.sortingOrder = behindPlayerOrder;

        _wallDust.Play();
    }

    private void BuildJumpPuff()
    {
        var go = new GameObject("JumpPuff");
        go.transform.SetParent(_vfxRoot, false);
        _jumpPuff = go.AddComponent<ParticleSystem>();
        _jumpPuff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _jumpPuff.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = 0f;  // velocity via EmitParams or velocityOverLifetime
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startColor = dustColor;
        main.maxParticles = 30;
        main.useUnscaledTime = false;

        var emission = _jumpPuff.emission;
        emission.rateOverTime = 0f;

        var shape = _jumpPuff.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 0.05f, 0.01f);

        // Puff rises upward; horizontal spread given per-particle in PlayJumpPuff
        var vol = _jumpPuff.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        vol.y = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = _jumpPuff.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(dustColor);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AlphaBlendMat(dustColor);
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        SetSoftCircleTexture(psr);
        psr.sortingLayerName = "Default";
        psr.sortingOrder = behindPlayerOrder;
    }

    private void BuildLandBurst()
    {
        var go = new GameObject("LandBurst");
        go.transform.SetParent(_vfxRoot, false);
        _landBurst = go.AddComponent<ParticleSystem>();
        _landBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _landBurst.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = dustColor;
        main.maxParticles = 40;
        main.useUnscaledTime = false;

        var emission = _landBurst.emission;
        emission.rateOverTime = 0f;

        var shape = _landBurst.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.8f, 0.05f, 0.01f);

        // Burst spreads horizontally outward from landing point
        var vol = _landBurst.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        vol.y = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = _landBurst.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(dustColor);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AlphaBlendMat(dustColor);
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        SetSoftCircleTexture(psr);
        psr.sortingLayerName = "Default";
        psr.sortingOrder = behindPlayerOrder;
    }

    private void BuildDashBurst()
    {
        var go = new GameObject("DashBurst");
        go.transform.SetParent(_vfxRoot, false);
        _dashBurst = go.AddComponent<ParticleSystem>();
        _dashBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _dashBurst.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        // startSpeed = 0 — velocity injected via EmitParams.velocity in PlayDashBurst
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = dashColor;
        main.maxParticles = 30;
        main.useUnscaledTime = false;

        var emission = _dashBurst.emission;
        emission.rateOverTime = 0f;

        var shape = _dashBurst.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.1f;

        var col = _dashBurst.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOutGrad(dashColor);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AdditiveMat(dashColor);
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0.5f;
        psr.lengthScale = dashStreakLengthScale;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = frontOfPlayerOrder;
    }

    private void BuildGhostPool()
    {
        _ghostPool = new SpriteAfterimage[6];
        for (int i = 0; i < _ghostPool.Length; i++)
        {
            var go = new GameObject("SpriteGhost_" + i);
            // Unparented so player flip/scale never distorts the ghost
            _ghostPool[i] = go.AddComponent<SpriteAfterimage>();
            go.SetActive(false);
        }
    }

    #endregion

    #region Helpers

    private void EmitLandBurst(int count)
    {
        if (_landBurst == null) return;
        PositionAtFeet(_landBurst.transform);
        var ep = new ParticleSystem.EmitParams();
        _landBurst.Emit(ep, count);
    }

    private void PositionAtFeet(Transform t)
    {
        if (_collider != null)
            t.position = new Vector3(_collider.bounds.center.x, _collider.bounds.min.y, 0f);
        else
            t.position = transform.position;
    }

    private static Material AdditiveMat(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(s) { name = "MovementVFX_Additive" };
        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    2f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 1f);
        mat.SetFloat("_ZWrite",   0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);
        mat.renderQueue = 3000;
        mat.enableInstancing = true;
        return mat;
    }

    private static Material AlphaBlendMat(Color color)
    {
        // Must use URP Particles/Unlit — Sprites/Default is for SpriteRenderer and
        // silently breaks ParticleSystemRenderer (wrong vertex layout, no instancing variant).
        // _Blend=0 → Alpha (SrcAlpha / OneMinusSrcAlpha), visible on both light & dark backgrounds.
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        var mat = new Material(s) { name = "MovementVFX_AlphaBlend" };
        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    0f);    // 0 = Alpha blend
        mat.SetFloat("_SrcBlend", 5f);    // SrcAlpha
        mat.SetFloat("_DstBlend", 10f);   // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite",   0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        mat.enableInstancing = true;
        return mat;
    }

    private static void SetSoftCircleTexture(ParticleSystemRenderer psr)
    {
        if (_softCircleTex == null)
        {
            const int size = 32;
            _softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _softCircleTex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - dist / center);
                    alpha = alpha * alpha; // smooth falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            _softCircleTex.SetPixels(pixels);
            _softCircleTex.Apply();
        }
        psr.material.SetTexture("_BaseMap", _softCircleTex);
        psr.material.SetTexture("_MainTex", _softCircleTex);
    }

    private static ParticleSystem.MinMaxGradient FadeOutGrad(Color color)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 1f) },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    #endregion

    private void OnDestroy()
    {
        if (_ghostPool == null) return;
        foreach (var ghost in _ghostPool)
        {
            if (ghost != null)
                Destroy(ghost.gameObject);
        }
    }
}
