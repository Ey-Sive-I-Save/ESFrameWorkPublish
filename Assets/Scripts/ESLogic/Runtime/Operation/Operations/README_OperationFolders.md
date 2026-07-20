# Operation 文件夹分类

这里按 3D 动作游戏技能释放链路划分 Operation。新增 Operation 时，优先选择一个明确目录，并使用 `OperationTypeRegistryNames` 中的常量作为 Odin `TypeRegistryItem` 路径。

## 目录

- `00_Common`：组合、空操作、批处理、通用工具型 Operation。
- `01_ReleaseFlow`：释放开始、提交、取消、打断、结束、消耗、冷却、次数、充能。
- `02_Targeting`：使用者、主目标、目标列表、目标筛选、排序、数量限制、目标包写回。
- `03_ConditionBranch`：条件判断、if/switch/random/weighted random 等流程分支。
- `04_CombatHit`：命中检测、伤害、治疗、护盾、受击反馈、硬直、破防。
- `05_AttributeResource`：属性修改、资源修改、runtimeFloat/bool/int、上下文读写和缓存。
- `06_BuffState`：Buff 添加/移除/刷新/层数、状态标签、控制状态、免疫。
- `07_MovementPhysics`：冲刺、位移、牵引、击退、朝向、物理力、根运动处理。
- `08_AnimationAction`：Animator 参数、CrossFade、动作阶段标记、Timeline/Playable 触发。
- `09_GameObjectVFX`：对象激活、生成、挂点、销毁、Transform 操作、VFX 创建和停止。
- `10_Audio`：一次性音效、循环音效、停止音效、跟随目标播放。
- `11_CameraFeedback`：震屏、相机聚焦、镜头偏移、冲击反馈。
- `12_DurationChanneling`：持续伤害/治疗/属性、引导、光环、旧 Buffer Operation 思路迁移。
- `13_EventCallback`：事件派发、回调、消息通知、UI/任务/成就联动。
- `90_Debug`：日志、断言、Gizmos、链路测试。

## 规则

- 不要把战斗结算、表现、目标选择混在一个 Operation 里；复杂技能用多个 Clip 或组合 Operation 串起来。
- 高频运行的 Operation 优先使用固定字段和 `ESRuntimeTargetPack` 原生字段，只有需要动态计算时才用表达式。
- 持续性 Operation 必须考虑 Stop/Cancel，必要时使用 `MustTriggerStop`。
- 只用于编辑器验证的 Operation 放入 `90_Debug`，不要混入正式分类。
- 需要跨帧保存状态的 Operation 放入 `12_DurationChanneling` 或使用 `ESOpSupport` 的 Store。
