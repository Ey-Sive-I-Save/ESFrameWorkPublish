# ES 对话框迁移计划

状态：迁移计划；不等同于已完成迁移或 Unity 实机验收。

## 目标

把 ES 自有编辑器工具中的同步阻塞确认逐步迁移到 `ESAdvancedDialog`，把跨宿主语义迁移到 `ESDialog` 唯一合同，同时保留权限、Undo、Dirty、保存、取消和失败回滚仍由业务入口负责。

## 扫描口径与统计

扫描日期：2026-08-21（本轮复核）。使用 `rg` 扫描 `Assets/**/*.cs`，分别统计 `EditorUtility.DisplayDialog`/`DisplayDialogComplex`、`ESAdvancedDialog`/`ESDialog` 引用，再排除 `Obsolete`、拼写遗留目录 `Obsolute/OBSOLUTE`、`Tests`、`Examples` 做生产优先级归类。扫描结果以调用点为准，不以文件名或窗口标题猜测用途。本轮原始扫描收据保存在 `Artifacts/editor-dialog-call-scan.txt`；它只记录源码事实，不把第三方、Obsolete、Tests 或 Examples 误报为已迁移。

| 范围 | 结果 | 说明 |
| --- | ---: | --- |
| `EditorUtility.DisplayDialog*` 全部调用文件 | 116 个文件 / 588 个调用点 | `Assets/**/*.cs` 全量扫描 |
| `EditorUtility.DisplayDialog*` 生产候选 | 103 个文件 / 501 个调用点 | 排除 `Obsolete`、`Obsolute/OBSOLUTE`、`Tests`、`Examples` |
| 使用 `ESAdvancedDialog`/`ESDialog` 的生产文件（合同、Presenter、适配器和调用） | 15 个文件 | 不能视为全部已迁移 |
| 直接提交高级/通用对话框的生产文件 | 9 个文件 / 14 个调用行 | 已有调用仍需逐点核对 owner、稳定 ID、权限和取消边界 |

本轮生产候选的机器清单为 `Artifacts/editor-dialog-migration-inventory.csv`。按调用点聚合的
首批风险量为：Installer 16、Resource Pipeline 21、Shader/Material 14、World 5、
Graph/Agent 12、MenuTree/SimpleTools 291；第三方 Easy Save 24 单独保留在 P3。

本轮复核发现 `Assets/Plugins/ES/Editor/ESMenuTreeWindow/SimpleToolsWindow/HierchyTools/OBSOLUTE/`
目录中的 1 个遗留文件、6 个调用因目录拼写不含 `Obsolete`，被旧版 104/507 统计误计入生产；现已从机器迁移清单排除，并保留在原始扫描收据中供追溯。

统计是迁移分组的基线，不是“已迁移数量”。第三方包和历史代码的调用保留在扫描范围中，避免重复扫描时丢失风险来源。复核命令：

```powershell
rg -l 'EditorUtility\.DisplayDialog(?:Complex)?\s*\(' Assets -g '*.cs'
rg -n 'EditorUtility\.DisplayDialog(?:Complex)?\s*\(' Assets -g '*.cs'
```

## 优先级

### P0：ES 自有生产代码中的直接同步对话框

优先处理会在窗口按钮、导入、发布、迁移、资源分析或批量操作中直接调用 `EditorUtility.DisplayDialog`/`DisplayDialogComplex` 的代码。迁移目标是 `ESAdvancedDialogService.Show` 或 `ShowAsync`；确认后的业务写入仍必须经过正式 C# 入口，并保留 Undo、Dirty、目标校验、取消和失败回滚。
首批执行顺序固定为：`ESInstaller.cs` -> `ESResPipeline` -> `ESShader` -> `World`；每个文件迁移完成后才从清单中标记 `已迁移`，不能以批量替换或编译通过代替合同验收。

### P1：已有高级对话框但合同不完整

为已有 `ESAdvancedDialogRequest` 补齐稳定 `dialogId`、明确 `owner`、明确 `Show`/`ShowAsync` 选择和可取消生命周期。同步 `ShowModal` 只允许短确认，不允许异步校验、队列或 `AllowParallel`。

### P2：复杂确认、输入和 `DisplayDialogComplex`

将需要输入、选择、多步骤确认、异步校验、进度、辅助动作或详细失败信息的调用迁移到 `ESDialog`/`ESAdvancedDialog`。显示文本可以本地化，字段、选项和动作必须使用稳定 ID；对话框确认不直接授予删除、发布、写资产或保存场景权限。

### P3：第三方、Obsolete、Examples、Tests

第三方包和已标记遗留代码不在 P0 迁移范围；Examples 与 Tests 只在验证合同或迁移 API 时同步更新。禁止把测试夹具或示例路径当成生产迁移完成证据。

## 禁止事项

- Runtime 数据层直接弹 Editor 对话框。
- 批处理、导入、域重载或 AssemblyStream 注册期间同步阻塞对话框。
- 以显示文本作为稳定 dialogId、FieldId 或 OptionId。
- 通过对话框回调绕过正式权限、Undo、Dirty、保存或发布门禁。
- 用 `Application.isPlaying`、焦点窗口或调用程序集猜测 Editor/Runtime Host。

## 迁移清单字段

每个调用点记录：文件、当前调用、调用类型、owner、是否同步阻塞、风险、目标 API、优先级、状态、验证证据和回滚说明。

## 本轮合同增强

- Editor 窗口生命周期现在有声明式 `ESWindowSleepContract`：直接窗口必须标记
  `Full` 或 `Transient`，核心绑定入口会拒绝合同与 `allowSemiSleep` 不一致；对话框、进度、
  Popup、命令面板和临时输入窗口统一标记为 `Transient`，长生命周期工作台标记为 `Full`。
- `ESWindowFoundation.EnsureStandardSystemActionBar` 为直接 IMGUI 长生命周期窗口提供正常流布局的显式 System 宿主；Shader 烘焙、SSU 迁移、Agent 候选审查、世界对话编辑器已改用该入口。
- `ESDialogRequest.Owner` 可携带明确宿主上下文；编辑器 presenter 优先使用它，迁移调用点不应继续依赖焦点/鼠标悬停猜测。
- Editor presenter 不再从焦点或鼠标悬停窗口猜测 owner；缺失 owner 默认拒绝，只有调用方显式设置 `AllowMainWorkspaceFallback = true`（或高级请求的 `allowMainWorkspaceFallback = true`）时才允许落到主编辑器工作区，传入非 `EditorWindow` owner 会被拒绝。
- `ESDialogRequest.CreateSnapshot()` 必须保留 `AllowMainWorkspaceFallback`；请求快照不能把已声明的 owner 例外静默清除。
- ownerless Editor 请求的唯一允许例外是显式 `AllowMainWorkspaceFallback = true`；迁移表中每个调用点必须记录 owner 来源或 fallback 理由，禁止恢复焦点窗口、鼠标窗口或调用程序集猜测。
- 对话框原生标题现在直接包含 `ES 对话框 · 模态/非模态 · 语义 · 标题`，首屏身份条同时显示 `ES 对话框`、稳定 `dialogId` 和“仅输入 / 确认”边界提示。
- `ShowModal` 仍只允许短同步确认：拒绝异步校验、异步动作、队列策略、并行策略和重复活动 ID。
- `ESDialog` 基础合同现在统一校验稳定 dialog/field/option 身份、枚举、队列与并行冲突、初始焦点引用及显示文本边界。

| 文件 | 当前调用 | 类型 | 风险 | 目标 API | 优先级 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| `Assets/Plugins/ES/Editor/Installer/ESInstaller.cs` | `DisplayDialog*` | 安装/依赖检查 | 可能阻塞检查与窗口生命周期 | `ESAdvancedDialog` + 明确 owner | P0 | 待迁移 |
| `Assets/Plugins/ES/Editor/ESResPipeline/*` | `DisplayDialog*` | 资源收集/发布 | 批量操作期间阻塞、取消语义弱 | `ShowAsync` + `ESProgressCenter` | P0 | 待迁移 |
| `Assets/Plugins/ES/Editor/ESShader/*` | `DisplayDialog*` | Shader/材质迁移 | 资产写入前确认与失败恢复不统一 | `ESAdvancedDialog` | P0 | 待迁移 |
| `Assets/Scripts/ESLogic/Editor/World/*` | `DisplayDialog*` | 世界编辑器 | owner、Undo/Dirty 与窗口重建耦合 | `ESAdvancedDialog` + owner | P0 | 待迁移 |
| `Assets/Plugins/ES/Editor/ESGraphViewV2/*` | `ESAdvancedDialogRequest` | 图/技能流程 | 部分请求需稳定 ID 与取消边界 | `ShowAsync` 或短 `ShowModal` | P1 | 部分已迁移 |
| `Assets/Plugins/ES/Obsolete/*` | `DisplayDialog*` | 遗留 | 不应恢复旧入口 | 保留/归档 | P3 | 不迁移 |
| `Assets/Plugins/Easy Save 3/*` | `DisplayDialog*` | 第三方 | 外部包责任边界 | 上游升级或保留 | P3 | 不迁移 |

## 完成判定

单个调用点只有在稳定身份、明确 owner、正确的同步/异步选择、取消和业务权限边界完成，并通过目标程序集静态编译与 Unity Editor/ReloadDomain 交互验证后，才能标记为“已迁移”。本计划本身不宣称 P0/P1 已完成。
