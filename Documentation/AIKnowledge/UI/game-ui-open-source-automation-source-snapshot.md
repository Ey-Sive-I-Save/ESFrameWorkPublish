# 开源游戏 UI 自动化方案来源快照

本文件是 ES UI Knowledge 的外部架构校准快照。它只保存三个开源仓库中与 AI UI
装配直接相关的最小合同和局限，不复制第三方实现，也不把仓库 README、静态产物或
第三方测试转换成当前 ES/Unity 的运行时证据。

`RetrievedAtUtc`: `2026-08-26T00:00:00Z`
`Retrieval`: GitHub API/raw content at pinned commits; bounded repository and file lookup  
`Scope`: design input normalization, intermediate representation, source mapping, flow separation,
visual comparison, conformance and direct screenshot-to-UGUI workflow claims.

## Repositories

| Repository | Commit | License signal | What was used | What was not inferred |
|---|---|---|---|---|
| `Crackerrrrrr/design-to-unity` | `49f0c2360a619c778d796f6c3f9c227850741768` | MIT (`LICENSE`) | Design Implementation Packet、AssetManifest、source map、readiness、Prefab verifier、visual diff | 不证明当前项目可直接安装、Unity 导入或商业素材授权 |
| `ProdaZhang/figkit` | `bb91f7ff9a02ec68c8358404f6b18ff5bc70be40` | MIT (`LICENSE`) | 版本化 UI IR、独立 `flow.json`、多后端 capability/conformance、known-loss 和真实运行记录 | 不把其 UI Toolkit 后端、跨引擎运行证据移植为 ES 事实 |
| `phucnguyen752/unity-ui-mcp` | `99217ea3f048519862e24573a34f89fca6093969` | MIT (`LICENSE`) | 截图 -> AI JSON -> Unity UGUI Prefab 的直接工作流及其 target resolution/PPUM 规则 | README/Skill 的能力描述不证明多 profile、来源追踪、业务语义或视觉验收 |
| `codewriter-packages/Unity-Prefab-XML` | `da9b16d30710b823d9e20f0a56231ef3e699cc1b` | MIT (`LICENSE`) | 用可读 XML、资产绑定和模板代替 LLM 直接生成 Unity Prefab YAML；明确 TMP、Selectable、Slider 和 LayoutGroup 的常见陷阱 | Unity 6000+ 目标版本；不证明当前 Unity 2022.3/UGUI 兼容、视觉质量或商业素材授权 |
| `41h0-siro/cedec2026-lt-ai-ui-workflow` | `81aaddb46cf5ceac7f6d541e17c5b334c181d1c1` | MIT (`LICENSE`) | Claude Code -> Figma Make -> Figma Dev Mode MCP -> UXML/USS -> uLoopMCP 编译/截图/人工复核 | 单一 demo 屏幕、Unity 6000.4.5f1；不证明自动业务接入、UGUI、跨 profile 或商业验收 |
| `shinjh0380/unity-ui-toolkit-ai-rebuilder-harness` | `822b6464d43eaee4730945d9b913a423a7c60737` | 未声明 | 16:9 截图分析审批、生成无文字素材、资产锁、不可覆盖的 vNNN、编译、Unity MCP 放置和 graphics-enabled capture 分层 | 明确排除非 16:9、交互绑定和自动安装；未在当前项目执行或验证 |

## Pinned source files

SHA-256 计算于固定 commit 的 UTF-8 raw response bytes。

| Repository path | Raw URL | SHA-256 |
|---|---|---|
| `design-to-unity/README.md` | `https://raw.githubusercontent.com/Crackerrrrrr/design-to-unity/49f0c2360a619c778d796f6c3f9c227850741768/README.md` | `8bfb3f56cd043b563f7d4238042beab0073022d473c64a01d7bdb298e83d7257` |
| `design-to-unity/src/design_handoff_mcp/normalizer.py` | `https://raw.githubusercontent.com/Crackerrrrrr/design-to-unity/49f0c2360a619c778d796f6c3f9c227850741768/src/design_handoff_mcp/normalizer.py` | `d2ab64269370ba9eda38dd6d880e67176fffabcfcd3f89f499e41de594a90cb0` |
| `design-to-unity/src/design_handoff_mcp/unity_prefab_verifier.py` | `https://raw.githubusercontent.com/Crackerrrrrr/design-to-unity/49f0c2360a619c778d796f6c3f9c227850741768/src/design_handoff_mcp/unity_prefab_verifier.py` | `6f248cc589007176bab544e934325593975b982d99aba45cae975ac65d062ac2` |
| `design-to-unity/src/design_handoff_mcp/visual_diff.py` | `https://raw.githubusercontent.com/Crackerrrrrr/design-to-unity/49f0c2360a619c778d796f6c3f9c227850741768/src/design_handoff_mcp/visual_diff.py` | `479fe5fcea158615628ab220cd5e7c05dfc42aa17a911f0e3610154c16223ac7` |
| `design-to-unity/src/design_handoff_mcp/unity_editor_validator.py` | `https://raw.githubusercontent.com/Crackerrrrrr/design-to-unity/49f0c2360a619c778d796f6c3f9c227850741768/src/design_handoff_mcp/unity_editor_validator.py` | `185398837017ec5874bab31edf8021c4e181329e52f2a57d2a48897bb1397c74` |
| `figkit/README.md` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/README.md` | `a9630a6d40cf0bb37b5938e5a3189218e8f8e9ea0ab04925bfde42919bc24fed` |
| `figkit/spec/ui.json-schema.md` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/spec/ui.json-schema.md` | `57fe06a5c98f901c31f6604d3948f5cb85aa1a85b198d0c8dab84c05575041e9` |
| `figkit/spec/flow-events.md` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/spec/flow-events.md` | `7ed42bc19416bec815d73f36e46c40485b46192934e5da92ec4d2bb0a726a400` |
| `figkit/figma2unity/SKILL.md` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/figma2unity/SKILL.md` | `2b811f9643a87901ec40b61a0bc44036dd70de687b7e5a462699d9a771b2ab25` |
| `figkit/figma2unity/scripts/ui_to_unity.py` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/figma2unity/scripts/ui_to_unity.py` | `8eb991c4268feee0fb623c8fb1a6064fcdc3fe49a6af92f77d0dc3140bba0b56` |
| `figkit/tools/conformance/test_conformance.py` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/tools/conformance/test_conformance.py` | `964ee1eff3b4b384df3f0f8e741ca98b259a313cb0361ccb3ad269c42d73d9ea` |
| `figkit/docs/verification.md` | `https://raw.githubusercontent.com/ProdaZhang/figkit/bb91f7ff9a02ec68c8358404f6b18ff5bc70be40/docs/verification.md` | `5c29bfcdf569dcc19eccbf4aef9036c0b629cdb146c34caeded7e53a977b5344` |
| `unity-ui-mcp/README.md` | `https://raw.githubusercontent.com/phucnguyen752/unity-ui-mcp/99217ea3f048519862e24573a34f89fca6093969/README.md` | `faf59dae92f8b5c0aca7b97a197ac4c31ee20266d6faf44c80d7185561f904f7` |
| `unity-ui-mcp/Assets/UnityMCP/AI_SKILL.md` | `https://raw.githubusercontent.com/phucnguyen752/unity-ui-mcp/99217ea3f048519862e24573a34f89fca6093969/Assets/UnityMCP/AI_SKILL.md` | `ade2135276c3b8fead151ed8a2207ece80ef5b5a49cf1f4dc9d675c776478d91` |
| `unity-ui-mcp/Assets/UnityMCP/Models/CommandModels.cs` | `https://raw.githubusercontent.com/phucnguyen752/unity-ui-mcp/99217ea3f048519862e24573a34f89fca6093969/Assets/UnityMCP/Models/CommandModels.cs` | `ac8e8a532eb97ba5686ee146c28d3bb3e8bee3372bd561c733862c6560667b4c` |
| `Unity-Prefab-XML/README.md` | `https://raw.githubusercontent.com/codewriter-packages/Unity-Prefab-XML/da9b16d30710b823d9e20f0a56231ef3e699cc1b/README.md` | `6309ec7a73cdacb4086303768f3ed1c8454c27de25230a2fa386d9e56e526a46` |
| `Unity-Prefab-XML/GUIDE.md` | `https://raw.githubusercontent.com/codewriter-packages/Unity-Prefab-XML/da9b16d30710b823d9e20f0a56231ef3e699cc1b/GUIDE.md` | `9e5174e2e1520984ea6bac730457924d2e1aee7c9603470c98dee166220aac2a` |
| `Unity-Prefab-XML/FORMAT.md` | `https://raw.githubusercontent.com/codewriter-packages/Unity-Prefab-XML/da9b16d30710b823d9e20f0a56231ef3e699cc1b/FORMAT.md` | `7e583d495b1b9879df2dd4f43ce2819606a68f215c8d9ef79b515bc4d2c73bc6` |
| `cedec2026-lt-ai-ui-workflow/README.md` | `https://raw.githubusercontent.com/41h0-siro/cedec2026-lt-ai-ui-workflow/81aaddb46cf5ceac7f6d541e17c5b334c181d1c1/README.md` | `133dafc11fc2f83d9f07fa66ab09c23e2e8d0958741c8f5b407e3a4306077166` |
| `unity-ui-toolkit-ai-rebuilder-harness/README.md` | `https://raw.githubusercontent.com/shinjh0380/unity-ui-toolkit-ai-rebuilder-harness/822b6464d43eaee4730945d9b913a423a7c60737/README.md` | `93c173609d6ddfd7765ec67dc9a95a859cfa32f4566c068a76c003e0a4cbde4c` |

## Locked observations

### Design-to-Unity packet pattern

- 设计源先被归一化为中间 packet，而不是直接写 Prefab。packet 保留节点树、全局/局部几何、
  Unity RectTransform hint、语义候选、AssetManifest、可复用 Prefab 候选和 warnings。
- source map 把设计节点、素材和生成的 Unity 对象重新连起来；Prefab verifier 会比较 source-map
  计数与 YAML 计数、检查文件 ID/素材引用，并把 Unity 导入和截图作为后续步骤，而不是把静态
  YAML 视为运行时通过。
- visual diff 会检查参考图与 Unity 截图的尺寸、方向和差异，并在自动调整或复杂素材时产生
  warning/human-review 状态。它不是“低 diff 就自动 Accepted”。

### FigKit IR/flow/conformance pattern

- `.ui.json` 保存设计帧的像素/几何 IR，`flow.json` 单独保存守卫、弹窗、列表、动作和应用钩子；
  Figma prototype transition 不是业务数据，应用语义由显式 app hook 接管。
- IR 有版本字段、稳定 Figma node id 和 additive-only 变更纪律；消费者要声明每个字段是 render、
  approximate 或 known-loss，并把降级写入报告/映射文档。
- conformance 夹具会检查 schema 字段是否被所有后端认领、坏 IR/坏 flow 是否一致拒绝、陌生字段
  是否告警、known-loss 是否留痕，以及多个后端是否从同一 IR 生成结果。实际 Unity/Player 结果
  仍单独记录，不能由静态转换测试替代。

### Direct screenshot-to-UGUI pattern

- `unity-ui-mcp` 的最小闭环是读取目标分辨率，AI 从截图计算百分比，写 JSON，再由 Unity Editor
  构建并保存 Prefab；它明确要求每个元素独立测量、Panel/Button 独立 PPUM，并把生成 Prefab
  放入已有 Canvas。
- 这个模式适合说明 AI 生成入口，但其公开 Skill 没有证明稳定设计节点身份、AssetManifest/
  license、跨 profile/state Fixture、业务语义隔离或 Unity/GPU 视觉证据；ES 不把这些 README
  声明升级为能力事实。

### LLM-friendly materialization pattern

- PrefabXML 的核心不是 XML 本身，而是把 LLM 输出限制在可读的中间格式，再由 Unity importer 解析
  真实组件、对象引用和资产绑定。它明确指出 Prefab YAML 的 fileID/GUID/序列化细节不适合作为
  LLM 的直接输出。
- 其指南把高频失败写成可执行规则：TMP 字段名、Selectable 必须有 target Graphic、Slider 必须
  有 Fill/Handle、LayoutGroup 驱动子节点时不能再写绝对 RectTransform、Image 的原生尺寸需要
  `LayoutElement.preferred*` 约束。这些应进入 ES 的 schema/validator，而不是只留在提示词中。
- ES 仍保留 ScreenSpec v3 作为 canonical 协议；PrefabXML 只能作为外部实现校准，不引入第二套
  项目权威格式。

### Design-tool/MCP/runtime evidence pattern

- CEDEC 2026 demo 的有效部分是串联而非单个工具：Figma 负责视觉设计和 Auto Layout，MCP 负责读取
  设计源，UXML/USS 负责可读的 Unity 表达，MCP/CLI 负责编译和截图，最后仍需要人工复核。它把
  设计质量和 Unity 证据分成连续关卡。
- AI Rebuilder Harness 进一步把截图分析审批、无文字素材生成、资产锁、不可覆盖版本和 graphics-
  enabled Unity capture 分开；这比“生成一个 PNG/Prefab 路径”强，因为每一步有 stale/version/hash
  条件。
- 共同限制是它们没有从截图可靠恢复 Unity 父子约束、业务语义或全部响应式行为；ES 必须把检测框
  降级为候选 MeasurementEvidence，并要求结构化 IR、profile/state 和 Unity 重读快照。

## ES adaptation decisions

1. 将 packet/IR/source-map/readiness 分别映射到 `ScreenSpec`、`AssetManifest`、`LayoutPlan`、
   `BehaviorSpec`、`Fixture Matrix` 和 `Materializer`；不新增第三套中间权威。
2. 所有参考图到 Prefab 的路线必须保留输入 hash、稳定节点/组件 ID、字段损失、warnings 和
   可重放的 profile/state；没有这些信息时只能输出候选或 `Blocked`。
3. Flow/Prototype 只提供可观察的交互候选；库存、经济、导航、服务端数据和业务 Guard 必须通过
   ES Bridge/BehaviorSpec 明确接入，不能从屏幕名称或 prototype link 猜测。
4. 视觉比较的 baseline 必须是原始设计输入或经过声明的同一 source，不得用另一个生成后端当真值；
   尺寸重采样、方向翻转、复杂素材和 known-loss 必须在报告中显式留痕。
5. 后端能力矩阵必须逐字段声明 `render`/`approx`/`known-loss`，unknown field 只告警不静默丢弃；
   ES 仍需 Unity Editor、GPU、输入和 Runtime 证据才能提升结论。
6. AI 生成链必须把“设计资产”和“Unity 物化”拆成可复核版本：参考图 hash/测量回执、素材锁与
   fallback、ScreenSpec/LayoutPlan、Unity import、GPU capture 分别记录；任何一层失败都不能用
   下一层的文件存在掩盖。

## Evidence boundary

本快照只证明固定 commit 的公开仓库文件内容和本次架构归纳。它不证明第三方仓库安全、可维护、
与当前 ES 包兼容、拥有商业素材授权或在本项目 Unity 2022.3 中可运行；未执行第三方安装、Unity
导入、PlayMode、GPU capture、Profiler、Player、IL2CPP 或发布验收。

## StaleWhen

任一仓库 commit、许可证、raw 文件哈希、上游协议/Schema、Unity 版本、当前 ES ScreenSpec/
Materializer/Fixture 合同或来源权利状态变化时，本快照及其派生 Knowledge 必须重新读取。
