// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;

namespace EmberFrameWork
{
    /// <summary>单线程模块注册与生命周期管理。由启动入口显式调用 Update 和 Shutdown。</summary>
    public static class ModuleSystem
    {
        private sealed class Entry
        {
            public readonly Module Module;
            public readonly int Priority;
            public Entry(Module module) { Module = module; Priority = module.Priority; }
        }

        private static readonly Dictionary<Type, Entry> Interfaces = new Dictionary<Type, Entry>();
        private static readonly Dictionary<Type, Entry> Implementations = new Dictionary<Type, Entry>();
        private static readonly List<Entry> Modules = new List<Entry>();
        private static readonly List<Entry> UpdateSnapshot = new List<Entry>();
        private static readonly HashSet<Type> ConstructingTypes = new HashSet<Type>();
        private static readonly HashSet<Type> InitializingTypes = new HashSet<Type>();
        private static readonly HashSet<Type> InitializingInterfaces = new HashSet<Type>();
        private static bool _updating;
        private static bool _shuttingDown;
        private static int _shutdownVersion;

        /// <summary>已初始化的模块实例数量；同一实例的多个接口只计一次。</summary>
        public static int Count => Modules.Count;

        /// <summary>
        /// 获取模块。未注册时按同程序集、同命名空间的 IFoo → Foo 约定创建。
        /// 自定义实现或需要保留静态构造引用时使用 RegisterModule。
        /// </summary>
        public static T GetModule<T>() where T : class
        {
            Type contract = ValidateInterface<T>();
            if (Interfaces.TryGetValue(contract, out var existing)) return (T)(object)existing.Module;
            EnsureCanRegister();
            if (InitializingInterfaces.Contains(contract))
                throw new InvalidOperationException($"Circular module initialization: {contract.FullName}.");
            if (!contract.Name.StartsWith("I", StringComparison.Ordinal) || contract.Name.Length < 2)
                throw new InvalidOperationException($"No registered module for {contract.FullName}.");

            string implementationName = string.IsNullOrEmpty(contract.Namespace)
                ? contract.Name.Substring(1)
                : contract.Namespace + "." + contract.Name.Substring(1);
            Type implementation = contract.Assembly.GetType(implementationName, false);
            if (implementation == null || implementation.IsAbstract || implementation.ContainsGenericParameters ||
                !typeof(Module).IsAssignableFrom(implementation) || !contract.IsAssignableFrom(implementation))
                throw new InvalidOperationException($"Cannot resolve module {implementationName} for {contract.FullName}.");

            if (Implementations.TryGetValue(implementation, out existing))
            {
                Interfaces.Add(contract, existing);
                return (T)(object)existing.Module;
            }
            if (InitializingTypes.Contains(implementation) || !ConstructingTypes.Add(implementation))
                throw new InvalidOperationException($"Circular module construction: {implementation.FullName}.");
            try
            {
                return RegisterModule<T>((Module)Activator.CreateInstance(implementation, true));
            }
            finally { ConstructingTypes.Remove(implementation); }
        }

        /// <summary>按接口显式注册；重复注册同一实例直接返回，替换存活实例会报错。</summary>
        public static T RegisterModule<T>(Module module) where T : class
        {
            Type contract = ValidateInterface<T>();
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (!(module is T result)) throw new ArgumentException($"Module must implement {contract.FullName}.", nameof(module));
            EnsureCanRegister();
            Type implementation = module.GetType();
            if (InitializingInterfaces.Contains(contract) || InitializingTypes.Contains(implementation))
                throw new InvalidOperationException($"Circular module initialization: {contract.FullName}.");
            if (Interfaces.TryGetValue(contract, out var existing))
            {
                if (!ReferenceEquals(existing.Module, module))
                    throw new InvalidOperationException($"Module interface already registered: {contract.FullName}.");
                return result;
            }
            if (Implementations.TryGetValue(implementation, out existing))
            {
                if (!ReferenceEquals(existing.Module, module))
                    throw new InvalidOperationException($"Module implementation already registered: {implementation.FullName}.");
                Interfaces.Add(contract, existing);
                return result;
            }

            var entry = new Entry(module);
            InitializingInterfaces.Add(contract);
            InitializingTypes.Add(implementation);
            try
            {
                try { module.OnInit(); }
                catch (Exception initializationError)
                {
                    try { module.Shutdown(); }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException("Module initialization and cleanup failed.", initializationError, cleanupError);
                    }
                    throw;
                }
                // Publish only fully initialized modules. Equal priorities preserve registration order.
                int index = Modules.FindIndex(item => item.Priority < entry.Priority);
                if (index < 0) Modules.Add(entry);
                else Modules.Insert(index, entry);
                Implementations.Add(implementation, entry);
                Interfaces.Add(contract, entry);
                return result;
            }
            finally
            {
                InitializingInterfaces.Remove(contract);
                InitializingTypes.Remove(implementation);
            }
        }

        /// <summary>更新期间新注册的模块从下一次 Update 开始执行。</summary>
        public static void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (_shuttingDown || InitializingTypes.Count != 0 || ConstructingTypes.Count != 0)
                throw new InvalidOperationException("Cannot update modules during initialization or shutdown.");
            if (_updating) throw new InvalidOperationException("Recursive module updates are not supported.");
            _updating = true;
            int version = _shutdownVersion;
            UpdateSnapshot.AddRange(Modules);
            try
            {
                foreach (var entry in UpdateSnapshot)
                {
                    if (version != _shutdownVersion) break;
                    if (entry.Module is IUpdateModule updateModule)
                        updateModule.Update(elapseSeconds, realElapseSeconds);
                }
            }
            finally
            {
                UpdateSnapshot.Clear();
                _updating = false;
            }
        }

        /// <summary>按更新顺序的逆序关闭；尽量清理所有模块后汇总异常，可重复调用。</summary>
        public static void Shutdown()
        {
            if (_shuttingDown) return;
            if (InitializingTypes.Count != 0 || ConstructingTypes.Count != 0)
                throw new InvalidOperationException("Cannot shut down the module system during initialization.");
            _shuttingDown = true;
            _shutdownVersion++;
            List<Exception> errors = null;
            try
            {
                for (int i = Modules.Count - 1; i >= 0; i--)
                {
                    try { Modules[i].Module.Shutdown(); }
                    catch (Exception error)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                Modules.Clear();
                Interfaces.Clear();
                Implementations.Clear();
                _shuttingDown = false;
            }
            if (errors != null) throw new AggregateException("Module shutdown callbacks failed.", errors);
        }

        private static Type ValidateInterface<T>() where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface) throw new ArgumentException($"Modules must be requested by interface: {type.FullName}.");
            return type;
        }

        private static void EnsureCanRegister()
        {
            if (_shuttingDown) throw new InvalidOperationException("Cannot create or register modules during shutdown.");
        }
    }
}
