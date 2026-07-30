# P0：IL2CPP 工具链必须可被 Unity 检测，禁止以“存在 cl.exe”代替安装完成

> 级别：P0（违反即停止 Player/HybridCLR 构建并先修复工具链）  
> 适用范围：Windows IL2CPP、HybridCLR Prebuild、StripAOTDlls、Player Build 及 ES 发布前代码构建。  
> 适用对象：所有 AI、自动化脚本和项目维护者。

## 最高结论

Windows IL2CPP 需要同时满足：

1. MSVC x64/x86 C++ 编译器真实存在；
2. Windows 10/11 SDK 真实存在；
3. Visual Studio 安装实例已由 Visual Studio Installer 注册；
4. Unity 的 `vswhere.exe` 或对应 `VS170COMNTOOLS` 检测入口能够找到该实例。

仅能找到 `cl.exe`、仅能在 Developer Command Prompt 中运行 `cl`，或卸载注册表中存在“Visual Studio 生成工具 2022”，都不代表 Unity 能使用 IL2CPP。实例注册损坏时，Unity Bee 必须视为工具链不可用。

## P0 禁止事项

1. 禁止为了绕过检测把 Windows Player 改回 Mono；正式 HybridCLR Player 必须使用 IL2CPP。
2. 禁止把 VS Code 当作 C++ 编译工具链。VS Code 只能作为编辑器，不能替代 MSVC、Windows SDK 或 Visual Studio Installer。
3. 禁止看到 `cl.exe` 就声称工具链已完成；必须执行带组件要求的 `vswhere` 验证。
4. 禁止通过复制 Visual Studio 目录、手工伪造注册表或把任意目录加入 PATH 冒充安装实例。
5. 禁止在工具链未通过验证时反复烘焙资源、规划 AB、构建 AB 或修改运行时代码；这些操作不能修复 IL2CPP C++ 编译失败。
6. 禁止为了通过构建删除 HybridCLR、关闭 AOT Metadata 或跳过 `PrebuildCommand.GenerateAll()`。
7. 自定义安装路径必须保留 Visual Studio Installer 的实例注册；注册丢失时，环境变量只能作为辅助，不是替代修复。

## 强制诊断顺序

先检查 Unity 版本和目标平台，再执行：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" `
  -products * `
  -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
  -property installationPath
```

命令必须输出一个真实安装目录，例如：

```text
F:\re\VSCPIUS
```

然后确认该目录下存在：

```text
VC\Tools\MSVC\*\bin\Hostx64\x64\cl.exe
```

`vswhere` 无输出时，即使 `cl.exe` 存在，也必须在 Visual Studio Installer 中对对应实例执行“修复”，不能直接继续 Unity 构建。修复后必须完全退出并重新打开 Unity/Unity Hub；旧 Unity 进程不会读取新环境和新注册信息。

## Unity/HybridCLR 验收顺序

1. `vswhere` 能找到带 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 的实例。
2. Unity 重新启动后执行一个最小 Windows IL2CPP Player Build。
3. 最小 Player 成功后，才执行 HybridCLR 的 AOT/热更生成和 ES Consumer 发布。
4. 只有资源内容、资源身份、AB 计划或发布清单发生变化时，才重新执行资源管线；工具链修复本身不要求重新烘焙资源。
5. 若构建目录曾使用 Mono，必须使用新的输出目录或 `Clean Build`，不能把 Mono 产物与 IL2CPP 产物混用。

## 错误归因

以下错误属于本机工具链/安装实例问题，不得归因于 ESAssetReferenceBaker、AssetBundle Manifest、GameCore 或测试场景资产缺失：

```text
ToolchainNotFoundException
Unable to detect any compatible Visual Studio installation
Could not set up a toolchain for Architecture x64
```

必须记录：Unity 版本、目标架构、`vswhere` 输出、MSVC 路径、Windows SDK 版本及 Installer 修复结果。未记录这些证据，不得声称“IL2CPP 已可用”。

本警告优先级为 P0。任何临时绕过、降级 Mono 或跳过 HybridCLR 的做法都不能覆盖本规则。
