# EmberFrameWork 待办清单

本文件统一记录项目后续工作。新任务追加到对应分类，执行前核对范围与依赖，完成后更新状态和验证结果。勾选表示已完成；未勾选表示待执行，不代表已授权立即实施。

最后更新：2026-11-14。

## 启动框架迁移

- [x] **FSM-01：迁移独立 FSM 核心。** 从 TEngine 适配 `IFsm<T>`、`FsmBase`、`FsmState<T>`、`Fsm<T>`，放置于 `EmberFrameWork/Assets/Scripts/FrameWork/Fsm`。支持状态初始化、进入、更新、退出、销毁及共享数据。使用独立纯 C# 程序集，当前由调用方驱动。验证：Unity 编译通过，13 项 EditMode 测试全部通过。
- [ ] **CORE-01：迁移对象池并接回 FSM。** 检查 TEngine 的 `MemoryPool`、`IMemory` 及必要依赖；明确获取、归还、清理与重复归还规则，覆盖初始化失败回收和池复用测试。当前 FSM 使用普通实例，`Shutdown()` 释放引用，`Clear()` 为兼容清理入口。
- [ ] **CORE-02：整理通用框架工具。** 评估并迁移 `GameFrameworkException`、`Utility.Text.Format`、`TypeNamePair`。按模块实际需要接入，统一异常与类型名称标识；当前 FSM 使用标准异常和字符串格式化。
- [ ] **STARTUP-02：迁移模块管理。** 接入 `ModuleSystem`、模块生命周期及 `FsmModule`，统一驱动多个 FSM 的更新与释放。依赖：FSM-01；实施时复核 CORE-01、CORE-02 的接入顺序。
- [ ] **STARTUP-03：迁移流程框架及配置。** 接入 `ProcedureBase`、`ProcedureModule`、流程配置与必要编辑器支持。依赖：STARTUP-02。
- [ ] **STARTUP-04：迁移启动入口。** 接入 `GameEntry` 和基础启动流程，验证初始化顺序、重复入口防护、退出清理及多次进入 Play Mode。依赖：STARTUP-03。

本轮范围为基础启动框架。YooAsset 更新下载、加载界面和 HybridCLR 热更新需要另行确定实施范围。

## 后续工作登记

后续其他模块的任务继续记录在本文件，采用稳定编号，并补充目标、依赖和验收条件。
