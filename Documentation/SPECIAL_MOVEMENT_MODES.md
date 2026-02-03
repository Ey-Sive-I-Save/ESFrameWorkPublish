# 飞行/游泳/驾驶运动状态控制系统

## 🎯 概述

本文档设计了统一的"特殊运动模式"系统，涵盖飞行、游泳、驾驶等非标准地面运动。

### 核心特性

- **统一API** - 所有特殊运动共享相同的参数和状态管理
- **物理模拟** - 支持浮力、阻力、加速度等物理特性
- **状态转换** - 平滑进入/退出特殊运动模式
- **动画系统** - 使用2D BlendTree实现360度方向控制
- **零GC设计** - 所有运行时数据预分配

---

## 🏗️ 架构设计

### 运动模式枚举

```csharp
public enum MovementMode
{
    Grounded = 0,    // 地面运动（Walk/Run）
    Airborne = 1,    // 空中运动（跳跃/下落）
    Flying = 2,      // 飞行模式
    Swimming = 3,    // 游泳模式
    Driving = 4,     // 驾驶模式
    Climbing = 5,    // 攀爬模式
}
```

### 参数扩展

```csharp
public enum StateDefaultFloatParameter
{
    // ... 现有参数 (1-15) ...
    
    // ===== 特殊运动参数 (16-25) =====
    MovementMode = 16,          // 运动模式（0-5）
    VerticalInput = 17,         // 垂直输入（-1上升, +1下降）
    HorizontalInput = 18,       // 水平输入（前后移动）
    StrafeInput = 19,           // 横移输入（左右移动）
    
    // 飞行参数
    Altitude = 20,              // 高度（米）
    PitchAngle = 21,            // 俯仰角（度）
    RollAngle = 22,             // 翻滚角（度）
    GlideRatio = 23,            // 滑翔比例
    
    // 游泳参数
    WaterDepth = 24,            // 水深（米）
    OxygenLevel = 25,           // 氧气水平（0-1）
    
    // 驾驶参数
    SteeringAngle = 26,         // 转向角（度）
    Throttle = 27,              // 油门（0-1）
    BrakeForce = 28,            // 刹车力（0-1）
}
```

---

## 🛫 飞行系统设计

### 物理模型

```csharp
public class FlyingMovementModule : EntityBasicModuleBase
{
    [Header("飞行参数")]
    [Tooltip("最大飞行速度（m/s）")]
    public float maxFlySpeed = 15f;
    
    [Tooltip("上升/下降速度（m/s）")]
    public float verticalSpeed = 5f;
    
    [Tooltip("加速度（m/s²）")]
    public float acceleration = 10f;
    
    [Tooltip("阻力系数")]
    public float drag = 0.5f;
    
    [Tooltip("最大俯仰角（度）")]
    public float maxPitchAngle = 60f;
    
    [Tooltip("最大翻滚角（度）")]
    public float maxRollAngle = 45f;
    
    // 当前状态
    private Vector3 _velocity;
    private float _currentPitch;
    private float _currentRoll;
    
    protected override void Update()
    {
        if (MyCore == null || MyCore.stateDomain == null) return;
        
        // 1. 获取输入
        Vector3 input = new Vector3(
            MyCore.stateDomain.stateMachine.stateContext.StrafeInput,
            MyCore.stateDomain.stateMachine.stateContext.VerticalInput,
            MyCore.stateDomain.stateMachine.stateContext.HorizontalInput
        );
        
        // 2. 应用加速度
        Vector3 targetVelocity = input * maxFlySpeed;
        _velocity = Vector3.MoveTowards(_velocity, targetVelocity, acceleration * Time.deltaTime);
        
        // 3. 应用阻力
        _velocity *= (1f - drag * Time.deltaTime);
        
        // 4. 更新位置
        transform.position += _velocity * Time.deltaTime;
        
        // 5. 更新姿态（俯仰和翻滚）
        float targetPitch = -input.y * maxPitchAngle;
        float targetRoll = -input.x * maxRollAngle;
        
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, 5f * Time.deltaTime);
        _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, 5f * Time.deltaTime);
        
        transform.rotation = Quaternion.Euler(_currentPitch, transform.eulerAngles.y, _currentRoll);
        
        // 6. 更新Context参数
        var context = MyCore.stateDomain.stateMachine.stateContext;
        context.Speed = _velocity.magnitude;
        context.SpeedX = _velocity.x;
        context.SpeedY = _velocity.y;
        context.SpeedZ = _velocity.z;
        context.Altitude = transform.position.y;
        context.PitchAngle = _currentPitch;
        context.RollAngle = _currentRoll;
    }
}
```

### 动画配置（使用2D BlendTree）

```csharp
// 飞行动画BlendTree - 8方向
var flyingAnimator = new StateAnimationMixCalculatorForBlendTree2DFreeformDirectional
{
    parameterX = StateDefaultFloatParameter.StrafeInput,
    parameterY = StateDefaultFloatParameter.HorizontalInput,
    smoothTime = 0.2f,
    samples = new[]
    {
        // 中心 - 悬停
        new ClipSample2D { clip = flyHoverClip, position = Vector2.zero },
        
        // 8方向飞行
        new ClipSample2D { clip = flyForwardClip, position = new Vector2(0, 1) },      // 前
        new ClipSample2D { clip = flyBackClip, position = new Vector2(0, -1) },        // 后
        new ClipSample2D { clip = flyLeftClip, position = new Vector2(-1, 0) },        // 左
        new ClipSample2D { clip = flyRightClip, position = new Vector2(1, 0) },        // 右
        new ClipSample2D { clip = flyForwardLeftClip, position = new Vector2(-0.7f, 0.7f) },
        new ClipSample2D { clip = flyForwardRightClip, position = new Vector2(0.7f, 0.7f) },
        new ClipSample2D { clip = flyBackLeftClip, position = new Vector2(-0.7f, -0.7f) },
        new ClipSample2D { clip = flyBackRightClip, position = new Vector2(0.7f, -0.7f) },
        
        // 垂直运动（使用secondaryClip）
        new ClipSample2D { clip = flyAscendClip, position = new Vector2(0, 0.5f) },    // 上升
        new ClipSample2D { clip = flyDescendClip, position = new Vector2(0, -0.5f) },  // 下降
    }
};
```

### 状态转换

```csharp
// 地面 → 飞行
var groundToFly = new StateTransition
{
    targetStateName = "Flying",
    conditions = new List<StateCondition>
    {
        new StateCondition
        {
            parameterName = "FlyTrigger",  // 按下飞行键
            conditionType = ConditionType.Greater,
            floatValue = 0.5f
        },
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.IsGrounded,
            conditionType = ConditionType.Less,
            floatValue = 0.5f  // 必须在空中
        }
    }
};

// 飞行 → 地面
var flyToGround = new StateTransition
{
    targetStateName = "Locomotion",
    conditions = new List<StateCondition>
    {
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.IsGrounded,
            conditionType = ConditionType.Greater,
            floatValue = 0.5f  // 接地
        },
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.Speed,
            conditionType = ConditionType.Less,
            floatValue = 1f  // 低速
        }
    }
};
```

---

## 🏊 游泳系统设计

### 物理模型

```csharp
public class SwimmingMovementModule : EntityBasicModuleBase
{
    [Header("游泳参数")]
    [Tooltip("最大游泳速度（m/s）")]
    public float maxSwimSpeed = 8f;
    
    [Tooltip("水面浮力")]
    public float buoyancy = 9.8f;
    
    [Tooltip("水阻力系数")]
    public float waterDrag = 2f;
    
    [Tooltip("潜水下沉速度（m/s）")]
    public float diveSpeed = 3f;
    
    [Tooltip("氧气消耗速度（/秒）")]
    public float oxygenConsumptionRate = 0.1f;
    
    [Tooltip("水面呼吸恢复速度（/秒）")]
    public float oxygenRecoveryRate = 0.5f;
    
    private Vector3 _swimVelocity;
    private float _oxygenLevel = 1f;
    private float _waterSurfaceY = 0f;
    
    protected override void Update()
    {
        if (MyCore == null || MyCore.stateDomain == null) return;
        
        var context = MyCore.stateDomain.stateMachine.stateContext;
        
        // 1. 检测水深
        float depthBelowSurface = _waterSurfaceY - transform.position.y;
        context.WaterDepth = depthBelowSurface;
        
        // 2. 获取输入
        Vector3 input = new Vector3(
            context.StrafeInput,
            context.VerticalInput,
            context.HorizontalInput
        );
        
        // 3. 应用游泳速度
        Vector3 targetVelocity = input * maxSwimSpeed;
        _swimVelocity = Vector3.Lerp(_swimVelocity, targetVelocity, 5f * Time.deltaTime);
        
        // 4. 应用浮力（在水中时）
        if (depthBelowSurface > 0f)
        {
            _swimVelocity.y += buoyancy * Time.deltaTime;
            
            // 消耗氧气（深水时）
            if (depthBelowSurface > 1f)
            {
                _oxygenLevel -= oxygenConsumptionRate * Time.deltaTime;
                _oxygenLevel = Mathf.Max(0f, _oxygenLevel);
            }
        }
        else
        {
            // 在水面恢复氧气
            _oxygenLevel += oxygenRecoveryRate * Time.deltaTime;
            _oxygenLevel = Mathf.Min(1f, _oxygenLevel);
        }
        
        // 5. 应用水阻力
        _swimVelocity *= (1f - waterDrag * Time.deltaTime);
        
        // 6. 更新位置
        transform.position += _swimVelocity * Time.deltaTime;
        
        // 7. 限制最大深度
        if (transform.position.y < _waterSurfaceY - 50f)
        {
            transform.position = new Vector3(
                transform.position.x,
                _waterSurfaceY - 50f,
                transform.position.z
            );
        }
        
        // 8. 更新Context
        context.Speed = _swimVelocity.magnitude;
        context.SpeedX = _swimVelocity.x;
        context.SpeedY = _swimVelocity.y;
        context.SpeedZ = _swimVelocity.z;
        context.OxygenLevel = _oxygenLevel;
    }
}
```

### 动画配置

```csharp
// 游泳动画 - 分层系统
// Layer 0: 基础游泳动作
var swimBaseCalculator = new StateAnimationMixCalculatorForBlendTree2DFreeformDirectional
{
    parameterX = StateDefaultFloatParameter.StrafeInput,
    parameterY = StateDefaultFloatParameter.HorizontalInput,
    smoothTime = 0.15f,
    samples = new[]
    {
        new ClipSample2D { clip = swimIdleClip, position = Vector2.zero },
        new ClipSample2D { clip = swimForwardClip, position = new Vector2(0, 1) },
        new ClipSample2D { clip = swimBackClip, position = new Vector2(0, -1) },
        new ClipSample2D { clip = swimLeftClip, position = new Vector2(-1, 0) },
        new ClipSample2D { clip = swimRightClip, position = new Vector2(1, 0) },
    }
};

// Layer 1: 深度混合（表层/深水）
var depthBlendCalculator = new StateAnimationMixCalculatorForBlendTree1D
{
    parameterFloat = StateDefaultFloatParameter.WaterDepth,
    smoothTime = 0.2f,
    samples = new[]
    {
        new ClipSampleForBlend1D { clip = surfaceSwimClip, threshold = 0f },   // 水面
        new ClipSampleForBlend1D { clip = underwaterSwimClip, threshold = 5f }, // 深水
    }
};

// Layer 2: 垂直运动（浮起/下潜）
var verticalSwimCalculator = new StateAnimationMixCalculatorForBlendTree1D
{
    parameterFloat = StateDefaultFloatParameter.VerticalInput,
    smoothTime = 0.1f,
    samples = new[]
    {
        new ClipSampleForBlend1D { clip = diveDownClip, threshold = -1f },    // 下潜
        new ClipSampleForBlend1D { clip = swimNeutralClip, threshold = 0f },  // 平游
        new ClipSampleForBlend1D { clip = surfaceUpClip, threshold = 1f },    // 浮起
    }
};
```

### 氧气系统UI

```csharp
public class OxygenUI : MonoBehaviour
{
    public Image oxygenBar;
    private StateMachineContext _context;
    
    void Update()
    {
        float oxygen = _context.OxygenLevel;
        oxygenBar.fillAmount = oxygen;
        
        // 氧气耗尽警告
        if (oxygen < 0.2f)
        {
            oxygenBar.color = Color.red;
            // 应用伤害或强制浮出水面
        }
        else
        {
            oxygenBar.color = Color.Lerp(Color.yellow, Color.green, oxygen);
        }
    }
}
```

---

## 🚗 驾驶系统设计

### 车辆控制模块

```csharp
public class VehicleDrivingModule : EntityBasicModuleBase
{
    [Header("车辆参数")]
    [Tooltip("最大速度（km/h）")]
    public float maxSpeed = 120f;
    
    [Tooltip("加速度（m/s²）")]
    public float acceleration = 15f;
    
    [Tooltip("刹车减速度（m/s²）")]
    public float brakeDeceleration = 30f;
    
    [Tooltip("最大转向角（度）")]
    public float maxSteeringAngle = 35f;
    
    [Tooltip("转向速度（度/秒）")]
    public float steeringSpeed = 180f;
    
    private float _currentSpeed;
    private float _currentSteering;
    
    protected override void Update()
    {
        if (MyCore == null || MyCore.stateDomain == null) return;
        
        var context = MyCore.stateDomain.stateMachine.stateContext;
        
        // 1. 获取输入
        float throttle = context.Throttle;
        float brake = context.BrakeForce;
        float steering = context.StrafeInput;
        
        // 2. 加速/减速
        if (throttle > 0.1f)
        {
            _currentSpeed += acceleration * throttle * Time.deltaTime;
        }
        else if (brake > 0.1f)
        {
            _currentSpeed -= brakeDeceleration * brake * Time.deltaTime;
        }
        else
        {
            // 自然减速
            _currentSpeed -= 5f * Time.deltaTime;
        }
        
        _currentSpeed = Mathf.Clamp(_currentSpeed, 0f, maxSpeed / 3.6f); // km/h → m/s
        
        // 3. 转向
        float targetSteering = steering * maxSteeringAngle;
        _currentSteering = Mathf.MoveTowards(_currentSteering, targetSteering, steeringSpeed * Time.deltaTime);
        
        // 4. 更新位置和朝向
        if (_currentSpeed > 0.1f)
        {
            // 基于速度的转向半径
            float turnRadius = _currentSpeed / Mathf.Tan(Mathf.Deg2Rad * Mathf.Abs(_currentSteering));
            float angularVelocity = _currentSpeed / turnRadius * Mathf.Sign(_currentSteering);
            
            transform.Rotate(0f, angularVelocity * Mathf.Rad2Deg * Time.deltaTime, 0f);
            transform.position += transform.forward * _currentSpeed * Time.deltaTime;
        }
        
        // 5. 更新Context
        context.Speed = _currentSpeed;
        context.SpeedZ = _currentSpeed;  // 前进方向
        context.SteeringAngle = _currentSteering;
        
        // 动画参数
        context.LocomotionState = _currentSpeed > 0.5f ? 2f : 0f; // Idle/Driving
    }
}
```

### 驾驶动画

```csharp
// 驾驶动画 - 简单1D混合（速度）
var drivingCalculator = new StateAnimationMixCalculatorForBlendTree1D
{
    parameterFloat = StateDefaultFloatParameter.Speed,
    smoothTime = 0.15f,
    samples = new[]
    {
        new ClipSampleForBlend1D { clip = driveIdleClip, threshold = 0f },      // 静止
        new ClipSampleForBlend1D { clip = driveSlowClip, threshold = 5f },      // 慢速
        new ClipSampleForBlend1D { clip = driveNormalClip, threshold = 15f },   // 正常
        new ClipSampleForBlend1D { clip = driveFastClip, threshold = 30f },     // 高速
    }
};

// 转向动画（可选的additiveLayer）
var steeringCalculator = new StateAnimationMixCalculatorForBlendTree1D
{
    parameterFloat = StateDefaultFloatParameter.SteeringAngle,
    smoothTime = 0.05f,
    samples = new[]
    {
        new ClipSampleForBlend1D { clip = steerLeftClip, threshold = -35f },    // 左转
        new ClipSampleForBlend1D { clip = steerCenterClip, threshold = 0f },    // 直行
        new ClipSampleForBlend1D { clip = steerRightClip, threshold = 35f },    // 右转
    }
};
```

### 进入/退出车辆

```csharp
public class VehicleInteraction : MonoBehaviour
{
    public GameObject vehicle;
    private bool _isDriving;
    
    public void EnterVehicle(Entity player)
    {
        // 1. 切换运动模式
        player.stateDomain.stateMachine.stateContext.MovementMode = (float)MovementMode.Driving;
        
        // 2. 禁用角色碰撞
        player.GetComponent<Collider>().enabled = false;
        
        // 3. 绑定到车辆
        player.transform.SetParent(vehicle.transform);
        player.transform.localPosition = Vector3.zero;
        
        // 4. 切换到驾驶状态
        player.stateDomain.stateMachine.TransitionTo("Driving");
        
        // 5. 激活车辆模块
        player.basicDomain.ActivateModule<VehicleDrivingModule>();
        
        _isDriving = true;
    }
    
    public void ExitVehicle(Entity player)
    {
        // 1. 恢复运动模式
        player.stateDomain.stateMachine.stateContext.MovementMode = (float)MovementMode.Grounded;
        
        // 2. 启用角色碰撞
        player.GetComponent<Collider>().enabled = true;
        
        // 3. 脱离车辆
        player.transform.SetParent(null);
        player.transform.position = vehicle.transform.position + vehicle.transform.right * 2f;
        
        // 4. 切换回地面状态
        player.stateDomain.stateMachine.TransitionTo("Locomotion");
        
        // 5. 停用车辆模块
        player.basicDomain.DeactivateModule<VehicleDrivingModule>();
        
        _isDriving = false;
    }
}
```

---

## 🔄 统一状态转换系统

### MovementMode管理器

```csharp
public class MovementModeManager
{
    private StateMachineContext _context;
    private Entity _entity;
    
    public void SetMovementMode(MovementMode mode)
    {
        float oldMode = _context.MovementMode;
        _context.MovementMode = (float)mode;
        
        // 触发模式切换事件
        OnMovementModeChanged((MovementMode)oldMode, mode);
    }
    
    private void OnMovementModeChanged(MovementMode oldMode, MovementMode newMode)
    {
        // 退出旧模式
        switch (oldMode)
        {
            case MovementMode.Grounded:
                _entity.basicDomain.DeactivateModule<EntityBasicMoveRotateModule>();
                break;
            case MovementMode.Flying:
                _entity.basicDomain.DeactivateModule<FlyingMovementModule>();
                break;
            case MovementMode.Swimming:
                _entity.basicDomain.DeactivateModule<SwimmingMovementModule>();
                break;
            case MovementMode.Driving:
                _entity.basicDomain.DeactivateModule<VehicleDrivingModule>();
                break;
        }
        
        // 进入新模式
        switch (newMode)
        {
            case MovementMode.Grounded:
                _entity.basicDomain.ActivateModule<EntityBasicMoveRotateModule>();
                _entity.stateDomain.stateMachine.TransitionTo("Locomotion");
                break;
            case MovementMode.Flying:
                _entity.basicDomain.ActivateModule<FlyingMovementModule>();
                _entity.stateDomain.stateMachine.TransitionTo("Flying");
                break;
            case MovementMode.Swimming:
                _entity.basicDomain.ActivateModule<SwimmingMovementModule>();
                _entity.stateDomain.stateMachine.TransitionTo("Swimming");
                break;
            case MovementMode.Driving:
                _entity.basicDomain.ActivateModule<VehicleDrivingModule>();
                _entity.stateDomain.stateMachine.TransitionTo("Driving");
                break;
        }
    }
}
```

---

## 📊 性能优化

### 模块池化

```csharp
public class MovementModulePool
{
    private Dictionary<Type, Stack<EntityBasicModuleBase>> _modulePools = new Dictionary<Type, Stack<EntityBasicModuleBase>>();
    
    public T GetModule<T>() where T : EntityBasicModuleBase, new()
    {
        var type = typeof(T);
        if (!_modulePools.TryGetValue(type, out var pool) || pool.Count == 0)
        {
            return new T();
        }
        return (T)pool.Pop();
    }
    
    public void ReturnModule<T>(T module) where T : EntityBasicModuleBase
    {
        var type = typeof(T);
        if (!_modulePools.ContainsKey(type))
        {
            _modulePools[type] = new Stack<EntityBasicModuleBase>();
        }
        _modulePools[type].Push(module);
    }
}
```

---

## 🔗 相关文档

- [WALK_RUN_LOCOMOTION_SYSTEM.md](./WALK_RUN_LOCOMOTION_SYSTEM.md) - 地面运动系统
- [ENUM_PARAMETER_OPTIMIZATION_REVIEW.md](./ENUM_PARAMETER_OPTIMIZATION_REVIEW.md) - 参数优化

---

*最后更新: 2026-02-04*
*作者: ES Framework Team*
