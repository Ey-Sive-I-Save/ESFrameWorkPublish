# P0：IL2CPP 工具链必须可被 Unity 检测

`Status`: `current`
`StableId`: `es.aiwarning.p0.il2cpp-toolchain-detection.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `il2cpp`, `visual-studio`, `vswhere`, `hybridclr`
`Applicability`: Windows IL2CPP、HybridCLR Prebuild/StripAOTDlls、Player Build 与 ES 发布前构建。
`EvidenceRef`: `Documentation/AIKnowledge/entries/aiwarning-p0-il2cpp-toolchain-detection.md`
`StaleWhen`: Unity/VS/MSVC/SDK 版本、vswhere 检测、HybridCLR 构建流程或 SourceRefs 变化。

## 长期 P0 约束

- Unity 可用的 Windows IL2CPP 工具链必须同时具备 MSVC x64/x86、Windows 10/11 SDK、Visual Studio Installer 注册实例，以及 `vswhere`/`VS170COMNTOOLS` 可检测入口；仅有 `cl.exe`、Developer Command Prompt 或卸载注册表记录不算完成。
- 正式 HybridCLR Player 必须使用 IL2CPP；VS Code 只是编辑器。禁止改回 Mono、复制目录、伪造注册表/PATH、删除 HybridCLR、关闭 AOT Metadata 或跳过 `PrebuildCommand.GenerateAll()`。
- 工具链未通过前禁止反复烘焙资源、规划/构建 AB 或改运行时代码；自定义安装仍须保留 Installer 实例注册，环境变量只能辅助。
- 诊断先确认 Unity/平台，再用带 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` 的 `vswhere` 找到真实安装目录，并确认 `VC/Tools/MSVC/*/bin/Hostx64/x64/cl.exe`。无输出必须在 Installer 修复并重启 Unity/Hub。
- 验收顺序为：vswhere 实例 → 重启 Unity → 最小 Windows IL2CPP Player → HybridCLR AOT/热更 → ES Consumer 发布；资源管线仅在资源/身份/AB/清单变化时重跑，Mono/IL2CPP 输出不得混用。
- `ToolchainNotFoundException`、无法检测兼容 VS 或无法建立 x64 工具链属于本机安装实例问题，不得归因于 ES 资产/场景；必须记录 Unity、架构、vswhere、MSVC、SDK 和 Installer 修复证据。

详细诊断命令、验收证据和原文快照见 Knowledge：`es.aiwarning.p0.il2cpp-toolchain-detection.v1`。
