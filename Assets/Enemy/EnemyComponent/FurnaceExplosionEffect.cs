using UnityEngine;

public class FurnaceExplosionEffect : MonoBehaviour
{
    private SpriteRenderer _ringSR;
    private SpriteRenderer _flashSR;
    private float _radius;
    private float _elapsed;

    private const float RingDuration = 0.4f;
    private const float FlashDuration = 0.15f;
    private const float TotalLifetime = 1.5f;

    public void Setup(SpriteRenderer ring, SpriteRenderer flash, float radius)
    {
        _ringSR = ring;
        _flashSR = flash;
        _radius = radius;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (_ringSR != null)
        {
            float rt = Mathf.Clamp01(_elapsed / RingDuration);
            _ringSR.transform.localScale = Vector3.one * Mathf.Lerp(0f, _radius * 2f, rt);
            Color rc = _ringSR.color;
            rc.a = Mathf.Lerp(0.9f, 0f, rt);
            _ringSR.color = rc;
        }

        if (_flashSR != null)
        {
            float ft = Mathf.Clamp01(_elapsed / FlashDuration);
            _flashSR.transform.localScale = Vector3.one * Mathf.Lerp(_radius * 0.5f, _radius * 0.8f, ft);
            Color fc = _flashSR.color;
            fc.a = Mathf.Lerp(1f, 0f, ft);
            _flashSR.color = fc;
        }

        if (_elapsed >= TotalLifetime)
            Destroy(gameObject);
    }
}
