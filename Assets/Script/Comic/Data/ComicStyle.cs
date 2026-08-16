using System;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>Sequence-wide look applied uniformly by <see cref="ComicPageBuilder"/> — the
    /// panel border/gutter and the dim treatment for panels already read. One place to restyle
    /// an entire comic instead of touching every panel.</summary>
    [Serializable]
    public class ComicStyle
    {
        public Color borderColor = Color.black;
        [Min(0f)] public float borderWidth = 4f;
        [Min(0f)] public float gutter = 16f;

        [Tooltip("Tint drawn over panels that have already been revealed and left behind.")]
        public Color dimColor = Color.black;
        [Range(0f, 1f)] public float dimAmount = 0.55f;
    }
}
