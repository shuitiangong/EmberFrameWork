# EmberFrameWork 待办清单

本文件统一记录项目后续工作。新任务追加到对应分类，执行前核对范围与依赖，完成后更新状态和验证结果。勾选表示已完成；未勾选表示待执行，不代表已授权立即实施。

最后更新：2026-11-15。

## 迁移基线核对（2026-11-15）

### 范围与迁移原则

只迁入当前需要的功能及其真实依赖；已经选中的功能尽量保留 TEngine 原实现。新增依赖应能指向已选功能的实际调用、类型引用或资产引用。命名空间、程序集引用、资源路径与依赖版本适配应局部处理，并记录理由。

本次基线为 `E:/workspace/TEngine/UnityProject` 与仓库中的 `EmberFrameWork/`。两者 Unity 版本均为 `2022.3.62f1c1`。当前选中范围为基础模块管理、FSM／流程、更新驱动、日志与工具、事件、内存池、对象池、计时器、单例、资源初始化及加载、UI 示例启动链。音频、场景模块、多语言、调试窗口、Launcher 下载界面、HybridCLR 和 Demo 业务暂未选中。

### 真实依赖与原实现差异

| 功能 | 已核对的依赖与差异 | 当前处理 |
| --- | --- | --- |
| FSM、流程、对象池、计时器、更新驱动 | 对照来源代码，忽略版权注释、空白及 TEngine 命名空间替换后实现一致；FSM 核心移动至 Core/Fsm | 保留现有实现 |
| 数据结构、事件、内存池、GameTime | 同上；UI 事件管理使用 GameEventMgr 和 MemoryPool | 保留实际依赖 |
| ModuleSystem | 保留原模块查找与更新结构；省略未迁入的 Utility.Marshal.FreeCachedHGlobal 调用 | 当前未引入对应 Marshal 缓存；保持该裁剪 |
| SingletonSystem、Singleton | 来源为 GameLogic；调整命名空间及 Singleton 构造检查中的名称字符串 | 保留原生命周期机制 |
| UIBase、UIWindow、UIWidget、WindowAttribute、IUIResourceLoader | 主要变更为 GameLogic → EmberFrameWork 和版权说明 | 保留原实现 |
| UIModule | 移除 Debugger／ErrorLogger 接入；隐藏关闭依赖 Timer，事件绑定依赖 GameEvent，实例加载依赖 IResourceModule | 保留这些真实依赖，暂不迁入调试窗口 |
| ResourceModule | 源项目 YooAsset 2.3.8，目标 manifest 为 3.0.7；现有 Configure／InitPackage、后端接口、状态及关闭逻辑为本工程适配 | 保留版本适配，后续按所选接口核对原资源加载及引用计数语义 |
| 资源对象池 | InitializeAssetPool 获取 IObjectPoolModule；资源加载缓存和 AssetsReference 参与资源归还 | 属于已选资源加载功能的依赖 |
| RootModule | 保留模块轮询与基础运行设置；省略语言、JSON helper、DPI converter 和资源低内存回调 | 当前裁剪点单独登记，后续需求涉及时再补齐 |
| Settings／GameModule | Settings 仅暴露流程配置；GameModule 仅暴露 Timer 和 UI | 按已选功能保留入口 |
| GameEntry／启动流程 | 保留原入口结构；显式配置资源，省略调试模块；资源就绪后激活 UI 示例，注册单例释放回调 | 使用本地启动链，暂不接入热更新 GameApp |

当前启动链为 `GameEntry → ProcedureLaunch → ProcedureInitResources → ProcedureStartGame → UIExampleWindow`。RootModule 驱动 ModuleSystem；UpdateDriver 提供 Unity 生命周期回调，SingletonSystem 借助该驱动更新 UI。两者职责均有实际调用。

`InitPackage` 已包含包初始化、版本获取及清单加载；下载器和下载进度界面尚未纳入当前范围。资源目录采用 `Assets/RawAssets`，源项目采用 `Assets/AssetRaw`。当前已有 YooAsset／UniTask 包及 YooAssetUniTask 适配插件，后续迁移沿用目标工程依赖，不覆盖源项目的内嵌包。

### 验证记录

- [x] **AUDIT-01：检查当前迁移边界及原实现差异。** 已核对上述代码和依赖。当前工作区在检查开始时干净。
- [x] **AUDIT-02：确认 Editor 连接与编译状态。** MCP 返回目标路径为 `D:/workspace/EmberFrameWork/EmberFrameWork/Assets`；检查时未播放、未编译，`scriptCompilationFailed=false`，Launch 场景无未保存修改。
- [ ] **AUDIT-03：运行现有 EditMode 回归。** 本次 MCP `tests-run` 在 300 秒后超时，未取得测试结果；后续状态查询也未返回。结果记为未验证，不认定为测试通过或测试用例失败。连接恢复后先确认现存测试运行状态，再决定是否重跑。
- [ ] **AUDIT-04：验证 Launch 资源初始化与 UI 示例。** 因上述工具无响应尚未执行播放验证；保留待办。

### 历史记录适用范围

以下启动框架条目为早期阶段记录。当前代码已恢复多处 TEngine 原实现，并迁入内存池、工具、资源及 UI；早期条目中的异常保护、普通 FSM 实例、同步流程启动及 GameEntry 轮询描述不代表当前行为。CORE-01、CORE-02 的勾选状态保留为历史记录，不能据此重复迁入已有代码。框架主程序集仍为 Runtime／Editor，当前另有资源适配和测试程序集；后续不自行增加模块级程序集。

项目约定：框架仅使用与 TEngine 对应的 Runtime、Editor 两个程序集，后续迁移不自行新增模块级或测试专用程序集。FSM 核心位于 `Runtime/Core/Fsm`。

## 启动框架迁移

- [x] **LAYOUT-01：框架 Runtime / Editor 分层。** Core 位于 `FrameWork/Runtime/Core`，FSM 位于其 `Fsm` 子目录，对应测试位于 `FrameWork/Editor/Tests`。保留脚本 GUID，移除原 Core、Fsm 及两套测试专用程序集，统一使用 Runtime、Editor 两个程序集；已同步文档路径及 IDE 工程。验证：Unity 编译通过，Editor 程序集中 27 项 EditMode 测试全部通过。
- [x] **FSM-01：迁移独立 FSM 核心。** 从 TEngine 适配 `IFsm<T>`、`FsmBase`、`FsmState<T>`、`Fsm<T>`，放置于 `EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Core/Fsm`。支持状态初始化、进入、更新、退出、销毁及共享数据。核心采用纯 C# 实现并归入 Runtime 程序集，当前由调用方驱动。验证：Unity 编译通过，13 项 EditMode 测试全部通过。
- [ ] **CORE-01：迁移对象池并接回 FSM。** 检查 TEngine 的 `MemoryPool`、`IMemory` 及必要依赖；明确获取、归还、清理与重复归还规则，覆盖初始化失败回收和池复用测试。当前 FSM 使用普通实例，`Shutdown()` 释放引用，`Clear()` 为兼容清理入口。
- [ ] **CORE-02：整理通用框架工具。** 评估并迁移 `GameFrameworkException`、`Utility.Text.Format`、`TypeNamePair`。按模块实际需要接入，统一异常与类型名称标识；当前 FSM 使用标准异常和字符串格式化。
- [x] **STARTUP-02：迁移模块管理。** 已接入 `Module`、`IUpdateModule`、`ModuleSystem`，位于 `EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Core`。支持接口获取、显式注册、按优先级更新、逆序关闭、重复注册防护和异常清理；由调用方显式驱动。当前使用标准 C# 工具，CORE-01、CORE-02 保留待办。验证：Unity 编译通过，14 项 EditMode 测试全部通过。本步骤按用户要求不包含 `FsmModule`。
- [x] **FSM-02：接入 FSM 管理模块。** 已迁移 `IFsmModule`、`FsmModule`，位于 `FrameWork/Runtime/Module/FsmModule`，归入现有 Runtime 程序集。通过 `ModuleSystem` 创建、按持有者类型与名称管理 FSM，统一轮询和关闭，支持更新期间增删及异常清理。当前使用 `(Type, string)` 键和普通 FSM 实例，CORE-01、CORE-02 继续保留待办。验证：新增 12 项管理模块测试，连同既有测试共 39 项 EditMode 测试全部通过，Unity 编译通过。
- [x] **STARTUP-03：迁移流程框架及配置。** 已接入 `ProcedureBase`、`IProcedureModule`、`ProcedureModule`、`ProcedureSetting` 和自定义 Inspector。运行时代码位于 `Runtime/Module/ProcedureModule`，编辑器位于 `Editor/Inspector`，配置资产位于 `FrameWork/Settings/ProcedureSetting.asset`。支持流程初始化、入口启动、FSM 切换、重启、配置校验与序列化。配置启动为同步调用；资产已在 STARTUP-04 中绑定基础启动流程。验证：新增 13 项测试，现有共 52 项 EditMode 测试全部通过，Unity 编译通过。
- [x] **STARTUP-04：迁移启动入口。** 已接入 `Scripts/GameEntry.cs` 和 `Scripts/Procedure` 下的 `ProcedureLaunch → ProcedureStartGame`。`Assets/Scenes/Launch.unity` 为构建首场景，绑定 `FrameWork/Settings/Prefab/GameEntry.prefab` 与流程配置。入口负责模块更新、跨场景持久化和退出清理，重复入口仅移除重复组件。验证：默认 Play Mode 启动、重复入口、禁用后重新启用、关闭 Domain Reload／Scene Reload 后连续两次启动及退出清理均通过；编辑器设置已恢复，52 项 EditMode 回归测试全部通过。当前停留在基础启动完成阶段，业务游戏逻辑另行接入。

本轮范围为基础启动框架。YooAsset 更新下载、加载界面和 HybridCLR 热更新需要另行确定实施范围。

## 后续工作登记

后续其他模块的任务继续记录在本文件，采用稳定编号，并补充目标、依赖和验收条件。
