# ES AIWarnings 协作入口

本目录保存 ESFramework 的长期项目约束、架构事实、验收标准和历史交接。它不是产品文档，也不替代当前源码、Unity 验证或工作树检查。

## 阅读顺序

1. 先读本入口，再读 `当前状态（CurrentStatus）.md`，确认编译、验收与正在推进的边界。
2. 改任何代码前，按任务在 `规则索引（RuleIndex）.md` 找到命中的 P0 与专项规则。
3. 读取命中的 `10_P0最高约束（P0Guardrails）` 原文；P0 不可绕过，也不能由旧摘要替代。
4. 再读当前任务对应的架构现状、运行时专项、编辑器工具或验证标准原文。
5. 只有当前问题需要决策背景或失败原因时，才读取直接关联的 `80_交接与复盘（Handover）`；只有评审提案或废止方向时，才读取 `90_提案与废止（Archive）`。
6. 最后回读当前源码、检查工作树，并按任务风险完成编译、Unity 或 Player 验证。
7. 如果任务匹配项目 Skill，读取 `.agents/skills/<skill>/SKILL.md`；Skill 只提供执行能力，不能扩大 AICommand 或用户授权。

## 上下文加载规则

禁止把“开始 ES 任务”理解为递归读取全部 AIWarnings。当前目录包含大量跨领域规则、历史复盘和提案；一次性全量加载会挤占源码、工具输出与推理空间，并稀释真正命中的 P0。

固定加载链路为：

```text
README
  -> CurrentStatus
  -> RuleIndex
  -> 命中的 P0 原文
  -> 当前领域专项原文
  -> 直接关联的交接/复盘
  -> 必要时才读历史与提案
```

- 普通任务通常把规则读取控制在约 1～2 万字符；复杂跨系统任务通常为 2～5 万字符。这只是上下文预算建议，不是截断必读规则的硬限制。
- 真正标准是读取完成当前任务所需的最小权威规则集。P0、现行状态和任务专项必须读取原文，不能只依赖旧摘要、搜索片段或其他 AI 的转述。
- `80_交接与复盘（Handover）`、`90_提案与废止（Archive）` 默认不得全量加载。
- 跨系统任务按领域分批读取；每批只保留规则路径、状态、结论、禁止事项和证据入口，再进入下一领域。摘要用于导航，不能冒充原文权威。
- 全项目治理审计允许分批遍历，但不得一次把全目录塞入上下文，也不得把分批摘要写成“全部规则已经逐条复核”。
- AIWarnings 必须由 `规则索引（RuleIndex）.md`、明确的 AICommand 或任务命中关系路由；禁止无目标递归扫目录后凭关键词自行扩大任务范围。

## 目录状态

| 目录 | 用途 | 读取优先级 |
|---|---|---:|
| `10_P0最高约束（P0Guardrails）` | 编码、身份、GameCore、资源、编辑器生命周期、性能和构建硬约束 | 最高 |
| `20_架构现状（Architecture）` | 当前 Entity、输入、状态机、GameManager 等职责边界 | 高 |
| `30_运行时专项（RuntimeOperations）` | Pool、Item、Shot、物理与运动专项 | 按任务 |
| `40_编辑器与工具（EditorTooling）` | 预览、窗口、SO 表格、工具与资产包工作流 | 按任务 |
| `50_验证与发布（ValidationRelease）` | PlayMode、资源计划与发布验收标准 | 验收必读 |
| `80_交接与复盘（Handover）` | 历史上下文、失败复盘、项目交接 | 参考 |
| `90_提案与废止（Archive）` | 待验收方案和已废止方向 | 不作为现行事实 |

## 当前强制结论

- 所有文本文件统一使用 UTF-8；禁止默认代码页覆写和机械转码。
- RuntimeKey 仅在当前进程、当前强类型表生命周期内有效，禁止持久化。
- Tag 使用 `ESTagCollection` 的 Host SetTag 与 Lease/LeaseSet 所有权模型；禁止恢复无来源 Add/Remove API、第二套 Tag event 或旧 Tag 容器。
- 运行时不依赖 `ESAssetLibrary`；正式寻址以 Manifest/Table 和发布 Bundle Index 为准。
- 资源加载必须区分 Resident、Owner Scope、Temporary 引用计数与独立 Lease；业务不得销毁全局 `TemporaryScope`。
- 查询 GameManager Module 使用 `TryGetModule<T>()`，仅明确初始化时使用 `GetOrCreateModule<T>()`；旧 `GetModuleFast<T>()` 不得恢复。
- GameCore 只能被内容层引用，禁止反向直接引用 Prefab、GameObject 或场景内容。
- 普通编辑器初始化优先 AssemblyStream；禁止在域重载路径中做全盘扫描和重资源操作。
- 核心热路径在初始化阶段验证依赖，运行时避免重复判空、字符串、LINQ、反射和临时集合。
- 测试场景的操作引导、验收路线、运行态诊断、键位说明和区域导视，优先复用 `ESSceneValidationGuide`；它只属于测试场景，禁止以一次性 OnGUI 或测试 MonoBehaviour 污染正式角色、载具、相机与技能 Prefab。具体路由见 `50_验证与发布（ValidationRelease）/测试场景验收（SceneValidation）/测试场景导视与诊断复用_AI协作警告.md`。
- 多来源申请、集中决策、单点执行的领域必须遵守 `ES 活跃请求仲裁协议`：`Request -> Lease -> Active Set -> Arbitration -> Commit -> Executor`。统一协议不等于建立万能管理器；Camera 的 Director 首切片已存在，但仍待 Unity/PlayMode/Profiler 验收，不能按已交付使用。
- `ESGenericLife` 是根对象的通用生命周期组织器；Pool 仅是当前已实现分部。Pool 回调必须遵守 `IESGameObjectPoolLifecycle`，不得恢复全子树 Reset 广播。修改 Pool 前必须阅读 `30_运行时专项（RuntimeOperations）/对象池（Pool）` 与 `Documentation/ES_GENERIC_LIFE.md`。
- `ContextPool` 是明确宿主拥有的局部可变上下文；宿主结束必须 `ClearAllRuntimeValues()`。它不得替代 Tag、Stat、Permit、Resource Scope 或跨对象 Lease。
- `ESCommandPlayerRunner.TickAll()` 只能由 `MODULE_ESCommandModule` 驱动；ESCommand 运行时、交互运行时、编辑器序列化与 GraphView 均有独立专项规则，不能用输入文档、AI Command 模板或 SimpleTools 文档替代。
- AI 协作历程只有在用户明确要求时才能创建、更新或恢复；普通任务禁止自动落账。连续约 10 轮后 AI 只能询问一次，用户确认前不得写入或催促。获准维护时仍严格一窗口一文件；失联窗口先从本机 `history.jsonl` 模糊定位 session 候选，再人工确认归属并从 `rollout-*.jsonl` 逐轮恢复。候选分数不得直接授权合并或覆盖已有档案。
- 当前 GraphView / NodeRunner 是阻断性历史实验实现，禁止新增任何正式业务依赖，直至数据模型、稳定身份、Undo、迁移与运行时快照完成重构验收。
- 模块成熟度统一使用 `Proposed -> Scaffolded -> Experimental/Implementing -> Integrating -> Verifying -> Stable -> Deprecated -> Archived`；`Blocked` 只能作为附加结论。目录、接口或源码存在不等于完成，半成品不得默认注册、渗透稳定模块或进入正式发布链路。具体路由见 `20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md`。
- `Documentation/DOCUMENTATION_CATALOG.md` 是文档分类唯一入口。历史归档、未来方案、生成报告和待源码复验资料不得替代现行规范或 AIWarnings。
- 不恢复 `EntityAIInputSystemModule`、`EntityInputStateModule` 等旧输入兼容类型；应清理序列化坏引用。
- ES 自有 Unity 菜单根统一为 `【ES】/`。
- 项目级 Agent Skills 位于 `.agents/skills`。当前基础层包含 `es-use-ai-command`、`es-unity-compile`、`es-fix-compile-error`、`es-utf8-guard`、`es-worktree-audit`；跨系统治理层包含 `es-module-lifecycle`；ES 领域层包含 `es-gamecore-integration`、`es-resource-pipeline`、`es-tag-config`、`es-entity-authoring`、`es-input-action`、`es-command-authoring`、`es-editor-tooling`、`es-release-acceptance`。Skill 不进入 Unity `Assets`，不生成 `.meta`，不属于运行时或发布内容。
- 项目级 AI 文件夹归属、Skill 内部结构和 14 个 Skill 的简介统一见 `.agents/README.md`；不得在其他目录另建重复 Skill 清单。

## 协作边界

- `AIWarnings`：长期事实、架构边界、禁止事项和验收规则。
- `AICommands`：可复制的任务执行协议，定义权限、必读路径和验证方式。
- `Agent Skills`：项目级可复用执行能力，保存于 `.agents/skills`；可以调用脚本和 UnityMCP，但不能自行授予写权限或覆盖证据边界。
- `AITalk`：会话过程和共识记录，不替代源码验证。
- 交互风格不能授权改代码，也不能覆盖项目安全规则。

维护本目录时，必须在文档顶部明确其状态：现行约束、已实现事实、联调中、待验收提案、历史复盘或已废止。出现冲突时，以 P0 约束、当前源码和最新验收证据为准。

Agent Skills 与 AICommands 的完整映射和后续扩展边界见：

```text
20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md
```
