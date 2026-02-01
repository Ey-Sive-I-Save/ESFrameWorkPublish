using System;
using UnityEngine;

namespace ES.Optimizations
{
    /// <summary>
    /// 共享StateContext的语法糖扩展
    /// 
    /// 设计原则：
    /// - StateContext是整个状态机的全局共享容器，不属于任何单个State
    /// - 这些扩展方法是对共享Context的便捷操作
    /// - State通过StateRuntime.Context访问共享Context
    /// </summary>
    public static class SharedContextExtensions
    {
        #region Float参数操作（共享Context）
        
        /// <summary>
        /// 获取共享Float参数(带默认值)
        /// </summary>
        public static float Get(this StateContext sharedCtx, string name, float defaultValue = 0f)
        {
            return sharedCtx.GetFloat(name, defaultValue);
        }
        
        /// <summary>
        /// 设置共享Float参数(Fluent API)
        /// </summary>
        public static StateContext Set(this StateContext sharedCtx, string name, float value)
        {
            sharedCtx.SetFloat(name, value);
            return sharedCtx;
        }
        
        /// <summary>
        /// 递增共享Float参数
        /// </summary>
        public static StateContext Increment(this StateContext sharedCtx, string name, float delta = 1f)
        {
            float current = sharedCtx.GetFloat(name, 0f);
            sharedCtx.SetFloat(name, current + delta);
            return sharedCtx;
        }
        
        /// <summary>
        /// 递减共享Float参数
        /// </summary>
        public static StateContext Decrement(this StateContext sharedCtx, string name, float delta = 1f)
        {
            float current = sharedCtx.GetFloat(name, 0f);
            sharedCtx.SetFloat(name, current - delta);
            return sharedCtx;
        }
        
        /// <summary>
        /// 限制共享Float参数范围
        /// </summary>
        public static StateContext Clamp(this StateContext sharedCtx, string name, float min, float max)
        {
            float current = sharedCtx.GetFloat(name, 0f);
            sharedCtx.SetFloat(name, Mathf.Clamp(current, min, max));
            return sharedCtx;
        }
        
        #endregion
        
        #region Int参数操作（共享Context）
        
        /// <summary>
        /// 获取共享Int参数(带默认值)
        /// </summary>
        public static int GetInt(this StateContext sharedCtx, string name, int defaultValue = 0)
        {
            return sharedCtx.GetInt(name, defaultValue);
        }
        
        /// <summary>
        /// 设置共享Int参数(Fluent API)
        /// </summary>
        public static StateContext SetInt(this StateContext sharedCtx, string name, int value)
        {
            sharedCtx.SetInt(name, value);
            return sharedCtx;
        }
        
        /// <summary>
        /// 递增共享Int参数
        /// </summary>
        public static StateContext IncrementInt(this StateContext sharedCtx, string name, int delta = 1)
        {
            int current = sharedCtx.GetInt(name, 0);
            sharedCtx.SetInt(name, current + delta);
            return sharedCtx;
        }
        
        #endregion
        
        #region Bool参数操作（共享Context）
        
        /// <summary>
        /// 切换共享Bool参数
        /// </summary>
        public static StateContext Toggle(this StateContext sharedCtx, string name)
        {
            bool current = sharedCtx.GetBool(name, false);
            sharedCtx.SetBool(name, !current);
            return sharedCtx;
        }
        
        /// <summary>
        /// 多个Bool参数AND运算
        /// </summary>
        public static bool AllTrue(this StateContext sharedCtx, params string[] names)
        {
            foreach (var name in names)
            {
                if (!sharedCtx.GetBool(name, false))
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// 多个Bool参数OR运算
        /// </summary>
        public static bool AnyTrue(this StateContext sharedCtx, params string[] names)
        {
            foreach (var name in names)
            {
                if (sharedCtx.GetBool(name, false))
                    return true;
            }
            return false;
        }
        
        #endregion
        
        #region Trigger操作（共享Context）
        
        /// <summary>
        /// 激活共享Trigger(简写)
        /// </summary>
        public static StateContext Fire(this StateContext sharedCtx, string triggerName)
        {
            sharedCtx.SetTrigger(triggerName);
            return sharedCtx;
        }
        
        /// <summary>
        /// 批量激活共享Trigger
        /// </summary>
        public static StateContext FireAll(this StateContext sharedCtx, params string[] triggers)
        {
            foreach (var trigger in triggers)
                sharedCtx.SetTrigger(trigger);
            return sharedCtx;
        }
        
        /// <summary>
        /// 检查多个Trigger是否全部激活
        /// </summary>
        public static bool AllTriggered(this StateContext sharedCtx, params string[] triggers)
        {
            foreach (var trigger in triggers)
            {
                if (!sharedCtx.GetTrigger(trigger))
                    return false;
            }
            return true;
        }
        
        #endregion
        
        #region Curve操作（共享Context）
        
        /// <summary>
        /// 设置共享曲线并采样
        /// </summary>
        public static StateContext SetCurveValue(this StateContext sharedCtx, string curveName, AnimationCurve curve, float time)
        {
            sharedCtx.SetCurve(curveName, curve);
            float value = curve.Evaluate(time);
            sharedCtx.SetFloat($"{curveName}_Value", value);
            return sharedCtx;
        }
        
        /// <summary>
        /// 获取共享曲线采样值
        /// </summary>
        public static float EvaluateCurve(this StateContext sharedCtx, string curveName, float time)
        {
            var curve = sharedCtx.GetCurve(curveName);
            return curve?.Evaluate(time) ?? 0f;
        }
        
        #endregion
        
        #region Entity操作（共享Context）
        
        /// <summary>
        /// 检查共享Entity是否存在
        /// </summary>
        public static bool HasEntity(this StateContext sharedCtx, string name)
        {
            return sharedCtx.GetEntity(name) != null;
        }
        
        /// <summary>
        /// 安全获取共享Entity组件
        /// </summary>
        public static T GetEntityComponent<T>(this StateContext sharedCtx, string entityName) where T : Component
        {
            var obj = sharedCtx.GetEntity(entityName);
            if (obj is GameObject go)
                return go.GetComponent<T>();
            if (obj is Component comp)
                return comp.GetComponent<T>();
            return null;
        }
        
        #endregion
        
        #region 链式调用（共享Context）
        
        /// <summary>
        /// 快速配置共享参数
        /// </summary>
        public static StateContext Configure(this StateContext sharedCtx, Action<StateContext> config)
        {
            config?.Invoke(sharedCtx);
            return sharedCtx;
        }
        
        /// <summary>
        /// 条件性设置共享参数
        /// </summary>
        public static StateContext SetIf(this StateContext sharedCtx, bool condition, string name, float value)
        {
            if (condition)
                sharedCtx.SetFloat(name, value);
            return sharedCtx;
        }
        
        /// <summary>
        /// 批量设置共享Float参数
        /// </summary>
        public static StateContext SetMultiple(this StateContext sharedCtx, params (string name, float value)[] pairs)
        {
            foreach (var pair in pairs)
                sharedCtx.SetFloat(pair.name, pair.value);
            return sharedCtx;
        }
        
        #endregion
        
        #region Debug辅助（共享Context）
        
        /// <summary>
        /// 打印共享参数值
        /// </summary>
        public static StateContext DebugLog(this StateContext sharedCtx, string name)
        {
            float f = sharedCtx.GetFloat(name, float.NaN);
            if (!float.IsNaN(f))
            {
                Debug.Log($"[SharedContext] {name} = {f}");
                return sharedCtx;
            }
            
            int i = sharedCtx.GetInt(name, int.MinValue);
            if (i != int.MinValue)
            {
                Debug.Log($"[SharedContext] {name} = {i}");
                return sharedCtx;
            }
            
            bool b = sharedCtx.GetBool(name, false);
            Debug.Log($"[SharedContext] {name} = {b}");
            return sharedCtx;
        }
        
        #endregion
    }
    
    /// <summary>
    /// 共享参数名常量 - 避免硬编码字符串
    /// 注意：这些是全局共享的Context参数，不属于任何单个State
    /// </summary>
    public static class SharedParamNames
    {
        // 通用运动参数
        public const string Speed = "Speed";
        public const string MoveSpeed = "MoveSpeed";
        public const string Direction = "Direction";
        public const string DirectionX = "DirectionX";
        public const string DirectionY = "DirectionY";
        
        // 状态参数
        public const string IsGrounded = "IsGrounded";
        public const string IsMoving = "IsMoving";
        public const string IsCrouching = "IsCrouching";
        public const string IsAiming = "IsAiming";
        
        // 战斗参数
        public const string ComboIndex = "ComboIndex";
        public const string AttackPower = "AttackPower";
        public const string HitReaction = "HitReaction";
        
        // Trigger
        public const string Jump = "Jump";
        public const string Attack = "Attack";
        public const string Dodge = "Dodge";
        public const string Use = "Use";
        
        // IK参数
        public const string IKWeight = "IKWeight";
        public const string HeadLookWeight = "HeadLookWeight";
        public const string HandIKWeight = "HandIKWeight";
        
        // 临时代价
        public const string StaminaCost = "StaminaCost";
        public const string WillpowerCost = "WillpowerCost";
        
        // Entity引用
        public const string Target = "Target";
        public const string Weapon = "Weapon";
        public const string InteractObject = "InteractObject";
    }
    
    /// <summary>
    /// 使用示例 - 展示如何在State中访问共享Context
    /// </summary>
    public class SharedContextUsageExample
    {
        /// <summary>
        /// 在State组件中通过StateRuntime访问共享Context
        /// </summary>
        public void ExampleInStateComponent(StateRuntime runtime)
        {
            // 从StateRuntime获取共享Context
            StateContext sharedCtx = runtime.Context;
            
            // 传统写法
            sharedCtx.SetFloat("Speed", 5.0f);
            float speed = sharedCtx.GetFloat("Speed", 0f);
            sharedCtx.SetTrigger("Jump");
            
            // 优雅的链式写法（操作共享Context）
            sharedCtx.Set(SharedParamNames.Speed, 5.0f)
                     .Set(SharedParamNames.Direction, 45f)
                     .Fire(SharedParamNames.Jump)
                     .Increment(SharedParamNames.ComboIndex);
            
            // 条件性设置
            bool isRunning = true;
            sharedCtx.SetIf(isRunning, SharedParamNames.MoveSpeed, 10f);
            
            // 批量设置
            sharedCtx.SetMultiple(
                (SharedParamNames.Speed, 5f),
                (SharedParamNames.Direction, 90f),
                (SharedParamNames.AttackPower, 100f)
            );
            
            // 逻辑运算
            if (sharedCtx.AllTrue(SharedParamNames.IsGrounded, SharedParamNames.IsMoving))
            {
                // 在地面且移动中
            }
            
            if (sharedCtx.AnyTrue(SharedParamNames.Jump, SharedParamNames.Dodge))
            {
                // 跳跃或闪避
            }
            
            // 配置块
            sharedCtx.Configure(ctx =>
            {
                ctx.SetFloat(SharedParamNames.Speed, 5f);
                ctx.SetBool(SharedParamNames.IsMoving, true);
                ctx.SetTrigger(SharedParamNames.Jump);
            });
            
            // 安全获取组件
            var target = sharedCtx.GetEntityComponent<Transform>(SharedParamNames.Target);
            if (target != null)
            {
                // 使用target
            }
        }
    }
    
    /// <summary>
    /// State专属数据定义 - 与共享Context分离
    /// 每个State实例拥有自己的专属数据，不会与其他State共享
    /// </summary>
    [Serializable]
    public class StateLocalData
    {
        // State专属的运行时数据
        public float enterTime;              // 进入时间
        public float stateTime;              // 状态持续时间
        public float normalizedTime;         // 归一化时间(0-1)
        public bool isInRecovery;            // 是否处于后摇阶段
        
        // State专属的配置数据
        public int loopCount;                // 循环次数
        public int currentComboStep;         // 当前连招步骤
        public float localSpeedMultiplier;   // 局部速度倍率
        
        // State专属的缓存数据
        public UnityEngine.Object cachedTarget;     // 缓存的目标对象
        public Vector3 cachedPosition;       // 缓存的位置
        public Quaternion cachedRotation;    // 缓存的旋转
        
        /// <summary>
        /// 重置State专属数据
        /// </summary>
        public void Reset()
        {
            enterTime = 0f;
            stateTime = 0f;
            normalizedTime = 0f;
            isInRecovery = false;
            loopCount = 0;
            currentComboStep = 0;
            localSpeedMultiplier = 1f;
            cachedTarget = null;
            cachedPosition = Vector3.zero;
            cachedRotation = Quaternion.identity;
        }
    }
}
