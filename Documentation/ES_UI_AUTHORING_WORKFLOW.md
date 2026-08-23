# ES 游戏 UI ScreenSpec v3 工作流

本页定义当前唯一的视觉 UI 生产链：

```text
参考图/Brief -> AI 视觉拆解 -> ScreenSpec v3 -> v3 校验
  -> ESUIGameScreenMaterializer -> Prefab + Fixture Scene + GPU 证据
```

它只生成高保真 UGUI 预制件和场景内 UI，不创建运行时 Window、Presenter、库存、战斗、经济或
输入系统。业务连接点只能作为 ScreenSpec 中的稳定意图 ID，交由其他 Skill 实现。

## 生产输入

ScreenSpec 必须位于 `Assets/UI/Contracts/`，并包含：

- `schemaVersion: 3`、稳定 `screenId`、已注册的 screen template；
- profiles（分辨率、方向和安全区）与 states（default、selected、loading、empty、disabled、error 等）；
- AssetManifest（来源、哈希、fallback 和许可证状态）；
- 递归 components（类型、区域、布局约束、内容/资源槽和状态变体）；
- behaviors（仅描述输入意图和视觉状态转换，不执行业务）；
- reference/design evidence（来源区域、视觉决策、响应式决策和不确定性）。

注册组件和模板见 `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json`。
模板起点见 `.agents/skills/es-ui-prefab-authoring/references/game-ui-screen-spec.v3.template.json`。

## 生成与验证

```powershell
$env:PYTHONUTF8 = '1'
python .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py `
  Assets/UI/Contracts/<screen>.screen-spec.v3.json
```

通过后，计算 spec SHA-256，并以 Unity BatchMode 调用：
`ES.Editor.ESUIGameScreenMaterializer.RegenerateFromSpecBatchMode`。

入口只接受 ScreenSpec v3，拒绝旧字段、未知字段和不受支持的组件。它在声明的 profile/state
矩阵中确定性生成：

- `Assets/UI/Prefabs/Generated/<screen>.prefab`；
- `Assets/UI/Scenes/Generated/<screen>Fixture.unity`；
- `ES/UIEvidence/<screen>/` 下的结构快照、状态快照和 GPU PNG。

重复相同 spec hash 必须产生相同的层级和序列化结果。不要手改生成 YAML；布局问题回到 spec
的 LayoutPlan（锚点、Canvas、区域、响应式约束）修订。

## 布局治理

Anchor/Pivot/Size/Parent/Sibling 决定 RectTransform 在父节点中的几何；Canvas 边界决定缩放、
排序、合批、材质、射线和重建范围。默认使用一个 root Canvas/CanvasScaler；嵌套 Canvas 必须
由 spec 声明角色、排序和隔离原因。不能用拆 Canvas 掩盖锚点错误，也不能改锚点修复排序错误。

每个 profile 都要检查安全区、长文本换行、最小 44x44 交互目标、重叠、裁剪和响应式变体。

## 验收证据

完成一次生成至少要保存：spec hash、Unity 版本、profile/state 矩阵、Prefab/Scene 路径、语义
快照路径、PNG 路径和占位资源清单。静态 JSON 校验或 Unity 进程成功不等于视觉通过；必须读取
新鲜 GPU PNG，并确认内容可见、层级完整、布局不溢出。

Fixture 只驱动可重复的视觉状态，不伪装真实业务。真实输入可用性、运行时数据、性能和发布验收
必须由项目自身的 Runtime/QA 流程提供证据。
