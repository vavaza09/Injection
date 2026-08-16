using System;
using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// A from-state -> layout-state animation, evaluated by <see cref="ComicTweenRunner"/>.
    /// Reused for both entrance (from-state -> layout) and exit (layout -> from-state, played
    /// in reverse) so panels/layers only need one struct authored per direction.
    /// </summary>
    [Serializable]
    public class ComicTween
    {
        [Tooltip("Starting offset relative to the layout position, in reference-resolution pixels.")]
        public Vector2 fromOffset;

        [Tooltip("Starting scale multiplier (1 = layout scale).")]
        public float fromScale = 1f;

        [Tooltip("Starting rotation offset in degrees, relative to layout rotation.")]
        public float fromRotation;

        [Tooltip("Starting alpha (0-1).")]
        [Range(0f, 1f)] public float fromAlpha = 1f;

        [Tooltip("Seconds after the beat activates before this tween starts.")]
        [Min(0f)] public float delay;

        [Tooltip("Tween duration in seconds. 0 = snap instantly to the layout state.")]
        [Min(0f)] public float duration = 0.35f;

        [Tooltip("Maps elapsed 0-1 to blend 0-1. Empty/flat curve = linear.")]
        public AnimationCurve ease;

        public bool IsInstant => duration <= 0f;

        public float EvaluateEase(float t01)
        {
            if (ease == null || ease.length == 0) return t01;
            return ease.Evaluate(t01);
        }

        public static ComicTween DefaultEntrance() => new ComicTween
        {
            fromOffset = Vector2.zero,
            fromScale = 1f,
            fromRotation = 0f,
            fromAlpha = 0f,
            delay = 0f,
            duration = 0.35f
        };

        public static ComicTween DefaultExit() => new ComicTween
        {
            fromOffset = Vector2.zero,
            fromScale = 1f,
            fromRotation = 0f,
            fromAlpha = 0f,
            delay = 0f,
            duration = 0.25f
        };
    }
}
