using System.Collections.Generic;
using UnityEngine;

namespace Core.Logging
{
    [CreateAssetMenu(fileName = "LogConfig", menuName = "Core/Log Config")]
    public class LogConfig : ScriptableObject
    {
        [Header("Global Settings")]
        [Tooltip("เปิด/ปิด logging ทั้งระบบ")]
        public bool globalEnabled = true;

        [Header("Per-Class Settings")]
        [Tooltip("เปิด/ปิด logging ของแต่ละ class")]
        public List<ClassLogSetting> classSettings = new List<ClassLogSetting>();

   
        public bool IsEnabled(string className)
        {
            if (!globalEnabled) return false;

            var setting = classSettings.Find(s => s.className == className);
            return setting?.isEnabled ?? true;
        }
    }

    [System.Serializable]
    public class ClassLogSetting
    {
        public string className;
        public bool isEnabled = true;
    }
}