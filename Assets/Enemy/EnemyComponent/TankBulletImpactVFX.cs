using UnityEngine;
using System.Collections;

public class TankBulletImpactVFX : MonoBehaviour
{
    // Set by TankBullet before calling Initialize()
    public float flashMaxScale = 1.5f;
    public float fireballMaxScale = 1.2f;
    public int sparkCount = 20;
    public float sparkMaxSpeed = 10f;
    public float sparkGravity = 2.5f;
    public int smokeCount = 8;
    public float ringMaxScale = 2f;

    private Vector2 _hitDirection;

    public void Initialize(Vector2 bulletDirection)
    {
        _hitDirection = bulletDirection;
        StartCoroutine(PlayImpact());
    }

    private IEnumerator PlayImpact()
    {
        ExplosionFlashLight2D.Spawn(transform.position);

        // LAYER 1: Big bright flash
        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(transform, false);
        var flashSr = flash.AddComponent<SpriteRenderer>();
        flashSr.sprite = CreateCircle(32);
        flashSr.color = new Color(1f, 0.95f, 0.6f, 1f);
        flashSr.sortingOrder = 20;
        flash.transform.localScale = Vector3.one * 0.1f;

        float ft = 0f;
        while (ft < 0.1f)
        {
            ft += Time.deltaTime;
            float p = ft / 0.1f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, flashMaxScale, p);
            Color fc = flashSr.color;
            fc.a = Mathf.Lerp(1f, 0f, p * p);
            flashSr.color = fc;
            yield return null;
        }
        Destroy(flash);

        // LAYER 2: Fireball glow (expands and fades)
        GameObject fireball = new GameObject("Fireball");
        fireball.transform.SetParent(transform, false);
        var fbSr = fireball.AddComponent<SpriteRenderer>();
        fbSr.sprite = CreateCircle(32);
        fbSr.color = new Color(1f, 0.5f, 0.1f, 0.8f);
        fbSr.sortingOrder = 18;
        fireball.transform.localScale = Vector3.one * 0.3f;

        float fbt = 0f;
        while (fbt < 0.3f)
        {
            fbt += Time.deltaTime;
            float p = fbt / 0.3f;
            fireball.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, fireballMaxScale, p);
            Color c = fbSr.color;
            c.a = Mathf.Lerp(0.8f, 0f, p);
            c.r = Mathf.Lerp(1f, 0.8f, p);
            c.g = Mathf.Lerp(0.5f, 0.15f, p);
            fbSr.color = c;
            yield return null;
        }
        Destroy(fireball);

        // LAYER 3: Spark burst
        ParticleSystem sparks = CreatePS("Sparks", 19);
        var spMain = sparks.main;
        spMain.loop = false;
        spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        spMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, sparkMaxSpeed);
        spMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        spMain.startColor = new Color(1f, 0.85f, 0.3f, 1f);
        spMain.simulationSpace = ParticleSystemSimulationSpace.World;
        spMain.gravityModifier = sparkGravity;
        spMain.maxParticles = sparkCount + 10;

        var spEmit = sparks.emission;
        spEmit.rateOverTime = 0f;
        short spMin = (short)Mathf.Max(1, sparkCount - 5);
        short spMax = (short)(sparkCount + 5);
        spEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, spMin, spMax) });

        var spShape = sparks.shape;
        spShape.shapeType = ParticleSystemShapeType.Cone;
        spShape.angle = 60f;
        spShape.radius = 0.08f;
        float angle = Mathf.Atan2(-_hitDirection.y, -_hitDirection.x) * Mathf.Rad2Deg;
        sparks.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        var spSz = sparks.sizeOverLifetime;
        spSz.enabled = true;
        spSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var spCol = sparks.colorOverLifetime;
        spCol.enabled = true;
        Gradient sg = new Gradient();
        sg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f),
                new GradientColorKey(new Color(0.6f, 0.1f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        spCol.color = sg;
        sparks.Play();

        // LAYER 4: Smoke puffs
        ParticleSystem smoke = CreatePS("Smoke", 17);
        var smMain = smoke.main;
        smMain.loop = false;
        smMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        smMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        smMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        smMain.startColor = new Color(0.35f, 0.3f, 0.25f, 0.5f);
        smMain.simulationSpace = ParticleSystemSimulationSpace.World;
        smMain.gravityModifier = -0.3f;
        smMain.maxParticles = smokeCount + 4;

        var smEmit = smoke.emission;
        smEmit.rateOverTime = 0f;
        short smMin = (short)Mathf.Max(1, smokeCount - 2);
        short smMax = (short)(smokeCount + 2);
        smEmit.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, smMin, smMax) });

        var smShape = smoke.shape;
        smShape.shapeType = ParticleSystemShapeType.Circle;
        smShape.radius = 0.15f;

        var smSz = smoke.sizeOverLifetime;
        smSz.enabled = true;
        smSz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.1f)));

        var smCol = smoke.colorOverLifetime;
        smCol.enabled = true;
        Gradient smg = new Gradient();
        smg.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.45f, 0.4f, 0.35f), 0f),
                new GradientColorKey(new Color(0.25f, 0.22f, 0.18f), 0.5f),
                new GradientColorKey(new Color(0.15f, 0.13f, 0.1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        smCol.color = smg;
        smoke.Play();

        // LAYER 5: Shockwave ring
        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(transform, false);
        var ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = CreateRing(64);
        ringSr.color = new Color(1f, 0.7f, 0.2f, 0.7f);
        ringSr.sortingOrder = 16;
        ring.transform.localScale = Vector3.zero;

        float rt = 0f;
        while (rt < 0.2f)
        {
            rt += Time.deltaTime;
            float p = rt / 0.2f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0f, ringMaxScale, p);
            Color rc = ringSr.color;
            rc.a = Mathf.Lerp(0.7f, 0f, p * p);
            ringSr.color = rc;
            yield return null;
        }
        Destroy(ring);

        yield return new WaitForSeconds(0.8f);
        Destroy(gameObject);
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
                a = a * a * (3f - 2f * a); // smoothstep
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Sprite CreateCircle(int size)
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

    private static Sprite CreateRing(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = dist / center;
                float alpha;
                if (t < 0.7f || t > 1.0f) alpha = 0f;
                else if (t < 0.8f) alpha = (t - 0.7f) / 0.1f;
                else if (t < 0.9f) alpha = 1f;
                else alpha = 1f - (t - 0.9f) / 0.1f;
                px[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
