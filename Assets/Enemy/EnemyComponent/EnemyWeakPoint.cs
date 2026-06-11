using UnityEngine;
using System.Collections.Generic;

public class EnemyWeakPoint : MonoBehaviour
{
    [SerializeField] private Enemy ownerEnemy;
    [SerializeField] private List<Collider2D> weakPointColliders = new List<Collider2D>();

    public Enemy OwnerEnemy => ownerEnemy;
    public List<Collider2D> WeakPointColliders => weakPointColliders;

    private void Awake()
    {
        if (ownerEnemy == null)
        {
            ownerEnemy = GetComponentInParent<Enemy>();
        }
    }

    public bool IsWeakPoint(Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        if (weakPointColliders != null && weakPointColliders.Count > 0)
        {
            return weakPointColliders.Contains(hitCollider);
        }

        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
        {
            return ownCollider == hitCollider;
        }

        return false;
    }
}
