# 游戏核心五窗口并行任务与文件所有权

## 目的

在不重写攻击、扣血、属性、位移、状态机、对象池和资源运行时的前提下，把现有 ESFramework 能力组装成可验证的 3D 游戏纵切片。本文是窗口分工与交接权威，不替代源码、AIWarnings、AIKnowledge、Skill 或运行时契约。

## 所有窗口的强制前置阅读

每个窗口开始前必须完整阅读并在交付中列出版本/路径：

1. 项目根 `AGENTS.md`。
2. `ES/AISpace/README.md` 与 `ES/AISpace/AISPACE_AUTHORITY.json`。
3. `Assets/Plugins/ES/AIWarnings/` 当前命中的 Start、CurrentStatus、RuleIndex，以及 P0“实际可玩闭环与运行证据”规则。
4. `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml`，再按本窗口对象读取匹配条目的 `requiredReads` 和 SourceRefs。
5. 本窗口绑定的项目 Skill 完整 `SKILL.md`、references、静态验证脚本说明。

阅读门禁：先搜索现有实现和权威入口；发现已有能力时只能调用、配置或验证，禁止另起同名系统、临时替代实现或复制一份契约。

## 五窗口边界

| 窗口 | 负责目标 | 唯一写入范围 | 必读重点 | 明确禁止 | 交付证据 |
|---|---|---|---|---|---|
| W1 可玩入口 | 唯一正式测试场景、启动顺序、GameCore/ResourcePlan 绑定 | `Assets/Scenes/Tests/`（仅指定正式场景）、对应 Scene Builder、必要的 GameCore/ResourcePlan 配置 | `es-gamecore-integration`、`es-editor-tooling`、`CHARACTER_PREFAB_CONTRACT.md`、Scene Builder authority、ResourcePlan/Lease Knowledge | 不写攻击、HP、Stats、Movement；不在场景脚本塞玩法逻辑；不创建第二个启动根 | 场景依赖图、唯一入口、启动失败路径、Builder/Prefab 身份 |
| W2 控制与相机 | 输入、控制权生命周期、现有移动模块和相机接入 | 指定输入配置/绑定文件、控制接入适配文件、相机绑定配置；不得改核心运动算法 | `es-entity-authoring`、`es-entity-prefab-validation`、`ES_CAMERA_RUNTIME_STANDARD.md`、`entity-input-command-runtime`、control ownership | 不直写 Transform/KCC/Entity 状态；不复制输入系统；不改位移核心 | 输入→Writer→AIDomain→Movement→Camera 链路；获取/释放/回池证据 |
| W3 战斗接入 | 现有攻击、武器、命中、伤害协议的调用和场景绑定 | 指定武器/目标 DataInfo、Equipment/Combat 接入配置、非核心测试夹具 | `es-entity-authoring`、`es-api-contract-review`、`aiwarning-p0-projectile-weapon-hotpath.md`、Equipment/Combat Knowledge | 不新写攻击算法；不直接扣血；不修改属性计算；不复制 Combat/HP/Stats | 唯一调用入口图；攻击取消/打断/命中/死亡/重置观察点 |
| W4 资源与内容 | 资源收集、分类、依赖、AssetPackage/Library/ResourcePlan 初始化及 AISpace 外置 | `ES/ResourcePipeline/` 工作产物、指定资源分组元数据、AISpace 对应内容；不得改运行时 Provider 语义 | `es-resource-collection`、`es-resource-pipeline`、`es-resource-publish-audit`、AssetPackage/Library/ResourcePlan Knowledge、`ES/AISpace/README.md` | 不把自定义 JSON当运行时权威；不把 AISpace 当 Provider；不直接改 Consumer/GameCore 权威列表 | 资源分组/依赖/许可证/归属表、可回滚迁移记录、运行时引用清单 |
| W5 验证与证据 | 静态、编译、PlayMode、重置/回池、反馈可观察性和反例矩阵 | `ES/Output/` 证据、测试报告、只读验证脚本结果；不得改玩法源码以迎合测试 | `es-adversarial-review`、`es-release-acceptance`、`es-observability-evidence`、P0 可玩闭环规则、Scene validation guide | 不用日志冒充运行证据；不修核心逻辑；不声称未运行 Unity 的结论 | 分层 receipt：static/compile/PlayMode/presentation/Profiler/Player；未证实项和阻断 |

## 共享写入规则

- 同一文件只有一个窗口拥有写权限；其他窗口只读并提交问题单。
- 正式场景只由 W1 修改；W5 不修改场景来制造通过结果。
- Entity、Combat、Health、Stats、Movement、State、Pool、Provider 核心默认只读。
- 任何发现“已有支持但不可用”，必须报告现有入口、调用条件、失败证据和最小修复范围，不得新建平行实现。
- 各窗口交付必须包含：已读文档、事实/假设、修改文件、验证命令、未证实项、对其他窗口的依赖。
- 合流顺序：W4 先提供资源身份与依赖，W1 完成唯一场景入口，W2/W3 接入现有系统，W5 做最终分层验证；静态盘点可完全并行。

## 统一完成标准

只有在真实 Unity 场景中证明“启动、控制、移动、攻击、命中、状态观察、反馈、死亡、重置、回池、重入”后，才可称为纵切片可玩。静态结构、文档存在或 Prefab 数量均不构成可玩证明。
