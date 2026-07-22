# 物理架构：简单起步协作警告

当前先落最小公共层，不做大一统物理框架。

## 已实现

源码：

```text
Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESPhysicsQueryModule.cs
```

入口：

```text
ESGameManager.PhysicsQueryModule
ESGameManager.GetModuleFast<ESPhysicsQueryModule>()
```

GameManager 默认自动创建：

```text
autoCreatePhysicsQueryModule = true
```

## 职责

`ESPhysicsQueryModule` 不应只是 `Physics.XNonAlloc` 的薄包装。它必须至少提供可被业务直接复用的语义入口。

底层查询：

```text
RaycastNonAlloc
SphereCastNonAlloc
OverlapSphereNonAlloc
OverlapBoxNonAlloc
LayerMask 配置
共享缓存
溢出统计
```

语义入口：

```text
ShotCast                 从 from 到 to 查询飞行物命中；radius=0 自动走 Raycast，radius>0 走 SphereCast
TryGetNearestShotHit      直接取最近飞行物命中
TryFindBestInteraction    交互候选：Overlap 后按距离/朝向筛最佳目标
TrapOverlapSphere         陷阱/区域球形检测
TrapOverlapBox            陷阱/区域盒形检测
```

它不负责：

```text
角色移动
伤害
Buff
Shot 飞行状态
必中规则
VFX
对象池
陷阱业务逻辑
武器释放逻辑
```

## 推荐使用

高频或可重入代码优先自己传入缓存：

```text
Raycast(..., RaycastHit[] results)
SphereCast(..., RaycastHit[] results)
OverlapSphere(..., Collider[] results)
OverlapBox(..., Collider[] results)
```

简单低频逻辑可以用共享缓存：

```text
RaycastShared(...)
SphereCastShared(...)
OverlapSphereShared(...)
OverlapBoxShared(...)
```

共享缓存不是并发安全容器。不要在同一段逻辑里嵌套调用共享查询后还长期持有 `SharedRaycastHits / SharedColliders` 的内容。

## 后续路线

简单顺序：

```text
1. 交互探测接 OverlapSphere/Raycast
2. Shot 命中 solver 已优先接入 ESPhysicsQueryModule.ShotCast
3. 陷阱/区域接 OverlapBox/OverlapSphere
4. 近战武器接 SphereCast/Capsule 思路
5. 再考虑分组 Tick、空间哈希、Job/Burst
```

不要一上来把 KCC、Item、Shot、Trap、Weapon 全部揉成一个大 Domain。当前公共物理层只是“查询服务”，业务仍由 Entity/Item/Skill/Op 各自消费结果。
