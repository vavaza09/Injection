using UnityEngine;

/// <summary>
/// Spawns a directional metal-spark "starburst" impact — a bright white-hot core flare, a
/// handful of LONG sharp rays fanning out from the hit surface (the dominant visual, like
/// anime impact speed-lines), and a few small trailing embers that arc down under gravity.
/// Few big dramatic elements read much better than many small ones at gameplay speed.
/// Call MetalSparkFX.Spawn(position, awayFromSurface) from anywhere.
/// </summary>
public static class MetalSparkFX
{
    public static void Spawn(Vector3 pos, Vector2 awayFromSurface)
    {
        var root = new GameObject("MetalSpark_FX");
        root.transform.position = pos;

        float angle = Mathf.Atan2(awayFromSurface.y, awayFromSurface.x) * Mathf.Rad2Deg;

        AddCoreFlare(root.transform);
        AddRays(root.transform, angle);
        AddEmbers(root.transform, angle);

        Object.Destroy(root, 0.6f);
    }

    // ── Bright white-hot core flare — the punchy epicenter ──────────────────

    static void AddCoreFlare(Transform parent)
    {
        var go = new GameObject("CoreFlare");
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SoftCircleSprite();
        sr.color = new Color(1f, 0.95f, 0.7f, 1f);
        sr.sortingOrder = 14;

        var runner = go.AddComponent<FlareRunner>();
        runner.Run(sr, 0.16f);
    }

    private class FlareRunner : MonoBehaviour
    {
        public void Run(SpriteRenderer sr, float duration) => StartCoroutine(Fade(sr, duration));

        private System.Collections.IEnumerator Fade(SpriteRenderer sr, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                float scale = Mathf.Lerp(0.15f, 1.4f, 1f - (1f - p) * (1f - p));
                sr.transform.localScale = Vector3.one * scale;
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, p * p);
                sr.color = c;
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    // ── Long sharp rays — the dominant "starburst" shape ─────────────────────
    // Few particles, big lengthScale, wide fan so they read as distinct blades, not a haze.

    static void AddRays(Transform parent, float angleDeg)
    {
        var go = new GameObject("Rays");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AdditiveMat(new Color(1f, 0.85f, 0.4f));
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 28f;
        psr.sortingOrder = 13;

        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new Color(1f, 1f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = 10;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 8) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f; // wide fan so rays spread distinctly, not a tight jet
        shape.radius = 0.02f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -3f),
            new Keyframe(0.6f, 0.5f),
            new Keyframe(1f, 0f)));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.85f), 0f),
                new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.5f),
                new GradientColorKey(new Color(0.7f, 0.15f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        ps.Play();
    }

    // ── A few small trailing embers — arc down under gravity ────────────────

    static void AddEmbers(Transform parent, float angleDeg)
    {
        var go = new GameObject("Embers");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = AdditiveMat(new Color(1f, 0.6f, 0.2f));
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 6f;
        psr.sortingOrder = 12;

        var main = ps.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(1f, 0.8f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.2f;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = 8;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.04f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0f),
                new GradientColorKey(new Color(0.6f, 0.1f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        ps.Play();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static Sprite SoftCircleSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float t = Mathf.Clamp01(1f - d / c);
                float a = t * t;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Material AdditiveMat(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(s) { name = "MetalSpark_Runtime" };
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 1f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.renderQueue = 3000;
        mat.enableInstancing = true;
        return mat;
    }
}
