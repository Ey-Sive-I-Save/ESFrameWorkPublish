# IL2CPP 工具链检测边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.il2cpp-toolchain-detection.v1`  
`Authority`: `AIWarnings` 原文与当前 Unity/Visual Studio/HybridCLR 证据边界  
`RouteKeys`: `aiwarnings`, `p0`, `il2cpp`, `visual-studio`, `vswhere`, `hybridclr`  
`HashSchema`: `v2`  
`ContentHash`: `e44f7aac6cae35276642e69c4d765a689aa376b8b8b51fd4a95ac6eceebfefab`  
`SourceSetHash`: `e44f7aac6cae35276642e69c4d765a689aa376b8b8b51fd4a95ac6eceebfefab`  
`EntryBodyHash`: `d532fd891d9dc3ce757a8536522feb705c1f23c4915e3b7e32d895c32b0aa66b`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Unity/VS/MSVC/SDK 版本、vswhere 检测、HybridCLR 流程或任一 SourceRef 哈希变化。

## 迁移说明

Warning 保留工具链四项必要条件、禁止绕过、诊断顺序、Unity/HybridCLR 验收和错误归因边界；本条目承载命令细节、证据记录模板、产物隔离规则和原文语义。Knowledge 不授予构建、Installer 修复或发布权限。

## 详细合同

### 必要条件与禁止事项

必须同时具备 MSVC x64/x86、Windows 10/11 SDK、Visual Studio Installer 注册实例以及 `vswhere`/`VS170COMNTOOLS` 可检测入口。`cl.exe`、Developer Command Prompt 或卸载注册表记录单独存在不代表 Unity 可用。禁止改回 Mono、把 VS Code 当工具链、复制目录、伪造注册表/PATH、删除 HybridCLR、关闭 AOT Metadata 或跳过 `PrebuildCommand.GenerateAll()`；工具链失败时不应反复烘焙资源、规划/构建 AB 或改运行时代码。

### 诊断与验收

先确认 Unity 版本和目标平台，再执行：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
```

输出必须是一个真实安装目录，并确认 `VC\Tools\MSVC\*\bin\Hostx64\x64\cl.exe` 存在。无输出必须用 Visual Studio Installer 修复并完全重启 Unity/Hub。验收顺序：vswhere 实例 → Unity 重启 → 最小 Windows IL2CPP Player → HybridCLR AOT/热更 → ES Consumer 发布。资源内容/身份/AB/清单未变化时，工具链修复不要求重烘焙；Mono 与 IL2CPP 必须使用不同输出目录或 Clean Build。

### 错误归因与证据

`ToolchainNotFoundException`、`Unable to detect any compatible Visual Studio installation`、`Could not set up a toolchain for Architecture x64` 是本机工具链/安装实例问题，不得归因于 ESAssetReferenceBaker、Manifest、GameCore 或测试场景资产。必须记录 Unity 版本、目标架构、vswhere 输出、MSVC 路径、Windows SDK 版本和 Installer 修复结果；缺任一项不能声称 IL2CPP 可用。

## 原文快照

迁移前完整 Warning（74 行、3786 字节）由以下 SourceRef 保留，原始 SHA-256 为 `0b68750f825af7bf02cb55905043deba58abaa79aecef5751f760425f7d85fd5`。

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）/项目最高警告_IL2CPP工具链注册_禁止以编译器文件存在代替Unity可检测_AI协作警告.md` (`b2e6c750f781806c676b62922b7ba351e4efe1e83e03888535e62344ec68b4fc`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`f2da13326e6b93b97894201194fb74b787e213e284dad973824bbe8cf2664526`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-il2cpp-toolchain-detection.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
