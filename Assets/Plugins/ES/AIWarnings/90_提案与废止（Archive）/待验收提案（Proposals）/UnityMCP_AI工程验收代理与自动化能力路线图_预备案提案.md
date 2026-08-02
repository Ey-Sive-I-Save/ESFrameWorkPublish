# UnityMCP、AI 工程验收代理与自动化能力路线图预备案

状态：待验收提案 / 预备案 / 未实现。
备案日期：2026-08-03。
适用范围：UnityMCP、Agent Skills、AICommands、AIWarnings、Unity Editor 验收、资源发布与本地证据台账。

## 备案目的

本文件只登记 ESFramework 后续可能建设的 AI 工程能力，防止高价值方向在窗口切换后丢失。它不是开发计划、用户授权、现行架构事实或已交付能力，也不得据此自动修改源码、场景、Prefab、资源、发布配置或远端环境。

任何能力进入实施前，必须重新读取最新 README、CurrentStatus、RuleIndex、命中的 P0、当前源码和对应 AICommand，并由用户明确授权本次范围。

## 总体目标

把 AI 从“读取源码并提出修改”提升为受项目规则约束、能够在 Unity 中采集真实证据、识别序列化风险、执行分层验收并生成可追溯结果的工程代理。

```text
用户目标
  -> AICommand 确定权限
  -> RuleIndex 路由 P0 与专项
  -> Skill 收集最小任务上下文
  -> 安全事务执行修改
  -> UnityMCP 采集真实 Editor 证据
  -> Test Runner / PlayMode / Profiler / Player 分层验收
  -> 生成结构化证据包
  -> 本地台账与 Git 门禁
```

## 候选能力

| 能力 | 预期作用 | 绝对边界 |
|---|---|---|
| Unity 一键验收编排器 | 等待编译、读取 Console、运行指定测试、进入 PlayMode、截图并生成报告 | 不能把低层成功升级为 Profiler、IL2CPP 或发布通过 |
| 序列化健康审计器 | 检查 Missing Script、GUID/fileID、SerializeReference、坏引用、旧路径和稳定身份冲突 | 默认只读；自动修复必须独立授权并提供变更预览 |
| 任务上下文采集器 | 收集 Git 状态、Unity 状态、选中对象、场景、Prefab、程序集、规则与相关源码 | 只读取最小权威集合，禁止递归吞入全部 AIWarnings 或项目源码 |
| Prefab 契约验证器 | 验证 Entity、Profile、DataInfo、组件唯一性、挂点、池化和资源引用 | 不得凭模板猜测业务身份或自动覆盖人工配置 |
| 资源发布数字流水线 | 编排 Bake、Plan、Build、Publish、Upload、Root Manifest 与回滚证据 | Root Manifest 必须最后切换；真实上传和删除必须单独授权 |
| 性能预算回归 | 对比 GC Alloc、主线程耗时、加载延迟、资源峰值和对象池命中率 | 必须基于同场景、同平台、同采样条件，禁止跨环境伪比较 |
| ReloadDomain 泄漏检测 | 检查静态缓存、事件订阅、EditorApplication 回调和预览对象释放 | 不能仅凭反射命中宣称泄漏，必须结合生命周期证据 |
| 场景与 Prefab 语义 Diff | 展示层级、组件、引用和序列化字段变化 | 不以 YAML 文本顺序变化冒充语义变化 |
| 安全事务与回滚 | 修改前建立范围快照，失败时恢复本次场景、Prefab 或 SO 修改 | 不得覆盖任务外用户改动；回滚目标必须精确验证 |
| 证据追踪图 | 关联用户需求、AICommand、P0、源码、测试、Unity 证据和发布结果 | 图中存在节点不代表验收通过；每条结论必须保留证据等级 |

## 建议优先级

### 第一阶段：只读采证

1. 任务上下文采集器。
2. 序列化健康审计器的只读扫描。
3. Unity 编译、Console 与指定 Test Runner 的证据归档。

第一阶段不得自动修改场景、Prefab、SO 或发布资源，先证明采集结果稳定、可重复且不会扩大上下文。

### 第二阶段：受控验收

1. Unity 一键验收编排器。
2. Prefab 契约验证器。
3. ReloadDomain 泄漏检测。
4. 场景与 Prefab 语义 Diff。

每项必须支持明确输入、超时、取消、失败状态和机器可读输出，不能只返回“成功”字符串。

### 第三阶段：高风险工程自动化

1. 安全事务与精确回滚。
2. 性能预算回归。
3. 资源发布数字流水线。
4. 跨任务证据追踪图。

第三阶段涉及资产写入、长时间运行、远端环境或发布状态，必须建立更严格的授权、隔离区和恢复策略。

## ES 专项切入点

- GameCore 根 SO、RuntimeData 与事务重注入闭环检查。
- ResourcePlan、Consumer、Library、Manifest、Provider 与 Scope 依赖图。
- Entity、角色 Prefab、DataInfo、挂点和池化契约检查。
- Input Action、Profile、RuntimeMode 与玩家消费端全链路验证。
- ESGameTag、ConfigKey、Catalog、BakeTable 与 RuntimeKey 稳定身份审计。
- ESCommand Player、Runner、Start/Stop 与未结束实例诊断。
- 对象池租借、归还、跨作用域持有和迟到 Lease 检测。
- 发布 Manifest、Bundle Hash、远端文件和 Root 切换一致性验证。

## 开工前置条件

任一能力进入实现前必须同时满足：

1. 用户明确选择本次能力和允许写入范围。
2. 存在匹配的 AICommand；若不存在，先单独评审命令合同。
3. RuleIndex 能路由到对应 P0、专项规则和证据标准。
4. 明确输入、输出、超时、取消、回滚和失败语义。
5. 明确证据等级，不把 `.csproj`、Editor、Test Runner、PlayMode、Profiler、IL2CPP 和发布相互替代。
6. 默认只读；涉及场景、资产、远端上传、删除或发布时再次取得明确授权。
7. 先以一个最小真实任务前向验证，再考虑扩展为通用 Skill 或 Plugin。

## 禁止事项

- 禁止看到本提案后直接生成十个“已实现”Skill。
- 禁止为了自动化而绕过 AICommand、P0、Undo、Git 工作树或文档门禁。
- 禁止把 UnityMCP 工具可调用解释为用户已授权修改。
- 禁止自动上传正式 Bucket、切换正式 Root Manifest 或删除远端版本。
- 禁止在域重载路径递归扫描全部 Assets、Packages、AIWarnings 或磁盘。
- 禁止把截图、Console 清洁或单次 PlayMode 写成商业发布已通过。

## 未来验收要求

每个落地能力至少需要：

- 一个明确的 AICommand 或用户授权合同；
- 一个最小真实任务的成功与失败样例；
- 严格 UTF-8、工作树影响和取消/超时验证；
- Unity 实际运行证据，而非仅有模拟返回值；
- 已知缺口、误报/漏报边界和恢复方式；
- 在 CurrentStatus 中按真实证据登记为已实现、联调中或待验收。

在上述条件满足前，本文件始终只是一份预备案提案。
