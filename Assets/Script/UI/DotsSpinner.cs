using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class DotsSpinner : MonoBehaviour
    {
        [Header("Dots")]
        [SerializeField] private Sprite dotSprite;          // null = auto circle
        [SerializeField] private int    dotCount      = 12;
        [SerializeField] private float  radius        = 40f;
        [SerializeField] private Vector2 dotSize      = new Vector2(14f, 14f);
        [SerializeField] private Color  dotColor      = Color.white;

        [Header("Animation")]
        [SerializeField] private float  stepsPerSecond = 10f;

        [Header("Tail")]
        [SerializeField] private float  minAlpha = 0.08f;
        [SerializeField] private float  minScale = 0.45f;

        private Image[] _dots;
        private int     _currentStep;
        private float   _timer;

        private void Awake()
        {
            BuildDots();
        }

        private void BuildDots()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            Sprite sprite = dotSprite != null ? dotSprite : MakeCircleSprite(32);
            _dots = new Image[dotCount];

            for (int i = 0; i < dotCount; i++)
            {
                var go   = new GameObject("Dot_" + i);
                go.transform.SetParent(transform, false);

                var rect        = go.AddComponent<RectTransform>();
                rect.sizeDelta  = dotSize;

                float angleRad  = -i * (360f / dotCount) * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Sin(angleRad) * radius,
                                                    Mathf.Cos(angleRad) * radius);

                var img          = go.AddComponent<Image>();
                img.sprite       = sprite;
                img.color        = dotColor;
                img.raycastTarget = false;

                _dots[i] = img;
            }

            UpdateDots();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            float interval = 1f / Mathf.Max(stepsPerSecond, 1f);
            if (_timer < interval) return;
            _timer      -= interval;
            _currentStep = (_currentStep + 1) % dotCount;
            UpdateDots();
        }

        private void UpdateDots()
        {
            for (int i = 0; i < dotCount; i++)
            {
                int   offset = ((_currentStep - i) % dotCount + dotCount) % dotCount;
                float t      = 1f - (float)offset / dotCount;   // 1=head, ~0=tail
                float curve  = t * t;                            // ease — tail fades faster

                Color c = dotColor;
                c.a         = Mathf.Lerp(minAlpha, 1f, curve);
                _dots[i].color           = c;
                _dots[i].transform.localScale = Vector3.one * Mathf.Lerp(minScale, 1f, curve);
            }
        }

        // Procedural soft-edge circle so no external sprite is needed.
        private static Sprite MakeCircleSprite(int size)
        {
            var   tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float a    = Mathf.Clamp01(center - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
