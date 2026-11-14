# EmberFrameWork
Unity游戏框架

项目后续工作统一记录在 [待办清单](TODO.md)，完成任务时同步更新验证结果。

FSM 核心位于 `EmberFrameWork/Assets/Scripts/FrameWork/Fsm`，使用命名空间 `EmberFrameWork`。接口、状态基类及生命周期来源于 TEngine，MIT 许可保留在该目录的 `LICENSE.txt`。

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

当前核心使用普通对象实例及标准 C# 异常，模块驱动与对象池接入安排见待办清单。EditMode 测试程序集为 `EmberFrameWork.Fsm.EditorTests`。
