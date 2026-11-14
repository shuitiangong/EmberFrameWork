// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;

namespace EmberFrameWork
{
    public abstract class MemoryObject : IMemory
    {
        public virtual void Clear() { }
        public abstract void InitFromPool();
        public abstract void RecycleToPool();
    }

    public static partial class MemoryPool
    {
        public static T Alloc<T>() where T : MemoryObject, new()
        {
            lock (Sync)
            {
                T memory = Acquire<T>();
                _callbackDepth++;
                try { memory.InitFromPool(); return memory; }
                catch (Exception initError)
                {
                    try { Release(memory); }
                    catch (Exception clearError)
                    {
                        GetCollection(typeof(T)).Discard(memory);
                        throw new AggregateException(initError, clearError);
                    }
                    throw;
                }
                finally { _callbackDepth--; }
            }
        }

        public static void Dealloc(MemoryObject memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            lock (Sync)
            {
                if (TryGetLoan(memory, out var collection)) collection.Release(memory, true);
            }
        }
    }
}
