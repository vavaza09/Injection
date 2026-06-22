using System.Collections;
using UnityEngine;

public class EnemyDeathExplosion : MonoBehaviour
{
    [Header("Freeze Flash")]
    [SerializeField] private float freezeDuration = 1f;
    [SerializeField] private float flashInterval = 0.08f;

    [Header("Explosion Animation")]
    [SerializeField] private Sprite[] explosionFrames;
    [SerializeField] private float explosionFps = 12f;
    [SerializeField] private float explosionScale = 0.5f;

    [Header("Body Parts")]
    [SerializeField] private Sprite[] bodyPartSprites;
    [SerializeField] private float bodyPartScale = 1f;
    [SerializeField] private float launchForce = 6f;
    [SerializeField] private float upwardBias = 1.5f;
    [SerializeField] private float partRotationSpeed = 220f;
    [SerializeField] private float partGravityScale = 1.5f;
    [SerializeField] private float partLifetime = 2.2f;
    [SerializeField] private float partFadeDuration = 0.8f;

    [Header("Smoke Trail")]
    [SerializeField] private float trailTime = 0.4f;
    [SerializeField] private float trailStartWidth = 0.12f;
    [SerializeField] private Color trailStartColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
    [SerializeField] private Color trailEndColor = new Color(0.2f, 0.2f, 0.2f, 0f);

    private SpriteRenderer[] _renderers;
    private Animator _anim;
    private string _sortingLayer;
    private int _sortingOrder;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>();
        _anim = GetComponentInChildren<Animator>();

        if (_renderers.Length > 0)
        {
            _sortingLayer = _renderers[0].sortingLayerName;
            _sortingOrder = _renderers[0].sortingOrder;
        }
        else
        {
            _sortingLayer = "Default";
            _sortingOrder = 0;
        }
    }

    public void TriggerDeath()
    {
        if (_anim != null) _anim.speed = 0f;
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Phase 1: Freeze flash
        float elapsed = 0f;
        bool flashOn = false;
        Color[] origColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            origColors[i] = _renderers[i].color;

        while (elapsed < freezeDuration)
        {
            Color c = flashOn ? Color.white : origColors[0];
            foreach (SpriteRenderer sr in _renderers)
                if (sr != null) sr.color = c;
            flashOn = !flashOn;
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        // Phase 2: Hide all sprite renderers
        foreach (SpriteRenderer sr in _renderers)
            if (sr != null) sr.enabled = false;

        // Phase 3: Explosion animation + body parts launch simultaneously
        Vector3 origin = transform.position;

        // Spawn body parts immediately when explosion fires
        if (bodyPartSprites != null)
        {
            foreach (Sprite part in bodyPartSprites)
            {
                if (part == null) continue;
                SpawnPart(part, origin);
            }
        }

        if (explosionFrames != null && explosionFrames.Length > 0)
        {
            GameObject exGO = new GameObject("_ExplosionFX");
            exGO.transform.position = origin;
            exGO.transform.localScale = Vector3.one * explosionScale;

            SpriteRenderer exSR = exGO.AddComponent<SpriteRenderer>();
            exSR.sortingLayerName = _sortingLayer;
            exSR.sortingOrder = _sortingOrder + 5;

            float frameDt = 1f / Mathf.Max(explosionFps, 1f);
            foreach (Sprite frame in explosionFrames)
            {
                if (exGO == null) break;
                exSR.sprite = frame;
                yield return new WaitForSeconds(frameDt);
            }

            if (exGO != null) Destroy(exGO);
        }

        Destroy(gameObject);
    }

    private void SpawnPart(Sprite sprite, Vector3 origin)
    {
        GameObject go = new GameObject("_BodyPart");
        go.transform.position = origin + (Vector3)(Random.insideUnitCircle * 0.25f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        go.transform.localScale = Vector3.one * bodyPartScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = _sortingLayer;
        sr.sortingOrder = _sortingOrder;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = partGravityScale;
        rb.freezeRotation = false;
        rb.angularVelocity = Random.Range(-partRotationSpeed, partRotationSpeed);

        Vector2 dir = Random.insideUnitCircle;
        dir.y += upwardBias;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();
        rb.linearVelocity = dir * Random.Range(launchForce * 0.6f, launchForce);

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailStartColor;
        trail.endColor = trailEndColor;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.generateLightingData = false;

        BodyPartEffect effect = go.AddComponent<BodyPartEffect>();
        effect.Initialize(partLifetime, partFadeDuration);
    }
}
