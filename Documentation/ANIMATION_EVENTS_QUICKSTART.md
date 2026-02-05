# 动画事件系统快速开始

> **3分钟上手ES动画事件系统**

---

## 🚀 快速示例

### 1. 创建带事件的攻击动画

```csharp
using ES;
using UnityEngine;

public class PlayerAttackState : StateBase
{
    protected override void OnAnimationEvent(string eventName, string eventParam)
    {
        switch (eventName)
        {
            case "OnHitFrame":
                // 在动画的击中帧触发
                DealDamage(50);
                PlayHitEffect();
                break;
                
            case "OnWindupComplete":
                // 蓄力完成
                canCancel = false; // 不可取消
                break;
                
            case "OnRecoveryStart":
                // 进入恢复期
                canCancel = true; // 可取消
                break;
        }
    }
    
    private void DealDamage(int damage)
    {
        Debug.Log($"造成{damage}点伤害");
        // 实际伤害逻辑...
    }
    
    private void PlayHitEffect()
    {
        // 播放命中特效
    }
}
```

---

### 2. 配置AnimationClipConfig（未来版本）

```csharp
// 创建攻击动画配置
var attackConfig = new AnimationClipConfig
{
    clip = attackClip,
    speed = 1.2f,
    triggerEvents = new List<TriggerEventAt>
    {
        // 事件1：击中帧（30%进度）
        new TriggerEventAt
        {
            normalizedTime = 0.3f,
            eventName = "OnHitFrame",
            eventParam = "damage:50",
            triggerOnce = true
        },
        
        // 事件2：恢复期开始（70%进度）
        new TriggerEventAt
        {
            normalizedTime = 0.7f,
            eventName = "OnRecoveryStart",
            triggerOnce = true
        }
    }
};
```

---

### 3. 在StateMachine上监听事件

```csharp
using ES;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public StateMachine stateMachine;
    
    void Start()
    {
        // 注册动画事件监听
        stateMachine.OnAnimationEvent += HandleAnimationEvent;
    }
    
    void OnDestroy()
    {
        // 取消监听
        stateMachine.OnAnimationEvent -= HandleAnimationEvent;
    }
    
    private void HandleAnimationEvent(StateBase state, string eventName, string eventParam)
    {
        Debug.Log($"[AnimEvent] State:{state.strKey} | Event:{eventName} | Param:{eventParam}");
        
        switch (eventName)
        {
            case "OnFootstep":
                PlayFootstepSound();
                break;
                
            case "OnHitFrame":
                // 解析参数
                if (eventParam.StartsWith("damage:"))
                {
                    int damage = int.Parse(eventParam.Substring(7));
                    DealDamage(damage);
                }
                break;
                
            case "OnWeaponTrailStart":
                EnableWeaponTrail(true);
                break;
                
            case "OnWeaponTrailEnd":
                EnableWeaponTrail(false);
                break;
        }
    }
    
    private void PlayFootstepSound()
    {
        // 播放脚步声
    }
    
    private void DealDamage(int damage)
    {
        // 造成伤害
    }
    
    private void EnableWeaponTrail(bool enable)
    {
        // 控制武器拖尾特效
    }
}
```

---

### 4. 临时状态（播放一次退出）

```csharp
using ES;
using UnityEngine;

public class EnemyHitReaction : MonoBehaviour
{
    public StateMachine enemyStateMachine;
    public AnimationClip knockbackClip;
    
    public void OnHit(Vector3 hitDirection)
    {
        // 播放受击动画（播放一次自动退出）
        enemyStateMachine.AddTemporaryAnimation(
            tempKey: "Knockback",
            clip: knockbackClip,
            pipeline: StatePipelineType.Main,
            speed: 1.0f,
            loopable: false  // ✅ 播放一次退出
        );
        
        // 监听退出事件
        enemyStateMachine.OnStateExited += OnKnockbackComplete;
    }
    
    private void OnKnockbackComplete(StateBase state, StatePipelineType pipeline)
    {
        if (state.strKey.Contains("__temp_Knockback"))
        {
            Debug.Log("受击动画播放完毕，恢复正常");
            
            // 取消监听
            enemyStateMachine.OnStateExited -= OnKnockbackComplete;
            
            // 恢复待机状态
            enemyStateMachine.TryActivateState("Idle");
        }
    }
}
```

---

### 5. 循环临时状态（持续效果）

```csharp
public class BuffSystem : MonoBehaviour
{
    public StateMachine playerStateMachine;
    public AnimationClip burningClip;
    
    public void ApplyBurningBuff(float duration)
    {
        // 播放燃烧动画（循环播放）
        playerStateMachine.AddTemporaryAnimation(
            tempKey: "Burning",
            clip: burningClip,
            pipeline: StatePipelineType.Buff,
            speed: 1.0f,
            loopable: true  // ✅ 循环播放
        );
        
        // duration秒后移除
        StartCoroutine(RemoveBuffAfterDelay(duration));
    }
    
    private IEnumerator RemoveBuffAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 移除临时状态
        playerStateMachine.RemoveTemporaryAnimation("Burning");
        Debug.Log("燃烧Buff结束");
    }
}
```

---

## 🎯 常见用例

### 用例1：脚步声

```csharp
// 在行走动画的脚接触地面时触发
var walkConfig = new AnimationClipConfig
{
    clip = walkClip,
    triggerEvents = new List<TriggerEventAt>
    {
        new TriggerEventAt { normalizedTime = 0.2f, eventName = "OnFootstep" },
        new TriggerEventAt { normalizedTime = 0.7f, eventName = "OnFootstep" }
    }
};
```

### 用例2：武器拖尾

```csharp
// 攻击动画中控制武器拖尾特效
var slashConfig = new AnimationClipConfig
{
    clip = slashClip,
    triggerEvents = new List<TriggerEventAt>
    {
        new TriggerEventAt { normalizedTime = 0.2f, eventName = "OnWeaponTrailStart" },
        new TriggerEventAt { normalizedTime = 0.6f, eventName = "OnWeaponTrailEnd" }
    }
};
```

### 用例3：技能特效

```csharp
// 技能释放动画中的特效触发
var skillConfig = new AnimationClipConfig
{
    clip = skillClip,
    triggerEvents = new List<TriggerEventAt>
    {
        new TriggerEventAt { normalizedTime = 0.1f, eventName = "OnCastStart", eventParam = "effect:charge" },
        new TriggerEventAt { normalizedTime = 0.5f, eventName = "OnCastRelease", eventParam = "effect:fireball" },
        new TriggerEventAt { normalizedTime = 0.9f, eventName = "OnCastEnd" }
    }
};
```

---

## ⚡ 性能最佳实践

### ✅ 推荐做法

```csharp
// 1. 缓存状态引用
private StateBase _attackState;

void Start()
{
    _attackState = stateMachine.GetStateByString("Attack");
}

// 2. 使用枚举而非字符串
public enum AnimEventType
{
    OnHitFrame,
    OnFootstep,
    OnWeaponTrail
}

// 3. 避免在事件中分配内存
private void OnAnimationEvent(string eventName, string eventParam)
{
    // ❌ 避免: new GameObject(), Instantiate()
    // ✅ 推荐: 使用对象池
    EffectPool.Get(eventName);
}
```

### ❌ 避免做法

```csharp
// ❌ 避免在每帧查找状态
void Update()
{
    var state = stateMachine.GetStateByString("Attack"); // 每帧查找，性能差
}

// ❌ 避免频繁订阅/取消订阅
void Update()
{
    stateMachine.OnAnimationEvent += Handler;  // 每帧订阅，内存泄漏
}

// ❌ 避免在事件中进行复杂计算
private void OnAnimationEvent(string eventName, string eventParam)
{
    // ❌ 复杂的物理计算
    // ✅ 应该标记需要处理，在Update中处理
}
```

---

## 🐛 调试技巧

### 启用调试日志

```csharp
// 在StateMachine上启用调试
stateMachine.enableContinuousStats = true;

// 或者全局启用
StateMachineDebugSettings.Global.logStateTransitions = true;
```

### 可视化当前进度

```csharp
void OnGUI()
{
    var state = stateMachine.GetRunningStates().FirstOrDefault();
    if (state != null)
    {
        GUI.Label(new Rect(10, 10, 300, 20), 
            $"State: {state.strKey}");
        GUI.Label(new Rect(10, 30, 300, 20), 
            $"Progress: {state.normalizedProgress:F2} ({state.totalProgress:F2})");
        GUI.Label(new Rect(10, 50, 300, 20), 
            $"Loop: {state.loopCount}");
        GUI.Label(new Rect(10, 70, 300, 20), 
            $"Time: {state.hasEnterTime:F2}s");
    }
}
```

---

## 📚 相关文档

- [完整改进文档](ANIMATION_SYSTEM_IMPROVEMENTS.md)
- [改进总结](ANIMATION_SYSTEM_IMPROVEMENTS_SUMMARY.md)
- [StateSharedData快速参考](STATE_SHARED_DATA_QUICK_REFERENCE.md)
- [系统分析报告](ES_STATE_SYSTEM_ANALYSIS.md)

---

**开始使用ES动画事件系统，让你的游戏动画更生动！** 🎮✨
