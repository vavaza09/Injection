using UnityEngine;

namespace Core.Logging
{
    // Release players drop Info-level logs globally. On WebGL every Debug.Log is forwarded to the
    // browser console, and the per-frame/per-dash movement diagnostics cost enough to cause stutter.
    internal static class ReleaseLogFilter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply()
        {
            if (Application.isEditor || Debug.isDebugBuild)
            {
                return;
            }

            Debug.unityLogger.filterLogType = LogType.Warning;
        }
    }
}
