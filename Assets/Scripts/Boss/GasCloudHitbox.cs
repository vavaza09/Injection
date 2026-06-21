using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GasCloudHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float tickInterval = 1f;
    [Tooltip("Seconds after spawn before the gas starts dealing damage (lets it spread first).")]
    [SerializeField] private float damageDelay = 3f;

    private float _lastTick = float.MinValue;
    private bool  _canDamage;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public void Init(float duration)
    {
        StartCoroutine(DamageRoutine(duration));
    }

    private IEnumerator DamageRoutine(float duration)
    {
        // Wait before damage becomes active so gas has time to spread.
        yield return new WaitForSeconds(damageDelay);
        _canDamage = true;

        // Keep damaging until the cloud lifetime ends.
        yield return new WaitForSeconds(duration - damageDelay);
        _canDamage = false;

        Destroy(gameObject, 0.1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_canDamage || !other.CompareTag("Player")) return;
        _lastTick = Time.time;
        other.GetComponentInParent<character>()?.TakeDamage(damage);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!_canDamage || !other.CompareTag("Player")) return;
        if (Time.time - _lastTick < tickInterval) return;
        _lastTick = Time.time;
        other.GetComponentInParent<character>()?.TakeDamage(damage);
    }
}
