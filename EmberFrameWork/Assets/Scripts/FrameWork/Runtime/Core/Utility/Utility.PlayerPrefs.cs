// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
namespace EmberFrameWork
{
    public static partial class Utility
    {
        /// <summary>资源更新版本及启动配置的本地记录。应在 Unity 主线程调用。</summary>
        public static class PlayerPrefs
        {
            public static bool HasKey(string key) => UnityEngine.PlayerPrefs.HasKey(key);
            public static bool HasSetting(string settingName) => HasKey(settingName);
            public static string GetString(string key, string defaultValue = "") => UnityEngine.PlayerPrefs.GetString(key, defaultValue);
            public static float GetFloat(string key, float defaultValue = 0f) => UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
            public static bool GetBool(string key, bool defaultValue) => UnityEngine.PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;

            public static void SetString(string key, string value, bool save = true)
            {
                UnityEngine.PlayerPrefs.SetString(key, value);
                if (save) Save();
            }

            public static void SetFloat(string key, float value, bool save = true)
            {
                UnityEngine.PlayerPrefs.SetFloat(key, value);
                if (save) Save();
            }

            public static void SetBool(string key, bool value, bool save = true)
            {
                UnityEngine.PlayerPrefs.SetInt(key, value ? 1 : 0);
                if (save) Save();
            }

            public static bool Save()
            {
                UnityEngine.PlayerPrefs.Save();
                return true;
            }
        }
    }
}
