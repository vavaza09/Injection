using System.Collections;
using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour
{
    private IEnumerator Start()
    {
        float maxDuration = 0f;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            float d = main.duration + main.startLifetime.constantMax;
            if (d > maxDuration) maxDuration = d;
        }
        if (maxDuration <= 0f) maxDuration = 2f;

        yield return new WaitForSecondsRealtime(maxDuration);
        Destroy(gameObject);
    }
}
