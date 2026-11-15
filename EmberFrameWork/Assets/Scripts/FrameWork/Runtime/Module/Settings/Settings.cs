// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using UnityEngine;

namespace EmberFrameWork
{
    public class Settings : MonoBehaviour
    {
        private static Settings _instance;

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Utility.Unity.FindObjectOfType<Settings>();

                    if (_instance != null)
                    {
                        return _instance;
                    }
                }

                return _instance;
            }
        }

        [SerializeField]
        private ProcedureSetting procedureSetting;

        public static ProcedureSetting ProcedureSetting => Instance.procedureSetting;
    }
}
