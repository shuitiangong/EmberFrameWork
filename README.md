# EmberFrameWork
Unity游戏框架

项目后续工作统一记录在 [待办清单](TODO.md)，完成任务时同步更新验证结果。

框架目录采用 `Runtime` 与 `Editor` 分层：`EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Core` 存放核心代码，FSM 位于其 `Fsm` 子目录；`FrameWork/Editor` 存放编辑器代码，当前测试分别位于 `Editor/Tests/Core` 和 `Editor/Tests/Fsm`。

框架程序集边界沿用 TEngine，仅保留 `EmberFrameWork.Runtime` 和 `EmberFrameWork.Editor` 两个程序集。后续模块归入对应层，不自行新增 TEngine 中不存在的模块级或测试专用程序集。Editor 程序集引用 Runtime，并通过 `includePlatforms: ["Editor"]` 限定在编辑器中编译；现有测试所需的 NUnit 引用集中配置在该程序集。

FSM 核心位于 `EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Core/Fsm`，使用命名空间 `EmberFrameWork`。接口、状态基类及生命周期来源于 TEngine，MIT 许可保留在该目录的 `LICENSE.txt`。

```csharp
var fsm = Fsm<MyOwner>.Create("Startup", owner, new FirstState(), new NextState());
fsm.Start<FirstState>();
// 由持有者的 Update 驱动。
fsm.Update(Time.deltaTime, Time.unscaledDeltaTime);
// 持有者结束生命周期时清理。
fsm.Shutdown();
```

状态继承 `FsmState<MyOwner>`，通过 `OnInit`、`OnEnter`、`OnUpdate`、`OnLeave`、`OnDestroy` 实现行为。在当前状态内调用 `ChangeState<NextState>(fsm)` 切换。所有状态先完成注册，再按传入顺序初始化；状态时间累加逻辑时间，切换时归零。

每个状态实例只能属于一个存活的 FSM。`OnEnter` 和 `OnUpdate` 支持切换；`OnInit` 和 `OnLeave` 期间禁止启动、更新、关闭或切换。进入、更新或退出回调异常会关闭状态机并向调用方传播异常；销毁阶段尽量执行全部清理，汇总异常。`Shutdown()` 可重复调用，销毁后 `Update()` 无操作，重新启动及写入数据会抛出异常。

当前核心使用普通对象实例及标准 C# 异常；FSM 管理模块已接入，对象池迁移安排见待办清单。EditMode 测试程序集为 `EmberFrameWork.Editor`。

## 模块管理

模块管理位于 `EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Core`，程序集为 `EmberFrameWork.Runtime`，使用命名空间 `EmberFrameWork`。`Module` 定义初始化、优先级及关闭行为；实现 `IUpdateModule` 的模块参与统一轮询。该目录保留 TEngine 的 MIT 许可。

```csharp
// 模块实现继承 Module，并实现其业务接口。
ModuleSystem.RegisterModule<IMyModule>(new MyModule());
var module = ModuleSystem.GetModule<IMyModule>();

// 由启动入口每帧调用一次。
ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime);
// 会话结束时关闭，清理静态注册信息。
ModuleSystem.Shutdown();
```

`GetModule<T>()` 优先返回已注册接口。未注册时，按接口所在程序集及命名空间查找去掉首字母 `I` 的实现类，例如 `IMyModule` 对应 `MyModule`；实现需要继承 `Module`、实现对应接口并提供无参构造函数。显式注册允许自定义实现名称、构造参数，并为构建提供直接的类型引用；纯反射创建的实现需要按项目代码裁剪配置保留。

优先级在注册时读取，高优先级先更新；同优先级按注册顺序更新；关闭按更新顺序的逆序执行。同一实例注册多个接口只初始化、更新和关闭一次。已注册接口或实现类型不允许被另一实例替换。

初始化失败会调用失败实例的 `Shutdown()` 并向调用方传播异常，该实例不进入注册表；初始化期间已成功创建的其他模块继续保留。循环初始化会报错。关闭阶段即使部分回调抛出异常，也会继续关闭其他模块、清理注册表，最终汇总异常。

更新时使用复用的快照列表，新注册模块从下一次更新开始执行；更新期间关闭系统会停止当前快照的后续执行。更新回调异常直接向调用方传播，当前轮询停止，注册信息保留。初始化和关闭期间禁止全局更新，初始化期间禁止全局关闭；关闭回调可以获取已有模块，但不能新增模块。

当前模块管理为单线程纯 C# 实现。使用下文的 `GameEntry` 时，由入口统一负责每帧驱动及退出清理；独立使用模块系统时，由调用方负责。关闭 Domain Reload 时也需要在每个会话结束时调用 `Shutdown()`。EditMode 测试程序集为 `EmberFrameWork.Editor`。

## FSM 管理模块

`IFsmModule` 和 `FsmModule` 位于 `EmberFrameWork/Assets/Scripts/FrameWork/Runtime/Module/FsmModule`，沿用 TEngine 的模块接口，归入 `EmberFrameWork.Runtime`。现有 FSM 核心继续位于 `Runtime/Core/Fsm`。模块实现通过 `Preserve` 标记保留类型和无参构造函数，供 `ModuleSystem` 按名称创建。

```csharp
var fsms = ModuleSystem.GetModule<IFsmModule>();
var fsm = fsms.CreateFsm("Startup", owner, new FirstState(), new NextState());
fsm.Start<FirstState>();

// 每帧调用一次，自动更新模块管理的所有运行中 FSM。
ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime);

// 按需销毁单个 FSM；退出时统一关闭模块系统。
fsms.DestroyFsm(fsm);
ModuleSystem.Shutdown();
```

使用模块管理的 FSM 由模块统一驱动，调用方只执行 `ModuleSystem.Update()`。直接通过 `Fsm<T>.Create()` 创建的独立 FSM 仍由调用方自行管理。

FSM 的唯一标识为泛型持有者类型与名称，名称区分大小写，`null` 与空字符串表示默认名称。同一类型的多个持有者需要不同名称。创建完成后需显式调用 `Start<TState>()`，接口提供泛型与 `Type` 查询、列表查询、按名称或实例销毁等重载。

更新按创建顺序执行；更新期间新建的 FSM 从下一次更新开始执行，已销毁的 FSM 跳过。通过实例销毁时校验实例身份，旧引用和外部独立实例不会误删同名的新 FSM。直接关闭的 FSM 在下一次查询或更新时从注册表移除。初始化失败不占用名称；更新异常向调用方传播并停止本轮更新，失败 FSM 由核心关闭后从注册表移除，其余 FSM 保留。

模块关闭时阻止新增 FSM，并继续清理所有 FSM，最后汇总回调异常。FSM 初始化回调期间禁止关闭或轮询该模块。关闭后的旧模块引用不可重新创建 FSM；下一会话通过 `ModuleSystem.GetModule<IFsmModule>()` 获取新模块。

## 流程框架与配置

`ProcedureBase`、`IProcedureModule`、`ProcedureModule` 和 `ProcedureSetting` 位于 `Runtime/Module/ProcedureModule`，自定义配置 Inspector 位于 `Editor/Inspector`，均使用现有 Runtime／Editor 程序集。

流程继承 `ProcedureBase`，对应 FSM 的持有者类型为 `IProcedureModule`。模块优先级为 `-2`，FSM 模块为 `1`；统一关闭时先销毁流程，再关闭 FSM 管理模块。流程由 FSM 模块更新，调用方每帧仍只调用一次 `ModuleSystem.Update()`。

```csharp
var procedures = ModuleSystem.GetModule<IProcedureModule>();
procedures.Initialize(ModuleSystem.GetModule<IFsmModule>(), new FirstProcedure(), new NextProcedure());
procedures.StartProcedure<FirstProcedure>();

// 在流程生命周期回调内切换，procedureOwner 的类型为 IFsm<IProcedureModule>。
// ChangeState<NextProcedure>(procedureOwner);

// 重启会销毁原流程 FSM，重新初始化，并进入新列表第一项。
procedures.RestartProcedure(new FirstProcedure(), new NextProcedure());
```

`IsInitialized` 表示流程 FSM 已初始化且未销毁。模块提供当前流程、状态时间和按类型查询接口。重复初始化或重复启动会报错；重启会在销毁旧 FSM 前验证新列表，空列表、重复类型或属于其他 FSM 的实例均被拒绝。新流程初始化或进入失败时异常向调用方传播，调用方可显式重新初始化；模块关闭时释放所持有的流程引用。

配置资产为 `EmberFrameWork/Assets/Scripts/FrameWork/Settings/ProcedureSetting.asset`，也可通过 Unity 的 `Assets > Create > EmberFrameWork > ProcedureSetting` 创建。Inspector 列出运行时程序集内可实例化的流程，支持勾选可用流程和选择入口；播放期间禁用编辑。无法解析的已选类型会保留并提示，由使用者显式移除。

```csharp
// setting 为 Inspector 中引用的 ProcedureSetting 资产。
setting.StartProcedure();
```

配置采用类型名称序列化，支持程序集限定名与 TEngine 风格的完整类型名。类型必须继承 `ProcedureBase`、非抽象、非开放泛型，并具有公开无参构造函数。`TryValidate(out error)` 仅验证配置，不构造实例；`Configure(names, entrance)` 支持程序化设置。反射创建的具体流程在启用代码裁剪的构建中应使用 `[UnityEngine.Scripting.Preserve]` 或对应的 `link.xml` 保留。

`ProcedureSetting.StartProcedure()` 同步完成验证、实例化、初始化和入口进入；此版本省略 TEngine 原实现启动前的单次 `UniTask.Yield()`，调用方可直接捕获启动异常。当前资产包含 `ProcedureLaunch` 和 `ProcedureStartGame`，入口为 `ProcedureLaunch`；流程在首次更新时切换到 `ProcedureStartGame`。

## 基础启动入口

打开 `EmberFrameWork/Assets/Scenes/Launch.unity` 并进入 Play Mode。Console 依次输出 `ProcedureLaunch` 和 `ProcedureStartGame`，表示基础启动完成。该场景已设为构建列表首场景，当前仅包含启动入口及相机，游戏业务和后续场景尚未接入。

| 内容 | Unity Assets 下的路径 |
| --- | --- |
| 启动组件 | `Scripts/GameEntry.cs` |
| 具体启动流程 | `Scripts/Procedure/ProcedureLaunch.cs`、`ProcedureStartGame.cs` |
| 流程配置 | `Scripts/FrameWork/Settings/ProcedureSetting.asset` |
| 启动预制体 | `Scripts/FrameWork/Settings/Prefab/GameEntry.prefab` |
| 启动场景 | `Scenes/Launch.unity` |

`GameEntry` 和具体流程编译到 Unity 默认 `Assembly-CSharp`，框架继续保留 Runtime／Editor 两个程序集。入口组件位于 `EmberFrameWork.Game` 命名空间，具体流程沿用 `Procedure` 命名空间。

`GameEntry` 必须挂载在场景根对象上并引用有效的流程配置，执行顺序为 `-10000`。入口先初始化 FSM 模块，再按配置初始化流程模块并进入启动流程。`ProcedureLaunch` 首次更新时切换到 `ProcedureStartGame`；当前流程不调用尚未迁移的资源更新、音频、语言或 HybridCLR 模块。

初始化成功后入口通过 `DontDestroyOnLoad` 跨场景保留，`Update()` 每帧统一驱动模块。重复入口只销毁重复的 `GameEntry` 组件，避免重复初始化及重复轮询。禁用、销毁或退出应用时由持有入口关闭模块；重新启用后重新创建流程。启动或更新异常会记录错误、停止并清理框架，然后禁用入口，避免持续重复报错。

每次进入 Play Mode 时通过 `SubsystemRegistration` 重置静态入口和遗留模块，关闭 Scene Reload 时另由 `AfterSceneLoad` 检查现存入口。验证已覆盖默认模式启动、重复入口、禁用恢复，以及关闭 Domain Reload 和 Scene Reload 后连续两次启动与退出清理。调用方可通过 `GameEntry.Instance.IsInitialized`、`CurrentProcedure` 和 `LastError` 查询状态。
