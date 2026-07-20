# 通用武器场景模板

这个示例只提供“场景里的武器结构模板”，不包含真实开火、扣弹、命中、伤害、换弹逻辑。

## 创建方式

在 Unity 菜单执行：

`ES/Weapon/创建通用武器场景模板`

会在当前场景生成：

```text
WeaponTemplate_通用武器
├── 00_运行根
├── 10_挂载与握持
│   ├── HoldSocket
│   ├── BackSocket
│   ├── RightHandGrip
│   ├── LeftHandGrip
│   ├── AimReference
│   └── RecoilPivot
├── 20_射击与弹道
│   ├── Muzzle
│   ├── ShellEject
│   ├── Magazine
│   ├── Chamber
│   ├── RayOrigin
│   └── ProjectileSpawn
├── 30_表现资源
│   ├── ModelRoot
│   ├── ColliderRoot
│   ├── VFXRoot
│   ├── AudioRoot
│   └── AnimationRoot
└── 40_调试占位
    └── AimPreviewTarget
```

## 节点职责

- `HoldSocket`：角色手持时武器挂载点。
- `BackSocket`：收枪或背挂时使用。
- `RightHandGrip` / `LeftHandGrip`：IK 握持点。
- `AimReference`：瞄准方向参考点。
- `RecoilPivot`：后坐力表现轴心。
- `Muzzle`：枪口火焰、射线方向、子弹出生参考。
- `ShellEject`：弹壳抛出点。
- `Magazine` / `Chamber`：换弹、上膛动画 marker 对齐参考。
- `RayOrigin`：Hitscan 检测起点。
- `ProjectileSpawn`：Projectile 出生点。
- `ModelRoot`：放武器模型。
- `ColliderRoot`：放拾取、碰撞、命中体积。
- `VFXRoot` / `AudioRoot`：表现资源挂点。
- `AnimationRoot`：武器自身动画或约束根。

## 接入边界

这个模板只负责“场景结构”和“挂点命名”。正式逻辑建议按下面边界接入：

- `Item`：武器作为世界物品或投射物的基础宿主。
- `Entity`：武器拥有者、阵营、输入、目标来源。
- `StateMachine`：持枪、开火、换弹、收枪等动作状态。
- `StateFinalIKDriver`：握持、瞄准、后坐力、探头、受击反馈。
- `Operation`：命中、Buff、音效、特效、对象开关等可编排行为。

不要在模板脚本里直接写真实射击逻辑。模板应保持轻、稳定、可复制。

## 后续扩展建议

- 第一阶段：只用模板确定模型挂点和 IK 姿势。
- 第二阶段：接入状态机动画 marker，例如开火点、换弹生效点、上膛点。
- 第三阶段：接入弹道和命中逻辑。
- 第四阶段：接入配件系统、皮肤覆盖、Buff 修改、网络同步。
