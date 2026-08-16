using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Comic
{
    /// <summary>Built representation of one panel — owned by <see cref="ComicPageView"/>,
    /// populated by <see cref="ComicPageBuilder"/>.</summary>
    internal sealed class ComicPanelRuntime
    {
        public ComicPanel data;
        public RectTransform root;   // unclipped — carries offset/scale/rotation/alpha, holds border + clip
        public RectTransform clip;   // Mask child, holds layers
        public CanvasGroup canvasGroup;
        public Image dimOverlay;
        public Vector2 basePos;
        public float baseRotation;
        public float entranceElapsed;
        public float exitElapsed;
        public readonly List<ComicLayerRuntime> layers = new List<ComicLayerRuntime>();
    }

    /// <summary>Built representation of one layer (Sprite or Text) inside a panel.</summary>
    internal sealed class ComicLayerRuntime
    {
        public ComicLayer data;
        public RectTransform rt;
        public Image image;         // Sprite kind
        public TextMeshProUGUI tmp; // Text kind
        public Color baseColor;
        public Vector2 basePos;
        public float baseRotation;
        public string resolvedText;
        public float entranceElapsed;
        public float exitElapsed;
        public float idleClock;
        public float flipbookClock;
        public float typewriterElapsed;
    }

    /// <summary>
    /// Built, live representation of one <see cref="ComicPage"/>. State at beat N is a pure
    /// function of N: a panel/layer is visible whenever <c>beatIndex &lt;= beat</c>, and its
    /// entrance only plays from t=0 when <c>beatIndex == beat</c> (the exact beat just applied)
    /// — anything with a lower beatIndex is by definition already settled, whether beat N was
    /// reached by walking through every beat in order or by jumping straight to it. This is what
    /// lets the Comic Editor's beat scrubber and normal live play share one code path with no
    /// divergence, and is exactly what <c>ApplyBeat(N)</c> is unit-tested against.
    /// </summary>
    public sealed class ComicPageView
    {
        private const float Settled = 1e6f;

        private readonly ComicPage _page;
        private readonly ComicStyle _style;
        private readonly List<ComicPanelRuntime> _panels = new List<ComicPanelRuntime>();

        public RectTransform Root { get; }
        public int CurrentBeat { get; private set; } = -1;
        public bool IsExiting { get; private set; }
        public int MaxBeatIndex => _page.MaxBeatIndex();

        internal ComicPageView(ComicPage page, ComicStyle style, RectTransform root)
        {
            _page = page;
            _style = style;
            Root = root;
        }

        internal void AddPanel(ComicPanelRuntime panel) => _panels.Add(panel);

        /// <summary>Reveals/hides/dims panels and layers for <paramref name="beat"/>. When
        /// <paramref name="animate"/> is false, everything visible snaps straight to its settled
        /// layout state (used for editor scrubbing and for a hard Skip).</summary>
        public void ApplyBeat(int beat, bool animate)
        {
            CurrentBeat = beat;
            IsExiting = false;

            int focusIndex = -1;
            int bestBeat = int.MinValue;
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (p.data.beatIndex <= beat && p.data.beatIndex >= bestBeat)
                {
                    bestBeat = p.data.beatIndex;
                    focusIndex = i;
                }
            }

            for (int i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                bool visible = panel.data.beatIndex <= beat;
                panel.root.gameObject.SetActive(visible);
                if (!visible) continue;

                bool fresh = panel.data.beatIndex == beat;
                panel.entranceElapsed = fresh && animate ? 0f : Settled;
                panel.exitElapsed = 0f;

                if (panel.dimOverlay != null)
                {
                    bool dimmed = i != focusIndex;
                    var c = _style.dimColor;
                    panel.dimOverlay.color = new Color(c.r, c.g, c.b, dimmed ? _style.dimAmount : 0f);
                }

                for (int j = 0; j < panel.layers.Count; j++)
                {
                    var layer = panel.layers[j];
                    bool layerVisible = layer.data.beatIndex <= beat;
                    layer.rt.gameObject.SetActive(layerVisible);
                    if (!layerVisible) continue;

                    bool layerFresh = layer.data.beatIndex == beat;
                    layer.entranceElapsed = layerFresh && animate ? 0f : Settled;
                    layer.exitElapsed = 0f;
                    layer.idleClock = 0f;

                    if (layer.tmp != null && layer.data.reveal == TextRevealMode.Typewriter)
                    {
                        bool typing = layerFresh && animate;
                        layer.typewriterElapsed = typing ? 0f : Settled;
                        layer.tmp.maxVisibleCharacters = typing ? 0 : layer.resolvedText.Length;
                    }
                }
            }
        }

        /// <summary>Advances in-flight entrance/idle/flipbook/typewriter animation. Never
        /// changes what is hidden/visible/focused — only <see cref="ApplyBeat"/> does that.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (IsExiting)
            {
                TickExit(unscaledDeltaTime);
                return;
            }

            for (int i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (!panel.root.gameObject.activeSelf) continue;

                float panelDuration = ComicTweenRunner.TotalDuration(panel.data.entrance);
                if (panel.entranceElapsed < panelDuration) panel.entranceElapsed += unscaledDeltaTime;

                var sample = ComicTweenRunner.EvaluateEntrance(panel.data.entrance, panel.entranceElapsed);
                ApplyPanelSample(panel, sample);

                for (int j = 0; j < panel.layers.Count; j++)
                {
                    var layer = panel.layers[j];
                    if (!layer.rt.gameObject.activeSelf) continue;

                    float layerDuration = ComicTweenRunner.TotalDuration(layer.data.entrance);
                    if (layer.entranceElapsed < layerDuration) layer.entranceElapsed += unscaledDeltaTime;

                    var layerSample = ComicTweenRunner.EvaluateEntrance(layer.data.entrance, layer.entranceElapsed);
                    if (ComicTweenRunner.IsFinished(layer.data.entrance, layer.entranceElapsed) && layer.data.idle.kind != IdleKind.None)
                    {
                        layer.idleClock += unscaledDeltaTime;
                        layerSample.offset += ComicTweenRunner.EvaluateIdleOffset(layer.data.idle, layer.idleClock);
                        layerSample.scale *= ComicTweenRunner.EvaluateIdleScale(layer.data.idle, layer.idleClock);
                    }
                    ApplyLayerSample(layer, layerSample);

                    if (layer.image != null && layer.data.flipbookFrames.Count > 0)
                        TickFlipbook(layer, unscaledDeltaTime);

                    if (layer.tmp != null && layer.data.reveal == TextRevealMode.Typewriter && layer.typewriterElapsed < Settled)
                    {
                        layer.typewriterElapsed += unscaledDeltaTime;
                        int visibleChars = Mathf.Clamp(Mathf.FloorToInt(layer.typewriterElapsed * Mathf.Max(1f, layer.data.charsPerSecond)), 0, layer.resolvedText.Length);
                        layer.tmp.maxVisibleCharacters = visibleChars;
                    }
                }
            }
        }

        private static void TickFlipbook(ComicLayerRuntime layer, float dt)
        {
            layer.flipbookClock += dt;
            int count = layer.data.flipbookFrames.Count;
            float fps = Mathf.Max(0.01f, layer.data.flipbookFps);
            int rawFrame = Mathf.FloorToInt(layer.flipbookClock * fps);
            int frame = layer.data.flipbookLoop ? ((rawFrame % count) + count) % count : Mathf.Min(rawFrame, count - 1);
            layer.image.sprite = layer.data.flipbookFrames[frame];
        }

        /// <summary>Forces every currently-animating panel/layer in the active beat straight to
        /// its settled state — the first Advance press while a beat is still animating.</summary>
        public void SnapCurrentBeat()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (!panel.root.gameObject.activeSelf) continue;
                panel.entranceElapsed = Settled;

                for (int j = 0; j < panel.layers.Count; j++)
                {
                    var layer = panel.layers[j];
                    if (!layer.rt.gameObject.activeSelf) continue;
                    layer.entranceElapsed = Settled;
                    if (layer.tmp != null && layer.data.reveal == TextRevealMode.Typewriter)
                    {
                        layer.typewriterElapsed = Settled;
                        layer.tmp.maxVisibleCharacters = layer.resolvedText.Length;
                    }
                }
            }
        }

        public bool IsAnimating
        {
            get
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    var panel = _panels[i];
                    if (!panel.root.gameObject.activeSelf) continue;
                    if (!ComicTweenRunner.IsFinished(panel.data.entrance, panel.entranceElapsed)) return true;

                    for (int j = 0; j < panel.layers.Count; j++)
                    {
                        var layer = panel.layers[j];
                        if (!layer.rt.gameObject.activeSelf) continue;
                        if (!ComicTweenRunner.IsFinished(layer.data.entrance, layer.entranceElapsed)) return true;
                        if (layer.tmp != null && layer.data.reveal == TextRevealMode.Typewriter &&
                            layer.tmp.maxVisibleCharacters < layer.resolvedText.Length) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Starts every currently-visible panel/layer's exit tween, used when the page
        /// itself is about to be replaced by the next one.</summary>
        public void BeginExit()
        {
            IsExiting = true;
            for (int i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (!panel.root.gameObject.activeSelf) continue;
                panel.exitElapsed = 0f;
                for (int j = 0; j < panel.layers.Count; j++)
                {
                    var layer = panel.layers[j];
                    if (layer.rt.gameObject.activeSelf) layer.exitElapsed = 0f;
                }
            }
        }

        private void TickExit(float dt)
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (!panel.root.gameObject.activeSelf) continue;

                float duration = ComicTweenRunner.TotalDuration(panel.data.exit);
                if (panel.exitElapsed < duration) panel.exitElapsed += dt;
                ApplyPanelSample(panel, ComicTweenRunner.EvaluateExit(panel.data.exit, panel.exitElapsed));

                for (int j = 0; j < panel.layers.Count; j++)
                {
                    var layer = panel.layers[j];
                    if (!layer.rt.gameObject.activeSelf) continue;
                    float layerDuration = ComicTweenRunner.TotalDuration(layer.data.exit);
                    if (layer.exitElapsed < layerDuration) layer.exitElapsed += dt;
                    ApplyLayerSample(layer, ComicTweenRunner.EvaluateExit(layer.data.exit, layer.exitElapsed));
                }
            }
        }

        public bool IsExitFinished
        {
            get
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    var panel = _panels[i];
                    if (!panel.root.gameObject.activeSelf) continue;
                    if (!ComicTweenRunner.IsFinished(panel.data.exit, panel.exitElapsed)) return false;
                    for (int j = 0; j < panel.layers.Count; j++)
                    {
                        var layer = panel.layers[j];
                        if (!layer.rt.gameObject.activeSelf) continue;
                        if (!ComicTweenRunner.IsFinished(layer.data.exit, layer.exitElapsed)) return false;
                    }
                }
                return true;
            }
        }

        private static void ApplyPanelSample(ComicPanelRuntime panel, ComicTweenRunner.Sample s)
        {
            panel.root.anchoredPosition = panel.basePos + s.offset;
            panel.root.localScale = Vector3.one * s.scale;
            panel.root.localEulerAngles = new Vector3(0f, 0f, panel.baseRotation + s.rotation);
            if (panel.canvasGroup != null) panel.canvasGroup.alpha = s.alpha;
        }

        private static void ApplyLayerSample(ComicLayerRuntime layer, ComicTweenRunner.Sample s)
        {
            layer.rt.anchoredPosition = layer.basePos + s.offset;
            layer.rt.localScale = Vector3.one * s.scale;
            layer.rt.localEulerAngles = new Vector3(0f, 0f, layer.baseRotation + s.rotation);
            float a = layer.baseColor.a * s.alpha;
            if (layer.image != null) layer.image.color = new Color(layer.baseColor.r, layer.baseColor.g, layer.baseColor.b, a);
            if (layer.tmp != null) layer.tmp.color = new Color(layer.baseColor.r, layer.baseColor.g, layer.baseColor.b, a);
        }

        public void Destroy()
        {
            if (Root == null) return;

            // Object.Destroy is a no-op-with-error outside Play Mode (e.g. the Comic Editor's
            // preview, which rebuilds this on every edit) — it must use DestroyImmediate there,
            // or the previous page's whole hierarchy silently leaks instead of being replaced.
            if (Application.isPlaying) Object.Destroy(Root.gameObject);
            else Object.DestroyImmediate(Root.gameObject);
        }
    }
}
