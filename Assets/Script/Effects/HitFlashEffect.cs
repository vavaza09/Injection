using UnityEngine;

public class HitFlashEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float maxScale = 1.5f;

    private SpriteRenderer _sr;
    private float _elapsed;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);

        float scale = scaleCurve.Evaluate(t) * maxScale;
        transform.localScale = new Vector3(scale, scale, 1f);

        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = alphaCurve.Evaluate(t);
            _sr.color = c;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}
