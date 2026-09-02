using System;
using Game.Components.CameraForesight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Camera.EditModeTests
{
    public class CameraForesightSolverTests
    {
        private const float MIN_SIZE = 8f;
        private const float MAX_SIZE = 9.5f;

        [TearDown]
        public void TearDown()
        {
            TestCameraProfile.DestroyAll();
        }

        // --- Momentum zoom ---

        [Test]
        public void ComputeZoomOrthoSize_AtRest_ReturnsExactlyMinSize()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE, maxOrthographicSize: MAX_SIZE);

            // Exact, not approximate: MovementComponent.SpeedFactor reports exactly 0
            // at rest (and when its Rigidbody2D is missing), so any drift here would
            // pop the lens away from the vcam's authored baseline on the first frame.
            Assert.AreEqual(MIN_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, 0f));
        }

        [Test]
        public void ComputeZoomOrthoSize_AtFullMomentum_ReturnsExactlyMaxSize()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE, maxOrthographicSize: MAX_SIZE);

            Assert.AreEqual(MAX_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, 1f));
        }

        [Test]
        public void ComputeZoomOrthoSize_LinearCurve_IsMonotonicNonDecreasingAcrossSweep()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            AssertMonotonicSweep(profile);
        }

        [Test]
        public void ComputeZoomOrthoSize_EasedButMonotonicCurve_IsStillMonotonicNonDecreasing()
        {
            // The contract is "monotonic given a monotonic curve", so it has to hold for
            // a designer's eased curve too, not just the default straight line.
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));

            AssertMonotonicSweep(profile);
        }

        [Test]
        public void ComputeZoomOrthoSize_SweepNeverLeavesAuthoredRange()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));

            for (int step = 0; step <= 100; step++)
            {
                float speedFactor = step / 100f;
                float size = CameraForesightSolver.ComputeZoomOrthoSize(profile, speedFactor);

                Assert.GreaterOrEqual(size, MIN_SIZE, $"below min at speedFactor {speedFactor}");
                Assert.LessOrEqual(size, MAX_SIZE, $"above max at speedFactor {speedFactor}");
            }
        }

        [Test]
        public void ComputeZoomOrthoSize_SpeedFactorOutsideUnitRange_IsClamped()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE, maxOrthographicSize: MAX_SIZE);

            // Never extrapolate below the resting baseline or beyond full momentum.
            Assert.AreEqual(MIN_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, -5f));
            Assert.AreEqual(MAX_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, 5f));
        }

        [Test]
        public void ComputeZoomOrthoSize_CurveOvershootingOne_ClampsToMaxSize()
        {
            // A hand-authored curve with a hump above 1 must not be allowed to zoom the
            // camera past the authored maximum.
            AnimationCurve overshooting = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 2f),
                new Keyframe(1f, 1f));

            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: overshooting);

            Assert.AreEqual(MAX_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, 0.5f));
        }

        [Test]
        public void ComputeZoomOrthoSize_CurveUndershootingZero_ClampsToMinSize()
        {
            AnimationCurve undershooting = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -3f),
                new Keyframe(1f, 1f));

            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: undershooting);

            Assert.AreEqual(MIN_SIZE, CameraForesightSolver.ComputeZoomOrthoSize(profile, 0.5f));
        }

        [Test]
        public void ComputeZoomOrthoSize_MidMomentumWithLinearCurve_LerpsHalfway()
        {
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.AreEqual(8.75f, CameraForesightSolver.ComputeZoomOrthoSize(profile, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeZoomOrthoSize_KeylessCurve_DegradesToLinearInsteadOfCollapsing()
        {
            // A profile authored with the curve emptied must not silently pin the lens
            // to the minimum for the whole speed range.
            CameraProfile profile = TestCameraProfile.Create(
                minOrthographicSize: MIN_SIZE,
                maxOrthographicSize: MAX_SIZE,
                zoomCurve: new AnimationCurve());

            Assert.AreEqual(8.75f, CameraForesightSolver.ComputeZoomOrthoSize(profile, 0.5f), 0.0001f);
        }

        [Test]
        public void ComputeZoomOrthoSize_NullProfile_Throws()
        {
            // This layer is intentionally dependency-free, so it has no logger to warn
            // through: throwing is the only way it can report a missing profile instead
            // of returning a silently wrong lens size. Consumers null-check once.
            Assert.Throws<ArgumentNullException>(
                () => CameraForesightSolver.ComputeZoomOrthoSize(null, 0.5f));
        }

        // --- Look down ---

        [Test]
        public void ComputeLookDownOffset_NoLedgeDetected_ReturnsZero()
        {
            CameraProfile profile = TestCameraProfile.Create(ledgeMinDropHeight: 4f, lookDownOffset: 2.5f);

            // Deep drop, but nothing detected: the offset must stay off.
            Assert.AreEqual(0f, CameraForesightSolver.ComputeLookDownOffset(profile, false, 100f));
        }

        [Test]
        public void ComputeLookDownOffset_DropShallowerThanMinimum_ReturnsZero()
        {
            CameraProfile profile = TestCameraProfile.Create(ledgeMinDropHeight: 4f, lookDownOffset: 2.5f);

            Assert.AreEqual(0f, CameraForesightSolver.ComputeLookDownOffset(profile, true, 3.9f));
            Assert.AreEqual(0f, CameraForesightSolver.ComputeLookDownOffset(profile, true, 0f));
        }

        [Test]
        public void ComputeLookDownOffset_LedgeAndDeepEnoughDrop_ReturnsConfiguredOffset()
        {
            CameraProfile profile = TestCameraProfile.Create(ledgeMinDropHeight: 4f, lookDownOffset: 2.5f);

            Assert.AreEqual(2.5f, CameraForesightSolver.ComputeLookDownOffset(profile, true, 10f));
        }

        [Test]
        public void ComputeLookDownOffset_DropExactlyAtMinimum_Qualifies()
        {
            CameraProfile profile = TestCameraProfile.Create(ledgeMinDropHeight: 4f, lookDownOffset: 2.5f);

            // The threshold is inclusive: "minimum drop height that counts".
            Assert.AreEqual(2.5f, CameraForesightSolver.ComputeLookDownOffset(profile, true, 4f));
        }

        [Test]
        public void ComputeLookDownOffset_IsInstantaneousNotSmoothed()
        {
            CameraProfile profile = TestCameraProfile.Create(ledgeMinDropHeight: 4f, lookDownOffset: 2.5f);

            // Repeated calls must not ramp: the react/recover smoothing belongs to the
            // consumer, so this stays a pure decision with no hidden state.
            for (int call = 0; call < 5; call++)
            {
                Assert.AreEqual(2.5f, CameraForesightSolver.ComputeLookDownOffset(profile, true, 10f));
            }
        }

        [Test]
        public void ComputeLookDownOffset_NullProfile_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => CameraForesightSolver.ComputeLookDownOffset(null, true, 10f));
        }

        private static void AssertMonotonicSweep(CameraProfile profile)
        {
            float previous = float.NegativeInfinity;

            for (int step = 0; step <= 100; step++)
            {
                float speedFactor = step / 100f;
                float size = CameraForesightSolver.ComputeZoomOrthoSize(profile, speedFactor);

                // Tolerance absorbs single-ULP noise from Hermite curve evaluation while
                // still catching any dip large enough to be seen as a camera stutter.
                Assert.GreaterOrEqual(
                    size,
                    previous - 0.00001f,
                    $"zoom decreased at speedFactor {speedFactor}");

                previous = size;
            }
        }
    }
}
