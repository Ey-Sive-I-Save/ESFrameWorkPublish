# Item 运动模板说明

## 为什么加这个样例

本工程正在把世界大型逻辑体收敛为两类：

- `Entity`：生命体，例如玩家、NPC、怪物。
- `Item`：世界逻辑体，例如飞行物、门、机关、防御塔、陷阱、拾取物、武器、区域、平台。

飞行物 `Shot` 不能继续写成散落的临时 `MonoBehaviour`，否则后续会把运动、碰撞、伤害、特效、对象池、技能逻辑全揉在一起，系统上限很快锁死。

这个样例的目的不是做最终美术，而是给后续 AI 和开发者一个可运行的底座：

- Item 是根。
- 运动由 Item 模块负责。
- 碰撞只产生命中候选。
- 特效只是表现。
- 伤害、Buff、音效、对象池、复杂逻辑由外部事件消费。

## 场景位置

```text
Assets/Plugins/ES/3_Examples/1_Runtime/Example_ItemMotion/ES_Item_Shot_Template_Demo.unity
```

打开场景后主要对象：

```text
ES_Item_Shot_通用模板_场景样例
Shot_Target_必中目标
Ground_场景阻挡_地面
Wall_可选阻挡_移动到弹道中测试
```

## 脚本位置

```text
Assets/Scripts/ESLogic/Runtime/Item/Samples/ItemShotTemplatePreview.cs
```

脚本职责：

- 自动确保根对象有 `Item`。
- 自动维护模板分层。
- 把 Inspector 里的 Shot 配置应用到 `ItemShotModule`。
- 播放时可自动发射到目标点。
- 用 Gizmos 显示命中半径、飞行方向、目标连线。

## 模板分层

样例组件会维护以下子节点：

```text
Model_模型        只放可见模型
Collision_碰撞    只放碰撞体/触发器
VFX_表现          只放粒子、拖尾、材质表现
Debug_调试        只放调试提示
```

不要把伤害、Buff、对象池、技能释放逻辑塞进这些节点。

## 当前支持的 Shot 配置

```text
瞄准模式        Free / Target / MustHit / Scan
阻挡模式        None / WorldOnly / AnyBlocker
速度
加速度
最大速度
发射延迟
预热时间
锁头开始
锁头持续
转向速度
寿命
命中半径
命中层
使用重力
朝向速度方向
```

`MustHit` 是合法模式：战斗层已经判定应该命中，Shot 负责飞行表现，到达目标时产生命中候选。

## 后续扩展方向

优先扩展这些点：

- 更完整的 Game Layer 过滤：阵营、归属、目标类型。
- 反弹、穿透、擦碰、阻挡细分。
- 空间哈希或批量碰撞替代默认物理查询。
- 分组 Tick、距离降频、预算调度。
- LogicRandom / ViewRandom 分离。
- 命中、过期、停止事件接入 ESOutputOp。

不要优先扩展这些错误方向：

- 不要恢复旧 `Runtime/Movement`。
- 不要引入和 `Entity/Item` 并列的 `ESMotionBody`。
- 不要让 Shot 直接管理伤害、Buff、VFX、音效、Pool。
- 不要拆出一堆 `ItemMotionDomain / ItemCollisionDomain / ItemLifetimeDomain`。

第一版目标是跑通清晰底座，不是一次性塞满所有玩法。
