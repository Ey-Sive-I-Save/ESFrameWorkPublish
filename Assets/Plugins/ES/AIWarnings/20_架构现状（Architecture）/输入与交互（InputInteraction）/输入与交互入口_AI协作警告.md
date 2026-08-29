# ES 输入与交互入口 AI 协作警告

Status: current
StableId: es.aiwarning.arch.input-interaction-entry
Authority: AIWarnings；当前 ESInput/Entity 源码为事实权威。
RouteKeys: aiwarnings, architecture, input, interaction, runtime-mode, virtual-input, rebind
Applicability: 输入运行时、改键覆盖、虚拟输入、RuntimeMode 过滤和玩家交互入口衔接。
EvidenceRef: `Documentation/AIKnowledge/entries/entity-input-command-runtime.md`
Owner: ES Input/Entity runtime owners。
StaleWhen: ESInputModule、ESInputService、RuntimeMode、EntityAIDomain 或交互主链变化。

## 长期约束

- 唯一主链为 `Config/Profile → RuntimeBuilder → ESInputModule/Sources → ESInputService → Entity writer/AIDomain → 行为许可`；不得恢复旧 `ESInputRuntime` 包装层或幻想 path 表。
- `ESInputConfig` 只定义默认输入，`ESInputBindingProfile` 只存覆盖；`ESInputModule` 管理初始化、Profile、烘焙、重建和 UI 虚拟输入，`ESInputService` 只负责高频状态计算，二者不得合并。
- UI、触摸、命令和硬件源只写输入意图，不直接执行 Gameplay；RuntimeMode 只做粗粒度读取过滤，实体行为能否执行由 Entity/AI/State/Buff/Skill 许可层决定。
- 绑定路径必须来自 Unity Input System；保持按活跃索引轮询，禁止每帧扫描全部 Action、硬编码键位、一次性 OnGUI、Camera.main、FindObjectOfType、全局单例或原始 Input API 隐式依赖。
- 改键/重建替换内部表和输入源，不替换 Service 对象；不得把玩家 Input 模块扩张成怪物、网络、剧情等全局行为权限中心。
- 详细输入编译链、RuntimeMode、虚拟输入、Permit、交互衔接、失败边界和验证要求由 `es.project.entity-input-command-runtime.v1` Knowledge 承接；Knowledge 不授予执行授权。
