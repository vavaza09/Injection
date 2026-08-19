using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

// Spawns a brief flashing 2D point light at an explosion's origin. Shared by
// TankBulletImpactVFX and MortarExplosionVFX so both "explosion bullet" impacts flash.
public static class ExplosionFlashLight2D
{
    public static void Spawn(Vector3 position, float intensity = 5f, float outerRadius = 8f, float falloffIntensity = 0.7f, float duration = 0.25f, Color? color = null)
    {
        var go = new GameObject("ExplosionFlashLight");
        go.transform.position = position;

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color ?? new Color(1f, 0.75f, 0.35f);
        light.intensity = intensity;
        light.pointLightOuterRadius = outerRadius;
        light.falloffIntensity = falloffIntensity;

        go.AddComponent<FlashRunner>().Run(light, intensity, duration);
    }

    private class FlashRunner : MonoBehaviour
    {
        public void Run(Light2D light, float startIntensity, float duration) => StartCoroutine(Fade(light, startIntensity, duration));

        private IEnumerator Fade(Light2D light, float startIntensity, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(startIntensity, 0f, t / duration);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
