using System.IO;
using UnityEngine;

namespace Game.Persistence
{
    public static class SaveFileLocator
    {
        public const string FileName = "save.json";

        public static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists() => File.Exists(FullPath);

        public static void Delete()
        {
            if (File.Exists(FullPath))
                File.Delete(FullPath);
        }
    }
}
