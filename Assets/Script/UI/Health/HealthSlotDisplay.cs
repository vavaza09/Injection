using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Health
{
    public class HealthSlotDisplay : MonoBehaviour
    {
        [SerializeField] private Image[] slots;
        [SerializeField] private Sprite fullSprite;
        [SerializeField] private Sprite lostSprite;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Juice")]
        [SerializeField] private float lostPopScale = 1.35f;   // heart punches out when lost
        [SerializeField] private float lostPopDuration = 0.22f;
        [SerializeField] private float showPopScale = 1.15f;   // whole bar pops when it appears
        [SerializeField] private float showPopDuration = 0.25f;

        private float _targetAlpha;
        private int _lastRemaining = -1;
        private bool _wasVisible;
        private Coroutine[] _slotPops;
        private Coroutine _showPop;

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
            if (slots != null) _slotPops = new Coroutine[slots.Length];
        }

        private void Update()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, _targetAlpha, Time.deltaTime / fadeDuration);
        }

        public void Show(bool visible)
        {
            _targetAlpha = visible ? 1f : 0f;

            if (visible && !_wasVisible)
                _showPop = Restart(_showPop, Pop(transform, showPopScale, showPopDuration));
            _wasVisible = visible;
        }

        public void Refresh(int remaining, int max)
        {
            if (slots == null) return;
            if (_slotPops == null || _slotPops.Length != slots.Length)
                _slotPops = new Coroutine[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].gameObject.SetActive(i < max);
                slots[i].sprite = i < remaining ? fullSprite : lostSprite;

                // pop the slots that just went from full -> lost (skip the first fill)
                if (_lastRemaining >= 0 && i >= remaining && i < _lastRemaining)
                    _slotPops[i] = Restart(_slotPops[i],
                        Pop(slots[i].transform, lostPopScale, lostPopDuration));
            }

            _lastRemaining = remaining;
        }

        private Coroutine Restart(Coroutine running, IEnumerator routine)
        {
            if (running != null) StopCoroutine(running);
            return isActiveAndEnabled ? StartCoroutine(routine) : null;
        }

        private static IEnumerator Pop(Transform t, float scale, float duration)
        {
            if (t == null) yield break;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(e / duration);      // 1 -> 0
                float s = 1f + (scale - 1f) * k;                 // scale -> 1
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }
    }
}
