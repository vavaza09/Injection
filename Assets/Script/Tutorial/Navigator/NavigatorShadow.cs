using System.Collections;
using UnityEngine;

namespace Game.Tutorial.Navigator
{
    /// <summary>
    /// A single shared shadow ghost that loops a NavigatorClip relative to a step anchor.
    /// TutorialManager calls Play/Stop/Dim. Uses unscaled time so slow-motion doesn't affect it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class NavigatorShadow : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float fullAlpha = 1f;
        [SerializeField] private float dimAlpha = 0.3f;
        [SerializeField] private float fadeTime = 0.4f;

        [Header("Loop")]
        [SerializeField] private float loopGap = 0.5f;

        private SpriteRenderer _sr;
        private NavigatorClip _clip;
        private Transform _anchor;
        private Coroutine _playCoroutine;
        private Coroutine _fadeCoroutine;
        private bool _dimmed;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _sr.color = new Color(1, 1, 1, 0);
            gameObject.SetActive(false);
        }

        /// <summary>Start looping the clip relative to anchor. Fades in to fullAlpha.</summary>
        public void Play(NavigatorClip clip, Transform anchor)
        {
            if (clip == null || clip.IsEmpty) return;

            _clip = clip;
            _anchor = anchor;
            _dimmed = false;

            StopAllCoroutines();
            gameObject.SetActive(true);
            _playCoroutine = StartCoroutine(LoopRoutine());
            SetFade(fullAlpha);
        }

        /// <summary>Dim to dimAlpha — called when player makes their first input.</summary>
        public void Dim()
        {
            if (_dimmed) return;
            _dimmed = true;
            SetFade(dimAlpha);
        }

        /// <summary>Fade out and deactivate.</summary>
        public void Stop()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator LoopRoutine()
        {
            var frames = _clip.Frames;
            while (true)
            {
                float startTime = Time.unscaledTime;

                int fi = 0;
                while (fi < frames.Count)
                {
                    float elapsed = Time.unscaledTime - startTime;

                    // Advance frame index to match elapsed time
                    while (fi < frames.Count - 1 && frames[fi + 1].time <= elapsed)
                        fi++;

                    var frame = frames[fi];

                    // Position: lerp between current and next frame
                    if (fi < frames.Count - 1)
                    {
                        var next = frames[fi + 1];
                        float t = next.time > frame.time
                            ? (elapsed - frame.time) / (next.time - frame.time)
                            : 0f;
                        t = Mathf.Clamp01(t);
                        transform.position = _anchor.position + (Vector3)Vector2.Lerp(frame.localPos, next.localPos, t);
                    }
                    else
                    {
                        transform.position = _anchor.position + (Vector3)frame.localPos;
                    }

                    // Sprite and flip (snap to frame)
                    if (frame.sprite != null)
                        _sr.sprite = frame.sprite;
                    transform.localScale = new Vector3(frame.flipX, 1f, 1f);

                    yield return null;

                    // Check if clip has finished
                    if (Time.unscaledTime - startTime >= _clip.Duration)
                        break;
                }

                // Loop gap: teleport back to start, wait briefly
                if (frames.Count > 0)
                {
                    transform.position = _anchor.position + (Vector3)frames[0].localPos;
                    if (frames[0].sprite != null)
                        _sr.sprite = frames[0].sprite;
                }

                yield return new WaitForSecondsRealtime(loopGap);
            }
        }

        private void SetFade(float targetAlpha)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(targetAlpha));
        }

        private IEnumerator FadeTo(float target)
        {
            float start = _sr.color.a;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, target, elapsed / fadeTime);
                _sr.color = new Color(1, 1, 1, a);
                yield return null;
            }
            _sr.color = new Color(1, 1, 1, target);
        }

        private IEnumerator FadeOut()
        {
            float start = _sr.color.a;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, 0f, elapsed / fadeTime);
                _sr.color = new Color(1, 1, 1, a);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
