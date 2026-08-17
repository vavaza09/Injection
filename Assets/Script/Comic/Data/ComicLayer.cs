using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Game.Comic
{
    /// <summary>One sprite or text element inside a panel. Draw order is list order within
    /// the owning <see cref="ComicPanel"/> (back to front).</summary>
    [Serializable]
    public class ComicLayer
    {
        [Tooltip("Referenced by other layers' Auto Fit To field. Must be unique within the panel.")]
        public string id = "layer";

        public ComicLayerKind kind = ComicLayerKind.Sprite;

        [Tooltip("Beat at which this layer becomes visible. 0 = visible as soon as the panel is revealed.")]
        public int beatIndex;

        [Tooltip("Beat at which this layer plays its own Exit tween below and is removed, " +
            "independent of the panel it lives in. -1 = never (stays on screen for as long as the panel does).")]
        public int exitBeatIndex = -1;

        [Tooltip("Layout rect relative to the owning panel's un-rotated local frame (panel's bottom-left corner = 0,0). If the panel is rotated, this content rotates rigidly along with it.")]
        public Rect rect = new Rect(0, 0, 200, 100);
        public float rotation;

        // --- Sprite ---
        public Sprite sprite;
        public Color tint = Color.white;

        [Tooltip("Optional: additional frames played in sequence at Flipbook Fps. Empty = static sprite.")]
        public List<Sprite> flipbookFrames = new List<Sprite>();
        [Min(0.01f)] public float flipbookFps = 12f;
        public bool flipbookLoop = true;

        // --- Text ---
        [TextArea] public string text = "";
        [Tooltip("Optional localization key, resolved through IComicTextProvider. Falls back to Text when empty or unresolved.")]
        public string locKey = "";
        public TMP_FontAsset font;
        [Min(1f)] public float fontSize = 42f;
        public Color textColor = Color.white;
        public TextRevealMode reveal = TextRevealMode.Instant;
        [Tooltip("Typewriter reveal speed, in characters per second.")]
        [Min(1f)] public float charsPerSecond = 40f;

        // --- Layout coupling (e.g. a Sprite balloon fit to a Text layer) ---
        [Tooltip("Id of another layer in this panel whose bounds this layer should grow to enclose. Leave empty to use Rect as authored.")]
        public string autoFitTo = "";
        [Tooltip("Padding added around the target layer's bounds when Auto Fit To is set: (left, bottom, right, top).")]
        public Vector4 autoFitPadding = new Vector4(24, 16, 24, 16);

        // --- Animation ---
        public ComicTween entrance = ComicTween.DefaultEntrance();
        public ComicTween exit = ComicTween.DefaultExit();
        public ComicIdleLoop idle = new ComicIdleLoop();
    }
}
