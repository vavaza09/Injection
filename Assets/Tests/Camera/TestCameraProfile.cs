using System.Collections.Generic;
using System.Reflection;
using Game.Components.CameraForesight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Camera.EditModeTests
{
    /// <summary>
    /// Builds <see cref="CameraProfile"/> instances for tests.
    ///
    /// The profile is deliberately read-only at runtime (private [SerializeField]
    /// fields, get-only properties), so tests seed it by reflection rather than
    /// widening production API for the benefit of tests. The serialized field names
    /// are referenced from this one place only, so a rename fails loudly here
    /// instead of scattering across every fixture.
    ///
    /// Kept local to this assembly rather than added to Game.Tests.Fixtures: nothing
    /// outside the camera tests has any use for it.
    /// </summary>
    internal static class TestCameraProfile
    {
        private static readonly List<CameraProfile> Created = new List<CameraProfile>();

        internal static CameraProfile Create(
            float biasMaxDistance = 5.5f,
            float biasMinSpeedFactor = 0.5f,
            float biasDwellTime = 0.4f,
            float minOrthographicSize = 8f,
            float maxOrthographicSize = 9.5f,
            AnimationCurve zoomCurve = null,
            float ledgeProbeDistance = 3f,
            float ledgeMinDropHeight = 4f,
            float lookDownOffset = 2.5f,
            float lookDownReactTime = 0.4f,
            float lookDownRecoverTime = 0.6f,
            float confinerMargin = 0.5f)
        {
            CameraProfile profile = ScriptableObject.CreateInstance<CameraProfile>();

            SetField(profile, "_biasMaxDistance", biasMaxDistance);
            SetField(profile, "_biasMinSpeedFactor", biasMinSpeedFactor);
            SetField(profile, "_biasDwellTime", biasDwellTime);

            SetField(profile, "_minOrthographicSize", minOrthographicSize);
            SetField(profile, "_maxOrthographicSize", maxOrthographicSize);
            SetField(profile, "_zoomCurve", zoomCurve != null ? zoomCurve : AnimationCurve.Linear(0f, 0f, 1f, 1f));

            SetField(profile, "_ledgeProbeDistance", ledgeProbeDistance);
            SetField(profile, "_ledgeMinDropHeight", ledgeMinDropHeight);
            SetField(profile, "_lookDownOffset", lookDownOffset);
            SetField(profile, "_lookDownReactTime", lookDownReactTime);
            SetField(profile, "_lookDownRecoverTime", lookDownRecoverTime);

            SetField(profile, "_confinerMargin", confinerMargin);

            Created.Add(profile);
            return profile;
        }

        /// <summary>
        /// Destroys every profile built since the last call. Call from [TearDown] so
        /// CreateInstance'd assets do not accumulate across the run.
        /// </summary>
        internal static void DestroyAll()
        {
            for (int index = 0; index < Created.Count; index++)
            {
                CameraProfile profile = Created[index];
                if (profile != null)
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                }
            }

            Created.Clear();
        }

        private static void SetField(CameraProfile profile, string fieldName, object value)
        {
            FieldInfo field = typeof(CameraProfile).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(
                field,
                $"CameraProfile has no serialized field '{fieldName}'. If it was renamed, " +
                "update TestCameraProfile and add [FormerlySerializedAs] to the field.");

            field.SetValue(profile, value);
        }
    }
}
