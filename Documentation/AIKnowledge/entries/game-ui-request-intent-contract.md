# UI 需求意图合同

`KnowledgeId`: `es.project.game-ui-request-intent-contract.v1`
`Authority`: ES UI Prefab Authoring Skill、ScreenSpec Validator 与 UI 失败反馈规则
`RouteKeys`: `ui-automation`, `ui-workflow`, `ui-intent`, `ui-screen-family`, `ui-visual-design`, `requirements-traceability`, `intent-drift`, `reference-policy`
`HashSchema`: `v2`
`ContentHash`: `da07e0ea3749322f0dcb0954544d5e84081cae067e8c211d13471407d9e5fc98`
`SourceSetHash`: `da07e0ea3749322f0dcb0954544d5e84081cae067e8c211d13471407d9e5fc98`
`EntryBodyHash`: `71299c77e9390213c5033293942418e6a5fb395d8024fd5ff03224e0c490b35f`
`EvidenceLevel`: `S0`
`StaleWhen`: ScreenSpec schema、Task Classifier、UI Validator、Prefab Authoring Skill、视觉参考入口或产品边界规则变化。

## 目的

UI 生成必须先保留用户的需求意图，再进行屏幕族、布局、配色和素材设计。静态布局通过不能证明需求没有漂移。

每个质量门 Spec 必须声明 `intentContract`：

- `requestedScreenFamily`：用户请求的屏幕族，必须与 `screenType` 相同；
- `requestedPrimaryIntent`：用户的主要动作，必须能在组件交互中找到；
- `visualTarget`：目标产品、风格或参考对象；
- `fidelityMode`：`original`、`reference-guided` 或 `reference-match`；
- `referencePolicy`：`required`、`optional` 或 `not-required`；
- `referenceSources`：参考驱动时必须存在的来源清单；
- `productBoundary`：原创、受启发或官方资产边界。

当用户提出具体产品、品牌风格或“按参考图制作”时，不能把请求静默降级为原创通用主题。若没有参考源，必须阻断并报告 `IntentReferenceRequired`；若屏幕族或主动作不一致，必须报告 `IntentScreenFamilyMismatch` 或 `IntentPrimaryActionMissing`。

## 顺序

```text
用户请求
-> Task Classifier
-> intentContract
-> Reference/IR Ingest
-> ScreenSpec
-> Layout/Token/Asset/Fixture
```

任何内部 Token、Anchor 或 Validator 通过，都不能覆盖 `intentContract` 失败。没有真实参考图时，AI 只能声明原创或等待补充，不能声称模仿目标产品。

## 失败预防

### UI-FB-006：需求意图在分类或重建时漂移

- `erroneousBehavior`：用户指定产品、屏幕族或主动作后，生成器改成 generic/original 屏幕，或把大厅改成 combat HUD。
- `triggerAndSymptom`：`intentContract` 缺失、与 `screenType` 不一致、主动作未出现在交互组件，或 reference-guided 没有参考来源却继续物化。
- `rootCause`：把布局、Token 或模板默认值当成需求真相；rebuild 代码硬编码了屏幕族和视觉目标。
- `preventionCheck`：在 LayoutPlan、Token、Asset、Fixture 之前运行 `validate_intent_contract`；rebuild/iterate 必须继承源合同并绑定 `UI-FB-006`。
- `correctAction`：保持 `requestedScreenFamily`、`requestedPrimaryIntent`、`visualTarget`、`fidelityMode`、`referencePolicy` 和 `productBoundary`；参考驱动请求缺源时停止并报告 `intent-drift`。
- `stateConsistency`：Generator 重建/迭代时只从 `stateSemantics.affectedComponentIds` 派生非默认 `stateVariants`，并为每个执行目标派生或保留一个白名单 `effects`；它不能保留“声明但 Fixture 永不执行”的伪状态来掩盖目标状态覆盖缺失。
- `recoveryAction`：丢弃漂移产物，补充参考源或明确改为原创请求，重新生成并重新计算 Spec 哈希与证据。
- `presentEvidence`：Validator strict quality gate、rebuild-from-source-contract 生成模式、UI-FB-006 绑定和 iteration packet receipt。
- `missingEvidence`：Unity Prefab、GPU 截图、参考相似度、运行时行为和商业资产授权。
- `sourceOwnership`：`es-ui-prefab-authoring` Skill 的 ScreenSpec Validator 与 Generator；本条目只负责路由和防错，不拥有视觉验收结论。

## 使用合同

- 允许：在生成前读取本条目并拒绝低置信度分类；将需求合同传递给 ScreenSpec、LayoutPlan、AssetManifest、Fixture Driver 和 Materializer。
- 禁止：用静态布局通过、PNG 非空、文件存在或内部默认 Token 证明视觉目标已经满足；在无参考源时声称 reference-match。
- 完成验证：记录输入意图、源 Spec 哈希、生成模式、`intentContract` 快照、Validator 结果和 non-claims；任何一项缺失都只能标记为 `runtime-not-run`/`evidence-incomplete`。

## Evidence Boundary

本条目只证明需求意图与 Spec 之间的静态约束，不证明产品视觉相似度、Unity Prefab、GPU 截图或运行时行为。

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`d19f224ad40646a2820019a16b42ea0ecc0c35d2ae97d753f79afe8be42ebfd1`)
- `.agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py` (`c29986030b842af905536e699778ad7a5a267e415f8842b74ae42a7c80ed4739`)
- `.agents/skills/es-ui-prefab-authoring/scripts/generate_ui_iteration_packet.py` (`1c4b301eb2161953fc21f4fa10916c54c7abc542cc96606f0a9f1502fd44c352`)
- `.agents/skills/es-ui-prefab-authoring/references/ui-failure-feedback-rules.md` (`c694ed9fa2a38cd1d207068c254bf548010c48e5ee6bc6bf1bfb05ac63858875`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)
