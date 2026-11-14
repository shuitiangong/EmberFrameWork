// Adapted from TEngine's GameEntry (Copyright (c) 2022 ALEXTANG, MIT).
// See FrameWork/Runtime/Core/LICENSE.txt.
using System;
using EmberFrameWork;
using UnityEngine;

namespace EmberFrameWork.Game
{
    /// <summary>游戏唯一启动入口。负责模块更新、跨场景存活以及会话清理。</summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameEntry : MonoBehaviour
    {
        [SerializeField, Tooltip("启动流程配置，必须包含可用流程和入口流程。")]
        private ProcedureSetting procedureSetting;

        private static int _session;
        private static bool _quitting;
        private int _initializedSession;
        private bool _initializing;
        private bool _stopping;

        public static GameEntry Instance { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }
        public ProcedureBase CurrentProcedure => IsInitialized ? procedureSetting.CurrentProcedure : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // This also executes when Enter Play Mode has Domain Reload disabled.
            Utility.Unity.Shutdown();
            try { ModuleSystem.Shutdown(); }
            catch (Exception exception) { Log.Exception(exception); }
            Instance = null;
            _quitting = false;
            _session++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeExistingEntry()
        {
            // With Scene Reload disabled the existing component may not receive Awake again.
            foreach (var entry in FindObjectsOfType<GameEntry>())
                if (entry.isActiveAndEnabled) entry.Initialize();
        }

        private void Awake() => Initialize();
        private void OnEnable() => Initialize();

        private void Initialize()
        {
            if (!Application.isPlaying || _quitting || _initializing || _stopping || !isActiveAndEnabled) return;
            if (IsInitialized && _initializedSession == _session && Instance == this) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameEntry] 已有启动入口，移除重复的 GameEntry 组件。", this);
                Destroy(this);
                return;
            }

            IsInitialized = false;
            LastError = null;
            _initializing = true;
            try
            {
                if (transform.parent != null) throw new InvalidOperationException("GameEntry 必须挂在场景根对象上。");
                if (procedureSetting == null) throw new InvalidOperationException("GameEntry 未指定 ProcedureSetting。");
                if (!procedureSetting.TryValidate(out var error)) throw new InvalidOperationException(error);

                Instance = this;
                _initializedSession = _session;
                DontDestroyOnLoad(gameObject);
                Utility.Unity.Initialize(gameObject);
                ModuleSystem.GetModule<IFsmModule>();
                procedureSetting.StartProcedure();
                IsInitialized = Instance == this && isActiveAndEnabled;
            }
            catch (Exception exception)
            {
                Fail(exception);
                enabled = false;
            }
            finally { _initializing = false; }
        }

        private void Update()
        {
            if (!IsInitialized || Instance != this || _initializedSession != _session) return;
            try { ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime); }
            catch (Exception exception)
            {
                Fail(exception);
                enabled = false;
            }
        }

        private void Fail(Exception exception)
        {
            LastError = exception.ToString();
            StopFramework();
            Log.Exception(exception, this);
        }

        private void OnDisable() => StopFramework();
        private void OnDestroy() => StopFramework();
        private void OnApplicationQuit()
        {
            if (Instance != this) return;
            _quitting = true;
            StopFramework();
        }

        private void StopFramework()
        {
            IsInitialized = false;
            if (Instance != this || _initializedSession != _session || _stopping) return;
            _stopping = true;
            Utility.Unity.Shutdown();
            try { ModuleSystem.Shutdown(); }
            catch (Exception exception) { Log.Exception(exception, this); }
            finally
            {
                Instance = null;
                _initializedSession = 0;
                _stopping = false;
            }
        }
    }
}
