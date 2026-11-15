// Extracted from TEngine Utility.Unity (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
namespace EmberFrameWork
{
    public static partial class Utility
    {
        public static partial class Unity
        {
            public static T FindObjectOfType<T>() where T : UnityEngine.Object
            {
#if UNITY_6000_0_OR_NEWER
                return UnityEngine.Object.FindFirstObjectByType<T>();
#else
                return UnityEngine.Object.FindObjectOfType<T>();

#endif
            }

        }
    }
}
