# 2D混合树使用指南

## 问题诊断

### 症状
- 简单Clip动画可以正常播放
- 2D自由混合计算器不能正确产出动画

### 根本原因
2D混合计算器的`UpdateWeights`方法需要通过`StateContext`获取混合参数（如X/Y方向输入），但该调用之前被注释掉了。

## 解决方案

### 1. 架构改进（已完成）

**StateMachine.cs修改：**
- ✅ 添加`StateContext stateContext`实例
- ✅ 在`Initialize()`中自动创建StateContext
- ✅ 在`UpdateStateMachine()`中启用`state.UpdateAnimationWeights(stateContext, deltaTime)`
- ✅ 在`UpdateStateMachineWithDiagnostics()`中同步启用
- ✅ 添加便捷访问方法：`SetFloat()` / `GetFloat()`

**AnimationMixerCalculators.cs修改：**
- ✅ **1D混合树**：初始化时自动将第一个有效Clip权重设为1.0
- ✅ **2D混合树**：初始化时自动查找中心点（接近原点）或第一个有效Clip，权重设为1.0
- ✅ **DirectBlend**：已支持`defaultWeight`配置，无需修改

**核心改进：即使不调用UpdateWeights，动画也能立即播放**
- 之前：Mixer初始化后所有权重为0，必须调用UpdateWeights才能看到动画
- 现在：Mixer初始化时就有默认权重，立即可见动画效果
- 行为：类似SimpleClip，激活状态后即可播放默认动画

### 2. 使用2D混合树的完整流程

#### 步骤1：创建2D混合树状态

```csharp
// 创建2D自由方向混合计算器（适用于移动动画）
var calculator = new StateAnimationMixCalculatorForBlendTree2DFreeformDirectional
{
    // 设置参数键（通过StateContext获取）
    parameterX = "DirectionX",  // 或使用枚举: StateDefaultFloatParameter.SpeedX
    parameterY = "DirectionY",  // 或使用枚举: StateDefaultFloatParameter.SpeedY
    smoothTime = 0.1f,
    
    // 定义采样点（8方向 + 中心）
    samples = new[]
    {
        new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
        {
            position = Vector2.zero,  // 中心 - Idle
            clip = idleClip
        },
        new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
        {
            position = new Vector2(0, 1),  // 前
            clip = walkForwardClip
        },
        new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
        {
            position = new Vector2(0, -1),  // 后
            clip = walkBackwardClip
        },
        new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
        {
            position = new Vector2(-1, 0),  // 左
            clip = walkLeftClip
        },
        new StateAnimationMixCalculatorForBlendTree2D.ClipSample2D
        {
            position = new Vector2(1, 0),  // 右
            clip = walkRightClip
        }
        // ... 可以添加更多方向
    }
};

// 创建状态
var moveState = new StateBase();
moveState.stateSharedData = new StateSharedData();
moveState.stateSharedData.hasAnimation = true;
moveState.stateSharedData.animationConfig = new StateAnimationConfigData
{
    calculator = calculator
};
moveState.stateSharedData.InitializeRuntime();

// 注册到状态机
stateMachine.RegisterState("Move", moveState, StatePipelineType.Main);
stateMachine.TryActivateState("Move");
```

#### 步骤2：运行时更新混合参数

**方式A：直接通过StateMachine（推荐）**

```csharp
// 在Update()或FixedUpdate()中更新参数
void Update()
{
    // 获取角色输入（示例）
    Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    
    // 设置混合参数
    stateMachine.SetFloat("DirectionX", moveInput.x);
    stateMachine.SetFloat("DirectionY", moveInput.y);
    
    // 或使用枚举参数（零GC）
    stateMachine.SetFloat(StateParameter.FromEnum(StateDefaultFloatParameter.SpeedX), moveInput.x);
    stateMachine.SetFloat(StateParameter.FromEnum(StateDefaultFloatParameter.SpeedY), moveInput.y);
}
```

**方式B：直接访问StateContext**

```csharp
void Update()
{
    Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    
    // 直接访问StateContext
    stateMachine.stateContext.SetFloat("DirectionX", moveInput.x);
    stateMachine.stateContext.SetFloat("DirectionY", moveInput.y);
}
```

#### 步骤3：调用UpdateStateMachine

```csharp
void Update()
{
    // 1. 先更新参数
    Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    stateMachine.SetFloat("DirectionX", moveInput.x);
    stateMachine.SetFloat("DirectionY", moveInput.y);
    
    // 2. 更新状态机（内部会调用UpdateWeights并应用到Playable）
    stateMachine.UpdateStateMachine();
    
    // 3. 动画会自动通过PlayableGraph输出到Animator
}
```

### 3. 参数命名约定

#### 默认行为说明

**所有混合树现在都有默认可播放状态：**
- **1D混合树**：初始化后自动播放第一个Clip（通常是Idle），权重1.0
- **2D混合树**：初始化后自动播放中心点Clip（position最接近(0,0)），权重1.0
- **DirectBlend**：使用配置的`defaultWeight`

这意味着即使不设置任何参数，动画也会立即播放，不会出现静止状态。

#### 使用枚举参数（零GC，推荐）

```csharp
public enum StateDefaultFloatParameter
{
    None = 0,
    SpeedX = 1,    // 用于2D混合的X轴
    SpeedY = 2,    // 用于2D混合的Y轴
    SpeedZ = 3,
    AimYaw = 4,    // 用于Aim Offset的Yaw
    AimPitch = 5,  // 用于Aim Offset的Pitch
    Speed = 6,
    IsGrounded = 7
}

// 使用示例
calculator.parameterX = StateParameter.FromEnum(StateDefaultFloatParameter.SpeedX);
calculator.parameterY = StateParameter.FromEnum(StateDefaultFloatParameter.SpeedY);
```

#### 使用字符串参数（灵活性高）

```csharp
calculator.parameterX = "MoveX";
calculator.parameterY = "MoveY";

// 运行时设置
stateMachine.SetFloat("MoveX", 0.5f);
stateMachine.SetFloat("MoveY", 1.0f);
```

### 4. 编辑器快速初始化

在Inspector中选择2D混合计算器后，点击"初始化标准采样(8方向+中心)"按钮，会自动创建17个采样点：
- 1个中心点（Idle）
- 8个外圈方向（半径1.0）
- 8个内圈方向（半径0.5）

然后只需拖拽对应的AnimationClip到各个采样点即可。

### 5. 调试技巧

#### 实时监控参数值

```csharp
// 启用持续统计输出（在Inspector中点击"切换持续统计输出"按钮）
stateMachine.enableContinuousStats = true;

// 或手动打印参数
Debug.Log($"X: {stateMachine.GetFloat("DirectionX")}, Y: {stateMachine.GetFloat("DirectionY")}");
```

#### 诊断模式

```csharp
// 临时替换UpdateStateMachine为诊断版本
stateMachine.UpdateStateMachineWithDiagnostics();
```

### 6. 常见问题

#### Q: 动画不动，但没有报错？
**A**: 检查以下几点：
1. 是否在Update中调用了`stateMachine.UpdateStateMachine()`
2. Animator组件的Update Mode是否为Normal
3. Animator的Culling Mode是否为Always Animate
4. 注意：现在即使不设置参数，初始化后也应该能看到默认动画（1D的第一个Clip或2D的中心点）

#### Q: 初始化后看不到任何动画？
**A**: 检查Clip配置：
- 1D混合树：确保第一个采样点有Clip
- 2D混合树：确保至少有一个采样点有Clip（优先使用position最接近(0,0)的）
- 所有Clip都为null时无法播放

#### Q: 动画切换不平滑？
**A**: 调整`calculator.smoothTime`参数（0.1f～0.3f）

#### Q: 只有部分方向有动画？
**A**: 检查采样点的Clip是否都已赋值，未赋值的采样点权重为0

#### Q: 如何确认UpdateWeights被调用？
**A**: 在2D混合计算器的`CalculateWeights2D`方法开始处添加日志：

```csharp
protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input)
{
    Debug.Log($"[2D Blend] Input: {input}");  // 添加此行
    // ... 原有代码
}
```

### 7. 性能优化建议

1. **使用枚举参数**：`StateDefaultFloatParameter`路径零GC
2. **合理设置采样点数量**：8～17个采样点即可覆盖大部分场景
3. **调整smoothTime**：过小的值会导致频繁更新
4. **避免每帧重新创建计算器**：计算器应在状态注册时创建，整个生命周期复用

### 8. 完整示例代码

参见：
- `Assets/Scripts/ESLogic/State/Examples/BlendTreeExamples.cs`
- `Assets/Scripts/ESLogic/State/ValyeTypeSupport/1NormalFeatureSupportData/Examples/StateParameter_UsageExample.cs`

## 技术原理

### 更新链路

```
[MonoBehaviour.Update]
    ↓
[stateMachine.SetFloat()] → 更新StateContext参数
    ↓
[stateMachine.UpdateStateMachine()]
    ↓
[foreach runningStates]
    ↓
[state.UpdateAnimationWeights(stateContext, deltaTime)]
    ↓
[calculator.UpdateWeights(runtime, context, deltaTime)]
    ↓ (2D混合计算器内部)
[context.GetFloat(parameterX/Y)] → 获取输入向量
    ↓
[CalculateWeights2D()] → 计算三角形重心坐标
    ↓
[runtime.mixer.SetInputWeight()] → 更新各Clip权重
    ↓
[playableGraph.Evaluate(deltaTime)]
    ↓
[AnimationOutput → Animator] → 最终输出到角色
```

### 为什么SimpleClip不需要StateContext？

SimpleClip的`UpdateWeights`是空方法，因为单个Clip不需要动态混合：

```csharp
public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
{
    // 单Clip无需更新权重
}
```

而混合树计算器需要每帧读取参数并计算权重。但现在即使不调用UpdateWeights，也有默认行为：

**1D混合树初始化逻辑：**
```csharp
// 查找第一个有效Clip
int firstValidIndex = -1;
for (int i = 0; i < samples.Length; i++)
{
    if (samples[i].clip != null)
    {
        // 连接Playable
        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
        
        if (firstValidIndex == -1)
            firstValidIndex = i;
    }
}

// ★ 设置默认权重：第一个Clip权重为1
if (firstValidIndex >= 0)
{
    runtime.mixer.SetInputWeight(firstValidIndex, 1f);
    runtime.lastInput = samples[firstValidIndex].threshold;
}
```

**2D混合树初始化逻辑：**
```csharp
// 查找最接近中心点(0,0)的采样点
int centerIndex = -1;
float minDistToCenter = float.MaxValue;

for (int i = 0; i < samples.Length; i++)
{
    if (samples[i].clip != null)
    {
        float distToCenter = samples[i].position.sqrMagnitude;
        if (distToCenter < minDistToCenter)
        {
            minDistToCenter = distToCenter;
            centerIndex = i;
        }
    }
}

// ★ 设置默认权重：中心点权重为1
if (centerIndex >= 0)
{
    runtime.mixer.SetInputWeight(centerIndex, 1f);
    runtime.lastInput2D = samples[centerIndex].position;
}
```

调用UpdateWeights后，权重会根据输入参数动态更新：

```csharp
public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
{
    float paramX = context.GetFloat(parameterX, 0f);  // ★ 必须从context读取
    float paramY = context.GetFloat(parameterY, 0f);
    Vector2 input = new Vector2(paramX, paramY);
    
    CalculateWeights2D(runtime, input);  // 根据输入计算各Clip权重
}
```

## 总结

**修复前：** 
1. 2D混合计算器的UpdateWeights调用被注释，导致权重永不更新
2. 所有混合树初始化后权重全为0，必须调用UpdateWeights才能看到动画

**修复后：** 
1. 每帧通过StateContext传递参数，计算器动态更新权重
2. 混合树初始化时设置默认权重，即使不更新参数也能播放默认动画

**使用要点：**
1. **自动播放**：状态激活后立即播放默认动画（1D的第一个Clip / 2D的中心点）
2. **动态混合**：通过`stateMachine.SetFloat()`设置参数，UpdateWeights会自动调用
3. **零配置启动**：不设置任何参数时也能看到动画效果
4. 调用`stateMachine.UpdateStateMachine()`驱动整个系统

**适用场景：**
- ✅ 8方向移动系统（Idle → Walk → Run）
- ✅ Aim Offset（Yaw/Pitch瞄准偏移）
- ✅ 任何需要2D空间平滑混合的动画
- ✅ 快速原型测试（无需立即配置参数）
