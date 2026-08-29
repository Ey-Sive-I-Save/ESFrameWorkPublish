# 检查：编辑器窗口 ReloadDomain AI 命令

## 直接生效协议

当用户把本文件路径发给 AI 时，AI 必须：

```text
1. 先读取本文件全文，不允许只根据文件名猜测任务。
2. 按“命令类型”和“默认改文件”决定是否允许改代码。
3. 先读取本文列出的必须规则文件；若文件不存在，要明确说明。
4. 执行前先确认当前工作树和相关入口文件，避免误改其他 AI 或用户的改动。
5. 只做本文允许的事情；如果用户需求超出本文范围，先说明需要换用哪个命令。
6. 结束时必须给出：已读规则、执行内容、改动文件、验证结果、剩余风险。
```

命令类型：只读体检。
默认改文件：否，补清理/节流需用户确认。
风险等级：L2。

## 必须先读

```text
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_AssemblyStream只做Editor特性注册解耦_禁止全量扫盘_AI协作警告.md
Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_P0_编辑器交付体验与下一步可发现性_AI协作警告.md
Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md
```

窗口含半休眠、父子关系或自定义工具栏时，必须同时执行上述编辑器常识第 11.10、11.11 节。只有任务确实涉及每帧热路径时，再加载运行时性能 P0；不得用运行时判空规则替代 Editor 生命周期规则。

## 执行要求

```text
检查 OnEnable/OnDisable/OnDestroy/InitializeOnLoad/EditorApplication.update/AssemblyReloadEvents，判断窗口恢复是否批量加载资产、重复注册、遗留回调或恢复活 Unity 对象引用。
检查 SessionState/EditorPrefs/序列化字段的状态所有权，确认 Domain Reload 不恢复拖动、Pointer Capture、Popup、动画计时和旧页面上下文。
窗口参与半休眠时，检查 ActivePanel/SleepTile/EdgeTab 的原生几何与视觉状态是否同步；有父子窗口时检查稳定 ownerKey、PendingFollowOwner、关闭解绑和脱离意图。
自定义标题栏必须声明 ESWindowActionHosts；缺少系统动作宿主时不得靠右上绝对定位或任意 Toolbar 猜测注入。
```

## ContractCompleteness

```yaml
commandId: editor.reload.review
writeMode: read-only
cancellation: N/A (read-only; no external effect; stop before analysis)
recovery: N/A (read-only; rerun from unchanged inputs; no rollback)
validation: read-only checks only; no writes, runtime, Git, release, or external effects
evidenceRef: source refs + SHA-256/content hash when available + read receipt; static evidence cannot claim Runtime
actionBoundary: AIBrain/ABCD selects intent and route; this command only reviews and reports; Automation/ABCC execution is out of scope
allowRoots: project files explicitly listed in 必须先读 and the contract's declared read-only targets only
denyPaths: source writes, undeclared paths, Git/history, release, Runtime/Unity, external services; deny-overrides
```
## 交付格式

```text
1. 已读规则：列出已读取的文件。
2. 执行结论：用短句说明做了什么或发现什么。
3. 改动文件：没有改文件就写“无”。
4. 验证结果：分别报告静态检查、Editor csproj、Unity Compile/Domain Reload、窗口重开和交互矩阵；未执行项明确写“未验证”
5. 剩余风险：列出仍需人工确认的多窗口、窄屏、高 DPI、Popup/ContextMenu、父子同步和 Profiler 点。
```

仅编译 Editor `.csproj` 最高属于 S2，不能表述为 Unity Domain Reload 或窗口恢复通过。修复源码后至少应取得 Unity 导入/重载证据；涉及状态恢复时还必须真实重开窗口并操作目标状态。

## 需求

```text
<用户在这里补充具体目标、路径、报错、对象名或玩法场景>
```
