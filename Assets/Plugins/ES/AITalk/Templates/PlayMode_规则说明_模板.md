# PlayMode 规则说明

模式名称：

主持者：

参与者：

席位表：

```text
PlayerAI_A
PlayerAI_B
PlayerAI_C
PlayerAI_D
```

玩家身份规则：

```text
玩家自称不是权威身份。
主持者按加入顺序或席位表分配权威玩家 ID。
玩家必须接受主持者分配的真实玩家身份。
行动中的 PLAYER 字段必须等于权威玩家 ID。
重复自称或冲突自称不允许覆盖其他玩家。
```

## 玩法目标

## 阶段流程

```text
准备 -> 发言/行动 -> 结算 -> 结束
```

## 公开信息

## 私密信息

```text
是否存在私密信息：是/否
私密信息目录：PlayMode/Private/AI名称/
主持者内部状态：PlayMode/Private/ModeratorAI/State_Internal.md
越权读取处理：
```

公开信息禁止泄露：

```text
隐藏身份
私密行动提交者
私密行动目标
私密行动结果
主持者内部结算细节
```

## 允许动作

```text
PLAYER:
ACTION:
TARGET:
VALUE:
REASON:
```

动作目录：

```text
公开动作：PlayMode/Actions/Public/
私密动作：PlayMode/Actions/Private/AI名称/ 或 PlayMode/Private/AI名称/Actions/
```

## 动作示例

```text
PLAYER: PlayerAI_A
ACTION: VOTE
TARGET: PlayerA
REASON: 示例理由
```

身份不匹配处理：

```text
缺少 PLAYER、PLAYER 与主持者分配身份不一致、写错目录：要求重发或判为无效。
```

## 轮询和节奏

```text
轮询间隔：5-15 秒
建议时长：5-15 分钟
发言长度：短句优先
玩家持续轮询：未进入结束阶段前，玩家不能提交一次行动后结束任务
超时策略：
```

## 角色能力标签

如果玩法存在角色差异，玩家需要填写少量标签，主持者按标签和局势结算。

```text
输出
控制
保护
位移
治疗
资源
推塔
侦查
爆发
持续作战
```

## 结束条件
