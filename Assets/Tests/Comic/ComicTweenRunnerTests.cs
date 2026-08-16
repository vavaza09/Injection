using NUnit.Framework;
using UnityEngine;
using Game.Comic;

namespace Game.Tests.Comic
{
    public class ComicTweenRunnerTests
    {
        private static ComicTween MakeTween(Vector2 fromOffset, float fromScale, float fromRotation, float fromAlpha, float delay, float duration)
        {
            return new ComicTween
            {
                fromOffset = fromOffset,
                fromScale = fromScale,
                fromRotation = fromRotation,
                fromAlpha = fromAlpha,
                delay = delay,
                duration = duration,
                ease = null
            };
        }

        [Test]
        public void EvaluateEntrance_AtZero_ReturnsFromState()
        {
            var tween = MakeTween(new Vector2(-300, 0), 1.2f, -5f, 0f, 0f, 0.4f);
            var s = ComicTweenRunner.EvaluateEntrance(tween, 0f);
            Assert.AreEqual(tween.fromOffset, s.offset);
            Assert.AreEqual(tween.fromScale, s.scale);
            Assert.AreEqual(tween.fromRotation, s.rotation);
            Assert.AreEqual(tween.fromAlpha, s.alpha);
        }

        [Test]
        public void EvaluateEntrance_AtOrAfterDuration_ReturnsIdentity()
        {
            var tween = MakeTween(new Vector2(-300, 0), 1.2f, -5f, 0f, 0f, 0.4f);
            var s = ComicTweenRunner.EvaluateEntrance(tween, 0.4f);
            Assert.AreEqual(Vector2.zero, s.offset);
            Assert.AreEqual(1f, s.scale);
            Assert.AreEqual(0f, s.rotation);
            Assert.AreEqual(1f, s.alpha);

            var sLate = ComicTweenRunner.EvaluateEntrance(tween, 10f);
            Assert.AreEqual(1f, sLate.alpha);
        }

        [Test]
        public void EvaluateEntrance_RespectsDelay_HoldsFromStateUntilDelayElapsed()
        {
            var tween = MakeTween(new Vector2(100, 0), 1f, 0f, 0f, 0.5f, 0.5f);
            var s = ComicTweenRunner.EvaluateEntrance(tween, 0.2f);
            Assert.AreEqual(tween.fromOffset, s.offset);
            Assert.AreEqual(tween.fromAlpha, s.alpha);
        }

        [Test]
        public void EvaluateEntrance_Midpoint_LinearlyInterpolatesWithNullCurve()
        {
            var tween = MakeTween(new Vector2(100, 0), 1f, 0f, 0f, 0f, 1f);
            var s = ComicTweenRunner.EvaluateEntrance(tween, 0.5f);
            Assert.AreEqual(50f, s.offset.x, 0.001f);
            Assert.AreEqual(0.5f, s.alpha, 0.001f);
        }

        [Test]
        public void EvaluateEntrance_ZeroDuration_IsInstant_AlwaysIdentity()
        {
            var tween = MakeTween(new Vector2(500, 0), 2f, 20f, 0f, 0f, 0f);
            var s = ComicTweenRunner.EvaluateEntrance(tween, 0f);
            Assert.AreEqual(Vector2.zero, s.offset);
            Assert.AreEqual(1f, s.alpha);
        }

        [Test]
        public void EvaluateExit_AtZero_ReturnsIdentity()
        {
            var tween = MakeTween(Vector2.zero, 1f, 0f, 0f, 0f, 0.25f);
            var s = ComicTweenRunner.EvaluateExit(tween, 0f);
            Assert.AreEqual(1f, s.alpha);
        }

        [Test]
        public void EvaluateExit_AtOrAfterDuration_ReturnsFromStateAsDestination()
        {
            var tween = MakeTween(new Vector2(0, -50), 0.8f, 3f, 0f, 0f, 0.25f);
            var s = ComicTweenRunner.EvaluateExit(tween, 0.25f);
            Assert.AreEqual(tween.fromOffset, s.offset);
            Assert.AreEqual(tween.fromAlpha, s.alpha);
        }

        [Test]
        public void IsFinished_TrueOnlyAtOrAfterDuration()
        {
            var tween = MakeTween(Vector2.zero, 1f, 0f, 0f, 0.1f, 0.3f);
            Assert.IsFalse(ComicTweenRunner.IsFinished(tween, 0.2f));
            // 0.5f, not the exact 0.1f+0.3f boundary — float addition doesn't guarantee that
            // literal sum is bit-identical, so sit comfortably past it instead of razor's-edge.
            Assert.IsTrue(ComicTweenRunner.IsFinished(tween, 0.5f));
        }

        [Test]
        public void TotalDuration_IsDelayPlusDuration()
        {
            var tween = MakeTween(Vector2.zero, 1f, 0f, 0f, 0.2f, 0.3f);
            Assert.AreEqual(0.5f, ComicTweenRunner.TotalDuration(tween), 0.0001f);
        }

        [Test]
        public void NullTween_EvaluatesAsIdentity_AndIsFinished()
        {
            var s = ComicTweenRunner.EvaluateEntrance(null, 5f);
            Assert.AreEqual(1f, s.alpha);
            Assert.IsTrue(ComicTweenRunner.IsFinished(null, 0f));
            Assert.AreEqual(0f, ComicTweenRunner.TotalDuration(null));
        }
    }
}
