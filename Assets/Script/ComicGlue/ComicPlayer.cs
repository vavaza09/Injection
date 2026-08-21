using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Core.Logging;
using Game.Pause;
using Game.UI;
using Game.Tutorial;

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
        private const float DefaultSkipHoldSeconds = 0.6f;
        private const string DefaultSkipKeyboardBindingPath = "<Keyboard>/escape";
        private const string DefaultSkipGamepadBindingPath = "<Gamepad>/buttonNorth";
        private const float DefaultSkipIconSize = 32f;
        private const float DefaultSkipFontSize = 22f;
        private const string SkipGlyphsResourcePath = "Comic/ComicSkipPromptGlyphs";
        private static readonly Color SkipHintIdleColor = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color SkipHintActiveColor = new Color(1f, 0.85f, 0.3f, 1f);

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
        private TextMeshProUGUI _skipHintText;
        private Image _skipHintIcon;
        private InputDeviceTracker _deviceTracker;
        private ComicSkipPromptGlyphs _skipGlyphs;

        private ComicSequenceAsset _sequence;
        private int _pageIndex;
        private int _currentBeat;
        private ComicPageView _currentView;
        private bool _pauseGame;
        private bool _sceneAudioSuspended;
        private bool _restoreSceneAudioOnFinish = true;
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

            // Loaded before BuildCanvas/BuildInput so both can read the designer-tunable
            // binding/size overrides from it; see ComicSkipPromptGlyphs for what's editable.
            _skipGlyphs = Resources.Load<ComicSkipPromptGlyphs>(SkipGlyphsResourcePath);
            if (_skipGlyphs == null)
                _logger.LogWarning($"[ComicPlayer] No ComicSkipPromptGlyphs found at Resources/{SkipGlyphsResourcePath} — skip hint will fall back to built-in defaults.");

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

            BuildSkipHint(canvasGO.transform);

            canvasGO.SetActive(false);
        }

        /// <summary>Icon+text skip hint, bottom-right of the comic canvas. The icon is resolved
        /// per the actual bound key/button and live-switches keyboard/gamepad the same way
        /// <see cref="InputDeviceTracker"/> already drives <c>TutorialPromptUI</c>'s glyphs —
        /// reused directly here rather than re-implemented, since it's generic device tracking
        /// with no tutorial-specific logic in it.</summary>
        private void BuildSkipHint(Transform canvasParent)
        {
            float iconSize = (_skipGlyphs != null && _skipGlyphs.iconSize > 0f) ? _skipGlyphs.iconSize : DefaultSkipIconSize;
            float fontSize = (_skipGlyphs != null && _skipGlyphs.fontSize > 0f) ? _skipGlyphs.fontSize : DefaultSkipFontSize;

            var rowGO = new GameObject("SkipHint", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var rowRt = (RectTransform)rowGO.transform;
            rowRt.SetParent(canvasParent, false);
            rowRt.anchorMin = new Vector2(1f, 0f);
            rowRt.anchorMax = new Vector2(1f, 0f);
            rowRt.pivot = new Vector2(1f, 0f);
            rowRt.anchoredPosition = new Vector2(-40f, 32f);

            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = 10f;
            // Must be true: the layout group always POSITIONS children using their preferred
            // size regardless of these flags, but only APPLIES that size to the child's actual
            // RectTransform when control is on. With it off, both children stayed at Unity's
            // default 100x100 RectTransform rect — the icon rendered oversized and the
            // right-aligned text sat far from it inside its own oversized box, even though the
            // layout math positioned them as if they were the intended sizes.
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Row auto-sizes to its children (icon + text), so changing iconSize/fontSize on the
            // glyphs asset never clips or leaves dead space — the bottom-right pivot above keeps
            // it anchored to the same corner as it grows/shrinks.
            var fitter = rowGO.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGO = new GameObject("SkipHintIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGO.transform.SetParent(rowRt, false);
            _skipHintIcon = iconGO.GetComponent<Image>();
            _skipHintIcon.preserveAspect = true;
            _skipHintIcon.raycastTarget = false;
            _skipHintIcon.color = SkipHintIdleColor;
            var iconLe = iconGO.GetComponent<LayoutElement>();
            iconLe.preferredWidth = iconSize;
            iconLe.preferredHeight = iconSize;

            var textGO = new GameObject("SkipHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(rowRt, false);
            _skipHintText = textGO.GetComponent<TextMeshProUGUI>();
            _skipHintText.fontSize = fontSize;
            _skipHintText.alignment = TextAlignmentOptions.Right;
            _skipHintText.color = SkipHintIdleColor;
            _skipHintText.raycastTarget = false;
            _skipHintText.enableAutoSizing = false;
            // No LayoutElement here on purpose — TMP reports its own preferred width/height from
            // the live text + fontSize, so the row (and its ContentSizeFitter) always fit the
            // current font size instead of clipping against a size hardcoded for the old one.

            _deviceTracker = gameObject.AddComponent<InputDeviceTracker>();
            _deviceTracker.DeviceChanged += OnSkipHintDeviceChanged;

            RenderSkipHint();
        }

        private void OnSkipHintDeviceChanged(InputDeviceKind kind) => RenderSkipHint();

        private void RenderSkipHint()
        {
            bool gamepad = _deviceTracker != null && _deviceTracker.Current == InputDeviceKind.Gamepad;
            Sprite sprite = _skipGlyphs != null ? (gamepad ? _skipGlyphs.gamepadSprite : _skipGlyphs.keyboardSprite) : null;

            if (sprite != null)
            {
                _skipHintIcon.sprite = sprite;
                _skipHintIcon.gameObject.SetActive(true);
                _skipHintText.text = "Hold to Skip";
            }
            else
            {
                string fallback = _skipGlyphs != null
                    ? (gamepad ? _skipGlyphs.gamepadLabel : _skipGlyphs.keyboardLabel)
                    : (gamepad ? "Y" : "ESC");
                _skipHintIcon.gameObject.SetActive(false);
                _skipHintText.text = $"Hold [{fallback}] to Skip";
            }
        }

        private void BuildInput()
        {
            _advanceAction = new InputAction("ComicAdvance", InputActionType.Button);
            _advanceAction.AddBinding("<Keyboard>/space");
            _advanceAction.AddBinding("<Mouse>/leftButton");
            _advanceAction.AddBinding("<Gamepad>/buttonSouth");
            _advanceAction.performed += OnAdvancePerformed;

            float holdSeconds = (_skipGlyphs != null && _skipGlyphs.holdDuration > 0f) ? _skipGlyphs.holdDuration : DefaultSkipHoldSeconds;
            string kbBindingPath = (_skipGlyphs != null && !string.IsNullOrEmpty(_skipGlyphs.keyboardBindingPath)) ? _skipGlyphs.keyboardBindingPath : DefaultSkipKeyboardBindingPath;
            string gpBindingPath = (_skipGlyphs != null && !string.IsNullOrEmpty(_skipGlyphs.gamepadBindingPath)) ? _skipGlyphs.gamepadBindingPath : DefaultSkipGamepadBindingPath;

            _skipAction = new InputAction("ComicSkip", InputActionType.Button);
            _skipAction.AddBinding(kbBindingPath).WithInteraction($"hold(duration={holdSeconds})");
            _skipAction.AddBinding(gpBindingPath).WithInteraction($"hold(duration={holdSeconds})");
            _skipAction.performed += OnSkipPerformed;
            _skipAction.started += OnSkipStarted;
            _skipAction.canceled += OnSkipCanceled;

            _autoToggleAction = new InputAction("ComicAutoToggle", InputActionType.Button);
            _autoToggleAction.AddBinding("<Keyboard>/tab");
            _autoToggleAction.performed += OnAutoTogglePerformed;
        }

        private void OnAdvancePerformed(InputAction.CallbackContext ctx) => Advance();
        private void OnSkipPerformed(InputAction.CallbackContext ctx) => Skip();
        private void OnAutoTogglePerformed(InputAction.CallbackContext ctx) => AutoPlay = !AutoPlay;

        // Hold-in-progress feedback on the skip hint label — confirms to the player that the
        // press is registering as a hold, not lost input, before the full 0.6s completes.
        private void OnSkipStarted(InputAction.CallbackContext ctx)
        {
            if (_skipHintText != null) _skipHintText.color = SkipHintActiveColor;
            if (_skipHintIcon != null) _skipHintIcon.color = SkipHintActiveColor;
        }

        private void OnSkipCanceled(InputAction.CallbackContext ctx)
        {
            if (_skipHintText != null) _skipHintText.color = SkipHintIdleColor;
            if (_skipHintIcon != null) _skipHintIcon.color = SkipHintIdleColor;
        }

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
            // Beats advance on their own by default (no click needed) — Tab still lets the
            // player switch to manual pacing, and a tap/click still fast-forwards past the
            // current auto-wait even while this is on.
            AutoPlay = true;
            _autoWaitElapsed = 0f;
            _sequence = sequence;
            _onDone = onDone;
            _pauseGame = pauseGame;

            // Hand the audio bed over to this comic: cut the room's own music/ambient (and any
            // pooled looping SFX) so only the comic's beat-event audio is heard. Without this a
            // comic that plays on room entry lands straight on top of the scene music
            // SoundManager.OnSceneLoaded started a frame earlier — RoomManager waits exactly one
            // frame between the scene load and RoomEntered, which is what ComicPlayOnEntry fires on.
            _restoreSceneAudioOnFinish = sequence.RestoreSceneAudioOnFinish;
            _sceneAudioSuspended = sequence.SilenceSceneAudio;
            if (_sceneAudioSuspended) SoundManager.SuspendSceneAudio(sequence.SceneAudioFadeOut);

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
            if (IsPausedByOtherSystem()) return;

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
            if (IsPausedByOtherSystem()) return;
            EndSequence();
        }

        /// <summary>True while some other system (e.g. the pause menu) has its own handle
        /// pushed on top of this comic's own "comic" pause handle. Escape drives both the pause
        /// menu (tap) and this comic's skip (hold) as separate InputActions with no shared
        /// consumption, so without this guard, holding Escape to dismiss a pause menu opened
        /// mid-comic would also silently skip the comic underneath it.</summary>
        private bool IsPausedByOtherSystem()
        {
            foreach (var handle in PauseStack.Instance.ActiveHandles)
                if (handle != PauseHandle) return true;
            return false;
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
            if (_skipHintText != null) _skipHintText.color = SkipHintIdleColor;
            if (_skipHintIcon != null) _skipHintIcon.color = SkipHintIdleColor;
            _shakeTimeRemaining = 0f;
            _pagesContainer.anchoredPosition = Vector2.zero;

            if (_pauseGame)
            {
                PauseStack.Instance.Release(PauseHandle);
                PlayerInputGate.Set(true);
            }

            RestoreSceneAudio();

            var callback = _onDone;
            _onDone = null;
            _sequence = null;
            callback?.Invoke();
        }

        /// <summary>Gives the audio bed back to the scene. Idempotent via <see cref="_sceneAudioSuspended"/>,
        /// so the OnDestroy safety net can't double-restore after a normal <see cref="EndSequence"/> —
        /// and can't leave the game permanently silent if this object is torn down mid-comic.</summary>
        private void RestoreSceneAudio()
        {
            if (!_sceneAudioSuspended) return;
            _sceneAudioSuspended = false;
            SoundManager.RestoreSceneAudio(_restoreSceneAudioOnFinish);
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
            if (_skipAction != null)
            {
                _skipAction.performed -= OnSkipPerformed;
                _skipAction.started -= OnSkipStarted;
                _skipAction.canceled -= OnSkipCanceled;
                _skipAction.Dispose();
            }
            if (_autoToggleAction != null) { _autoToggleAction.performed -= OnAutoTogglePerformed; _autoToggleAction.Dispose(); }
            if (_deviceTracker != null) _deviceTracker.DeviceChanged -= OnSkipHintDeviceChanged;
            RestoreSceneAudio();
            if (_instance == this) _instance = null;
        }
    }
}
