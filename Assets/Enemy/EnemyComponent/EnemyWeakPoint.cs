using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyWeakPoint : MonoBehaviour
{
    private enum ArmorState { Intact, Breaking, Gone }

    [SerializeField] private Enemy ownerEnemy;
    [SerializeField] private List<Collider2D> weakPointColliders = new List<Collider2D>();

    [Header("Armor (optional)")]
    [SerializeField] private SpriteRenderer armorRenderer;
    [SerializeField] private Sprite armorDamagedSprite;
    [Tooltip("Seconds the broken plate stays attached before it falls off and exposes the weak point.")]
    [SerializeField] private float armorFallDelay = 1.5f;
    [SerializeField] private float armorWhiteFlashDuration = 0.15f;
    [SerializeField] private Color armorFlashColor = Color.white;
    [Tooltip("Material using the Custom/SpriteFlash shader — swapped in for the flash duration, " +
             "then restored. A plain color lerp can't turn a sprite white (multiplying by white " +
             "is a no-op); this shader lerps the sprite's RGB toward _FlashColor instead.")]
    [SerializeField] private Material armorFlashMaterial;

    [Header("Armor Fall-Off")]
    [SerializeField] private float fallGravityScale = 1.5f;
    [SerializeField] private float fallLaunchForce = 3f;
    [SerializeField] private float fallSpin = 180f;
    [SerializeField] private float fallLifetime = 2.2f;
    [SerializeField] private float fallFadeDuration = 0.8f;

    private ArmorState _armorState;
    private Material _baseArmorMaterial;
    private Material _flashMatInstance;

    public Enemy OwnerEnemy => ownerEnemy;
    public List<Collider2D> WeakPointColliders => weakPointColliders;

    /// <summary>True while the plate is still attached (Intact or mid-break) — the weak point cannot be killed yet.</summary>
    public bool HasArmor => _armorState != ArmorState.Gone;

    /// <summary>True once the plate has fallen off — the weak point can now be killed.</summary>
    public bool WeakPointExposed => _armorState == ArmorState.Gone;

    private void Awake()
    {
        if (ownerEnemy == null)
        {
            ownerEnemy = GetComponentInParent<Enemy>();
        }

        if (armorRenderer != null && armorDamagedSprite != null)
        {
            _armorState = ArmorState.Intact;
            _baseArmorMaterial = armorRenderer.sharedMaterial;

            if (armorFlashMaterial != null)
            {
                _flashMatInstance = new Material(armorFlashMaterial);
                _flashMatInstance.SetColor("_FlashColor", armorFlashColor);
            }
        }
        else
        {
            _armorState = ArmorState.Gone;
        }
    }

    private void OnDestroy()
    {
        if (_flashMatInstance != null)
        {
            Destroy(_flashMatInstance);
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

    /// <summary>Cracks the plate: blinks white, swaps to the damaged sprite, then falls off after armorFallDelay.</summary>
    public void HitArmor()
    {
        if (_armorState != ArmorState.Intact) return;
        _armorState = ArmorState.Breaking;
        SoundManager.PlaySound(SoundType.ENEMY_ARMOR_HIT);
        StartCoroutine(BreakSequence());
    }

    private IEnumerator BreakSequence()
    {
        armorRenderer.sprite = armorDamagedSprite;

        if (_flashMatInstance != null)
        {
            // Set _FlashAmount to full BEFORE swapping the material so the impact frame
            // never renders unlit-at-zero (a one-frame wrong-looking flash of the base color).
            _flashMatInstance.SetFloat("_FlashAmount", 1f);
            armorRenderer.sharedMaterial = _flashMatInstance;

            float t = 0f;
            while (t < armorWhiteFlashDuration)
            {
                t += Time.deltaTime;
                _flashMatInstance.SetFloat("_FlashAmount", 1f - t / armorWhiteFlashDuration);
                yield return null;
            }

            armorRenderer.sharedMaterial = _baseArmorMaterial;
        }

        yield return new WaitForSeconds(armorFallDelay);

        DetachAndFall();
        _armorState = ArmorState.Gone;
    }

    private void DetachAndFall()
    {
        SoundManager.PlaySound(SoundType.ENEMY_ARMOR_BREAK);

        GameObject plate = new GameObject("_ArmorPlate");
        plate.transform.SetPositionAndRotation(armorRenderer.transform.position, armorRenderer.transform.rotation);
        plate.transform.localScale = armorRenderer.transform.lossyScale;

        SpriteRenderer sr = plate.AddComponent<SpriteRenderer>();
        sr.sprite = armorDamagedSprite;
        sr.sortingLayerID = armorRenderer.sortingLayerID;
        sr.sortingOrder = armorRenderer.sortingOrder;
        sr.flipX = armorRenderer.flipX;
        sr.flipY = armorRenderer.flipY;

        Rigidbody2D rb = plate.AddComponent<Rigidbody2D>();
        rb.gravityScale = fallGravityScale;
        rb.freezeRotation = false;
        rb.angularVelocity = Random.Range(-fallSpin, fallSpin);
        rb.linearVelocity = new Vector2(Random.Range(-fallLaunchForce, fallLaunchForce) * 0.3f, fallLaunchForce);

        BodyPartEffect effect = plate.AddComponent<BodyPartEffect>();
        effect.Initialize(fallLifetime, fallFadeDuration);

        armorRenderer.enabled = false;
    }
}
