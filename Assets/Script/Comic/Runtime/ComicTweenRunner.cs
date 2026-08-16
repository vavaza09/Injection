using UnityEngine;

namespace Game.Comic
{
    /// <summary>
    /// Pure evaluation of <see cref="ComicTween"/> / <see cref="ComicIdleLoop"/> at a given
    /// elapsed time. No Unity lifecycle, no MonoBehaviour — callers (ComicPageView at runtime,
    /// the Comic Editor preview, EditMode tests) all drive the clock themselves, which is what
    /// makes "state at beat N" a pure function of N instead of something only Play Mode can
    /// reproduce.
    /// </summary>
    public static class ComicTweenRunner
    {
        public struct Sample
        {
            public Vector2 offset;
            public float scale;
            public float rotation;
            public float alpha;

            public static readonly Sample Identity = new Sample { offset = Vector2.zero, scale = 1f, rotation = 0f, alpha = 1f };
        }

        /// <summary>Entrance: at elapsed &lt; delay, the tween's from-state; at elapsed >=
        /// delay+duration, the settled layout state (identity).</summary>
        public static Sample EvaluateEntrance(ComicTween tween, float elapsed)
        {
            if (tween == null) return Sample.Identity;
            if (tween.IsInstant || elapsed >= tween.delay + tween.duration) return Sample.Identity;

            if (elapsed < tween.delay)
                return new Sample { offset = tween.fromOffset, scale = tween.fromScale, rotation = tween.fromRotation, alpha = tween.fromAlpha };

            float t01 = Mathf.Clamp01((elapsed - tween.delay) / tween.duration);
            float e = tween.EvaluateEase(t01);
            return new Sample
            {
                offset = Vector2.Lerp(tween.fromOffset, Vector2.zero, e),
                scale = Mathf.Lerp(tween.fromScale, 1f, e),
                rotation = Mathf.Lerp(tween.fromRotation, 0f, e),
                alpha = Mathf.Lerp(tween.fromAlpha, 1f, e),
            };
        }

        /// <summary>Exit: reuses the same struct in reverse — animates FROM the settled layout
        /// state TO the tween's from-state (interpreted here as the exit destination).</summary>
        public static Sample EvaluateExit(ComicTween tween, float elapsed)
        {
            if (tween == null) return Sample.Identity;

            var dest = new Sample { offset = tween.fromOffset, scale = tween.fromScale, rotation = tween.fromRotation, alpha = tween.fromAlpha };
            if (tween.IsInstant || elapsed >= tween.delay + tween.duration) return dest;
            if (elapsed < tween.delay) return Sample.Identity;

            float t01 = Mathf.Clamp01((elapsed - tween.delay) / tween.duration);
            float e = tween.EvaluateEase(t01);
            return new Sample
            {
                offset = Vector2.Lerp(Vector2.zero, tween.fromOffset, e),
                scale = Mathf.Lerp(1f, tween.fromScale, e),
                rotation = Mathf.Lerp(0f, tween.fromRotation, e),
                alpha = Mathf.Lerp(1f, tween.fromAlpha, e),
            };
        }

        public static bool IsFinished(ComicTween tween, float elapsed) =>
            tween == null || tween.IsInstant || elapsed >= tween.delay + tween.duration;

        public static float TotalDuration(ComicTween tween) =>
            tween == null ? 0f : tween.delay + tween.duration;

        public static Vector2 EvaluateIdleOffset(ComicIdleLoop idle, float time)
        {
            if (idle == null || idle.kind == IdleKind.None || idle.period <= 0f) return Vector2.zero;
            float phase = (time / idle.period) * Mathf.PI * 2f;

            switch (idle.kind)
            {
                case IdleKind.Drift:
                    return new Vector2(Mathf.Sin(phase) * idle.amplitude.x, Mathf.Cos(phase * 0.5f) * idle.amplitude.y);
                case IdleKind.Bob:
                    return new Vector2(0f, Mathf.Sin(phase) * idle.amplitude.y);
                case IdleKind.Pan:
                    return new Vector2((Mathf.PingPong(time / idle.period, 1f) * 2f - 1f) * idle.amplitude.x, 0f);
                default:
                    return Vector2.zero;
            }
        }

        public static float EvaluateIdleScale(ComicIdleLoop idle, float time)
        {
            if (idle == null || idle.period <= 0f) return 1f;

            if (idle.kind == IdleKind.Pulse)
            {
                float phase = (time / idle.period) * Mathf.PI * 2f;
                return 1f + (Mathf.Sin(phase) * 0.5f + 0.5f) * idle.zoomAmount;
            }
            if (idle.kind == IdleKind.Zoom)
            {
                float t = Mathf.PingPong(time / idle.period, 1f);
                return 1f + t * idle.zoomAmount;
            }
            return 1f;
        }
    }
}
