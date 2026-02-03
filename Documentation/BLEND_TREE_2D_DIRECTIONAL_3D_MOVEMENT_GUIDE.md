# 2D方向型混合树 - 3D游戏移动系统配置指南

## 📋 概述

`BlendTree2D Directional`（方向型混合树）专为3D游戏的八向移动系统设计，能够同时处理**移动方向**和**移动速度**。

## 🎯 典型应用场景

### 3D游戏移动系统（17个动画）

```
配置结构：
├── 中心点 (0, 0)           → Idle（静止）
├── 内圈8方向（半径0.5）    → Walk × 8（行走）
└── 外圈8方向（半径1.0）    → Run × 8（跑步）
```

## 🔧 配置步骤

### 1. 创建Calculator

在State配置中选择：
- Calculator类型：`2D混合树-方向型`

### 2. 初始化采样点

点击按钮：`初始化标准采样(8方向+中心)`

这会自动创建17个采样点：
```
索引0:  中心 (0.0, 0.0)           → Idle
索引1:  外圈 (1.0, 0.0)           → Run_Right
索引2:  外圈 (0.707, 0.707)       → Run_ForwardRight
索引3:  外圈 (0.0, 1.0)           → Run_Forward
索引4:  外圈 (-0.707, 0.707)      → Run_ForwardLeft
索引5:  外圈 (-1.0, 0.0)          → Run_Left
索引6:  外圈 (-0.707, -0.707)     → Run_BackLeft
索引7:  外圈 (0.0, -1.0)          → Run_Back
索引8:  外圈 (0.707, -0.707)      → Run_BackRight
索引9:  内圈 (0.5, 0.0)           → Walk_Right
索引10: 内圈 (0.354, 0.354)       → Walk_ForwardRight
索引11: 内圈 (0.0, 0.5)           → Walk_Forward
索引12: 内圈 (-0.354, 0.354)      → Walk_ForwardLeft
索引13: 内圈 (-0.5, 0.0)          → Walk_Left
索引14: 内圈 (-0.354, -0.354)     → Walk_BackLeft
索引15: 内圈 (0.0, -0.5)          → Walk_Back
索引16: 内圈 (0.354, -0.354)      → Walk_BackRight
```

### 3. 配置动画Clip

为每个采样点分配对应的AnimationClip：

| 索引 | Position | 动画类型 | 动画名称示例 |
|------|----------|----------|--------------|
| 0 | (0, 0) | Idle | `Character_Idle` |
| 1-8 | 外圈 | Run | `Character_Run_XXX` |
| 9-16 | 内圈 | Walk | `Character_Walk_XXX` |

### 4. 设置参数

- **parameterX**: `DirectionX` 或 `StateDefaultFloatParameter.SpeedX`
- **parameterY**: `DirectionY` 或 `StateDefaultFloatParameter.SpeedZ`
- **smoothTime**: `0.1` ~ `0.15`（推荐值）

## 🎮 运行时行为

### 输入 → 动画映射

| 输入 (X, Z) | 距离 | 角度 | 播放动画 | 说明 |
|-------------|------|------|----------|------|
| (0, 0) | 0 | - | Idle | 静止 |
| (0.3, 0) | 0.3 | 0° | Idle + Walk_Right | 慢速向右 |
| (0.5, 0) | 0.5 | 0° | Walk_Right | 行走向右 |
| (0.7, 0) | 0.7 | 0° | Walk_Right + Run_Right | 加速向右 |
| (1.0, 0) | 1.0 | 0° | Run_Right | 全速向右 |
| (0.707, 0.707) | 1.0 | 45° | Run_ForwardRight | 全速右前 |
| (0.354, 0.354) | 0.5 | 45° | Walk_ForwardRight | 行走右前 |
| (0.2, 0.2) | 0.28 | 45° | Idle + Walk_ForwardRight | 慢走右前 |

### 核心算法

```
1. 输入点落在某个三角形内
2. 计算三角形三个顶点的重心坐标 (u, v, w)
3. 权重分配：
   - 顶点0权重 = u
   - 顶点1权重 = v
   - 顶点2权重 = w
4. 最终动画 = Clip0 × u + Clip1 × v + Clip2 × w
```

**示例**：
- 输入 (0.25, 0) 落在 `[Idle, Walk_Right, Walk_BackRight]` 三角形内
- 重心坐标计算结果：(0.5, 0.5, 0.0)
- 最终混合：50% Idle + 50% Walk_Right → 慢速向右移动

## 🚀 优势

### 1. 自动速度混合

无需额外配置，距离原点越近 → 速度越慢：
- **距离 < 0.5**: Idle与Walk混合
- **距离 = 0.5**: 纯Walk
- **0.5 < 距离 < 1.0**: Walk与Run混合
- **距离 = 1.0**: 纯Run

### 2. 平滑方向过渡

从Forward到ForwardRight时，自动混合相邻动画：
```
Forward (100%) 
    ↓ 玩家转向
Forward (80%) + ForwardRight (20%)
    ↓
Forward (60%) + ForwardRight (40%)
    ↓
Forward (40%) + ForwardRight (60%)
    ↓
ForwardRight (100%)
```

### 3. 对角线运动

斜向移动时自动混合3个动画（Idle/Walk/Run的某个方向组合）。

## ⚙️ 参数调优

### smoothTime（平滑时间）

- **0.0**: 瞬时切换（适合街机游戏）
- **0.05**: 快速响应（适合竞技游戏）
- **0.1**: 标准平滑（推荐）
- **0.15**: 柔和过渡（适合RPG）
- **0.2+**: 缓慢变化（适合慢节奏游戏）

### 输入归一化

确保输入向量在合理范围内：
```csharp
// 推荐做法：
Vector3 moveDir = new Vector3(input.x, 0, input.y).normalized;
float speedFactor = Mathf.Clamp01(inputMagnitude); // 0~1
Vector2 blendInput = new Vector2(moveDir.x, moveDir.z) * speedFactor;
```

## 📌 实战案例

### 案例1：标准WASD移动

```csharp
// 在EntityBasicMoveRotateModule中：
Vector3 inputDir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
Vector3 normalizedDir = inputDir.normalized;

// 转换为局部坐标
Vector3 localDir = transform.InverseTransformDirection(normalizedDir);

// 设置BlendTree参数（自动混合速度）
context.SpeedX = localDir.x * inputMagnitude; // -1 ~ 1
context.SpeedZ = localDir.z * inputMagnitude; // -1 ~ 1
```

**效果**：
- 轻推摇杆 → Walk
- 全推摇杆 → Run
- 松开摇杆 → Idle

### 案例2：战斗模式（走跑切换）

```csharp
// 根据战斗状态调整速度倍率
float speedMultiplier = isCombatMode ? 0.5f : 1.0f; // 战斗时只能Walk
context.SpeedX = localDir.x * inputMagnitude * speedMultiplier;
context.SpeedZ = localDir.z * inputMagnitude * speedMultiplier;
```

### 案例3：冲刺系统

```csharp
// 冲刺时速度超过1.0
float sprintMultiplier = isSprintting ? 1.5f : 1.0f;
Vector2 input = new Vector2(localDir.x, localDir.z) * inputMagnitude * sprintMultiplier;

// 输入可以超过1.0，但需要额外配置外圈的Sprint动画
context.SpeedX = input.x; // 可能 > 1.0
context.SpeedZ = input.y;
```

**扩展配置**：添加第三圈（半径1.5）的Sprint动画。

## ⚠️ 常见问题

### Q1: 为什么动画卡在Idle不动？

**原因**：输入参数始终为(0, 0)。

**检查**：
1. `EntityBasicMoveRotateModule`是否正确更新`SpeedX/SpeedZ`
2. 参数名是否匹配（parameterX/Y）
3. 输入是否正确转换为局部坐标

### Q2: 动画混合不流畅，有跳变？

**原因**：三角化失败或采样点位置错误。

**解决**：
1. 检查Console日志：`[BlendTree2D-Directional] 三角化完成: XX个三角形`
2. 确保中心点是(0, 0)
3. 确保8个外圈点角度均匀分布
4. 增大smoothTime（0.15~0.2）

### Q3: 某个方向动画不播放？

**原因**：对应索引的Clip未分配。

**解决**：
1. 检查samples数组，确保所有17个索引都有Clip
2. 查看调试日志：当前权重分配
3. 使用`GetCurrentClip`检查当前播放的Clip

### Q4: 斜向移动时角色抖动？

**原因**：三角形退化或输入抖动。

**解决**：
1. 添加输入死区（deadzone）：
```csharp
if (inputMagnitude < 0.1f) {
    context.SpeedX = 0;
    context.SpeedZ = 0;
}
```
2. 增加输入平滑（已内置smoothTime）

## 🎓 高级技巧

### 技巧1：动态调整速度层级

```csharp
// 根据地形调整速度
float terrainSpeedMultiplier = GetTerrainSpeedMultiplier(); // 0.5 (泥地) ~ 1.0 (平地)
context.SpeedX *= terrainSpeedMultiplier;
context.SpeedZ *= terrainSpeedMultiplier;
```

### 技巧2：转向辅助

```csharp
// 在转向时自动降低速度
float turnSpeed = Vector3.Angle(lastMoveDir, currentMoveDir);
float turnPenalty = Mathf.Lerp(1.0f, 0.7f, turnSpeed / 180f);
context.SpeedX *= turnPenalty;
context.SpeedZ *= turnPenalty;
```

### 技巧3：IK配合

结合IK系统实现脚步对齐地形：
```csharp
// BlendTree控制身体动画
// IK系统调整脚部位置
// 完美结合！
```

## 📊 性能数据

| 操作 | 时间复杂度 | 说明 |
|------|-----------|------|
| 三角化 | O(n) | 仅初始化时执行一次 |
| 查找三角形 | O(n) | n = 三角形数量（通常16个） |
| 重心坐标 | O(1) | 固定计算 |
| 权重更新 | O(n) | n = 采样点数量（17个） |

**典型帧耗时**: < 0.1ms（在3GHz CPU上）

## 🎨 可视化调试

建议在Scene视图中绘制调试信息：

```csharp
void OnDrawGizmos()
{
    if (samples == null) return;
    
    // 绘制采样点
    foreach (var sample in samples)
    {
        Gizmos.color = Color.yellow;
        Vector3 pos = transform.position + new Vector3(sample.position.x, 0, sample.position.y);
        Gizmos.DrawSphere(pos, 0.05f);
        Gizmos.DrawLine(transform.position, pos);
    }
    
    // 绘制当前输入
    Gizmos.color = Color.red;
    Vector3 inputPos = transform.position + new Vector3(context.SpeedX, 0, context.SpeedZ);
    Gizmos.DrawSphere(inputPos, 0.1f);
}
```

## 📝 总结

**核心要点**：
1. ✅ 17个动画 = 1 Idle + 8 Walk + 8 Run
2. ✅ 距离控制速度：近=慢，远=快
3. ✅ 角度控制方向：自动混合相邻方向
4. ✅ 平滑过渡：内置SmoothDamp
5. ✅ 零额外代码：配置完成即可使用

**最佳实践**：
- 使用局部坐标系（玩家相对方向）
- smoothTime设置0.1~0.15
- 添加输入死区避免抖动
- 确保所有17个Clip都已分配

**下一步**：
- 测试8个方向的移动
- 调整smoothTime找到最佳手感
- 添加转身动画（可用1D混合树叠加）
- 配合根运动（Root Motion）实现位移同步
