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

当前核心使用普通对象实例及标准 C# 异常，模块驱动与对象池接入安排见待办清单。EditMode 测试程序集为 `EmberFrameWork.Editor`。

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

当前模块管理为单线程纯 C# 实现，调用方负责每帧驱动及退出清理。关闭 Domain Reload 时也需要在每个会话结束时调用 `Shutdown()`。Unity 驱动组件、启动入口和 `FsmModule` 按待办清单分步接入。EditMode 测试程序集为 `EmberFrameWork.Editor`。
