// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EmberFrameWork
{
    public static partial class MemoryPool
    {
        // 使用引用身份，避免对象自定义 Equals/GetHashCode 干扰所有权判断。
        private sealed class ReferenceComparer : IEqualityComparer<IMemory>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(IMemory x, IMemory y) => ReferenceEquals(x, y);
            public int GetHashCode(IMemory obj) => RuntimeHelpers.GetHashCode(obj);
        }

        // 仅在持有 Sync 锁时访问。
        private sealed class MemoryCollection
        {
            private readonly Type _type;
            private readonly Queue<IMemory> _unused = new Queue<IMemory>();
            private readonly HashSet<IMemory> _using = new HashSet<IMemory>(ReferenceComparer.Instance);
            private readonly HashSet<IMemory> _returning = new HashSet<IMemory>(ReferenceComparer.Instance);
            private int _acquired, _released, _added, _removed;

            public MemoryCollection(Type type) { _type = type; }

            private T Create<T>() where T : class, IMemory, new()
            {
                _callbackDepth++;
                try { var memory = new T(); _added++; return memory; }
                finally { _callbackDepth--; }
            }

            private IMemory Create()
            {
                _callbackDepth++;
                try { var memory = (IMemory)Activator.CreateInstance(_type); _added++; return memory; }
                finally { _callbackDepth--; }
            }

            public T Acquire<T>() where T : class, IMemory, new()
            {
                var memory = _unused.Count > 0 ? (T)_unused.Dequeue() : Create<T>();
                _using.Add(memory);
                _acquired++;
                return memory;
            }

            public IMemory Acquire()
            {
                var memory = _unused.Count > 0 ? _unused.Dequeue() : Create();
                _using.Add(memory);
                _acquired++;
                return memory;
            }

            public bool CanRelease(IMemory memory) => _using.Contains(memory) && !_returning.Contains(memory);

            public void Discard(IMemory memory)
            {
                if (_using.Remove(memory)) _removed++;
            }

            public void Release(IMemory memory, bool recycle)
            {
                _returning.Add(memory);
                _callbackDepth++;
                try
                {
                    if (recycle) ((MemoryObject)memory).RecycleToPool();
                    memory.Clear();
                    _using.Remove(memory);
                    _unused.Enqueue(memory);
                    _released++;
                }
                finally { _callbackDepth--; _returning.Remove(memory); }
                // 回调抛异常时对象仍处于借出状态，修复后可再次归还。
            }

            public void Add<T>(int count) where T : class, IMemory, new()
            {
                while (count-- > 0) _unused.Enqueue(Create<T>());
            }
            public void Add(int count) { while (count-- > 0) _unused.Enqueue(Create()); }
            public void Remove(int count)
            {
                count = Math.Min(count, _unused.Count);
                while (count-- > 0) { _unused.Dequeue(); _removed++; }
            }
            public MemoryPoolInfo GetInfo() => new MemoryPoolInfo(_type, _unused.Count, _using.Count, _acquired, _released, _added, _removed);
        }
    }
}
