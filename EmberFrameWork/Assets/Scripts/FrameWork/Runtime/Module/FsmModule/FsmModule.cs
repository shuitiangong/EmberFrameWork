// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace EmberFrameWork
{
    /// <summary>按持有者类型和名称管理 FSM，由 ModuleSystem 统一更新与关闭。</summary>
    [Preserve]
    internal sealed class FsmModule : Module, IFsmModule, IUpdateModule
    {
        private readonly Dictionary<(Type, string), FsmBase> _fsms = new Dictionary<(Type, string), FsmBase>();
        private readonly List<FsmBase> _orderedFsms = new List<FsmBase>();
        private readonly List<FsmBase> _updateSnapshot = new List<FsmBase>();
        private readonly HashSet<(Type, string)> _creating = new HashSet<(Type, string)>();
        private bool _updating;
        private bool _closed;

        [Preserve]
        public FsmModule() { }

        public override int Priority => 1;
        public int Count { get { PruneDestroyed(); return _fsms.Count; } }
        public override void OnInit() => ThrowIfClosed();

        public bool HasFsm<T>() where T : class => HasFsm<T>(string.Empty);
        public bool HasFsm<T>(string name) where T : class => GetFsm<T>(name) != null;
        public bool HasFsm(Type ownerType) => HasFsm(ownerType, string.Empty);
        public bool HasFsm(Type ownerType, string name) => GetFsm(ownerType, name) != null;
        public IFsm<T> GetFsm<T>() where T : class => GetFsm<T>(string.Empty);
        public IFsm<T> GetFsm<T>(string name) where T : class => (IFsm<T>)GetFsm(typeof(T), name);
        public FsmBase GetFsm(Type ownerType) => GetFsm(ownerType, string.Empty);
        public FsmBase GetFsm(Type ownerType, string name)
        {
            var key = Key(ownerType, name);
            PruneDestroyed();
            return _fsms.TryGetValue(key, out var fsm) ? fsm : null;
        }

        public FsmBase[] GetAllFsms()
        {
            PruneDestroyed();
            return _orderedFsms.ToArray();
        }

        public void GetAllFsms(List<FsmBase> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            PruneDestroyed();
            results.Clear();
            results.AddRange(_orderedFsms);
        }

        public IFsm<T> CreateFsm<T>(T owner, params FsmState<T>[] states) where T : class =>
            CreateFsm(string.Empty, owner, states);

        public IFsm<T> CreateFsm<T>(string name, T owner, params FsmState<T>[] states) where T : class
        {
            ThrowIfClosed();
            var key = Key(typeof(T), name);
            PruneDestroyed();
            if (_fsms.ContainsKey(key) || !_creating.Add(key))
                throw new InvalidOperationException($"FSM already exists or is initializing: {typeof(T).FullName}, '{name}'.");
            try
            {
                var fsm = Fsm<T>.Create(name, owner, states);
                _fsms.Add(key, fsm);
                _orderedFsms.Add(fsm);
                return fsm;
            }
            finally { _creating.Remove(key); }
        }

        public IFsm<T> CreateFsm<T>(T owner, List<FsmState<T>> states) where T : class =>
            CreateFsm(string.Empty, owner, states);

        public IFsm<T> CreateFsm<T>(string name, T owner, List<FsmState<T>> states) where T : class
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            return CreateFsm(name, owner, states.ToArray());
        }

        public bool DestroyFsm<T>() where T : class => DestroyFsm(typeof(T), string.Empty);
        public bool DestroyFsm<T>(string name) where T : class => DestroyFsm(typeof(T), name);
        public bool DestroyFsm(Type ownerType) => DestroyFsm(ownerType, string.Empty);
        public bool DestroyFsm(Type ownerType, string name)
        {
            var fsm = GetFsm(ownerType, name);
            return fsm != null && DestroyFsm(fsm);
        }

        public bool DestroyFsm<T>(IFsm<T> fsm) where T : class
        {
            if (fsm == null) throw new ArgumentNullException(nameof(fsm));
            return fsm is FsmBase implementation && DestroyFsm(implementation);
        }

        public bool DestroyFsm(FsmBase fsm)
        {
            if (fsm == null) throw new ArgumentNullException(nameof(fsm));
            if (_closed) return false;
            var key = Key(fsm.OwnerType, fsm.Name);
            // An old or externally created FSM must not destroy a replacement with the same key.
            if (!_fsms.TryGetValue(key, out var registered) || !ReferenceEquals(fsm, registered)) return false;
            if (fsm.IsDestroyed) { Remove(fsm); return false; }
            try { fsm.Shutdown(); }
            finally
            {
                // A lifecycle guard may reject shutdown before any cleanup occurs.
                if (fsm.IsDestroyed) Remove(fsm);
            }
            return true;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (_closed) return;
            if (_updating || _creating.Count != 0)
                throw new InvalidOperationException("Cannot recursively update FSMs or update during initialization.");
            PruneDestroyed();
            _updating = true;
            _updateSnapshot.AddRange(_orderedFsms);
            try
            {
                foreach (var fsm in _updateSnapshot)
                {
                    if (_closed) break;
                    if (!fsm.IsDestroyed) fsm.Update(elapseSeconds, realElapseSeconds);
                }
            }
            finally
            {
                _updateSnapshot.Clear();
                _updating = false;
                PruneDestroyed();
            }
        }

        public override void Shutdown()
        {
            if (_closed) return;
            if (_creating.Count != 0)
                throw new InvalidOperationException("Cannot shut down the FSM module while a state is initializing.");
            _closed = true;
            // Callbacks may query/prune the registry, so teardown also needs an independent snapshot.
            var snapshot = _orderedFsms.ToArray();
            _fsms.Clear();
            _orderedFsms.Clear();
            List<Exception> errors = null;
            foreach (var fsm in snapshot)
            {
                try { fsm.Shutdown(); }
                catch (Exception error)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(error);
                }
            }
            if (errors != null) throw new AggregateException("FSM module shutdown callbacks failed.", errors);
        }

        private void PruneDestroyed()
        {
            for (int i = _orderedFsms.Count - 1; i >= 0; i--)
                if (_orderedFsms[i].IsDestroyed) Remove(_orderedFsms[i]);
        }

        private void Remove(FsmBase fsm)
        {
            var key = Key(fsm.OwnerType, fsm.Name);
            if (_fsms.TryGetValue(key, out var current) && ReferenceEquals(fsm, current)) _fsms.Remove(key);
            _orderedFsms.Remove(fsm);
        }

        private static (Type, string) Key(Type type, string name)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return (type, name ?? string.Empty);
        }

        private void ThrowIfClosed()
        {
            if (_closed) throw new ObjectDisposedException(nameof(FsmModule));
        }
    }
}
