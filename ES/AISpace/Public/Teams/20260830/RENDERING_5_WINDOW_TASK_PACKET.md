# 渲染与画面五窗口任务接取包

## 接取规则

五个窗口先进入深度阅读准备态。未完成必读项、权威入口核对和重复实现排查前，不得接取实现任务。所有窗口默认只读；本包不授权修改 Shader、Camera、VFX、Audio、Animation、UI 或运行时核心。

所有窗口共同必读：

- `AGENTS.md`；
- `ES/AISpace/README.md`、`ES/AISpace/AISPACE_AUTHORITY.json`；
- AIWarnings Start/CurrentStatus/RuleIndex 与命中的 P0 规则；
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml` 及匹配条目的 `requiredReads`/SourceRefs；
- 本窗口绑定 Skill 的完整 `SKILL.md` 和必要 references；
- `Documentation/CHARACTER_PREFAB_CONTRACT.md` 与 `Documentation/ES_CAMERA_RUNTIME_STANDARD.md`（W1-W5 均需按范围回读）。

## 窗口定义

### W1 渲染架构与质量档

必读：`es-performance-budgeting`、`es-resource-pipeline`、`es-unity-compile`、Composite Shader/Material 契约、rendering-batching-evidence。

核对：`GraphicsSettings.asset`、`QualitySettings.asset`、URP 三档、RendererFeature、Volume、`ESCompositeShaderParameters.cs`、SRP Batcher、Shader Variant。

职责：确认当前生效质量档、候选档差异、唯一渲染参数权威和成本变量。

禁止：新增渲染管线、万能 Shader、平行参数表、凭配置宣称性能。

交付：事实路径、权威关系、档位基线、性能假设、验证矩阵、未证实项。

### W2 相机与角色表现接入

必读：`es-entity-authoring`、`es-entity-prefab-validation`、`es-editor-tooling`、相机运行时 Knowledge、Prefab Contract。

核对：`ESCameraModule`、`ESCameraDirector`、CM2 Adapter、SceneBinding、Animator/Playable/State、FinalIK、EntityTransformMapping、Socket/Mount。

职责：确认相机 Request/Lease 仲裁、角色视觉绑定、镜头与角色表现的合法接入点。

禁止：直写 VCam、Follow、LookAt、Priority；运行时 Find/自动造 Socket；重写位移、动画或 IK 核心。

交付：调用链、Prefab/场景所有权、生命周期、失败路径和 PlayMode 观察点。

### W3 VFX/Audio/Animation 运行表现

必读：`es-entity-authoring`、`es-resource-pipeline`、VFX/Audio Runtime Knowledge、Playable State Machine、AIWarnings 热路径规则。

核对：Operation 08–11、`ESVfxInfo/Group`、`ESAudioModule`、`ESVfxAudioEmitter`、Playable Clip、Handle、Scope、回池。

职责：把现有状态/事件映射到已有表现入口，定义成功、命中、受击、死亡、取消、打断、回池反馈。

禁止：新建 VFX/Audio/Animation 业务系统；直接改 HP、Stats、Combat；让 VFX 抢占 Camera/Light/Audio 写权。

交付：状态→表现映射表、生命周期、资源缺失/Scope 泄漏风险和验证信号。

### W4 UI/HUD 与画面可读性

必读：`es-ui-prefab-authoring`、`es-editor-tooling`、UI Visual Design System、ScreenSpec v3、UI Root/Window Knowledge。

核对：`ESUIRootCoordinator`、Window Lease、HUD/Page/Modal/Popup/Toast/System 六层、ScreenSpec、Token、LayoutPlan、Materializer。

职责：定义状态观察、信息层级、战斗可读性、遮挡预算、失焦/重载/池化行为。

禁止：另造 HUD 管理器、直接读取或写入 Combat/HP/Stats；用颜色单独表达状态；在 Token 链未闭合时直接生产完整 DTCG Token。

交付：HUD 观察契约、布局/状态矩阵、可读性规则、UI 生命周期和截图验收点。

### W5 创意导演、资源投影与证据验收

必读：`es-first-principles-analysis`、`es-adversarial-review`、`es-resource-collection`、`es-resource-publish-audit`、`es-observability-evidence`、渲染证据契约。

核对：现有风格规范、候选材质/Shader/VFX/Audio/动画/UI 资产、来源/许可证、AISpace 归属、Frame Debugger/Profiler/截图证据边界。

职责：提出风格候选（写实电影/高对比科幻/二次元/暗黑战术）、镜头与构图变量，并将候选映射到现有 Definition/Operation/ScreenSpec；制定静态、Unity、PlayMode、Profiler、Player 分层验收。

禁止：未经证据替换稳定系统；把实验室资产当正式接入；把截图/日志当运行证明；把候选资产直接写入正式 Assets。

交付：创意变量表、资产 provenance/license/哈希、投影清单、单变量对照方案、证据矩阵和风险反驳。

## 接取回执格式

每个窗口接取任务时必须返回：

1. 已完整阅读的文件与 Skill；
2. 已确认的现有权威入口；
3. 发现的重复实现或越界风险；
4. 本窗口只读范围和潜在写入范围；
5. 事实、假设、未知项；
6. 计划使用的验证命令与 `runtime-not-run` 声明。

只有收到五份接取回执后，才进入方案设计；任何窗口不得先行修改。
