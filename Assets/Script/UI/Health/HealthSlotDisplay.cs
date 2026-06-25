using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Health
{
    public class HealthSlotDisplay : MonoBehaviour
    {
        [SerializeField] private Image[] slots;
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color emptyColor  = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.25f;

        private float _targetAlpha;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _targetAlpha = 0f;
        }

        private void Update()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, _targetAlpha, Time.deltaTime / fadeDuration);
        }

        public void Show(bool visible) => _targetAlpha = visible ? 1f : 0f;

        public void Refresh(int remaining, int max)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].gameObject.SetActive(i < max);
                slots[i].color = i < remaining ? filledColor : emptyColor;
            }
        }
    }
}
