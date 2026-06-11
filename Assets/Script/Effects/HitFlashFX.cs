using UnityEngine;

/// <summary>
/// Spawns a hit-flash effect that looks like sharp impact streaks/spikes —
/// no textures required. Streaks are made by stretching thin particles along
/// their velocity, giving the spiky impact look.
/// Call HitFlashFX.Spawn(position) from anywhere.
/// </summary>
public static class HitFlashFX
{
    public static void Spawn(Vector3 pos)
    {
        var root = new GameObject("HitFlash_FX");
        root.transform.position = pos;

        AddSpikeStreaks(root.transform);   // main sharp lines radiating outward
        AddShortSparks(root.transform);    // tight inner burst
        AddCenterFlare(root.transform);    // single tiny bright center dot (NOT a square)

        Object.Destroy(root, 0.6f);
    }

    // ── Long sharp spike streaks (the main visual) ───────────────────────────
    // Stretched particles along velocity = thin lines/spikes, not squares

    static void AddSpikeStreaks(Transform parent)
    {
        var go  = new GameObject("Streaks");
        go.transform.SetParent(parent, false);

        var ps  = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr         = go.GetComponent<ParticleSystemRenderer>();
        psr.material    = AdditiveMat(new Color(1f, 0.9f, 0.5f));
        psr.renderMode  = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 14f;
        psr.sortingOrder = 11;

        var main = ps.main;
        main.duration        = 0.05f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(8f, 20f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 1f, 1f),
                                   new Color(1f, 0.85f, 0.3f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.None;
        main.maxParticles    = 20;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 16) });

        // Sphere shape so streaks fire in all directions
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.01f;

        var sol     = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f,  1f,  0f,  -2f),
                new Keyframe(0.5f, 0.4f),
                new Keyframe(1f,  0f)));

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = FadeOutGrad(new Color(1f, 0.7f, 0.2f));

        ps.Play();
    }

    // ── Short inner sparks ───────────────────────────────────────────────────
    // Slightly shorter, brighter — fills in the center area

    static void AddShortSparks(Transform parent)
    {
        var go  = new GameObject("InnerSparks");
        go.transform.SetParent(parent, false);

        var ps  = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr         = go.GetComponent<ParticleSystemRenderer>();
        psr.material    = AdditiveMat(new Color(1f, 1f, 0.8f));
        psr.renderMode  = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 8f;
        psr.sortingOrder = 12;

        var main = ps.main;
        main.duration        = 0.05f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 1f, 1f),
                                   new Color(1f, 0.95f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.None;
        main.maxParticles    = 20;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 10) });

        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.01f;

        var sol     = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)));

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = FadeOutGrad(new Color(1f, 0.9f, 0.5f));

        ps.Play();
    }

    // ── Tiny center flare — NOT a square ────────────────────────────────────
    // Single particle, very small, just a bright point at impact center

    static void AddCenterFlare(Transform parent)
    {
        var go  = new GameObject("CenterFlare");
        go.transform.SetParent(parent, false);

        var ps  = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr         = go.GetComponent<ParticleSystemRenderer>();
        psr.material    = AdditiveMat(new Color(1f, 1f, 1f));
        // Use Stretch with lengthScale=1 and velocityScale=0 → renders as a
        // very small stretched dot, much less "square" than Billboard
        psr.renderMode  = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 2f;
        psr.sortingOrder = 13;

        var main = ps.main;
        main.duration        = 0.05f;
        main.loop            = false;
        main.startLifetime   = 0.2f;
        main.startSpeed      = 0f;
        main.startSize       = 0.3f;
        main.startColor      = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.None;
        main.maxParticles    = 2;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var flareShape = ps.shape;
        flareShape.enabled = false;

        var sol     = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -4f),
                new Keyframe(1f, 0f)));

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = FadeOutGrad(Color.white);

        ps.Play();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static Material AdditiveMat(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(s) { name = "HitFlash_Runtime" };
        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    2f);   // Additive
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 1f);
        mat.SetFloat("_ZWrite",   0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);
        mat.renderQueue     = 3000;
        mat.enableInstancing = true;
        return mat;
    }

    static ParticleSystem.MinMaxGradient FadeOutGrad(Color color)
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
}
