using UnityEngine;
using System.Collections;

public class MortarExplosionVFX : MonoBehaviour
{
    // Set by FurnaceEnemy before calling Initialize()
    public float flashSizeMultiplier = 2.5f;
    public int fireballCount = 30;
    public int smokeCount = 13;
    public int sparkCount = 27;
    public float ringMultiplier = 3f;

    private float _radius = 2f;

    public void Initialize(float radius)
    {
        _radius = radius;
        StartCoroutine(PlayExplosion());
    }

    private IEnumerator PlayExplosion()
    {
        ExplosionFlashLight2D.Spawn(transform.position);

        // LAYER 1: INITIAL FLASH
        GameObject flash = CreateSpriteObj("Flash", 25);
        var flashSr = flash.GetComponent<SpriteRenderer>();
        flashSr.color = new Color(1f, 0.95f, 0.8f, 1f);
        flash.transform.localScale = Vector3.one * 0.3f;

        float ft = 0f;
        while (ft < 0.08f)
        {
            ft += Time.deltaTime;
            float p = ft / 0.08f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, _radius * flashSizeMultiplier, p);
            Color fc = flashSr.color;
            fc.a = Mathf.Lerp(1f, 0f, p * p);
            flashSr.color = fc;
            yield return null;
        }
        Destroy(flash);

        // LAYER 2: FIREBALL
        ParticleSystem fireball = CreatePS("Fireball", 20);
        var fireMain = fireball.main;
        fireMain.loop = false;
        fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(_radius * 2f, _radius * 5f);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        fireMain.startColor = new Color(1f, 0.6f, 0.1f, 1f);
        fireMain.simulationSpace = ParticleSystemSimulationSpace.World;
        fireMain.gravityModifier = 0.5f;
        fireMain.maxParticles = fireballCount + 10;

        var fireEmit = fireball.emission;
        fireEmit.rateOverTime = 0f;
        short fbMin = (short)Mathf.Max(1, fireballCount - 5);
        short fbMax = (short)(fireballCount + 5);
        fireEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, fbMin, fbMax) });

        var fireShape = fireball.shape;
        fireShape.shapeType = ParticleSystemShapeType.Circle;
        fireShape.radius = 0.15f;

        var fireSz = fireball.sizeOverLifetime;
        fireSz.enabled = true;
        fireSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

        var fireCol = fireball.colorOverLifetime;
        fireCol.enabled = true;
        Gradient fg = new Gradient();
        fg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.3f),
                new GradientColorKey(new Color(0.8f, 0.15f, 0f), 0.7f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f),
                new GradientAlphaKey(0.4f, 0.7f), new GradientAlphaKey(0f, 1f)
            });
        fireCol.color = fg;
        fireball.Play();

        // LAYER 3: SMOKE — now with soft circle texture so it looks like actual puffs
        ParticleSystem smoke = CreatePS("Smoke", 18);
        var smMain = smoke.main;
        smMain.loop = false;
        smMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        smMain.startSpeed = new ParticleSystem.MinMaxCurve(_radius * 0.6f, _radius * 1.8f);
        smMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        smMain.startColor = new Color(0.28f, 0.25f, 0.2f, 0.55f);
        smMain.simulationSpace = ParticleSystemSimulationSpace.World;
        smMain.gravityModifier = -0.35f;
        smMain.maxParticles = smokeCount + 6;

        var smEmit = smoke.emission;
        smEmit.rateOverTime = 0f;
        short smMin = (short)Mathf.Max(1, smokeCount - 3);
        short smMax = (short)(smokeCount + 3);
        smEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.03f, smMin, smMax) });

        var smShape = smoke.shape;
        smShape.shapeType = ParticleSystemShapeType.Circle;
        smShape.radius = 0.2f;

        var smSz = smoke.sizeOverLifetime;
        smSz.enabled = true;
        smSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.25f)));

        var smCol = smoke.colorOverLifetime;
        smCol.enabled = true;
        Gradient sg = new Gradient();
        sg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.4f, 0.35f, 0.28f), 0f),
                new GradientColorKey(new Color(0.22f, 0.2f, 0.16f), 0.5f),
                new GradientColorKey(new Color(0.12f, 0.1f, 0.08f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.55f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        smCol.color = sg;
        smoke.Play();

        // LAYER 4: SPARKS
        ParticleSystem sparks = CreatePS("Sparks", 22);
        var spMain = sparks.main;
        spMain.loop = false;
        spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        spMain.startSpeed = new ParticleSystem.MinMaxCurve(_radius * 4f, _radius * 9f);
        spMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        spMain.startColor = new Color(1f, 0.85f, 0.3f, 1f);
        spMain.simulationSpace = ParticleSystemSimulationSpace.World;
        spMain.gravityModifier = 3f;
        spMain.maxParticles = sparkCount + 10;

        var spEmit = sparks.emission;
        spEmit.rateOverTime = 0f;
        short spMin = (short)Mathf.Max(1, sparkCount - 7);
        short spMax = (short)(sparkCount + 7);
        spEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, spMin, spMax) });

        var spShape = sparks.shape;
        spShape.shapeType = ParticleSystemShapeType.Circle;
        spShape.radius = 0.1f;

        var spSz = sparks.sizeOverLifetime;
        spSz.enabled = true;
        spSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var spCol = sparks.colorOverLifetime;
        spCol.enabled = true;
        Gradient spg = new Gradient();
        spg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        spCol.color = spg;
        sparks.Play();

        // LAYER 5: SHOCKWAVE RING
        GameObject ring = CreateSpriteObj("Ring", 24);
        var ringSr = ring.GetComponent<SpriteRenderer>();
        ringSr.sprite = CreateRingSprite(64);
        ringSr.color = new Color(1f, 0.7f, 0.3f, 0.8f);
        ring.transform.localScale = Vector3.zero;

        float rt = 0f;
        while (rt < 0.35f)
        {
            rt += Time.deltaTime;
            float p = rt / 0.35f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0f, _radius * ringMultiplier, p);
            Color rc = ringSr.color;
            rc.a = Mathf.Lerp(0.8f, 0f, p);
            ringSr.color = rc;
            yield return null;
        }
        Destroy(ring);

        // LAYER 6: EMBER GLOW
        GameObject ember = CreateSpriteObj("Ember", 19);
        var emberSr = ember.GetComponent<SpriteRenderer>();
        emberSr.color = new Color(1f, 0.4f, 0.05f, 0.6f);
        ember.transform.localScale = Vector3.one * _radius * 1.2f;

        float et = 0f;
        while (et < 0.3f)
        {
            et += Time.deltaTime;
            float p = et / 0.3f;
            Color ec = emberSr.color;
            ec.a = Mathf.Lerp(0.6f, 0f, p);
            emberSr.color = ec;
            ember.transform.localScale = Vector3.one * Mathf.Lerp(_radius * 1.2f, _radius * 0.3f, p);
            yield return null;
        }
        Destroy(ember);

        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    private GameObject CreateSpriteObj(string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(32);
        sr.sortingOrder = sortOrder;
        return go;
    }

    private ParticleSystem CreatePS(string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = CreateSoftCircleTex(32);
        r.material = mat;
        r.sortingOrder = sortOrder;
        return ps;
    }

    // Baked soft radial gradient — makes smoke/sparks render as round puffs instead of square boxes
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
