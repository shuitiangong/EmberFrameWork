// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;

namespace EmberFrameWork
{
    /// <summary>由调用方驱动的有限状态机，保留 TEngine 的状态生命周期和数据接口。</summary>
    public sealed class Fsm<T> : FsmBase, IFsm<T> where T : class
    {
        private readonly Dictionary<Type, FsmState<T>> _states = new Dictionary<Type, FsmState<T>>();
        private readonly List<FsmState<T>> _orderedStates = new List<FsmState<T>>();
        private Dictionary<string, object> _dataMap;
        private T _owner;
        private FsmState<T> _currentState;
        private float _currentStateTime;
        private bool _isDestroyed;
        private bool _isInitializing;
        private bool _isLeaving;
        private bool _isUpdating;

        private Fsm() { }

        public T Owner => _owner;
        public override Type OwnerType => typeof(T);
        public override int FsmStateCount => _states.Count;
        public override bool IsRunning => _currentState != null;
        public override bool IsDestroyed => _isDestroyed;
        public FsmState<T> CurrentState => _currentState;
        public override string CurrentStateName => _currentState?.GetType().FullName;
        public override float CurrentStateTime => _currentStateTime;

        /// <summary>先验证、注册全部状态，再按传入顺序调用 OnInit。</summary>
        public static Fsm<T> Create(string name, T owner, params FsmState<T>[] states)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (states.Length == 0) throw new ArgumentException("At least one state is required.", nameof(states));

            var fsm = new Fsm<T> { Name = name, _owner = owner, _isInitializing = true };
            foreach (var state in states)
            {
                if (state == null) throw new ArgumentException("States must not contain null.", nameof(states));
                if (state.BoundFsm != null) throw new InvalidOperationException("State already belongs to a live FSM.");
                if (fsm._states.ContainsKey(state.GetType()))
                    throw new ArgumentException($"Duplicate state type: {state.GetType().FullName}.", nameof(states));
                fsm._states.Add(state.GetType(), state);
                fsm._orderedStates.Add(state);
            }

            foreach (var state in fsm._orderedStates) state.BoundFsm = fsm;
            int initializedCount = 0;
            try
            {
                foreach (var state in fsm._orderedStates)
                {
                    initializedCount++;
                    state.OnInit(fsm);
                }
                fsm._isInitializing = false;
                return fsm;
            }
            catch (Exception initializationError)
            {
                fsm._isDestroyed = true;
                var errors = new List<Exception> { initializationError };
                for (int i = initializedCount - 1; i >= 0; i--)
                {
                    try { fsm._orderedStates[i].OnDestroy(fsm); }
                    catch (Exception cleanupError) { errors.Add(cleanupError); }
                }
                fsm.ReleaseReferences();
                if (errors.Count > 1) throw new AggregateException("FSM initialization and cleanup failed.", errors);
                throw;
            }
        }

        public static Fsm<T> Create(string name, T owner, List<FsmState<T>> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            return Create(name, owner, states.ToArray());
        }

        public void Start<TState>() where TState : FsmState<T> => Start(typeof(TState));

        public void Start(Type stateType)
        {
            EnsureReady();
            if (IsRunning) throw new InvalidOperationException("FSM is already running.");
            Enter(GetRequiredState(stateType));
        }

        public bool HasState<TState>() where TState : FsmState<T> => HasState(typeof(TState));
        public bool HasState(Type stateType)
        {
            ValidateStateType(stateType);
            return _states.ContainsKey(stateType);
        }

        public TState GetState<TState>() where TState : FsmState<T> => (TState)GetState(typeof(TState));
        public FsmState<T> GetState(Type stateType)
        {
            ValidateStateType(stateType);
            return _states.TryGetValue(stateType, out var state) ? state : null;
        }

        public FsmState<T>[] GetAllStates() => _orderedStates.ToArray();
        public void GetAllStates(List<FsmState<T>> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            results.AddRange(_orderedStates);
        }

        public bool HasData(string name)
        {
            ValidateDataName(name);
            return _dataMap != null && _dataMap.ContainsKey(name);
        }

        public TData GetData<TData>(string name) => (TData)GetData(name);
        public object GetData(string name)
        {
            ValidateDataName(name);
            return _dataMap != null && _dataMap.TryGetValue(name, out var data) ? data : null;
        }

        public void SetData<TData>(string name, TData data) => SetData(name, (object)data);
        public void SetData(string name, object data)
        {
            ThrowIfDestroyed();
            ValidateDataName(name);
            if (_dataMap == null) _dataMap = new Dictionary<string, object>(StringComparer.Ordinal);
            _dataMap[name] = data;
        }

        public bool RemoveData(string name)
        {
            ThrowIfDestroyed();
            ValidateDataName(name);
            return _dataMap != null && _dataMap.Remove(name);
        }

        /// <summary>状态时间累加逻辑时间，真实时间原样传给状态。</summary>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (_isDestroyed) return;
            EnsureReady();
            if (_isUpdating) throw new InvalidOperationException("Recursive FSM updates are not supported.");
            if (!IsRunning) return;
            _currentStateTime += elapseSeconds;
            _isUpdating = true;
            try { _currentState.OnUpdate(this, elapseSeconds, realElapseSeconds); }
            catch (Exception error) { ShutdownAfterFailure(error); throw; }
            finally { _isUpdating = false; }
        }

        internal void ChangeState<TState>() where TState : FsmState<T> => ChangeState(typeof(TState));
        internal void ChangeState(Type stateType)
        {
            EnsureReady();
            if (!IsRunning) throw new InvalidOperationException("FSM has not started.");
            var next = GetRequiredState(stateType);
            var previous = _currentState;
            // Clear first so failed leave callbacks cannot cause a second OnLeave during shutdown.
            _currentState = null;
            _currentStateTime = 0f;
            _isLeaving = true;
            try { previous.OnLeave(this, false); }
            catch (Exception error)
            {
                _isLeaving = false;
                ShutdownAfterFailure(error);
                throw;
            }
            finally { _isLeaving = false; }
            Enter(next);
        }

        private void Enter(FsmState<T> state)
        {
            _currentStateTime = 0f;
            _currentState = state;
            // OnEnter may immediately transition to the next state, as in TEngine procedures.
            try { state.OnEnter(this); }
            catch (Exception error) { ShutdownAfterFailure(error); throw; }
        }

        /// <summary>先退出当前状态，再销毁所有状态。回调异常不会阻止其余状态清理。</summary>
        public override void Shutdown()
        {
            if (_isDestroyed) return;
            EnsureReady();
            _isDestroyed = true;
            var errors = new List<Exception>();
            try
            {
                if (_currentState != null)
                {
                    try { _currentState.OnLeave(this, true); }
                    catch (Exception error) { errors.Add(error); }
                }
                foreach (var state in _orderedStates)
                {
                    try { state.OnDestroy(this); }
                    catch (Exception error) { errors.Add(error); }
                }
            }
            finally { ReleaseReferences(); }
            if (errors.Count != 0) throw new AggregateException("FSM shutdown callbacks failed.", errors);
        }

        /// <summary>兼容 TEngine 清理入口；当前实现不接入对象池。</summary>
        public void Clear() => Shutdown();

        private void ReleaseReferences()
        {
            foreach (var state in _orderedStates) state.BoundFsm = null;
            _states.Clear();
            _orderedStates.Clear();
            _dataMap?.Clear();
            _dataMap = null;
            _currentState = null;
            _currentStateTime = 0f;
            _owner = null;
            _isInitializing = false;
        }

        private void ShutdownAfterFailure(Exception original)
        {
            try { Shutdown(); }
            catch (Exception cleanup) { throw new AggregateException("FSM callback and cleanup failed.", original, cleanup); }
        }

        private FsmState<T> GetRequiredState(Type type) => GetState(type) ??
            throw new InvalidOperationException($"FSM '{FullName}' does not contain state '{type.FullName}'.");

        private void EnsureReady()
        {
            ThrowIfDestroyed();
            if (_isInitializing || _isLeaving)
                throw new InvalidOperationException("FSM lifecycle operations are not allowed during OnInit or OnLeave.");
        }

        private void ThrowIfDestroyed()
        {
            if (_isDestroyed) throw new ObjectDisposedException(FullName);
        }

        private static void ValidateStateType(Type stateType)
        {
            if (stateType == null) throw new ArgumentNullException(nameof(stateType));
            if (!typeof(FsmState<T>).IsAssignableFrom(stateType))
                throw new ArgumentException("Type must derive from FsmState<T>.", nameof(stateType));
        }

        private static void ValidateDataName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Data name must not be empty.", nameof(name));
        }
    }
}
