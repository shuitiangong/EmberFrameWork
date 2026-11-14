// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberFrameWork
{
    /// <summary>配置可用流程及入口类型；流程实例由模块持有。</summary>
    [CreateAssetMenu(menuName = "EmberFrameWork/ProcedureSetting", fileName = "ProcedureSetting")]
    public sealed class ProcedureSetting : ScriptableObject
    {
        [SerializeField] private string[] availableProcedureTypeNames = Array.Empty<string>();
        [SerializeField] private string entranceProcedureTypeName = string.Empty;
        [NonSerialized] private IProcedureModule _procedureModule;

        public ProcedureBase CurrentProcedure => _procedureModule != null && _procedureModule.IsInitialized
            ? _procedureModule.CurrentProcedure : null;
        public float CurrentProcedureTime => _procedureModule != null && _procedureModule.IsInitialized
            ? _procedureModule.CurrentProcedureTime : 0f;

        /// <summary>程序化配置时先验证再写入；Inspector 使用相同的序列化字段。</summary>
        public void Configure(string[] availableTypes, string entranceType)
        {
            ResolveConfiguration(availableTypes, entranceType, out _);
            availableProcedureTypeNames = (string[])availableTypes.Clone();
            entranceProcedureTypeName = entranceType;
        }

        /// <summary>仅验证类型，不构造流程，不初始化模块。</summary>
        public bool TryValidate(out string error)
        {
            try
            {
                ResolveConfiguration(availableProcedureTypeNames, entranceProcedureTypeName, out _);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>同步完成配置验证、实例化、模块初始化和入口进入。</summary>
        public void StartProcedure()
        {
            var types = ResolveConfiguration(availableProcedureTypeNames, entranceProcedureTypeName, out var entrance);
            var module = ModuleSystem.GetModule<IProcedureModule>();
            if (module.IsInitialized) throw new InvalidOperationException("Procedures are already initialized. Use the module's restart API.");
            var procedures = new ProcedureBase[types.Length];
            for (int i = 0; i < types.Length; i++)
                procedures[i] = (ProcedureBase)Activator.CreateInstance(types[i]);
            module.Initialize(ModuleSystem.GetModule<IFsmModule>(), procedures);
            _procedureModule = module;
            module.StartProcedure(entrance);
        }

        public static bool IsProcedureType(Type type) => type != null && type.IsClass && !type.IsAbstract &&
            !type.ContainsGenericParameters && typeof(ProcedureBase).IsAssignableFrom(type) &&
            type.GetConstructor(Type.EmptyTypes) != null;

        /// <summary>支持程序集限定名，以及 TEngine 配置中的完整类型名。</summary>
        public static Type ResolveProcedureType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("流程类型名称不能为空。");
            Type result = Type.GetType(name, false);
            if (result == null && name.IndexOf(',') < 0)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var candidate = assembly.GetType(name, false);
                    if (candidate == null) continue;
                    if (result != null && result != candidate)
                        throw new InvalidOperationException($"流程类型名称不唯一，请使用程序集限定名：{name}");
                    result = candidate;
                }
            }
            if (!IsProcedureType(result))
                throw new InvalidOperationException($"流程类型不存在，或不是具有公开无参构造函数的具体流程：{name}");
            return result;
        }

        private static Type[] ResolveConfiguration(string[] names, string entranceName, out Type entrance)
        {
            if (names == null || names.Length == 0) throw new InvalidOperationException("请至少选择一个可用流程。");
            var types = new Type[names.Length];
            var unique = new HashSet<Type>();
            for (int i = 0; i < names.Length; i++)
            {
                types[i] = ResolveProcedureType(names[i]);
                if (!unique.Add(types[i])) throw new InvalidOperationException($"流程重复：{names[i]}");
            }
            entrance = ResolveProcedureType(entranceName);
            if (!unique.Contains(entrance)) throw new InvalidOperationException("入口流程必须包含在可用流程中。");
            return types;
        }
    }
}
