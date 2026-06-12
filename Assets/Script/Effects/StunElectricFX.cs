using UnityEngine;

/// <summary>
/// Attaches a looping 2D electric stun effect to an enemy.
/// Two layers: sparks bursting from the body outline + zigzag arc trails orbiting around.
/// Call StunElectricFX.Attach() when stun begins; Destroy the returned GameObject when stun ends.
/// </summary>
public static class StunElectricFX
{
    public static GameObject Attach(Transform parent, Bounds spriteBounds)
    {
        var root = new GameObject("StunElectric_FX");
        root.transform.SetParent(parent, worldPositionStays: false);

        // Centre on the sprite; offset in local space
        Vector3 localCenter = parent.InverseTransformPoint(spriteBounds.center);
        root.transform.localPosition = localCenter;

        // Counter-scale so particles appear the right size regardless of non-uniform enemy scale
        Vector3 ls = parent.lossyScale;
        root.transform.localScale = new Vector3(
            ls.x != 0f ? 1f / ls.x : 1f,
            ls.y != 0f ? 1f / ls.y : 1f,
            1f);

        float rx = spriteBounds.extents.x;
        float ry = spriteBounds.extents.y;
        float radius = Mathf.Max(rx, ry, 0.3f);

        AddSparks(root.transform, rx, ry);
        AddArcTrails(root.transform, radius);

        return root;
    }

    // ── Layer 1: short sparks bursting from sprite outline ──────────────────

    static void AddSparks(Transform parent, float rx, float ry)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material    = AdditiveMat(new Color(0.5f, 1f, 1f));
        psr.renderMode  = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = 0f;
        psr.lengthScale = 6f;
        psr.sortingOrder = 14;

        var main = ps.main;
        main.duration        = 1f;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 1f, 1f),
                                   new Color(0.4f, 0.95f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.None;
        main.maxParticles    = 60;

        // Bursts every 0.12s — makes it feel like intermittent electrical discharges
        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.00f, 3, 6, 999, 0.12f),
        });

        // Emit from sprite ellipse outline
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = Mathf.Max(rx, ry, 0.25f);
        shape.radiusThickness = 0f;   // emit from edge only

        var sol     = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),
                new Keyframe(0.6f, 0.3f),
                new Keyframe(1f, 0f)));

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = FadeOutGrad(new Color(0.3f, 0.9f, 1f));

        ps.Play();
    }

    // ── Layer 2: arc trail orbiting body (noise makes path zigzag) ──────────

    static void AddArcTrails(Transform parent, float radius)
    {
        var go = new GameObject("ArcTrails");
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        // Hide particle body — only trails are visible
        psr.material      = AdditiveMat(new Color(0f, 0f, 0f, 0f));
        psr.trailMaterial = AdditiveMat(new Color(0.2f, 0.8f, 1f));
        psr.renderMode    = ParticleSystemRenderMode.None;
        psr.sortingOrder  = 15;

        var main = ps.main;
        main.duration        = 1f;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize       = 0.05f;
        main.startColor      = new Color(0.3f, 0.9f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction      = ParticleSystemStopAction.None;
        main.maxParticles    = 20;

        var em = ps.emission;
        em.enabled      = true;
        em.rateOverTime = 6f;   // steady stream so arcs look continuous

        // Emit from circle outline — particles fly outward tangentially → orbital look
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = radius * 0.8f;
        shape.radiusThickness = 0f;

        // Noise = turbulence that makes straight path into zigzag arc
        var noise        = ps.noise;
        noise.enabled    = true;
        noise.strength   = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        noise.frequency  = 4f;
        noise.quality    = ParticleSystemNoiseQuality.Low;
        noise.scrollSpeed = 1.5f;
        noise.damping    = false;

        // Trails follow the zigzag path — this creates the lightning arc look
        var trails = ps.trails;
        trails.enabled         = true;
        trails.ratio           = 1f;
        trails.lifetime        = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
        trails.dieWithParticles = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail  = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)));
        trails.colorOverLifetime = FadeOutGrad(new Color(0.2f, 0.8f, 1f));
        trails.minVertexDistance = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = FadeOutGrad(new Color(0.3f, 0.9f, 1f));

        ps.Play();
    }

    // ── Helpers (mirror of HitFlashFX helpers, kept private here) ───────────

    static Material AdditiveMat(Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Additive");

        var mat = new Material(s) { name = "StunElectric_Runtime" };
        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    2f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 1f);
        mat.SetFloat("_ZWrite",   0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);
        mat.renderQueue      = 3000;
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
