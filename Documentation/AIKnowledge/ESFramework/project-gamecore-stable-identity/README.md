# GameCore 稳定身份候选知识包

`PackageId`: `esframework.project-gamecore-stable-identity`
`Status`: `Candidate / Unregistered`
`Authority`: `Derived navigation only`
`UnityVersion`: `2022.3.45f1 (a13dfa44d684)`
`EvidenceLevel`: `S1 / runtime-not-run`

本目录只整理 GameCore 根 SO、RuntimeData、ConfigKey、RuntimeKey、Catalog 与内容注册的当前事实。本轮已把三个详细条目登记到共享 `KnowledgeIndex.yaml`；登记只提供发现路由，不替代 SourceRef 新鲜度校验或 Runtime 证据。

## AI 强制阅读协议

本包不是“读一篇摘要即可动手”的授权。AI 必须按任务选择最小闭包，并在修改前回读所列 SourceRefs：

1. 先读本 README，确认候选状态、权限和停止条件。
2. 任何 GameCore 任务先读 `gamecore-root-runtime-data.md`。
3. 涉及 Key、Catalog、查表、存档、网络或热路径时再读 `configkey-runtimekey-catalog.md`。
4. 涉及创建、注册、迁移、Consumer、Bake、Inspector、窗口、MCP 或 C# 自动化写入时再读 `content-registration-transaction.md`。
5. 校验所读条目的每个 SourceRef 当前 SHA-256。任一缺失或漂移，立即把条目标为 stale，停止基于该条目设计，回读当前 P0 与源码。
6. 最后检查当前 branch、HEAD、工作树和用户授权范围；选用受管通道时再检查匹配 AICommand。Knowledge 只导航事实，不授予 AI 自行扩大源码、资产、Unity、Git、发布或外部执行范围。

只读一个领域条目而跳过本 README，或只读本 README 而不读领域 SourceRefs，都不算完成上下文加载。

## 四类身份先分层

| 问题 | 正确身份 | 明确禁止 |
|---|---|---|
| Group、SO 表格、策划命名和编辑器定位 | `SoDataInfo.KeyName` | 运行时查表、ConfigKey fallback、存档、网络 |
| 跨资产、版本、存档、网络或外部协议的业务身份 | 带 Scope 的 EnumKey/StringKey | 裸字符串猜测、跨类型表混查、静默归一化 |
| 精确定位主资产或子资产 | GUID + LocalFileId + type | 用显示名、路径、当前选择或首个同类型对象代替 |
| 当前进程、当前强类型表内的热路径槽位 | runtimeKey | 持久化、联网、跨表、跨进程或 `Ready=false` 时访问载荷 |

如果一个字段同时承担上述两类身份，设计即不合格；不得用兼容 fallback 掩盖。

## 全局停止条件

出现以下任一情况时，AI 必须停止写入并报告，不得自行猜测：

- SourceRef 缺失、哈希漂移、P0 与源码矛盾，或当前工作树存在目标文件重叠修改。
- 不清楚对象属于独立内容定义、实例状态、编辑器组织键、资产身份还是运行时槽位。
- 不清楚调用方属于普通 Consumer、领域 Table 作者、底层 Table 扩展者还是编辑器注册入口。
- 试图用 `KeyName`、路径、名字、InstanceID、注册顺序或旧 runtimeKey 补全身份。
- GameCore 根、嵌套配置或 RuntimeData 将反向直接持有 Prefab、GameObject、Component 或场景对象。
- 正式可枚举 `SoDataInfo` 没有匹配 Group、没有唯一主 Group，或准备把通用 Pack 当成默认聚合/资源发布包。
- commit 没有当前 Unity Editor 进程内成功 preview 的同一 `requestId`，或 expected identity/revision/current key 不完整。
- 目标 Dirty、revision/identity/key 冲突、Bake 进行中、非 Editor 主线程、注册锁繁忙或 Domain Reload 后复用旧资格。
- 没有真实 Unity/Test/MCP/Player 证据却准备声称已运行、可玩、可发布或 MCP 客户端可用。

## AI 输出最低格式

AI 在提出方案、修改或验收结论时，至少明确：

```text
对象分类：内容定义 / 实例状态 / 编辑器组织 / 资产身份 / 运行时槽位
稳定身份：Scope + EnumKey/StringKey，或说明为何仅为局部键
资产身份：GUID + LocalFileId + type，或不适用
所有者与入口：Consumer / 领域 Table / 底层 Table / ESContentRegistrationAuthoring
生命周期：创建、提交、Ready、清理、重建
失败处理：冲突、异常、Dirty、并发、重试如何 fail-closed
验证证据：S1 源码 / 编译 / Unity / Test / Player / MCP，逐项区分
未验证项：明确列出 runtime-not-run 或其他缺口
```

缺少其中任何与任务有关的字段，不得给出“完成”“安全”“可用”或“商业级”结论。

## 条目

- `gamecore-root-runtime-data.md`：根 SO 单向依赖、Info/Group/Pack 与 RuntimeData 稳定外壳。
- `configkey-runtimekey-catalog.md`：稳定业务身份、确定性 Catalog 与进程内 RuntimeKey。
- `content-registration-transaction.md`：统一内容注册的 preview/commit、CAS、回滚与幂等边界。

## 执行边界

- 已读取 AIBrain 入口、KnowledgeIndex、AIWarnings Start 链和命中的 P0 原文。
- 当前 AICommand Catalog 没有 AIKnowledge 受管写入的匹配命令，记为 `NoMatchingCommand`；它只影响受管通道。本目录写入以当前用户明确目标为授权来源。
- 仅做源码、规则、测试定义与 Unity 官方文档的静态核对；没有运行 Unity、EditMode、PlayMode、Profiler、Player 或发布验收。
- 首验后仓库 HEAD 从信封记录的 `023eaa0268ff447d2b74b64fb2a6345a6693c92d` 漂移到读取时的 `a31d58c740210f79eb346415168d7ba425037564`。本包各 SourceRef 均在当前工作树重新计算哈希，不沿用 HEAD 作为事实替代品。

## 注册前要求

本包已在当前用户明确授权下接入正式发现链；后续任一 SourceRef、目录条目或 HEAD 相关事实漂移，都必须将对应索引计划标为 stale 并重新核对。
