using Game.Components.CameraForesight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Camera.EditModeTests
{
    public class CameraConfinerClampTests
    {
        // A wide, shallow room: x in [-50, 50], y in [-20, 20].
        private static readonly Bounds Room = new Bounds(Vector3.zero, new Vector3(100f, 40f, 1f));

        private const float MARGIN = 0.5f;
        private const float HALF_WIDTH = 14.4f;
        private const float HALF_HEIGHT = 8f;

        // Safe span for the camera CENTRE, given the margin and half-extents above:
        // x in [-35.1, 35.1], y in [-11.5, 11.5].
        private const float SAFE_MAX_X = 35.1f;
        private const float SAFE_MAX_Y = 11.5f;

        // Aspect 2 keeps the expected values in the ortho tests readable; the shipping
        // value is 16/9, which the maths treats identically.
        private const float ASPECT = 2f;
        private const float MIN_ORTHO = 8f;
        private const float REQUESTED_ORTHO = 9.5f;

        // --- ClampBias ---

        [Test]
        public void ClampBias_WellInsideBounds_PassesThroughUnscaled()
        {
            Vector2 requested = new Vector2(5.5f, 0f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, Vector2.zero, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            Assert.AreEqual(requested.x, result.x, 0.0001f);
            Assert.AreEqual(requested.y, result.y, 0.0001f);
        }

        [Test]
        public void ClampBias_SmallRequestInOpenRoom_IsNeverAmplified()
        {
            Vector2 requested = new Vector2(1f, 0f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, Vector2.zero, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            // There are ~35 units of headroom; the clamp must not grow into them.
            Assert.AreEqual(1f, result.x, 0.0001f);
        }

        [Test]
        public void ClampBias_PartiallyExceedsBounds_ScalesDownProportionally()
        {
            Vector2 cameraCenter = new Vector2(32f, 0f);
            Vector2 requested = new Vector2(5.5f, 0f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            // 3.1 units of room left, so the request is trimmed to fill exactly that -
            // smaller than asked for, but still a real offset rather than a hard cut-off.
            float expected = SAFE_MAX_X - cameraCenter.x;

            Assert.Less(result.x, requested.x, "should be reduced");
            Assert.Greater(result.x, 0f, "partial reduction suffices, so it must stay non-zero");
            Assert.AreEqual(expected, result.x, 0.0001f);
        }

        [Test]
        public void ClampBias_ScalesUniformly_PreservingDirection()
        {
            // Only the vertical axis constrains this request, but scaling Y alone would
            // rotate the offset. A uniform scale keeps the 45 degree direction intact.
            Vector2 cameraCenter = new Vector2(0f, 9f);
            Vector2 requested = new Vector2(4f, 4f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            float expected = SAFE_MAX_Y - cameraCenter.y;

            Assert.AreEqual(expected, result.y, 0.0001f);
            Assert.AreEqual(result.y, result.x, 0.0001f, "direction must be preserved");
            Assert.Less(result.magnitude, requested.magnitude);
        }

        [Test]
        public void ClampBias_AtTheSafeEdgePushingOut_FullySuppresses()
        {
            Vector2 cameraCenter = new Vector2(SAFE_MAX_X, 0f);
            Vector2 requested = new Vector2(5.5f, 0f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            Assert.AreEqual(0f, result.x, 0.0001f);
        }

        [Test]
        public void ClampBias_CameraAlreadyOutsideSafeRegion_ReturnsZero()
        {
            Vector2 cameraCenter = new Vector2(40f, 0f);

            Vector2 pushingOut = CameraConfinerClamp.ClampBias(
                new Vector2(5.5f, 0f), cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);
            Vector2 pullingBack = CameraConfinerClamp.ClampBias(
                new Vector2(-5.5f, 0f), cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            // Recovering a camera that is already out of bounds is the confiner's job,
            // not the bias's - so the bias contributes nothing either way.
            Assert.AreEqual(Vector2.zero, pushingOut);
            Assert.AreEqual(Vector2.zero, pullingBack);
        }

        [Test]
        public void ClampBias_RoomSmallerThanFrustum_ReturnsZero()
        {
            Bounds tinyRoom = new Bounds(Vector3.zero, new Vector3(6f, 6f, 1f));

            Vector2 result = CameraConfinerClamp.ClampBias(
                new Vector2(5.5f, 0f), Vector2.zero, HALF_WIDTH, HALF_HEIGHT, tinyRoom, MARGIN);

            // No centre satisfies the safe region at all, so there is nothing to scale.
            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void ClampBias_ZeroRequest_StaysZero()
        {
            Vector2 result = CameraConfinerClamp.ClampBias(
                Vector2.zero, Vector2.zero, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            Assert.AreEqual(Vector2.zero, result);
        }

        [Test]
        public void ClampBias_LargerMarginLeavesLessRoom_TrimsHarder()
        {
            Vector2 cameraCenter = new Vector2(32f, 0f);
            Vector2 requested = new Vector2(5.5f, 0f);

            Vector2 tight = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);
            Vector2 tighter = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN + 1f);

            // Degradation tracks the margin smoothly instead of switching off at a step.
            Assert.Less(tighter.x, tight.x);
            Assert.Greater(tighter.x, 0f);
        }

        [Test]
        public void ClampBias_NegativeDirectionTowardLowerEdge_ScalesWithoutFlipping()
        {
            Vector2 cameraCenter = new Vector2(-32f, 0f);
            Vector2 requested = new Vector2(-5.5f, 0f);

            Vector2 result = CameraConfinerClamp.ClampBias(
                requested, cameraCenter, HALF_WIDTH, HALF_HEIGHT, Room, MARGIN);

            Assert.Less(result.x, 0f, "sign must survive the clamp");
            Assert.AreEqual(-3.1f, result.x, 0.0001f);
        }

        // --- ClampMaxOrthoSize ---

        [Test]
        public void ClampMaxOrthoSize_FitsInsideBounds_PassesThroughUnchanged()
        {
            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, Vector2.zero, ASPECT, Room, MARGIN);

            Assert.AreEqual(REQUESTED_ORTHO, result, 0.0001f);
        }

        [Test]
        public void ClampMaxOrthoSize_SmallRequestInOpenRoom_IsNeverAmplified()
        {
            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                MIN_ORTHO, MIN_ORTHO, Vector2.zero, ASPECT, Room, MARGIN);

            // ~19.5 of vertical headroom is available; the clamp must not zoom out into it.
            Assert.AreEqual(MIN_ORTHO, result, 0.0001f);
        }

        [Test]
        public void ClampMaxOrthoSize_NearTopEdge_ScalesDownSmoothly()
        {
            // Centre 11 up in a room whose top safe edge is 19.5: 8.5 of half-height left.
            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, new Vector2(0f, 11f), ASPECT, Room, MARGIN);

            Assert.AreEqual(8.5f, result, 0.0001f);
            Assert.Less(result, REQUESTED_ORTHO, "should be reduced");
            Assert.Greater(result, MIN_ORTHO, "partial reduction suffices, so it must not bottom out");
        }

        [Test]
        public void ClampMaxOrthoSize_CannotFitEvenAtMinimum_FallsBackToMinimum()
        {
            // Only 4.5 of half-height left, which is under the authored baseline.
            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, new Vector2(0f, 15f), ASPECT, Room, MARGIN);

            // Slight overscan beats collapsing the lens below the scene's baseline.
            Assert.AreEqual(MIN_ORTHO, result, 0.0001f);
        }

        [Test]
        public void ClampMaxOrthoSize_NarrowRoom_ConstrainedByAspectRatio()
        {
            // x in [-18, 18], y in [-40, 40]: 17.5 of half-width after the margin, which
            // at aspect 2 permits an 8.75 ortho size even though vertically 39.5 would fit.
            Bounds narrowRoom = new Bounds(Vector3.zero, new Vector3(36f, 80f, 1f));

            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, Vector2.zero, ASPECT, narrowRoom, MARGIN);

            Assert.AreEqual(8.75f, result, 0.0001f);
        }

        [Test]
        public void ClampMaxOrthoSize_DegenerateRoom_ReturnsMinimumRatherThanCollapsing()
        {
            Bounds emptyRoom = new Bounds(Vector3.zero, Vector3.zero);

            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, Vector2.zero, ASPECT, emptyRoom, MARGIN);

            Assert.AreEqual(MIN_ORTHO, result, 0.0001f);
        }

        [Test]
        public void ClampMaxOrthoSize_NonPositiveAspectRatio_DoesNotProduceNaNOrInfinity()
        {
            float result = CameraConfinerClamp.ClampMaxOrthoSize(
                REQUESTED_ORTHO, MIN_ORTHO, Vector2.zero, 0f, Room, MARGIN);

            Assert.IsFalse(float.IsNaN(result));
            Assert.IsFalse(float.IsInfinity(result));
            Assert.GreaterOrEqual(result, MIN_ORTHO);
            Assert.LessOrEqual(result, REQUESTED_ORTHO);
        }

        [Test]
        public void ClampMaxOrthoSize_ProgressivelyTighterPositions_DegradeMonotonically()
        {
            float previous = float.MaxValue;

            // Walking the camera toward the ceiling must shrink the permitted size
            // gradually and never jump, so the zoom pull-back reads as smooth.
            for (int step = 0; step <= 20; step++)
            {
                float centerY = step * 0.75f;
                float result = CameraConfinerClamp.ClampMaxOrthoSize(
                    REQUESTED_ORTHO, MIN_ORTHO, new Vector2(0f, centerY), ASPECT, Room, MARGIN);

                Assert.LessOrEqual(result, previous + 0.00001f, $"grew at centerY {centerY}");
                Assert.GreaterOrEqual(result, MIN_ORTHO);
                Assert.LessOrEqual(result, REQUESTED_ORTHO);

                previous = result;
            }
        }
    }
}
