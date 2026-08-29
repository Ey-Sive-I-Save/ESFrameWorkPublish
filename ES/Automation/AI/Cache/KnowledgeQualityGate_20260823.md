# ESFramework AIKnowledge 生产系统质量整改门禁

你不是在扩写文档，而是在修复 ESFramework 的 AIKnowledge 生产系统。

本轮目标：系统性修复当前知识库的四类问题：

1. 新知识不可发现或路由不准确；
2. 内容像给人看的说明，而不是给 AI 执行决策用的知识；
3. 条目之间重复、摘要重复、SourceRef 重复、边界重叠；
4. 没有真实降低 AI 的易错、漏步骤、误用 API、越权和证据夸大。

## 必须先读取

- 项目根 `AGENTS.md`；
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`；
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`；
- AIWarnings Start、CurrentStatus、RuleIndex；
- 本任务 routeKeys 命中的 1～3 个 Knowledge 条目及其 requiredReads；
- 相关 AICommand、Skill、测试和当前源码。

使用以下 Skill 的工作方式：

- `es-knowledge-creator`：控制输出范围、Authority、EvidenceLevel、SourceRef、ContentHash、StaleWhen；
- `es-ai-knowledge-curation`：维护索引、路由、来源闭环和条目边界；
- `es-knowledge-validator`：验证索引、路径、哈希、requiredReads、relatedSkills 和重复 KnowledgeId；
- `es-adversarial-review`：检查知识是否真的能降低错误，是否存在遗漏、误导或越权。

## 一、先做诊断，不要立即改写

先输出 `Knowledge Repair Diagnosis`，逐条列出：

- 不可发现条目；
- routeKeys 过宽、过窄、错误或缺失；
- requiredReads 缺失、过多或重复；
- relatedSkills 错绑；
- SourceRef 缺失、漂移或权威等级错误；
- ContentHash 不一致；
- Authority/EvidenceLevel 夸大；
- 与其他条目重复的事实；
- 只有描述、没有执行决策价值的段落；
- 没有覆盖的 AI 易错场景。

不要因为条目存在、源码存在或测试文件存在，就声称功能已经验证。

## 二、把知识改造成 AI 可执行知识

每个详细条目必须至少包含以下结构。

### 1. Scope

- 本条目负责什么；
- 明确不负责什么；
- 与哪些条目分界。

### 2. Trigger and routing

- AI 可能使用的自然语言触发词；
- 精确 routeKeys；
- 预期命中的 1～3 个条目；
- 可能误命中的相邻路由；
- 误路由时的回退策略。

### 3. Decision rules

- 什么时候可以继续；
- 什么时候必须先读取额外来源；
- 什么时候必须停止；
- 什么时候必须标记 stale、Deferred 或 Blocked；
- 什么时候必须请求 AICommand、TaskContract 或真实运行证据。

### 4. Verified facts

- 只写 SourceRef 能证明的事实；
- 每条事实绑定来源；
- 区分源码事实、AIWarnings 规则、官方文档、测试定义和真实运行证据。

### 5. Common AI failure modes

对每个易错点写清：

- 错误行为；
- 典型症状；
- 根因；
- 预防检查；
- 正确替代动作；
- 失败后的恢复动作；
- 仍缺少什么证据。

### 6. Execution checklist

- 开始前检查；
- 实施中检查；
- 完成后检查；
- 不可跳过的后置验证；
- 明确禁止事项。

### 7. Evidence boundary

- Static 可以证明什么；
- Runtime 尚未证明什么；
- 不得把 S1/S2 静态知识写成 Unity、PlayMode、Profiler、Player、IL2CPP 或发布已通过。

## 三、严格消除冗余

执行“一事实一归属”规则：

- 一个稳定事实只能有一个 canonical Knowledge owner；
- 其他条目只保留链接、适用条件和差异，不复制整段摘要；
- 相同 SourceRef 不等于相同知识，应按责任边界拆分；
- 相同 routeKeys 但不同决策对象时，必须明确区分；
- 旧条目不能静默覆盖，冲突事实必须保留并标记；
- 不要为了显得完整，把 AIWarnings、源码或官方文档大段复制进 Knowledge；
- 不要建立“Unity 总览”“项目总览”“万能 AI 指南”类包办条目。

为每组重复条目输出：

- canonicalEntry；
- duplicateEntries；
- 保留内容；
- 删除或压缩内容；
- 交叉链接方式；
- 不可合并的理由。

## 四、让知识真正降低 AI 错误率

不要只写“应该注意”，必须把规则写成可检查的约束。

重点覆盖：

- 忘记读取 AIWarnings、CurrentStatus、RuleIndex；
- 没有按 routeKeys 发现 Knowledge；
- 一次加载过多条目；
- 把 Knowledge 摘要当源码事实；
- SourceRef 哈希漂移仍继续使用；
- 把静态检查当成 Unity 运行验证；
- 忘记稳定身份、Owner、生命周期、Undo、Dirty、Save、Rollback；
- 忽略 Domain Reload、Prefab、序列化、AssetDatabase 或运行时所有权；
- 只创建 UI、Prefab 或 Graph 外观，没有验证正式资产；
- 忘记失败路径、取消、重复执行、恢复和幂等；
- 把“文件存在”“按钮存在”“测试源码存在”当成执行成功；
- 没有匹配 AICommand 却扩大权限；
- 把 `PlanTaskUnavailable` 误判成 `NoMatchingCommand`；
- 把临时扫描、旧上下文或旧快照写成长期事实。

## 五、验证可发现性

至少设计并执行一组路由探针：

- 10 个真实用户自然语言任务；
- 每个任务的预期 routeKeys；
- 预期命中的 1～3 个 Knowledge；
- 实际命中结果；
- 是否零命中；
- 是否过宽命中；
- 是否误命中；
- requiredReads 是否足够；
- 是否把无关条目带入上下文。

任何零命中、过宽命中或错误命中，都必须形成修复建议，不得只报告“索引存在”。

## 六、输出格式

最终输出必须包含：

1. `Diagnosis`
2. `Routing repair table`
3. `Canonical ownership and deduplication table`
4. `AI failure-prevention matrix`
5. `Proposed entry/index changes`
6. `SourceRef and hash plan`
7. `Validation plan`
8. `Unproven claims and runtime-not-run`
9. `Blocked items`
10. `Next bounded batch`

默认只输出 route-pack 和修复计划。

只有用户明确要求时，才写入详细条目或 `KnowledgeIndex.yaml`。

每批限制：

- 一个功能域；
- 最多 1～3 个条目；
- 明确允许修改的文件；
- 明确最大上下文预算；
- 明确停止条件；
- 修改前后必须重新运行 UTF-8、SourceRef、ContentHash、Index 和相关 Skill 验证。

## 禁止事项

- 全量递归读取全部 Knowledge；
- 为了通过校验而修改源码事实；
- 伪造 SourceRef、ContentHash、EvidenceLevel 或 Runtime 证据；
- 将临时判断写成长久事实；
- 删除冲突条目；
- 修改 Git、历史、审计、Unity、发布或外部状态；
- 在没有权限合同和用户授权时执行高风险操作。

## 成功标准

成功标准不是“新增了多少文字”，而是：

- AI 能否稳定找到正确条目；
- AI 能否根据条目采取下一步动作；
- AI 是否更少遗漏前置条件；
- AI 是否更少误用 API、资产、生命周期和权限；
- AI 是否能在证据不足时主动停止；
- 同一问题是否不再需要重复读取多个冗余条目。

本门禁的核心是把 Knowledge 从“说明文档”转成“路由 + 决策 + 错误预防 + 证据边界”的执行产品。
