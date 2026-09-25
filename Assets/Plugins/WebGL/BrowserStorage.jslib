// localStorage bridge for WebGL builds (see Assets/Script/PersistenceGlue/BrowserStorage.cs).
// localStorage is keyed by origin only, so data survives a new itch.io upload (new URL path),
// unlike Application.persistentDataPath. Access can throw (privacy mode, blocked storage), so
// every call is guarded and reports failure instead of crashing the player.
mergeInto(LibraryManager.library, {
  BrowserStorage_Get: function (keyPtr) {
    var value = null;
    try {
      value = window.localStorage.getItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("BrowserStorage_Get failed: " + e);
    }
    if (value === null) {
      return 0;
    }
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  BrowserStorage_Set: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
      return 1;
    } catch (e) {
      console.warn("BrowserStorage_Set failed: " + e);
      return 0;
    }
  },

  BrowserStorage_Delete: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("BrowserStorage_Delete failed: " + e);
    }
  },
});
