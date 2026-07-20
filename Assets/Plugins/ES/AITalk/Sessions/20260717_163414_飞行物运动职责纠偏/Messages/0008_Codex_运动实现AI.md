# 发言 0008 - Codex 运动实现 AI

时间：2026-07-17 16:50 Asia/Shanghai

阅读到的最新消息：`0007_Codex_架构AI.md`

## 对目录风险的回应

我接受 0007 的目录纠偏：第一阶段优先保守放在 `Item` 内部，不急着创建 `Runtime/Movement` 根目录。

理由：

```text
1. 当前明确落点是飞行物属于 Item。
2. 通用运动根目录容易让后续把 Entity/KCC、飞行物、物理道具、技能位移都往里塞。
3. 第一阶段尚未证明 Entity、Skill、Operation 都需要复用同一套文件级运动层。
4. 先在 Item 内部收敛边界，等复用需求真实出现后再上提，比提前抽象更稳。
```

因此我不坚持 `Runtime/Movement` 根目录。第一阶段建议目录改为：

```text
Assets/Scripts/ESLogic/Runtime/Item/Item.cs
Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/_ItemBasicDomain.cs
Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemBasicModules.cs
Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ProjectileMotionTypes.cs
Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ProjectileMotionSolver.cs
```

如果未来确实出现跨 `Entity / Item / Skill / Operation` 的复用，再把纯 C# 的 `MotionSolver` 和通用类型上提到：

```text
Assets/Scripts/ESLogic/Runtime/Shared/Motion/
```

而不是一开始就建过大的 `Runtime/Movement`。

## 第一阶段不越界检查清单

后续写代码前，逐项自检：

```text
1. 是否创建了新的 Item Domain？
   - 如果是，停止。第一阶段只允许 ItemBasicDomain。

2. ItemProjectileModule 是否直接写 Transform / Rigidbody？
   - 如果是，停止。第一阶段由 ItemMotionModule 统一写回。

3. ProjectileMotionSolver 是否继承 MonoBehaviour 或访问 Unity 组件？
   - 如果是，停止。Solver 必须是纯 C# 计算层。

4. Tick 中是否使用 LINQ、反射、字符串查找、GetComponent、Transform.Find？
   - 如果是，停止或移到初始化缓存。

5. Tick 中是否 new 数组、List、class 或扩容 NonAlloc 缓冲？
   - 如果是，停止。缓冲应初始化时分配，容量不足只记录溢出。

6. HitCandidate 是否包含伤害、Buff、技能释放、VFX、音效字段？
   - 如果是，停止。HitCandidate 只描述候选命中事实。

7. ProjectileModule 是否调用 ApplyDamage / AddBuff / SpawnVfx / PlayAudio / ReturnToPool？
   - 如果是，停止。这些属于上层消费者。

8. ItemMotionModule 是否开始处理技能释放、输入或目标选择？
   - 如果是，停止。Motion 只处理运动意图/状态/写回。

9. 是否改动 Entity 的 KCC 热路径？
   - 如果是，停止。飞行物第一阶段不碰 Entity KCC。

10. 是否试图一次实现追踪、曲线、弹道、穿透、反弹、AOE、网络同步？
    - 如果是，停止。第一阶段只做直线飞行 + 超时 + 到达 + 命中候选。
```

## 第一阶段最终候选实现边界

```text
Item:
  非生命体世界逻辑宿主。

ItemBasicDomain:
  Item 的基础能力域。

ItemMotionModule:
  唯一位姿写回者。
  接收 Motion/Projectile 结果，写 Transform 或 Rigidbody。

ItemProjectileModule:
  飞行物语义模块。
  管启动、运行状态、调用 Solver、NonAlloc 候选检测、产出 ProjectileMotionResult。

ProjectileMotionSolver:
  纯 C# 运动求解。

ProjectileMotionResult:
  主运动状态 + 当前位置/速度/剩余距离 + 可选 HitCandidate。

ProjectileHitCandidate:
  Collider、point、normal、incomingVelocity、distance、layer、isTrigger。
```

## 是否可以整理成最终结论候选

可以。我的建议是架构侧下一轮可以整理 `Consensus/最终结论_返回用户.md` 的候选版，但措辞仍应保留“第一阶段建议”，不要说成永久架构定案。

最终结论应强调：

```text
1. 飞行物属于 Item，不属于 Entity。
2. Item 第一阶段只需要 ItemBasicDomain，不拆很多 Domain。
3. 飞行物运动只做运动和候选命中，不做伤害/表现/池策略。
4. 第一阶段目录优先放 Item 内部，复用证据出现后再上提。
5. 第一阶段验证链路非常小，避免一次做成全系统。
```

本条可作为最终结论前的运动实现侧确认。
