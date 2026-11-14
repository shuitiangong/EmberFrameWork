# Luban 配置导出工具

本目录包含 Luban 5.1.0 生成器及配置源表。Unity 运行时使用 `com.code-philosophy.luban` 1.2.0。导出脚本支持 Windows，需要 .NET 8 运行时和 Windows 自带的 `tar.exe`。

## 导出入口

修改 `Config/Data` 下的 Excel 表格后，选择对应的导出方式。

| 格式 | Unity 菜单 | 快捷键 | 独立脚本 |
| --- | --- | --- | --- |
| JSON | `EmberFrameWork > Luban > 导出 JSON` | `Alt+X` | `GenerateJson.bat` |
| 二进制 | `EmberFrameWork > Luban > 导出二进制` | `Alt+Shift+X` | `GenerateBin.bat` |

命令行支持显式指定格式，省略参数时默认导出 JSON：

```powershell
./Luban/Generate.ps1 -Format Json
./Luban/Generate.ps1 -Format Bin
```

`Generate.bat -Format Json` 和 `Generate.bat -Format Bin` 使用相同流程。

## 输出目录

下表路径均相对于 Unity 项目根目录 `EmberFrameWork/`。

| 内容 | JSON | 二进制 |
| --- | --- | --- |
| 生成代码 | `Assets/ScriptsGenerate/GenerateJsonCfg` | `Assets/ScriptsGenerate/GenerateBinCfg` |
| 配置数据 | `Assets/RawAssets/Configs/Json/*.json` | `Assets/RawAssets/Configs/Bin/*.bytes` |
| C# 命名空间 | `EmberFrameWork.Cfg.Json` | `EmberFrameWork.Cfg.Bin` |
| 生成器 | `cs-simple-json` + `json` | `cs-bin` + `bin` |

两种格式使用独立的代码、数据和生成清单，可以同时存在。导出其中一种格式仅更新该格式的产物。生成代码会被覆盖，手写扩展代码应放在 `Assets/Scripts/LubanUtility` 等独立目录。

两套 C# 代码均采用平铺布局，`Tables.cs`、`Tbitem.cs`、`item.cs` 等文件直接位于各自的生成目录中。源表模块继续决定 C# 命名空间，输出目录不再按模块建立 `demo` 等子目录。不同模块中的类型需要使用唯一文件名；发生同名冲突时，导出会报错并停止代码同步。迁移旧目录时保留脚本 `.meta` 中的 GUID，并清理生成清单对应的空目录。

JSON 和二进制的 `vector2`、`vector3`、`vector4` 集中生成到 `Assets/Scripts/LubanUtility/LubanUtility.cs`。文件内保留两套命名空间及各自的反序列化实现。每次导出只更新所选格式的标记区段，保留另一格式的区段；原生成目录中的独立向量脚本由生成清单清理。该文件属于生成代码，手写扩展应放在其他文件中。修改 `Config/Defines/builtin.xml` 后，应分别导出两种格式以同步两套类型。

源表配置位于 `Config/luban.conf`，包含 `client-json` 和 `client-bin` 两个目标。当前保留 MiniTemplate 源表，表名为 `demo.Tbitem`。项目已移除 `Assets/Samples` 下的示例场景及运行脚本，导出流程使用上表中的正式目录。

## 数据读取

JSON 生成的 `Tables` 接受 `Func<string, Luban.SimpleJSON.JSONNode>`，二进制生成的 `Tables` 接受 `Func<string, Luban.ByteBuf>`。加载委托收到不带扩展名的表名，例如 `demo_tbitem`。

加载 JSON 文本后使用 `Luban.SimpleJSON.JSON.Parse(text)` 构造节点；加载二进制字节后使用 `new Luban.ByteBuf(bytes)` 构造缓冲区，并传给对应命名空间的 `Tables` 构造函数。

`Assets/RawAssets/Configs` 是配置资源目录。运行时需要由项目资源加载系统提供文本或字节数据；需要随构建发布时，应按项目打包规则收集对应格式的数据。

## 执行与清理

编辑器工具位于 `Assets/Editor/LubanTools`。菜单同时提供“打开配置表目录”和“打开工具目录”入口。导出在后台进程中执行，完成后向 Unity Console 输出格式、日志和退出状态，并刷新生成资源。执行期间禁止重复导出并暂缓程序集重载；编译期间和播放模式下禁用导出菜单。

生成器先写入 Unity 的 `Library/Luban` 临时目录，生成成功后同步到目标目录。`GeneratedManifest.Json.json` 和 `GeneratedManifest.Bin.json` 分别记录已生成文件，用于清理对应格式的失效产物及其 `.meta`。同步时保留未变化文件和已有文件的 `.meta`，以维持 Unity 资源 GUID。

## 来源与许可

- 编辑器菜单基于 [TEngine](https://github.com/ALEXTANGXIAO/TEngine) 的 `UnityProject/Assets/TEngine/Editor/LubanTools/LubanTools.cs` 适配，MIT 许可副本位于 `Assets/Editor/LubanTools/LICENSE.txt`。
- 生成器：[Luban v5.1.0](https://github.com/focus-creative-games/luban/releases/tag/v5.1.0)，归档 SHA-256：`bac9a1b8d69cfeaa7ef1d8c67b34a007324d1cd158e0b14294521b3879ba5213`。
- 配置源表和 schema：[luban_examples MiniTemplate](https://github.com/focus-creative-games/luban_examples/tree/c52e273f1f50a263e9257dddb35c8e222d9777b8/MiniTemplate)，许可见 `Config/LICENSE`。
