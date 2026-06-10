using TMPro;
using UnityEngine;

namespace Game.UI.Health
{
    public class PlayerHealthHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player player;
        [SerializeField] private TextMeshProUGUI remainingHitsLabel;
        [SerializeField] private TextMeshProUGUI hitCountLabel;

        [Header("Text Format")]
        [SerializeField] private string remainingHitsFormat = "Remaining Hits: {0}/{1}";
        [SerializeField] private string hitCountFormat = "Hits Taken: {0}";

        private int _lastRemainingHits = int.MinValue;
        private int _lastMaxHits = int.MinValue;
        private int _lastHitCount = int.MinValue;

        private void Awake()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<Player>();
            }

            AutoBindLabelsIfMissing();
            EnsureLabelsExist();
        }

        private void Start()
        {
            BindEvents();
            Refresh(force: true);
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        public void Initialize(Player targetPlayer)
        {
            UnbindEvents();
            player = targetPlayer;
            BindEvents();
            Refresh(force: true);
        }

        private void BindEvents()
        {
            if (player == null) return;
            player.Damaged  += OnHealthChanged;
            player.Healed   += OnHealthChanged;
            player.Died     += OnHealthChanged;
        }

        private void UnbindEvents()
        {
            if (player == null) return;
            player.Damaged  -= OnHealthChanged;
            player.Healed   -= OnHealthChanged;
            player.Died     -= OnHealthChanged;
        }

        private void OnHealthChanged(character _) => Refresh(force: true);

        private void AutoBindLabelsIfMissing()
        {
            if (remainingHitsLabel != null && hitCountLabel != null)
            {
                return;
            }

            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI label in labels)
            {
                if (label == null) continue;

                string labelName = label.gameObject.name.ToLowerInvariant();

                if (remainingHitsLabel == null &&
                    (labelName.Contains("remaining") || labelName.Contains("remain") || labelName.Contains("health")))
                {
                    remainingHitsLabel = label;
                    continue;
                }

                if (hitCountLabel == null &&
                    (labelName.Contains("hitcount") || labelName.Contains("hit_count") || labelName.Contains("hits")))
                {
                    hitCountLabel = label;
                }
            }
        }

        private void EnsureLabelsExist()
        {
            if (remainingHitsLabel == null)
                remainingHitsLabel = CreateFallbackLabel("RemainingHitsText", new Vector2(20f, -20f));

            if (hitCountLabel == null)
                hitCountLabel = CreateFallbackLabel("HitCountText", new Vector2(20f, -55f));
        }

        private TextMeshProUGUI CreateFallbackLabel(string objectName, Vector2 anchoredPosition)
        {
            RectTransform rootRect = transform as RectTransform;
            if (rootRect == null) return null;

            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(360f, 30f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = Color.white;
            return label;
        }

        private void Refresh(bool force)
        {
            if (player == null) return;

            int remainingHits = player.GetRemainingHits();
            int maxHits = player.GetMaxHits();
            int hitCount = player.GetHitCount();

            if (force || remainingHits != _lastRemainingHits || maxHits != _lastMaxHits)
            {
                if (remainingHitsLabel != null)
                    remainingHitsLabel.text = string.Format(remainingHitsFormat, remainingHits, maxHits);

                _lastRemainingHits = remainingHits;
                _lastMaxHits = maxHits;
            }

            if (force || hitCount != _lastHitCount)
            {
                if (hitCountLabel != null)
                    hitCountLabel.text = string.Format(hitCountFormat, hitCount);

                _lastHitCount = hitCount;
            }
        }
    }
}
