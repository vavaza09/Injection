using UnityEngine;

/// <summary>
/// One ghost frame that fades out. Pooled by PlayerMovementVFX.
/// Uses unscaled time so dash-impact hitstop doesn't freeze ghosts mid-fade.
/// </summary>
public class SpriteAfterimage : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _fadeDuration;
    private float _elapsed;
    private float _startAlpha;
    private bool _active;

    private void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingLayerName = "Default";
        _sr.sortingOrder = 0;
    }

    public void Show(Sprite sprite, Vector3 worldPos, Quaternion worldRot, Vector3 lossyScale,
                     Color tint, float startAlpha, float fadeDuration)
    {
        transform.position = worldPos;
        transform.rotation = worldRot;
        // Carry ±1 x-flip from player's root lossyScale
        transform.localScale = lossyScale;

        _sr.sprite = sprite;
        _sr.color = new Color(tint.r, tint.g, tint.b, startAlpha);
        _startAlpha = startAlpha;
        _fadeDuration = Mathf.Max(0.01f, fadeDuration);
        _elapsed = 0f;
        _active = true;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!_active) return;
        _elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _fadeDuration);
        Color c = _sr.color;
        c.a = Mathf.Lerp(_startAlpha, 0f, t);
        _sr.color = c;
        if (t >= 1f)
        {
            _active = false;
            gameObject.SetActive(false);
        }
    }
}
