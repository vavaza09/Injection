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

        public bool IsAvailable { get; private set; } = true;

        private SpriteRenderer _glowRenderer;
        private float          _baseAlpha;

        private void Awake()
        {
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;

            if (glowRoot == null)
                glowRoot = CreateDefaultGlow();

            _glowRenderer = glowRoot.GetComponent<SpriteRenderer>();
            if (_glowRenderer != null)
                _baseAlpha = _glowRenderer.color.a;
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

            amount    = energyAmount;
            IsAvailable = false;
            glowRoot.SetActive(false);

            if (respawnSeconds > 0f)
                StartCoroutine(RespawnAfter(respawnSeconds));

            return true;
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

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite      = CreateSoftCircleSprite();
            sr.color       = new Color(0.4f, 0.8f, 1f, 0.6f);
            sr.sortingOrder = 1;

            go.transform.localScale = Vector3.one * 1.5f;
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
