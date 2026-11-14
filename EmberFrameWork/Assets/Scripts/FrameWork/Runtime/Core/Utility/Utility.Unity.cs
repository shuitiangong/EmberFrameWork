// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;
using System.Collections;
using UnityEngine;

namespace EmberFrameWork
{
    public static partial class Utility
    {
        /// <summary>资源更新流程的协程入口；宿主由 GameEntry 显式管理。</summary>
        public static class Unity
        {
            private static UtilityCoroutineRunner _runner;

            public static void Initialize(GameObject owner)
            {
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (!Application.isPlaying) throw new InvalidOperationException("Coroutine runner requires Play Mode.");
                if (_runner != null)
                {
                    if (_runner.gameObject != owner) throw new InvalidOperationException("Coroutine runner already has an owner.");
                    return;
                }
                _runner = owner.GetComponent<UtilityCoroutineRunner>();
                if (_runner == null) _runner = owner.AddComponent<UtilityCoroutineRunner>();
            }

            public static Coroutine StartCoroutine(IEnumerator routine)
            {
                if (routine == null) return null;
                if (_runner == null) throw new InvalidOperationException("Initialize the framework before starting update coroutines.");
                return _runner.StartCoroutine(routine);
            }

            public static void StopCoroutine(Coroutine routine)
            {
                if (_runner != null && routine != null) _runner.StopCoroutine(routine);
            }

            public static void StopAllCoroutines()
            {
                if (_runner != null) _runner.StopAllCoroutines();
            }

            /// <summary>停止资源流程协程；重复关闭不会创建新宿主。</summary>
            public static void Shutdown()
            {
                StopAllCoroutines();
                _runner = null;
            }
        }
    }
}
