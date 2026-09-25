using System.Runtime.InteropServices;

// Thin wrapper over the browser's localStorage (Assets/Plugins/WebGL/BrowserStorage.jslib).
// Only functional in WebGL players; everywhere else IsAvailable is false and calls are no-ops.
public static class BrowserStorage
{
    // itch.io serves every game from the same origin, so keys need a game-unique prefix.
    private const string KEY_PREFIX = "BlueSteam/";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string BrowserStorage_Get(string key);
    [DllImport("__Internal")] private static extern int BrowserStorage_Set(string key, string value);
    [DllImport("__Internal")] private static extern void BrowserStorage_Delete(string key);

    public static bool IsAvailable => true;

    public static string Get(string key) => BrowserStorage_Get(KEY_PREFIX + key);

    public static bool Set(string key, string value) => BrowserStorage_Set(KEY_PREFIX + key, value) == 1;

    public static void Delete(string key) => BrowserStorage_Delete(KEY_PREFIX + key);
#else
    public static bool IsAvailable => false;

    public static string Get(string key) => null;

    public static bool Set(string key, string value) => false;

    public static void Delete(string key) { }
#endif
}
