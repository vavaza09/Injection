#if UNITY_EDITOR
using System;
using UnityEditor;

// Closes the Animator window before each domain reload to prevent the known
// UnityEditor.Graphs.Edge.WakeUp() NullReferenceException in affected Unity versions.
[InitializeOnLoad]
public static class AnimatorWindowGuard
{
    static AnimatorWindowGuard()
    {
        AssemblyReloadEvents.beforeAssemblyReload += CloseAnimatorWindow;
    }

    private static void CloseAnimatorWindow()
    {
        var type = Type.GetType("UnityEditor.Graphs.AnimatorControllerTool, UnityEditor.Graphs");
        if (type == null) return;
        var window = EditorWindow.GetWindow(type, false, null, false);
        window?.Close();
    }
}
#endif
