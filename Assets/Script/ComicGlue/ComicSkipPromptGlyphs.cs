using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// Keyboard/gamepad button glyphs for the <see cref="ComicPlayer"/> skip hint. Loaded via
    /// <c>Resources.Load</c> rather than an Inspector reference because <c>ComicPlayer</c>
    /// self-instantiates at runtime (mirrors <c>ScreenFader</c>'s lazy singleton pattern, no
    /// scene placement, no DI) and so has nowhere to serialize a direct asset reference into.
    /// Shape mirrors <c>TutorialPromptUI.GlyphBinding</c>'s sprite-with-text-fallback contract.
    /// </summary>
    [CreateAssetMenu(fileName = "ComicSkipPromptGlyphs", menuName = "Injection/Comic/Skip Prompt Glyphs")]
    public class ComicSkipPromptGlyphs : ScriptableObject
    {
        [Header("Glyphs")]
        public Sprite keyboardSprite;
        public Sprite gamepadSprite;
        public string keyboardLabel = "ESC";
        public string gamepadLabel = "Y";

        [Header("Binding")]
        [Tooltip("Input System control path bound to skip on keyboard, e.g. \"<Keyboard>/escape\". Empty falls back to the built-in default.")]
        public string keyboardBindingPath = "<Keyboard>/escape";
        [Tooltip("Input System control path bound to skip on gamepad, e.g. \"<Gamepad>/buttonNorth\". Empty falls back to the built-in default.")]
        public string gamepadBindingPath = "<Gamepad>/buttonNorth";
        [Tooltip("Seconds the button must be held before skip fires. 0 or less falls back to the built-in default.")]
        public float holdDuration = 0.6f;

        [Header("Size")]
        [Tooltip("Pixel size (width and height) of the skip hint icon. 0 or less falls back to the built-in default.")]
        public float iconSize = 32f;
        [Tooltip("Font size of the skip hint text. 0 or less falls back to the built-in default.")]
        public float fontSize = 22f;
    }
}
