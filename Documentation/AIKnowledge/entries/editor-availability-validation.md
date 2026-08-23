# 编辑器窗口与扩展可用性验证

状态：现行工程验证路由；静态检查已实现，Unity 交互与视觉证据按目标单独验收。

`KnowledgeId`: `es.engineering.editor-availability-validation.v1`
`Authority`: `Source + AIWarnings + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `editor`, `editor-window`, `editor-extension`, `reload-domain`, `interaction`, `visual`, `availability`, `validation`, `evidence`

Static routing keywords also include `inspector`, `drawer`, `dialog`, `popup`, `workbench`, `layout`, `responsive`, `high-dpi`, `single-axis-scroll`, `owner-lifecycle`, `undo-dirty`, `preview-lifecycle`, `editor-performance`, and `window-production-standard`.
`ContentHash`: `8297e5315e9042177e8a67a4d7bcfb34d9cb26a93d4189245962424fcbcb9387`
`StaleWhen`: 编辑器扩展规则、ReloadDomain/Undo/序列化边界、可用性矩阵、验证脚本或证据合同变化。

`SourceRefs`:

- `.agents/skills/es-editor-availability-validator/SKILL.md` (`2dc7c936a07c6b79b4dd8e253c2364715e69ab6285df4b3babad29d95cfde135`)
- `.agents/skills/es-editor-availability-validator/governance.json` (`6119498a1b429c870bf6cd0aace32df922ab667587aa328631acafd025e78ac4`)
- `.agents/skills/es-editor-availability-validator/scripts/Invoke-ESEditorAvailability.ps1` (`373a4168df6504443a306a4d050dad847722b7387111ebc91d83dd701ae746f6`)
- `.agents/skills/es-editor-availability-validator/references/availability-matrix.md` (`fd957446e1cf757da9a4ed814be08f6e1fa8de10cbf733285784e714e7050104`)
- `.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json` (`62f62dce9512fc6233dee03624f374ccfff0b56ed6a38f12d2801928808cb769`)
- `Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md` (`247e0989d895f0f69bc85c8425e7e78eda180934f621913401e2d2b311faee4e`)
- `.agents/skills/es-editor-tooling/SKILL.md` (`9b6f8adedc83e590f2af724af05e04b9db9b0585740a37e95b2b201bbc191f5d`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`d08d443a6b8bc4712142904375adb627b420981d674643b0ec3166753c152c37`)

## 裁决规则

编辑器工具的可用性不是“源码存在”或“窗口打开一次”。结构、静态边界、Unity 编译、ReloadDomain、交互、视觉、恢复和性能是独立维度；缺少必需 Unity 证据时输出 `Blocked` 或 `not-run`。截图不能证明序列化、Undo、资源生命周期或交互正确性。
