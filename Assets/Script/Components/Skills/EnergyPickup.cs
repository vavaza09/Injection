using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Components.Skills
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnergyPickup : MonoBehaviour
    {
        [SerializeField] private int   energyAmount   = 1;
        [SerializeField] private float respawnSeconds = 30f;
        [SerializeField] private GameObject glowRoot;

        [Header("Light")]
        [SerializeField] private Light2D _spotLight;

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

        [Header("Interact Prompt")]
        [SerializeField] private GameObject _interactPrompt;

        public bool IsAvailable { get; private set; } = true;

        private SpriteRenderer _glowRenderer;
        private float          _baseAlpha;
        private float          _baseIntensity;
        private Material       _defaultMaterial;
        private Material       _outlineMaterial;
        private Coroutine      _promptAnim;
        private Vector3        _promptTargetScale;
        private bool           _playerInRange;

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

            // Light2D setup
            if (_spotLight == null)
                _spotLight = GetComponentInChildren<Light2D>();
            if (_spotLight != null)
                _baseIntensity = _spotLight.intensity;

            // Glow sprite is only created as fallback when no Light2D is present
            if (glowRoot == null && _spotLight == null)
                glowRoot = CreateDefaultGlow();

            if (glowRoot != null)
            {
                _glowRenderer = glowRoot.GetComponent<SpriteRenderer>();
                if (_glowRenderer != null)
                    _baseAlpha = _glowRenderer.color.a;
            }

            var outlineSR = outlineTarget != null ? outlineTarget : _glowRenderer;
            if (outlineShader != null && outlineSR != null)
            {
                _defaultMaterial = outlineSR.sharedMaterial;
                _outlineMaterial = new Material(outlineShader);
                _outlineMaterial.SetColor("_OutlineColor", outlineColor);
                _outlineMaterial.SetFloat("_OutlineSize", outlineSize);
                _outlineMaterial.SetFloat("_AlphaThreshold", outlineAlphaThreshold);
            }

            // Interact prompt
            if (_interactPrompt == null)
                _interactPrompt = CreateDefaultPrompt();
            _promptTargetScale = _interactPrompt.transform.localScale;
            _interactPrompt.SetActive(false);
        }

        private void Update()
        {
            if (!IsAvailable) return;

            if (_spotLight != null && _spotLight.enabled)
            {
                _spotLight.intensity = Mathf.Max(0f, _baseIntensity + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * _baseIntensity);
                return;
            }

            // Fallback: pulse glow sprite alpha
            if (_glowRenderer == null) return;
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

            if (_spotLight != null) _spotLight.enabled = false;
            else if (glowRoot != null) glowRoot.SetActive(false);

            HidePrompt();
            if (_defaultMaterial != null)
            {
                var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
                if (sr != null) sr.material = _defaultMaterial;
            }

            if (respawnSeconds > 0f)
                StartCoroutine(RespawnAfter(respawnSeconds));

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = true;
            if (!IsAvailable) return;
            if (_outlineMaterial != null)
            {
                var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
                if (sr != null) sr.material = _outlineMaterial;
            }
            ShowPrompt();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            if (_defaultMaterial != null)
            {
                var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
                if (sr != null) sr.material = _defaultMaterial;
            }
            HidePrompt();
        }

        private IEnumerator RespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            IsAvailable = true;
            if (_spotLight != null) _spotLight.enabled = true;
            else if (glowRoot != null) glowRoot.SetActive(true);
            if (_playerInRange)
            {
                if (_outlineMaterial != null)
                {
                    var sr = outlineTarget != null ? outlineTarget : _glowRenderer;
                    if (sr != null) sr.material = _outlineMaterial;
                }
                ShowPrompt();
            }
        }

        private void ShowPrompt()
        {
            if (_interactPrompt == null || !IsAvailable) return;
            if (_promptAnim != null) StopCoroutine(_promptAnim);
            _promptAnim = StartCoroutine(AnimatePrompt(true));
        }

        private void HidePrompt()
        {
            if (_interactPrompt == null) return;
            if (_promptAnim != null) StopCoroutine(_promptAnim);
            _promptAnim = StartCoroutine(AnimatePrompt(false));
        }

        private IEnumerator AnimatePrompt(bool show)
        {
            if (show) _interactPrompt.SetActive(true);
            var t = _interactPrompt.transform;
            Vector3 from = show ? Vector3.zero : _promptTargetScale;
            Vector3 to   = show ? _promptTargetScale : Vector3.zero;
            float elapsed = 0f;
            const float dur = 0.15f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            t.localScale = to;
            if (!show) _interactPrompt.SetActive(false);
        }

        private GameObject CreateDefaultPrompt()
        {
            var root = new GameObject("InteractPrompt");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            root.transform.localScale    = new Vector3(0.004f, 0.004f, 1f);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 38f);

            // Background triangle placeholder
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.sizeDelta        = new Vector2(100f, 38f);
            bgRT.anchoredPosition = Vector2.zero;
            var img  = bgGO.AddComponent<UnityEngine.UI.Image>();
            img.color  = new Color(0.08f, 0.55f, 0.85f, 0.88f);
            img.sprite = CreateTriangleSprite();

            // "Interact" label
            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(root.transform, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.sizeDelta        = new Vector2(100f, 38f);
            txtRT.anchoredPosition = Vector2.zero;
            var tmp       = txtGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = "Interact";
            tmp.fontSize  = 20f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            return root;
        }

        private static Sprite CreateTriangleSprite()
        {
            const int size   = 64;
            var tex          = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels       = new Color32[size * size];
            int center       = size / 2;
            for (int y = 0; y < size; y++)
            {
                float t  = y / (float)(size - 1);
                int half = Mathf.RoundToInt(t * center);
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= center - half && x <= center + half;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
