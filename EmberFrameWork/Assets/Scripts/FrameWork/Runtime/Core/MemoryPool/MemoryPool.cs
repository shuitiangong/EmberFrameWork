// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;
using System.Collections.Generic;

namespace EmberFrameWork
{
    /// <summary>
    /// 按实际类型复用托管对象。所有操作串行化；对象借出后由调用者管理线程安全。
    /// 构造及生命周期回调在锁内执行，不能等待其他线程的内存池操作。
    /// </summary>
    public static partial class MemoryPool
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<Type, MemoryCollection> Collections = new Dictionary<Type, MemoryCollection>();
        private static int _callbackDepth;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool _enableStrictCheck = true;
#else
        private static bool _enableStrictCheck;
#endif
        /// <summary>启用时，重复、外来或已被 ClearAll 作废的归还会抛异常；禁用时忽略这些归还。</summary>
        public static bool EnableStrictCheck
        {
            get { lock (Sync) return _enableStrictCheck; }
            set { lock (Sync) _enableStrictCheck = value; }
        }

        public static int Count { get { lock (Sync) return Collections.Count; } }

        internal static bool IsAcquired(IMemory memory)
        {
            lock (Sync) return memory != null && Collections.TryGetValue(memory.GetType(), out var collection) && collection.CanRelease(memory);
        }

        /// <summary>开始新运行会话时清除旧池并恢复默认检查策略。</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                ClearAll();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _enableStrictCheck = true;
#else
                _enableStrictCheck = false;
#endif
            }
        }

        public static T Acquire<T>() where T : class, IMemory, new()
        {
            lock (Sync) return GetCollection(typeof(T)).Acquire<T>();
        }

        public static IMemory Acquire(Type memoryType)
        {
            ValidateType(memoryType, true);
            lock (Sync) return GetCollection(memoryType).Acquire();
        }

        public static void Release(IMemory memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            lock (Sync)
            {
                if (TryGetLoan(memory, out var collection)) collection.Release(memory, false);
            }
        }

        public static void Add<T>(int count) where T : class, IMemory, new()
        {
            ValidateCount(count);
            lock (Sync) GetCollection(typeof(T)).Add<T>(count);
        }

        public static void Add(Type memoryType, int count)
        {
            ValidateType(memoryType, true);
            ValidateCount(count);
            lock (Sync) GetCollection(memoryType).Add(count);
        }

        public static void Remove<T>(int count) where T : class, IMemory => Remove(typeof(T), count);
        public static void Remove(Type memoryType, int count)
        {
            ValidateType(memoryType, false);
            ValidateCount(count);
            lock (Sync) { if (Collections.TryGetValue(memoryType, out var collection)) collection.Remove(count); }
        }

        public static void RemoveAll<T>() where T : class, IMemory => RemoveAll(typeof(T));
        public static void RemoveAll(Type memoryType) => Remove(memoryType, int.MaxValue);

        /// <summary>清除空闲缓存及统计，不调用借出对象的 Clear；旧借用不能再归还。应在模块关闭后调用。</summary>
        public static void ClearAll()
        {
            lock (Sync)
            {
                if (_callbackDepth != 0) throw new InvalidOperationException("Cannot clear pools inside a pool callback.");
                Collections.Clear();
            }
        }

        public static MemoryPoolInfo[] GetAllMemoryPoolInfos()
        {
            lock (Sync)
            {
                var result = new MemoryPoolInfo[Collections.Count];
                int index = 0;
                foreach (var collection in Collections.Values) result[index++] = collection.GetInfo();
                return result;
            }
        }

        private static MemoryCollection GetCollection(Type type)
        {
            if (!Collections.TryGetValue(type, out var collection))
            {
                collection = new MemoryCollection(type);
                Collections.Add(type, collection);
            }
            return collection;
        }

        private static bool TryGetLoan(IMemory memory, out MemoryCollection collection)
        {
            if (Collections.TryGetValue(memory.GetType(), out collection) && collection.CanRelease(memory)) return true;
            if (_enableStrictCheck) throw new GameFrameworkException("Object was not acquired from this pool, was already returned, or its pool was cleared.");
            return false;
        }

        private static void ValidateCount(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidateType(Type type, bool requireConstructor)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !typeof(IMemory).IsAssignableFrom(type))
                throw new ArgumentException("Expected a concrete IMemory class.", nameof(type));
            if (requireConstructor && type.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException("Expected a public parameterless constructor.", nameof(type));
        }
    }
}
