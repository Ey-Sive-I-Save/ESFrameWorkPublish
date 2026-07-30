# ES AIWarnings 协作入口

本目录保存 ESFramework 的长期项目约束、架构事实、验收标准和历史交接。它不是产品文档，也不替代当前源码、Unity 验证或工作树检查。

## 阅读顺序

1. 先读 `当前状态（CurrentStatus）.md`，确认编译、验收与正在推进的边界。
2. 改任何代码前，先按任务在 `规则索引（RuleIndex）.md` 找到必读文档。
3. `10_P0最高约束（P0Guardrails）` 是不可绕过的项目规则。
4. 再读对应主题下的架构现状、运行时专项、编辑器工具或验证标准。
5. `80_交接与复盘（Handover）` 只提供上下文；`90_提案与废止（Archive）` 不能当作已实现事实。
6. 最后回读当前源码、检查工作树，并按任务风险完成编译、Unity 或 Player 验证。

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
- 运行时不依赖 `ESAssetLibrary`；正式寻址以 Manifest/Table 和发布 Bundle Index 为准。
- GameCore 只能被内容层引用，禁止反向直接引用 Prefab、GameObject 或场景内容。
- 普通编辑器初始化优先 AssemblyStream；禁止在域重载路径中做全盘扫描和重资源操作。
- 核心热路径在初始化阶段验证依赖，运行时避免重复判空、字符串、LINQ、反射和临时集合。
- `ESGenericLife` 是根对象的通用生命周期组织器；Pool 仅是当前已实现分部。Pool 回调必须遵守 `IESGameObjectPoolLifecycle`，不得恢复全子树 Reset 广播。修改 Pool 前必须阅读 `30_运行时专项（RuntimeOperations）/对象池（Pool）` 与 `Documentation/ES_GENERIC_LIFE.md`。
- `Documentation/DOCUMENTATION_CATALOG.md` 是文档分类唯一入口。历史归档、未来方案、生成报告和待源码复验资料不得替代现行规范或 AIWarnings。
- 不恢复 `EntityAIInputSystemModule`、`EntityInputStateModule` 等旧输入兼容类型；应清理序列化坏引用。
- ES 自有 Unity 菜单根统一为 `【ES】/`。

## 协作边界

- `AIWarnings`：长期事实、架构边界、禁止事项和验收规则。
- `AICommands`：可复制的任务执行协议，定义权限、必读路径和验证方式。
- `AITalk`：会话过程和共识记录，不替代源码验证。
- 交互风格不能授权改代码，也不能覆盖项目安全规则。

维护本目录时，必须在文档顶部明确其状态：现行约束、已实现事实、联调中、待验收提案、历史复盘或已废止。出现冲突时，以 P0 约束、当前源码和最新验收证据为准。
