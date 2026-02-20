# State Merge Cost Plan

This document proposes state channel occupancy and cost values based on StateSharedData merge and cost rules.

## Key Interpretation
- Cost is freedom loss after activating a state (higher cost = less freedom to layer other states).
- Channel mask defines which body parts are occupied. Overlap triggers merge checks; cost sum > 100 forces interrupt/deny.

## Proposed Configuration (Current State Pack)

| State | Channel Mask | Motion | Agility | Target | Rationale |
| --- | --- | --- | --- | --- | --- |
| 站立移动 | DoubleLeg | 30 | 20 | 10 | Basic locomotion should allow upper-body actions. |
| 下蹲 | DoubleLeg + BodySpine | 35 | 35 | 10 | Lower-body heavy, torso constrained. |
| 跳跃 | DoubleLeg + BodySpine | 50 | 45 | 20 | Uses full lower body and trunk; limited mixing. |
| 飞行 | AllBodyActive | 80 | 70 | 40 | Full-body control, low freedom. |
| 趴 | DoubleLeg + BodySpine | 40 | 50 | 20 | Body constrained; aiming still possible if upper-body state is light. |
| 游泳 | FourLimbs + BodySpine | 70 | 60 | 30 | Strong full-body involvement. |
| 骑乘 | AllBodyActive | 80 | 80 | 40 | Very low freedom, like driving. |
| 死亡 | AllBodyAndHeartAndMore | 100 | 100 | 100 | No other actions should mix. |
| 表情 | Head + Eye | 0 | 5 | 10 | Should mix with almost everything. |
| 攻击测试 | DoubleHand + BodySpine | 40 | 60 | 60 | Upper-body heavy; allows legs if costs permit. |
| 攀爬 | AllBodyActive | 80 | 70 | 30 | Full-body, high constraint. |
| 急停 | DoubleLeg | 10 | 10 | 0 | Should merge with locomotion easily. |
| 攀爬翻上 | AllBodyActive | 90 | 80 | 40 | Full-body, very constrained. |
| 翻越 | AllBodyActive | 70 | 60 | 30 | Full-body action, short duration. |
| 攀爬跳跃 | AllBodyActive | 85 | 70 | 40 | Full-body jump off wall. |

## Design Notes
- Locomotion uses DoubleLeg only, so upper-body actions (knife, bow) can layer.
- Torso-heavy actions use BodySpine, so they may conflict with other torso-heavy actions.
- Flight, riding, climbing, death occupy AllBodyActive to avoid unrealistic stacking.

## Natural Mix Examples
- 站立移动 (DoubleLeg) + 持刀/拉弓 (RightHand/LeftHand only).
- 站立移动 + 表情 (Head + Eye).
- 急停 + 站立移动 (low cost, same channel, should merge).

## Natural Conflict Examples
- 攀爬/攀爬翻上/翻越/攀爬跳跃 with any other body state.
- 死亡 with any other state.
- 骑乘 with 攻击测试 (if both use torso and hands).
