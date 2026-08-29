# 项目最高警告 P0：编辑器交付体验与下一步可发现性

Status: current
StableId: es.aiwarning.p0.editor-delivery-next-step-discoverability.v1
Authority: AIWarnings（长期 P0 约束）；详细交互与证据见 Knowledge
RouteKeys: aiwarnings, p0, editor, delivery, discoverability, next-step, recovery
Applicability: EditorWindow、Inspector、弹窗、向导、诊断面板、工具栏、AI/自动化产物和结果文件
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-editor-delivery-next-step-discoverability.md
StaleWhen: UI 入口、产物路径策略、交互状态、恢复协议或任一 SourceRef 哈希变化。

## P0 长期约束

- 完成标准覆盖配置、使用、查询、交付、恢复五阶段；每阶段必须显示可理解状态、就近操作和明确下一步，允许合理跳步、返回、取消、重试，不强迫无关线性流程。
- 首屏优先当前状态、用户目标、关键结果和主操作；实现细节、日志和 JSON 延迟展开。状态必须区分成功、失败、部分成功、等待输入、加载、权限不足和不可用，不能只靠颜色或空白/红色日志。
- 面向用户继续处理的主产物必须提供快速打开、项目窗口定位、打开报告、复制路径或等价入口；只在 Console/聊天/日志打印路径不算交付。路径、文件名、任务 ID、报告 ID 和窗口标题必须可互相对应。
- 打开/定位动作只允许本次任务声明的输出根或明确项目内安全路径；禁止借快速打开访问任意路径、凭据、系统目录、外部网络、临时缓存或不稳定时间戳路径。
- 失败、部分成功和待输入状态必须显示原因、影响范围、当前状态和最小恢复动作；窗口重开、域重载、编辑器重启或中断后应可恢复最近明确状态，并提示过期/漂移依赖。
- 长路径、哈希、SessionId、异常堆栈和 JSON 支持完整复制与详情展开；表格列名、单位、状态含义和排序依据明确；窄屏、高 DPI、不滚动首屏和无横向滚动仍保持主路径可见。
- 不得以自动弹窗、全局抢焦点、未授权外部程序或作者熟悉的内部目录/实现顺序替代可发现入口；不要求用户手工搜索项目树或重复 AI 已知的路径筛选。

## Knowledge 导航

完整五阶段交互、信息密度、状态/恢复合同、产物入口与路径安全验收见 `es.aiwarning.p0.editor-delivery-next-step-discoverability.v1`。本 Warning 不授予 UI、文件或外部程序操作权限。
