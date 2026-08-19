using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Core.Logging;
using Game.Pause;
using Game.UI;

namespace Game.Comic
{
    /// <summary>
    /// DontDestroyOnLoad overlay singleton that plays a <see cref="ComicSequenceAsset"/> —
    /// mirrors <c>ScreenFader</c>'s lazy self-creating pattern rather than being a DI-registered
    /// service, since nothing needs to inject it: callers just reach
    /// <see cref="Instance"/> and call <see cref="Play"/>. Lives directly in Assembly-CSharp
    /// (no asmdef) because it talks to <c>SoundManager</c>, <c>Player</c> and
    /// <see cref="PauseStack"/>, all of which are themselves default-assembly types — a custom
    /// asmdef cannot reference the default assembly (predefined assemblies always compile last).
    /// </summary>
    public class ComicPlayer : MonoBehaviour
    {
        private const string PauseHandle = "comic";
        private const float DefaultAutoAdvanceDelay = 2f;
        private const float SkipHoldSeconds = 0.6f;

        private static ComicPlayer _instance;
        public static ComicPlayer Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ComicPlayer");
                    _instance = go.AddComponent<ComicPlayer>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public bool IsPlaying { get; private set; }
        public bool AutoPlay { get; private set; }

        private Core.Logging.ILogger _logger;
        private ComicSfxDispatcher _sfxDispatcher;
        private Canvas _canvas;
        private RectTransform _pagesContainer;
        private CanvasGroup _fadeOverlay;

        private ComicSequenceAsset _sequence;
        private int _pageIndex;
        private int _currentBeat;
        private ComicPageView _currentView;
        private bool _pauseGame;
        private Action _onDone;
        private Coroutine _transitionRoutine;

        private float _autoWaitElapsed;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeTimeRemaining;

        private InputAction _advanceAction;
        private InputAction _skipAction;
        private InputAction _autoToggleAction;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _logger = new UnityLogger("ComicPlayer");
            _sfxDispatcher = new ComicSfxDispatcher(new SoundManagerSfxBackend(_logger));
            BuildCanvas();
            BuildInput();
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("ComicPlayer_Canvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below ScreenFader (9999) and LoadingScreen (32767) so a room-transition fade still
            // covers a comic that happens to still be tearing down; above the gameplay HUD.
            _canvas.sortingOrder = 5000;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var containerGO = new GameObject("PagesContainer", typeof(RectTransform));
            _pagesContainer = (RectTransform)containerGO.transform;
            _pagesContainer.SetParent(canvasGO.transform, false);
            _pagesContainer.anchorMin = Vector2.zero;
            _pagesContainer.anchorMax = Vector2.one;
            _pagesContainer.offsetMin = Vector2.zero;
            _pagesContainer.offsetMax = Vector2.zero;

            var overlayGO = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var overlayRt = (RectTransform)overlayGO.transform;
            overlayRt.SetParent(canvasGO.transform, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.GetComponent<Image>();
            overlayImg.color = Color.black;
            overlayImg.raycastTarget = false;
            _fadeOverlay = overlayGO.GetComponent<CanvasGroup>();
            _fadeOverlay.alpha = 0f;
            overlayGO.SetActive(false);

            canvasGO.SetActive(false);
        }

        private void BuildInput()
        {
            _advanceAction = new InputAction("ComicAdvance", InputActionType.Button);
            _advanceAction.AddBinding("<Keyboard>/space");
            _advanceAction.AddBinding("<Mouse>/leftButton");
            _advanceAction.AddBinding("<Gamepad>/buttonSouth");
            _advanceAction.performed += OnAdvancePerformed;

            _skipAction = new InputAction("ComicSkip", InputActionType.Button);
            _skipAction.AddBinding("<Keyboard>/escape").WithInteraction($"hold(duration={SkipHoldSeconds})");
            _skipAction.AddBinding("<Gamepad>/buttonNorth").WithInteraction($"hold(duration={SkipHoldSeconds})");
            _skipAction.performed += OnSkipPerformed;

            _autoToggleAction = new InputAction("ComicAutoToggle", InputActionType.Button);
            _autoToggleAction.AddBinding("<Keyboard>/tab");
            _autoToggleAction.performed += OnAutoTogglePerformed;
        }

        private void OnAdvancePerformed(InputAction.CallbackContext ctx) => Advance();
        private void OnSkipPerformed(InputAction.CallbackContext ctx) => Skip();
        private void OnAutoTogglePerformed(InputAction.CallbackContext ctx) => AutoPlay = !AutoPlay;

        private void EnableInput()
        {
            _advanceAction.Enable();
            _skipAction.Enable();
            _autoToggleAction.Enable();
        }

        private void DisableInput()
        {
            _advanceAction.Disable();
            _skipAction.Disable();
            _autoToggleAction.Disable();
        }

        /// <summary>Plays a comic sequence start to finish. Ignored (calls onDone immediately)
        /// if a comic is already playing or the sequence has no pages.</summary>
        public void Play(ComicSequenceAsset sequence, Action onDone = null, bool pauseGame = true)
        {
            if (sequence == null || sequence.PageCount == 0)
            {
                _logger.LogWarning("[ComicPlayer] Play() called with a null/empty sequence.");
                onDone?.Invoke();
                return;
            }
            if (IsPlaying)
            {
                _logger.LogWarning("[ComicPlayer] Already playing a comic; ignoring overlapping Play().");
                onDone?.Invoke();
                return;
            }

            IsPlaying = true;
            AutoPlay = false;
            _autoWaitElapsed = 0f;
            _sequence = sequence;
            _onDone = onDone;
            _pauseGame = pauseGame;

            if (_pauseGame)
            {
                PauseStack.Instance.Push(PauseHandle);
                PlayerInputGate.Set(false);
            }

            _canvas.gameObject.SetActive(true);
            EnableInput();

            LoadPage(0, true);
        }

        private void LoadPage(int index, bool firstPage)
        {
            _sfxDispatcher.StopAll();

            _pageIndex = index;
            var page = _sequence.GetPage(index);
            if (page == null)
            {
                EndSequence();
                return;
            }

            // Cut: the first panel's own entrance tween IS the transition, so it plays live.
            // Enveloped transitions (fade/crossfade/push) settle the new page instantly and let
            // the envelope carry the reveal instead — animating both at once would fight itself.
            bool isCut = firstPage || _currentView == null || page.enterTransition == ComicTransitionKind.Cut;

            var newView = ComicPageBuilder.Build(page, _sequence.Style, _pagesContainer, InlineComicTextProvider.Instance, _logger);
            _currentBeat = 0;
            newView.ApplyBeat(0, isCut);
            DispatchBeatEventsFor(page, 0);
            _sfxDispatcher.Reconcile(page, 0);

            if (isCut)
            {
                _currentView?.Destroy();
                _currentView = newView;
            }
            else
            {
                if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
                _transitionRoutine = StartCoroutine(TransitionRoutine(_currentView, newView, page));
            }
        }

        private IEnumerator TransitionRoutine(ComicPageView oldView, ComicPageView newView, ComicPage newPageData)
        {
            float duration = Mathf.Max(0.01f, newPageData.transitionDuration);
            oldView.BeginExit();

            switch (newPageData.enterTransition)
            {
                case ComicTransitionKind.FadeToBlack:
                {
                    _fadeOverlay.gameObject.SetActive(true);
                    _fadeOverlay.transform.SetAsLastSibling();
                    yield return FadeCanvasGroup(_fadeOverlay, 0f, 1f, duration * 0.5f);
                    oldView.Destroy();
                    yield return FadeCanvasGroup(_fadeOverlay, 1f, 0f, duration * 0.5f);
                    _fadeOverlay.gameObject.SetActive(false);
                    break;
                }

                case ComicTransitionKind.CrossFade:
                {
                    var oldGroup = GetOrAddCanvasGroup(oldView.Root);
                    var newGroup = GetOrAddCanvasGroup(newView.Root);
                    newGroup.alpha = 0f;
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        float f = Mathf.Clamp01(t / duration);
                        newGroup.alpha = f;
                        oldGroup.alpha = 1f - f;
                        yield return null;
                    }
                    newGroup.alpha = 1f;
                    oldView.Destroy();
                    break;
                }

                case ComicTransitionKind.Push:
                {
                    var oldRt = oldView.Root;
                    var newRt = newView.Root;
                    Vector2 oldStart = oldRt.anchoredPosition;
                    Vector2 newStart = newRt.anchoredPosition + new Vector2(1920f, 0f);
                    newRt.anchoredPosition = newStart;
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        float f = Mathf.Clamp01(t / duration);
                        oldRt.anchoredPosition = oldStart + new Vector2(-1920f * f, 0f);
                        newRt.anchoredPosition = newStart + new Vector2(-1920f * f, 0f);
                        yield return null;
                    }
                    oldView.Destroy();
                    break;
                }

                default: // Cut
                {
                    oldView.Destroy();
                    break;
                }
            }

            _currentView = newView;
            _transitionRoutine = null;
        }

        private static CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            return cg != null ? cg : rt.gameObject.AddComponent<CanvasGroup>();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (duration <= 0f) { group.alpha = to; yield break; }
            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>First press while the current beat is still animating snaps it to its
        /// settled state; the next press advances. At the last beat of the last page, ends
        /// the sequence.</summary>
        public void Advance()
        {
            if (!IsPlaying || _currentView == null || _transitionRoutine != null) return;

            if (_currentView.IsAnimating)
            {
                _currentView.SnapCurrentBeat();
                return;
            }

            int next = _currentBeat + 1;
            if (next > _currentView.MaxBeatIndex)
            {
                if (_pageIndex + 1 < _sequence.PageCount) LoadPage(_pageIndex + 1, false);
                else EndSequence();
                return;
            }

            _currentBeat = next;
            _currentView.ApplyBeat(_currentBeat, true);
            var page = _sequence.GetPage(_pageIndex);
            DispatchBeatEventsFor(page, _currentBeat);
            _sfxDispatcher.Reconcile(page, _currentBeat);
        }

        public void Skip()
        {
            if (!IsPlaying) return;
            EndSequence();
        }

        /// <summary>Dispatches the point-in-time (non-SFX) side effects for a beat that just
        /// activated: music switch and screen shake. SFX (one-shot or beat-ranged, with optional
        /// delay) is handled separately by <see cref="_sfxDispatcher"/>'s Reconcile/Tick, since it
        /// isn't a fire-once-per-beat concern the way these are.</summary>
        private void DispatchBeatEventsFor(ComicPage page, int beat)
        {
            if (page == null) return;

            for (int i = 0; i < page.beatEvents.Count; i++)
            {
                var e = page.beatEvents[i];
                if (e.beatIndex != beat) continue;

                if (!string.IsNullOrEmpty(e.musicName))
                {
                    if (Enum.TryParse(e.musicName, out MusicType music)) SoundManager.PlayMusic(music);
                    else _logger.LogWarning($"[ComicPlayer] Unknown Music name '{e.musicName}'.");
                }
                if (e.shakeAmplitude > 0f && e.shakeDuration > 0f)
                {
                    _shakeAmplitude = e.shakeAmplitude;
                    _shakeDuration = e.shakeDuration;
                    _shakeTimeRemaining = e.shakeDuration;
                }
            }
        }

        private void EndSequence()
        {
            IsPlaying = false;
            AutoPlay = false;
            DisableInput();
            _sfxDispatcher.StopAll();

            if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }
            _currentView?.Destroy();
            _currentView = null;
            _canvas.gameObject.SetActive(false);
            _shakeTimeRemaining = 0f;
            _pagesContainer.anchoredPosition = Vector2.zero;

            if (_pauseGame)
            {
                PauseStack.Instance.Release(PauseHandle);
                PlayerInputGate.Set(true);
            }

            var callback = _onDone;
            _onDone = null;
            _sequence = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!IsPlaying) return;

            _currentView?.Tick(Time.unscaledDeltaTime);
            _sfxDispatcher.Tick(Time.unscaledDeltaTime);
            TickShake();
            if (AutoPlay && _transitionRoutine == null) TickAutoPlay();
        }

        private void TickShake()
        {
            if (_shakeTimeRemaining <= 0f)
            {
                if (_pagesContainer.anchoredPosition != Vector2.zero)
                    _pagesContainer.anchoredPosition = Vector2.zero;
                return;
            }

            _shakeTimeRemaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_shakeTimeRemaining / _shakeDuration);
            float amp = _shakeAmplitude * t;
            _pagesContainer.anchoredPosition = new Vector2(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp));
        }

        private void TickAutoPlay()
        {
            if (_currentView == null || _currentView.IsAnimating)
            {
                _autoWaitElapsed = 0f;
                return;
            }

            _autoWaitElapsed += Time.unscaledDeltaTime;
            if (_autoWaitElapsed >= GetAutoAdvanceDelay(_currentBeat))
            {
                _autoWaitElapsed = 0f;
                Advance();
            }
        }

        private float GetAutoAdvanceDelay(int beat)
        {
            var page = _sequence?.GetPage(_pageIndex);
            if (page != null)
            {
                for (int i = 0; i < page.beatEvents.Count; i++)
                    if (page.beatEvents[i].beatIndex == beat) return Mathf.Max(0.1f, page.beatEvents[i].autoAdvanceAfter);
            }
            return DefaultAutoAdvanceDelay;
        }

        private void OnDestroy()
        {
            if (_advanceAction != null) { _advanceAction.performed -= OnAdvancePerformed; _advanceAction.Dispose(); }
            if (_skipAction != null) { _skipAction.performed -= OnSkipPerformed; _skipAction.Dispose(); }
            if (_autoToggleAction != null) { _autoToggleAction.performed -= OnAutoTogglePerformed; _autoToggleAction.Dispose(); }
            if (_instance == this) _instance = null;
        }
    }
}
