using System.Collections;
using UnityEngine;

namespace Game.Components.Skills
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnergyPickup : MonoBehaviour
    {
        [SerializeField] private int   energyAmount   = 1;
        [SerializeField] private float respawnSeconds = 30f;
        [SerializeField] private GameObject glowRoot;

        [Header("Pulse")]
        [SerializeField] private float pulseSpeed  = 2f;
        [SerializeField] private float pulseAmount = 0.25f;

        [Header("Proximity Outline")]
        [SerializeField] private SpriteRenderer outlineTarget;
        [SerializeField] private Shader  outlineShader;
        [SerializeField] private Color   outlineColor      = Color.white;
        [SerializeField] private float   outlineSize       = 2f;
        [SerializeField] [Range(0f, 1f)] private float outlineAlphaThreshold = 0.5f;
        [SerializeField] private float   proximityRadius = 1.5f;
        [SerializeField] private Vector2 colliderOffset  = Vector2.zero;
        [SerializeField] private Vector2 glowOffset = Vector2.zero;
        [SerializeField] private float   glowScale  = 1.5f;

        public bool IsAvailable { get; private set; } = true;

        private SpriteRenderer _glowRenderer;
        private float          _baseAlpha;
        private Material       _defaultMaterial;
        private Material       _outlineMaterial;

        private void OnDrawGizmosSelected()
        {
            Vector3 glowWorld = transform.position + (Vector3)glowOffset;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(glowWorld, glowScale * 0.5f);
        }

        private void OnValidate()
        {
            if (!TryGetComponent<CircleCollider2D>(out var col)) return;
            col.isTrigger = true;
            col.radius    = proximityRadius;
            col.offset    = colliderOffset;
        }

        private void Awake()
        {
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = proximityRadius;
            col.offset    = colliderOffset;

            if (glowRoot == null)
                glowRoot = CreateDefaultGlow();

            _glowRenderer = glowRoot.GetComponent<SpriteRenderer>();
            if (_glowRenderer != null)
                _baseAlpha = _glowRenderer.color.a;

            var outlineSR = outlineTarget != null ? outlineTarget : _glowRenderer;
            if (outlineShader != null && outlineSR != null)
            {
                _defaultMaterial = outlineSR.sharedMaterial;
                _outlineMaterial = new Material(outlineShader);
                _outlineMaterial.SetColor("_OutlineColor", outlineColor);
                _outlineMaterial.SetFloat("_OutlineSize", outlineSize);
                _outlineMaterial.SetFloat("_AlphaThreshold", outlineAlphaThreshold);
            }
        }

        private void Update()
        {
            if (!IsAvailable || _glowRenderer == null) return;

            float alpha = _baseAlpha + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * _baseAlpha;
            var c = _glowRenderer.color;
            c.a = Mathf.Clamp01(alpha);
            _glowRenderer.color = c;
        }

        public bool TryCollect(out int amount)
        {
            amount = 0;
            if (!IsAvailable) return false;

            amount      = energyAmount;
            IsAvailable = false;

            if (_defaultMaterial != null)
            {
                var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
                if (sr != null) sr.material = _defaultMaterial;
            }

            glowRoot.SetActive(false);

            if (respawnSeconds > 0f)
                StartCoroutine(RespawnAfter(respawnSeconds));

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsAvailable || _outlineMaterial == null) return;
            if (!other.CompareTag("Player")) return;
            var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
            sr.material = _outlineMaterial;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_defaultMaterial == null) return;
            if (!other.CompareTag("Player")) return;
            var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
            sr.material = _defaultMaterial;
        }

        private IEnumerator RespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            IsAvailable = true;
            glowRoot.SetActive(true);
        }

        private GameObject CreateDefaultGlow()
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = glowOffset;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite      = CreateSoftCircleSprite();
            sr.color       = new Color(0.4f, 0.8f, 1f, 0.6f);
            sr.sortingOrder = 1;

            go.transform.localScale = Vector3.one * glowScale;
            return go;
        }

        private static Sprite CreateSoftCircleSprite()
        {
            const int size = 64;
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float center = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist  = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float t     = Mathf.Clamp01(1f - dist / center);
                    byte  alpha = (byte)(t * t * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
