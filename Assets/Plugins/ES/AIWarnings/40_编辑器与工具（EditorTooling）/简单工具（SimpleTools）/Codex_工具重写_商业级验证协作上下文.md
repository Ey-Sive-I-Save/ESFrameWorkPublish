# Codex Tool Rewrite Context
Status: historical
StableId: es.aiwarnings.editor.codex-tool-rewrite-context.v1
Authority: ESFramework AIWarnings / historical tool-rewrite context
RouteKeys: aiwarnings, editor, simpletools, codex, runtimewatch, objectpool, validation, history
Applicability: ES 编辑器 SimpleTools、RuntimeWatch、ObjectPool 与工具重写验收
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-editor-codex-tool-rewrite-context.md`
StaleWhen: SimpleTools 源码、菜单/程序集边界、工具安全合同或验证证据变化
Knowledge: `es.aiwarning.editor.codex-tool-rewrite-context.v1`

## 历史边界（非当前交付事实）
- 本文件记录 2026-07～08 工具重写上下文，不是当前编译、Runtime、视觉或发布通过声明；职责限于 ES Editor 小工具，不重定义玩家运行时架构。
- 修改前必须检查脏工作树、菜单与 asmdef 边界；中文文本严格 UTF-8。工具写资产须预览、确认、Undo/可恢复、Dirty/保存、失败摘要和路径边界，疑似未使用资源只能隔离不能永久删除。
- SimpleTools 的扫描、缓存和渲染必须与执行口径一致；未激活场景用 root 递归收集，Prefab/ESSO 查询走正确入口，页面重绘不得隐式扫描或分配热路径临时集合。
- RuntimeWatch 通过注册表和宿主链路按需解析字段；只在前台聚焦页面自动采样，采样列表与渲染快照分离，不能递归扫全场景或把示例当能力上限。
- ObjectPool 仅提供运行时统计、PrefabPrewarmDataInfo 审计、GameManager 接入和 PlayMode 状态；配置写入需确认/Undo/场景脏标记，诊断页只读，不能把选中 Prefab 当作已接入池化体系。
- 所有批处理必须报告成功/失败/跳过；UI 文案不得超过真实行为，禁止绕过 P0、Undo、工作树或把一次 Editor/局部编译通过升级为全项目/Unity/Profiler/IL2CPP/发布通过。

## 已记录的验证边界
- 曾有 ES_Editor 局部编译通过记录，但完整依赖构建可能受其他脏文件阻塞；局部结果不得外推。
- RuntimeWatch 前台焦点门禁、ObjectPool Layout/Repaint 快照复用和 SimpleTools 统一入口已有静态整改记录；Unity 视觉、连续布局、Editor GC 和 PlayMode 仍需实际证据。

详细历史决策、工具入口、风险复盘、性能门禁和人工验收清单见 Knowledge。
