using UnityEngine;
using System.Collections;

/// <summary>
/// Runtime-generated dust / ground-slam burst spawned when the Stomper lands.
/// Mirrors the MortarExplosionVFX pattern (procedural sprites + a single coroutine
/// that self-destroys) but retuned for a dusty rock impact — no fire.
/// </summary>
public class StompImpactVFX : MonoBehaviour
{
    // Tunables — set by StompEnemy before Initialize(), or left at defaults.
    public float radius = 2f;
    public int dustCount = 22;
    public int debrisCount = 16;
    public float ringMultiplier = 2.4f;

    public void Initialize(float impactRadius)
    {
        radius = impactRadius;
        StartCoroutine(PlayImpact());
    }

    public void Initialize()
    {
        StartCoroutine(PlayImpact());
    }

    private IEnumerator PlayImpact()
    {
        // LAYER 1: GROUND FLASH PUFF — quick low dust kick at the feet
        GameObject flash = CreateSpriteObj("DustFlash", 24);
        var flashSr = flash.GetComponent<SpriteRenderer>();
        flashSr.color = new Color(0.62f, 0.55f, 0.45f, 0.65f);
        flash.transform.localScale = Vector3.one * 0.3f;

        float ft = 0f;
        while (ft < 0.1f)
        {
            ft += Time.deltaTime;
            float p = ft / 0.1f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 1.6f, p);
            Color fc = flashSr.color;
            fc.a = Mathf.Lerp(0.65f, 0f, p * p);
            flashSr.color = fc;
            yield return null;
        }
        Destroy(flash);

        // LAYER 2: DUST CLOUD — billowing low puffs spreading outward along the ground
        ParticleSystem dust = CreatePS("Dust", 18);
        var duMain = dust.main;
        duMain.loop = false;
        duMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        duMain.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 3f);
        duMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        duMain.startColor = new Color(0.66f, 0.58f, 0.46f, 0.6f);
        duMain.simulationSpace = ParticleSystemSimulationSpace.World;
        duMain.gravityModifier = -0.1f;
        duMain.maxParticles = dustCount + 8;

        var duEmit = dust.emission;
        duEmit.rateOverTime = 0f;
        short duMin = (short)Mathf.Max(1, dustCount - 4);
        short duMax = (short)(dustCount + 4);
        duEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, duMin, duMax) });

        // Flat sideways spread so the dust hugs the floor like a ground slam
        var duShape = dust.shape;
        duShape.shapeType = ParticleSystemShapeType.Circle;
        duShape.radius = radius * 0.4f;
        duShape.scale = new Vector3(1f, 0.35f, 1f);

        var duSz = dust.sizeOverLifetime;
        duSz.enabled = true;
        duSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.3f)));

        var duCol = dust.colorOverLifetime;
        duCol.enabled = true;
        Gradient dg = new Gradient();
        dg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.72f, 0.64f, 0.52f), 0f),
                new GradientColorKey(new Color(0.55f, 0.48f, 0.38f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.35f, 0.28f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.4f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        duCol.color = dg;
        dust.Play();

        // LAYER 3: DEBRIS FLECKS — small rocks kicked up and falling back under gravity
        ParticleSystem debris = CreatePS("Debris", 22);
        var deMain = debris.main;
        deMain.loop = false;
        deMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        deMain.startSpeed = new ParticleSystem.MinMaxCurve(radius * 3f, radius * 7f);
        deMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        deMain.startColor = new Color(0.45f, 0.39f, 0.31f, 1f);
        deMain.simulationSpace = ParticleSystemSimulationSpace.World;
        deMain.gravityModifier = 3.2f;
        deMain.maxParticles = debrisCount + 8;

        var deEmit = debris.emission;
        deEmit.rateOverTime = 0f;
        short deMin = (short)Mathf.Max(1, debrisCount - 5);
        short deMax = (short)(debrisCount + 5);
        deEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, deMin, deMax) });

        // Bias debris upward and outward in a shallow fan
        var deShape = debris.shape;
        deShape.shapeType = ParticleSystemShapeType.Cone;
        deShape.angle = 55f;
        deShape.radius = 0.1f;
        deShape.rotation = new Vector3(-90f, 0f, 0f);

        var deSz = debris.sizeOverLifetime;
        deSz.enabled = true;
        deSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.6f)));

        var deCol = debris.colorOverLifetime;
        deCol.enabled = true;
        Gradient deg = new Gradient();
        deg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.5f, 0.43f, 0.34f), 0f),
                new GradientColorKey(new Color(0.38f, 0.32f, 0.25f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        deCol.color = deg;
        debris.Play();

        // LAYER 4: GROUND CRACK RING — expanding dust shockwave ring along the floor
        GameObject ring = CreateSpriteObj("Ring", 23);
        var ringSr = ring.GetComponent<SpriteRenderer>();
        ringSr.sprite = CreateRingSprite(64);
        ringSr.color = new Color(0.6f, 0.53f, 0.42f, 0.8f);
        ring.transform.localScale = Vector3.zero;

        float rt = 0f;
        while (rt < 0.4f)
        {
            rt += Time.deltaTime;
            float p = rt / 0.4f;
            // Flattened ring so it reads as spreading across the ground, not a sphere
            float s = Mathf.Lerp(0f, radius * ringMultiplier, p);
            ring.transform.localScale = new Vector3(s, s * 0.45f, 1f);
            Color rc = ringSr.color;
            rc.a = Mathf.Lerp(0.8f, 0f, p);
            ringSr.color = rc;
            yield return null;
        }
        Destroy(ring);

        // LAYER 5: LINGERING DUST HAZE
        GameObject haze = CreateSpriteObj("Haze", 17);
        var hazeSr = haze.GetComponent<SpriteRenderer>();
        hazeSr.color = new Color(0.6f, 0.54f, 0.44f, 0.35f);
        haze.transform.localScale = Vector3.one * radius * 1.1f;

        float ht = 0f;
        while (ht < 0.45f)
        {
            ht += Time.deltaTime;
            float p = ht / 0.45f;
            Color hc = hazeSr.color;
            hc.a = Mathf.Lerp(0.35f, 0f, p);
            hazeSr.color = hc;
            haze.transform.localScale = Vector3.one * Mathf.Lerp(radius * 1.1f, radius * 1.8f, p);
            yield return null;
        }
        Destroy(haze);

        yield return new WaitForSeconds(1.3f);
        Destroy(gameObject);
    }

    private GameObject CreateSpriteObj(string objName, int sortOrder)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(32);
        sr.sortingOrder = sortOrder;
        return go;
    }

    private ParticleSystem CreatePS(string objName, int sortOrder)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = CreateSoftCircleTex(32);
        r.material = mat;
        r.sortingOrder = sortOrder;
        return ps;
    }

    // Baked soft radial gradient so dust/debris render as round puffs instead of square boxes
    private static Texture2D CreateSoftCircleTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float t = Mathf.Clamp01(d / c);
                float a = 1f - t;
                a = a * a * (3f - 2f * a); // smoothstep falloff
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - d / c);
                a *= a;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRingSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        Color[] px = new Color[size * size];
        float outer = c, inner = c * 0.75f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = 0f;
                if (d >= inner && d <= outer)
                {
                    float mid = (inner + outer) * 0.5f;
                    float hw = (outer - inner) * 0.5f;
                    a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / hw);
                }
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
