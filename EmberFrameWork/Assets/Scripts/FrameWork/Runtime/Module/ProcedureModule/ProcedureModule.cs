// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace EmberFrameWork
{
    [Preserve]
    internal sealed class ProcedureModule : Module, IProcedureModule
    {
        private IFsmModule _fsmModule;
        private IFsm<IProcedureModule> _procedureFsm;
        private bool _changing;
        private bool _closed;

        [Preserve] public ProcedureModule() { }
        public override int Priority => -2;
        public bool IsInitialized => !_closed && _procedureFsm != null && !_procedureFsm.IsDestroyed;
        public ProcedureBase CurrentProcedure => (ProcedureBase)RequireFsm().CurrentState;
        public float CurrentProcedureTime => RequireFsm().CurrentStateTime;
        public override void OnInit() => ThrowIfClosed();

        public void Initialize(IFsmModule fsmModule, params ProcedureBase[] procedures)
        {
            EnsureCanChange();
            if (fsmModule == null) throw new ArgumentNullException(nameof(fsmModule));
            if (IsInitialized) throw new InvalidOperationException("Procedures are already initialized. Use RestartProcedure.");
            ValidateProcedures(procedures, null);
            _changing = true;
            try
            {
                var fsm = fsmModule.CreateFsm<IProcedureModule>(this, procedures);
                _fsmModule = fsmModule;
                _procedureFsm = fsm;
            }
            finally { _changing = false; }
        }

        public void StartProcedure<T>() where T : ProcedureBase => StartProcedure(typeof(T));
        public void StartProcedure(Type procedureType)
        {
            EnsureCanChange();
            ValidateProcedureType(procedureType);
            RequireFsm().Start(procedureType);
        }

        public bool HasProcedure<T>() where T : ProcedureBase => HasProcedure(typeof(T));
        public bool HasProcedure(Type procedureType)
        {
            ValidateProcedureType(procedureType);
            return RequireFsm().HasState(procedureType);
        }

        public ProcedureBase GetProcedure<T>() where T : ProcedureBase => GetProcedure(typeof(T));
        public ProcedureBase GetProcedure(Type procedureType)
        {
            ValidateProcedureType(procedureType);
            return (ProcedureBase)RequireFsm().GetState(procedureType);
        }

        /// <summary>验证新流程列表后关闭旧流程，并以新列表第一项启动。</summary>
        public bool RestartProcedure(params ProcedureBase[] procedures)
        {
            EnsureCanChange();
            var previous = RequireFsm();
            ValidateProcedures(procedures, previous);
            _changing = true;
            try
            {
                if (!_fsmModule.DestroyFsm(previous)) return false;
                _procedureFsm = null;
                _procedureFsm = _fsmModule.CreateFsm<IProcedureModule>(this, procedures);
                _procedureFsm.Start(procedures[0].GetType());
                return true;
            }
            finally { _changing = false; }
        }

        public override void Shutdown()
        {
            if (_closed) return;
            EnsureCanChange();
            _closed = true;
            try
            {
                if (_procedureFsm != null && !_procedureFsm.IsDestroyed)
                    _fsmModule.DestroyFsm(_procedureFsm);
            }
            finally
            {
                _procedureFsm = null;
                _fsmModule = null;
            }
        }

        private IFsm<IProcedureModule> RequireFsm()
        {
            ThrowIfClosed();
            if (!IsInitialized) throw new InvalidOperationException("Initialize procedures before using them.");
            return _procedureFsm;
        }

        private void EnsureCanChange()
        {
            ThrowIfClosed();
            if (_changing) throw new InvalidOperationException("A procedure lifecycle operation is already in progress.");
        }

        private void ThrowIfClosed()
        {
            if (_closed) throw new ObjectDisposedException(nameof(ProcedureModule));
        }

        private static void ValidateProcedureType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!typeof(ProcedureBase).IsAssignableFrom(type))
                throw new ArgumentException("Type must derive from ProcedureBase.", nameof(type));
        }

        private static void ValidateProcedures(ProcedureBase[] procedures, IFsm<IProcedureModule> reusableFsm)
        {
            if (procedures == null) throw new ArgumentNullException(nameof(procedures));
            if (procedures.Length == 0) throw new ArgumentException("At least one procedure is required.", nameof(procedures));
            var types = new HashSet<Type>();
            foreach (var procedure in procedures)
            {
                if (procedure == null) throw new ArgumentException("Procedures must not contain null.", nameof(procedures));
                if (!types.Add(procedure.GetType())) throw new ArgumentException("Duplicate procedure type.", nameof(procedures));
                if (procedure.BoundFsm != null && !ReferenceEquals(procedure.BoundFsm, reusableFsm))
                    throw new InvalidOperationException("Procedure already belongs to another FSM.");
            }
        }
    }
}
