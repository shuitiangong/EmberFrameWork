# EmberFrameWork 待办清单

本文件统一记录项目后续工作。新任务追加到对应分类，执行前核对范围与依赖，完成后更新状态和验证结果。勾选表示已完成；未勾选表示待执行，不代表已授权立即实施。

最后更新：2026-11-16。

## 音频与场景 main 同步（2026-11-16）

基线继续使用本地 TEngine main `3ffab9c6`，保留当前 Unity 2022 与 YooAsset 3.0.7 依赖。

- 音频：恢复 main 的分类音量与开关、同步／异步播放、预加载池、暂停恢复、代理复用及 Restart 接口；迁入实际依赖 AudioData、AudioAgentRuntimeState 和 MemoryPoolExtension。Settings 增加 AudioSetting 引用，GameEntry.prefab 绑定已有配置资产；配置显式保留各分类音量为 1。
- 场景：恢复主场景及叠加场景管理、进度回调、激活、挂起恢复、查询及卸载接口。YooAsset 3 适配集中在明确包引用、`allowSceneActivation = !suspendLoad`、`AllowSceneActivation` 和 `UnloadSceneAsync`。
- 生命周期适配：音频退出销毁代理和自有节点；销毁后的异步回调释放未托管句柄；预加载池拒绝关闭／清理后到达的旧结果并释放竞争句柄。音频重启清理旧分类节点，扩充声道后的空代理可正常创建。
- 场景错误处理：保留 main 的状态字段与操作结构；回调入口复用异步入口，避免重复卸载。失败时清理占用标记和失败句柄，成功后才更新有效场景记录，检查当前 UniTask 适配器不会自动抛出的 YooAsset 失败状态。关闭时恢复挂起的场景加载，资源系统最终销毁仍由资源模块负责。
- 验证：使用安装的 Unity 2022 引用程序集和当前项目缓存依赖，完成 Runtime 的 Editor／Player 条件编译；仅出现 Inspector 序列化字段未显式赋值警告。通过 Unity 自带 Mono 执行 3 项离线回归测试：加载失败后重试、关闭后拒绝加载、音频数据重复清理。
- Editor 验证（连接恢复后）：3 项 AudioSceneMigrationTests 全部通过；在实际 Launch 会话执行 AudioSceneRuntimeVerification 的音频及场景验证入口，覆盖音频暂停恢复、渐出、分类音量、异步加载、重复预加载、池化复用、Restart，以及 Additive 加载、场景激活、挂起恢复、异步卸载和回调加载／卸载。
- 运行验证发现并修复两项问题：暂停状态独立于播放结束状态，避免暂停声道被复用；音频池键使用调用方定位路径，适配本项目 AddressDisable 收集规则。池内重复句柄会释放，避免相同路径竞争造成句柄泄漏。
- 退出检查通过：YooAsset 已销毁，音频根节点残留为 0，Unity 无编译错误；临时验证场景已删除，Editor 恢复 Launch 场景并退出播放。验证通过不包含真机、离线包及 Unity 6 条件分支。

## 资源模块 main 同步（2026-11-15）

本轮按用户授权完成加载核心、初始化职责和资源回收三个步骤。基线为本地 TEngine main `3ffab9c6`，目标依赖保持 YooAsset `3.0.7`、UniTask `2.5.11`。下文早期迁移记录中的 Backend、Configure 和初始化状态枚举描述已由本节取代。

- 加载核心：同步 main 的 `AssetObject`；恢复泛型委托到 `Type` 重载、资源信息查询、回调加载及直接句柄入口。池化加载每次借用对应一次 `UnloadAsset`；直接句柄由调用者 `Release/Dispose`，不进入对象池。
- 初始化：新增 `ResourceModuleDriver`，由其读取现有 `ResourceSetting` 预设并应用模块属性，然后调用 `Initialize`。GameEntry 的 Start 显式确保 Driver 初始化后启动流程。流程使用 `InitPackage(packageName, true)` 等待包与清单就绪；失败重试仍由启动流程管理。移除 `IResourcePackageBackend`、`YooAssetPackageBackend`、`ResourceInitializationState` 及模块内共享 Task 初始化状态机。
- 回收：恢复 main Driver 的定时系统回收、强制回收请求和 GC 开关；RootModule 低内存回调及场景加载完成接到该入口。GameEntry 退出时依次释放单例、清空入口缓存、关闭模块；资源模块取消任务、销毁资源池及自有 YooAsset 系统。
- 资产绑定：GameEntry.prefab 新增 ResourceModuleDriver，继续引用原 ResourceSetting.asset，已有脚本及资产 GUID 保留。

### 保留的适配与范围

- YooAsset 3 使用 `InitializePackageAsync`、Options 参数、`LoadPackageManifestAsync`、`SetAsyncOperationMaxTimeSlice` 和明确的 ResourcePackage 实例。main 的默认包静态加载调用不适用于此版本。
- 当前 UniTask 适配器等待结束后不自动抛出操作失败；保留显式状态检查、失败句柄释放、取消归还、同步与异步竞争处理、退出取消和资源销毁。加载失败仍遵循当前工程的异常语义。
- `IsReady` 根据已成功加载清单的包记录判断，供现有流程、音频和场景入口使用。初始化中同包重复请求被拒绝；已初始化但未就绪的包可重试清单阶段，已就绪的包不重复加载清单。
- ResourceSetting/ResourceOptions 保留为 Driver 的配置预设，不再作为资源模块的独立配置协议。AutoUnloadBundleWhenUnused 可配置，默认沿用当前 mini 的 true。
- 当前运行范围仍为编辑器模拟和离线模式；在线下载、Web、加密、微信及完整回调族未纳入本轮。没有修改包依赖或接入 HybridCLR。

### 验证结果

- 17 项资源配置、加载接口、初始化前置条件、启动流程和 AssetsReference 测试通过；2 项 UI PlayMode 测试通过。
- Launch 实际进入战斗场景，资源版本为 Simulate；Driver 和既有配置绑定有效。
- 在运行会话中执行 `ResourceInitializationTests.VerifyRunningGame()`：验证真实资源同步与 Type 重载、并发缓存加载、取消归还、资源池借用计数、Prefab 销毁归还、直接句柄释放、无效定位地址、显式清理及低内存强制回收。
- 在运行会话中发起加载后立即 Shutdown：待完成加载返回 null、IsReady 为 false、YooAssets.IsInitialized 为 false。
- EditMode Test Runner 中跨播放模式的集成入口曾出现 NullReferenceException；最终将真实加载验证拆为上述独立运行入口，常规自动测试保留在 Test Runner 中。没有将失败尝试计为通过。
- Unity 编译通过；尚未执行离线包构建或真机验证。

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
