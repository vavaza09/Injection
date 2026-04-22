using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Movement
{
    /// <summary>
    /// UI display for dash aim direction. The indicator is visible only while aim mode is held.
    /// </summary>
    public class DashAimDirectionDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform arrowTransform;
        [SerializeField] private Graphic arrowGraphic;
        [SerializeField] private Image ringImage;

        [Header("Style")]
        [SerializeField] private Color canDashColor = Color.white;
        [SerializeField] private Color cannotDashColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private bool hideWhileDashing = true;
        [SerializeField, Min(0f)] private float orbitRadius = 0f;
        [SerializeField] private float angleOffset = 0f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float orbitSmoothSpeed = 18f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Ring Guide")]
        [SerializeField] private bool autoCreateRingImage = true;
        [SerializeField, Min(16)] private int ringResolution = 128;
        [SerializeField, Min(1f)] private float ringThickness = 4f;
        [SerializeField] private Color ringCanDashColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color ringCannotDashColor = new Color(1f, 0f, 0f, 0.2f);

        private RectTransform _orbitTarget;
        private RectTransform _rotationTarget;
        private float _currentAngle;
        private bool _angleInitialized;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (arrowTransform == null)
            {
                arrowTransform = transform as RectTransform;
            }

            // Default to the visible graphic, fallback to assigned transform.
            _orbitTarget = arrowGraphic != null ? arrowGraphic.rectTransform : null;
            if (_orbitTarget == null)
            {
                _orbitTarget = arrowTransform;
            }

            _rotationTarget = _orbitTarget;

            if (orbitRadius <= 0f && _orbitTarget != null)
            {
                orbitRadius = _orbitTarget.anchoredPosition.magnitude;
            }

            if (orbitRadius <= 0f)
            {
                orbitRadius = 50f;
            }

            if (autoCreateRingImage && ringImage == null)
            {
                ringImage = CreateRuntimeRingImage();
            }

            if (ringImage != null)
            {
                ringImage.raycastTarget = false;
                UpdateRingTransform();
            }
        }

        public void Refresh(Vector2 aimDirection, bool isAimHeld, bool canDash, bool isDashing)
        {
            bool shouldShow = isAimHeld && (!hideWhileDashing || !isDashing);
            if (root != null && root.activeSelf != shouldShow)
            {
                root.SetActive(shouldShow);
            }

            if (ringImage != null && ringImage.gameObject.activeSelf != shouldShow)
            {
                ringImage.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                _angleInitialized = false;
                return;
            }

            Vector2 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (!_angleInitialized)
            {
                _currentAngle = targetAngle;
                _angleInitialized = true;
            }
            else if (orbitSmoothSpeed > 0f)
            {
                float t = 1f - Mathf.Exp(-orbitSmoothSpeed * Mathf.Max(0f, deltaTime));
                _currentAngle = Mathf.LerpAngle(_currentAngle, targetAngle, t);
            }
            else
            {
                _currentAngle = targetAngle;
            }

            float angleRad = _currentAngle * Mathf.Deg2Rad;
            Vector2 orbitDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            if (_orbitTarget != null)
            {
                _orbitTarget.anchoredPosition = orbitDirection * orbitRadius;
            }

            if (_rotationTarget != null)
            {
                _rotationTarget.localRotation = Quaternion.Euler(0f, 0f, _currentAngle + angleOffset);
            }

            if (arrowGraphic != null)
            {
                arrowGraphic.color = canDash ? canDashColor : cannotDashColor;
            }

            UpdateRingVisual(canDash);
        }

        private void UpdateRingVisual(bool canDash)
        {
            if (ringImage == null)
            {
                return;
            }

            ringImage.color = canDash ? ringCanDashColor : ringCannotDashColor;
            UpdateRingTransform();
        }

        private void UpdateRingTransform()
        {
            if (ringImage == null)
            {
                return;
            }

            RectTransform ringRect = ringImage.rectTransform;
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = Vector2.zero;

            float diameter = Mathf.Max(2f, orbitRadius * 2f);
            ringRect.sizeDelta = new Vector2(diameter, diameter);
        }

        private Image CreateRuntimeRingImage()
        {
            if (root == null)
            {
                return null;
            }

            GameObject ringObject = new GameObject("DashAimRing");
            ringObject.transform.SetParent(root.transform, false);

            RectTransform ringRect = ringObject.AddComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.anchoredPosition = Vector2.zero;

            ringObject.AddComponent<CanvasRenderer>();
            Image image = ringObject.AddComponent<Image>();
            image.sprite = BuildRingSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // Keep the guide behind arrows.
            ringRect.SetAsFirstSibling();
            return image;
        }

        private Sprite BuildRingSprite()
        {
            int size = Mathf.Clamp(ringResolution, 16, 512);
            float thicknessPx = Mathf.Clamp(ringThickness, 1f, size * 0.5f);
            float radius = (size - 1) * 0.5f;
            float halfThickness = thicknessPx * 0.5f;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float delta = Mathf.Abs(distance - radius);

                    float alpha = delta <= halfThickness
                        ? 1f
                        : Mathf.Clamp01(1f - (delta - halfThickness));

                    if (alpha <= 0f)
                    {
                        texture.SetPixel(x, y, clear);
                    }
                    else
                    {
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
        }
    }
}
