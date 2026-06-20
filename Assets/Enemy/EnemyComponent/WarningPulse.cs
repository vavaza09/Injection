using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WarningPulse : MonoBehaviour
{
    [SerializeField] public float pulseSpeed = 4f;
    [SerializeField] public float minAlpha = 0.2f;
    [SerializeField] public float maxAlpha = 0.6f;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_sr == null) return;
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Color c = _sr.color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        _sr.color = c;
    }
}
