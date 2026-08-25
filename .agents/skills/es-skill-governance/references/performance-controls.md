# Skill performance controls

## Authority and scope

本文件是 `es-skill-governance` 对所有 Project Skill 的统一运行性能约束。它约束执行方式和证据，不授权 AI 自行扩大读、写、Unity、Git、发布、删除或外部网络范围。当前用户明确指令决定动作授权；Skill 规则、AIWarnings、AICommand 和 TaskContract 约束实现质量及所选受管通道。

## Execution modes

每次 Skill 运行必须在计划或执行记录中标明一种模式：

- **Fast Path（默认）**：只做命中路由所需的最小读取、用户范围/P0 校验、输入边界检查和必要的局部验证；只有选用受管通道时才校验其 TaskContract。
- **Deep Path（显式）**：用于验收、CI、发布前或用户明确要求的完整检查；允许执行全量扫描、全量 Hash、Graph Bake、Unity 验证、网络适配器和证据复制，但必须声明预算、阶段、超时和证据范围。

Fast Path 不能为了速度省略安全、授权、身份或失败闭环；Deep Path 也不能被 Fast Path 隐式触发。

## Default limits

普通 Skill 调用不得隐式执行以下高成本操作：

1. 扫描全部 Project Skill、全部 `references/`、全部 Catalog 或无关项目目录。
2. 对整个仓库或全部资源重新计算 SHA-256；只哈希计划绑定文件和本次输出，且优先复用路径、大小、修改时间和已有 Hash 缓存。
3. 每次调用重新 Bake/验证完整 Graph；只处理命中任务的图和受影响的节点。
4. 启动 Unity 编译、Domain Reload、Test Runner、Profiler、Player 或 IL2CPP。
5. 同步调用 Feishu/外部 CLI、上传或长时间网络重试。
6. 在目标实现或证据需要之前复制并 Hash 大量无关证据文件。
7. 为每个细粒度事件强制落盘 JSON、时间线或 RunRecord；应在阶段边界、批次边界或终态批量写入。
8. 无上限 FanOut、重复序列化或无界并发；必须有项目级或任务级并发上限。

## Caching and invalidation

- Knowledge 只加载路由命中的最小集合，通常为 1～3 个条目及其 `requiredReads`。
- Skill、Catalog、AIWarnings 和治理 Hash 应按稳定身份缓存；来源路径、大小、修改时间或内容 Hash 变化时使对应缓存失效。缓存只能减少未变化文件的重复发现/读取，不能替代当前用户范围、稳定身份、受管通道 PlanHash 或必需证据校验。
- 缓存命中不能覆盖用户范围、身份、受管通道 PlanHash 或证据失效；失效时重建相关计划或进入 Deep Path，不能静默使用旧结果。

## Deep-path budget and failure

进入 Deep Path 前，计划必须声明目标路径、最大文件数/字节数、最大并发、重试次数、超时、可写范围和停止条件。预算不足、外部服务不可用或 Unity 正被其他受管流程占用时：

- 必需验收不得降级为 Fast Path，必须 `Blocked` 或 `Failed` 并记录原因；
- 可选诊断可以延期，但必须明确标记 `not-run`，不得报告通过；
- 取消、超时、Domain Reload、进程退出和部分失败必须保留可恢复状态。

## Evidence and measurement

每个 Workflow/Engineering Skill 的规模说明至少记录冷启动与稳态成本、项目项数/字节数、并发度、峰值内存、主要 I/O/网络阶段和超时。没有对应平台的实际测量，不得把“缓存命中”“结构上无显式分配”写成零延迟、低 GC、0 GC 或商业级性能承诺。
