# Editor 稳定选择与正式 Scene 身份导航

`KnowledgeId`: `es.editor.stable-selection-scene-identity.v1`
`Authority`: `Current source + AIWarnings`
`RouteKeys`: `editor`, `selection`, `stable-identity`, `object-generation`, `scene-identity`, `formal-scene`, `preview-boundary`, `external-drift`
`ContentHash`: `02a068a5cfffb837569e0395db1ecc6e27b90570bd9f2d6d36df276af2e6bf69`

## Canonical facts

- UI、资源、层级、2D/3D 视口和 Inspector 必须共享稳定选择；稳定选择至少区分 StableId、Kind、领域 key/资产 GUID 和对象代际。
- `UnityEngine.Object`、`SerializedObject`、InstanceId、视口实例和 PreviewScene 临时对象不能作为长期恢复身份，也不能单独证明正式 Scene 选择。
- 正式 Scene 选择必须通过稳定身份到正式 Source/Scene 对象的显式解析；解析失败、对象替换、代际变化或外部 Source 漂移必须拒绝猜名、拒绝旧引用并显示恢复动作。
- PreviewScene 只提供预览投影。临时对象命中、截图或预览重绘不能投影为正式 Scene 对象已选中、已修改或已提交。

## Failure prevention

| 失败面 | 预防检查 | 正确恢复 | 未证明 |
|---|---|---|---|
| 用 PreviewScene 临时对象冒充正式对象 | 检查选择 payload 是否为稳定身份并能解析正式 Source | 清空选择或显示“无法解析正式对象” | 当前 3D Unity 命中矩阵 |
| 同名/旧 InstanceId 恢复错误对象 | 检查对象代际和 Source/Scene owner | 阻断编辑，要求重新解析或人工选择 | 多窗口运行时冲突 |
| SelectionChanged 只 return 不刷新 | 检查选择事件至少触发安全重绘/刷新和 Inspector 同步 | 保留稳定选择，刷新投影并报告解析失败 | 真实视觉刷新效果 |

## Route boundary

本条目拥有通用稳定选择和正式 Scene 身份边界；Prefab、World、Graph 等条目只声明各自的身份字段和解析后端，不复制选择恢复规则。它不声明正式 Scene 已实现或已提交。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/专业工作台（Workbench）/专业工作台与World作者工具_贡献注册与正式资产边界_AI协作警告.md` (`50a1f1bbc68e78a1ad129fbdb6d6e2a4843e1b8e92888420c957eb87f62b59c4`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`bda2011a12df8424e091a5e6d1cd9cbb8c8297dc0ea64c9ee49927df66f23177`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs` (`e59d316efe0e4b3431a84a5cf88033b694927762002997d65d90e54e6fc4c65d`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs` (`aa995e0e230baddc62b8996ed887275e8e20f31758803f26324d2251a89475ac`)
- `Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs` (`fa5b0c5e7b5397c0019fe356f0a465418f158147c708dae1a4b168ee452d82c6`)

`EvidenceLevel`: `S1`（源码与 AIWarnings；正式 Scene 映射和 Unity 选择验收未完成）
`StaleWhen`: Stable selection schema、Scene/World identity mapping、SelectionChanged refresh、Preview boundary 或任一 SourceRef 哈希变化。
