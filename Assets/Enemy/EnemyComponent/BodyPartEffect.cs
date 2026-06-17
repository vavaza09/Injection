using System.Collections;
using UnityEngine;

public class BodyPartEffect : MonoBehaviour
{
    public void Initialize(float lifetime, float fadeDuration)
    {
        Destroy(gameObject, lifetime + 0.1f);
        StartCoroutine(FadeOut(Mathf.Max(0f, lifetime - fadeDuration), fadeDuration));
    }

    private IEnumerator FadeOut(float delay, float fadeDuration)
    {
        yield return new WaitForSeconds(delay);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float elapsed = 0f;
        Color c = sr.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            sr.color = c;
            yield return null;
        }
    }
}
