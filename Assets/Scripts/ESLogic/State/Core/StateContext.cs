using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 上下文参数类型
    /// </summary>
    public enum ContextParameterType
    {
        Float,          // 浮点数
        Int,            // 整数
        Bool,           // 布尔值
        Trigger,        // 触发器(使用后自动重置)
        StateValue,     // 状态枚举值
        Entity,         // 实体对象引用
        String,         // 字符串标记
        Curve           // 曲线参数(用于IK等)
    }

    /// <summary>
    /// 上下文参数定义
    /// </summary>
    [Serializable]
    public class ContextParameter
    {
        public string name;
        public ContextParameterType type;
        
        // 不同类型的值存储
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public UnityEngine.Object entityValue;
        public AnimationCurve curveValue;
        
        public ContextParameter(string name, ContextParameterType type)
        {
            this.name = name;
            this.type = type;
        }
    }

    /// <summary>
    /// 状态机上下文 - 管理所有可变参数
    /// 
    /// 设计原则：
    /// 1. 整个状态机共享一个StateMachineContext
    /// 2. 枚举参数使用数组索引，零开销直接访问（史上最强性能）
    /// 3. 字符串参数使用字典，支持退化到ContextPool
    /// 4. 统一的Get/Set方法，支持枚举和字符串
    /// </summary>
    public class StateMachineContext
    {
        // ==================== 状态机元数据 ====================
        /// <summary>
        /// 上下文唯一标识
        /// </summary>
        public string contextID;
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public float creationTime;
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public float lastUpdateTime;
        
        /// <summary>
        /// 共享数据 - 用于存储任意类型的运行时数据
        /// </summary>
        private Dictionary<string, object> _sharedData;
        
        /// <summary>
        /// 运行时标记 - 用于状态机逻辑判断
        /// </summary>
        private HashSet<string> _runtimeFlags;
        
        // ==================== 枚举参数 - 直接字段（性能最优：无数组边界检查，CPU缓存友好）====================
        
        // ===== 核心运动参数 (1-7) =====
        public float SpeedX;
        public float SpeedY;
        public float SpeedZ;
        public float AimYaw;
        public float AimPitch;
        public float Speed;
        public float IsGrounded;
        
        // ===== 运动阈值 (8-10) =====
        public float WalkSpeedThreshold = 0.65f;  // 默认走路速度上限
        public float RunSpeedThreshold = 1.0f;    // 默认跑步速度上限
        public float SprintSpeedThreshold = 1.5f; // 默认冲刺速度上限
        
        // ===== 运动状态标记 (11-15) =====
        public float IsWalking;
        public float IsRunning;
        public float IsSprinting;
        public float IsCrouching;
        public float IsSliding;
        
        // ===== 运动控制按键 =====
        public float IsSprintKeyPressed; // 是否按住冲刺键（1=按下，0=松开）
        
        // ==================== 字符串参数 - 字典存储（支持退化） ====================
        private Dictionary<string, float> _floatParams;
        private Dictionary<string, int> _intParams;
        private Dictionary<string, bool> _boolParams;
        private Dictionary<string, string> _stringParams;
        private Dictionary<string, UnityEngine.Object> _entityParams;
        private Dictionary<string, AnimationCurve> _curveParams;
        private HashSet<string> _activeTriggers;

        // 退化到Entity的ContextPool（仅字符串参数）
        private ContextPool _fallbackContextPool;

        // 参数变更事件
        public event Action<string, float> OnFloatChanged;
        public event Action<string, int> OnIntChanged;
        public event Action<string, bool> OnBoolChanged;
        public event Action<string> OnTriggerFired;

        public StateMachineContext(ContextPool fallbackPool = null)
        {
            // 初始化元数据
            contextID = Guid.NewGuid().ToString();
            creationTime = Time.time;
            lastUpdateTime = Time.time;
            _sharedData = new Dictionary<string, object>();
            _runtimeFlags = new HashSet<string>();
            
            // 初始化参数字典
            _floatParams = new Dictionary<string, float>();
            _intParams = new Dictionary<string, int>();
            _boolParams = new Dictionary<string, bool>();
            _stringParams = new Dictionary<string, string>();
            _entityParams = new Dictionary<string, UnityEngine.Object>();
            _curveParams = new Dictionary<string, AnimationCurve>();
            _activeTriggers = new HashSet<string>();
            _fallbackContextPool = fallbackPool;
        }

        /// <summary>
        /// 设置退化ContextPool
        /// </summary>
        public void SetFallbackContextPool(ContextPool pool)
        {
            _fallbackContextPool = pool;
        }

        #region Float Parameters
        
        /// <summary>
        /// 设置默认枚举参数（直接字段访问，性能最优）
        /// </summary>
        public void SetDefaultFloat(StateDefaultFloatParameter param, float value)
        {
            switch (param)
            {
                case StateDefaultFloatParameter.SpeedX: SpeedX = value; break;
                case StateDefaultFloatParameter.SpeedY: SpeedY = value; break;
                case StateDefaultFloatParameter.SpeedZ: SpeedZ = value; break;
                case StateDefaultFloatParameter.AimYaw: AimYaw = value; break;
                case StateDefaultFloatParameter.AimPitch: AimPitch = value; break;
                case StateDefaultFloatParameter.Speed: Speed = value; break;
                case StateDefaultFloatParameter.IsGrounded: IsGrounded = value; break;
                case StateDefaultFloatParameter.WalkSpeedThreshold: WalkSpeedThreshold = value; break;
                case StateDefaultFloatParameter.RunSpeedThreshold: RunSpeedThreshold = value; break;
                case StateDefaultFloatParameter.SprintSpeedThreshold: SprintSpeedThreshold = value; break;
                case StateDefaultFloatParameter.IsWalking: IsWalking = value; break;
                case StateDefaultFloatParameter.IsRunning: IsRunning = value; break;
                case StateDefaultFloatParameter.IsSprinting: IsSprinting = value; break;
                case StateDefaultFloatParameter.IsCrouching: IsCrouching = value; break;
                case StateDefaultFloatParameter.IsSliding: IsSliding = value; break;
            }
        }
        
        /// <summary>
        /// 获取默认枚举参数（直接字段访问，性能最优）
        /// </summary>
        public float GetDefaultFloat(StateDefaultFloatParameter param, float defaultValue = 0f)
        {
            switch (param)
            {
                case StateDefaultFloatParameter.SpeedX: return SpeedX;
                case StateDefaultFloatParameter.SpeedY: return SpeedY;
                case StateDefaultFloatParameter.SpeedZ: return SpeedZ;
                case StateDefaultFloatParameter.AimYaw: return AimYaw;
                case StateDefaultFloatParameter.AimPitch: return AimPitch;
                case StateDefaultFloatParameter.Speed: return Speed;
                case StateDefaultFloatParameter.IsGrounded: return IsGrounded;
                case StateDefaultFloatParameter.WalkSpeedThreshold: return WalkSpeedThreshold;
                case StateDefaultFloatParameter.RunSpeedThreshold: return RunSpeedThreshold;
                case StateDefaultFloatParameter.SprintSpeedThreshold: return SprintSpeedThreshold;
                case StateDefaultFloatParameter.IsWalking: return IsWalking;
                case StateDefaultFloatParameter.IsRunning: return IsRunning;
                case StateDefaultFloatParameter.IsSprinting: return IsSprinting;
                case StateDefaultFloatParameter.IsCrouching: return IsCrouching;
                case StateDefaultFloatParameter.IsSliding: return IsSliding;
                default: return defaultValue;
            }
        }
        
        /// <summary>
        /// SetFloat枚举重载 - 直接调用SetDefaultFloat
        /// </summary>
        public void SetFloat(StateDefaultFloatParameter param, float value)
        {
            SetDefaultFloat(param, value);
        }
        
        /// <summary>
        /// GetFloat枚举重载 - 直接调用GetDefaultFloat
        /// </summary>
        public float GetFloat(StateDefaultFloatParameter param, float defaultValue = 0f)
        {
            return GetDefaultFloat(param, defaultValue);
        }
        
        /// <summary>
        /// 设置字符串参数
        /// </summary>
        public void SetFloat(string name, float value)
        {
            bool changed = !_floatParams.TryGetValue(name, out float oldValue) || !Mathf.Approximately(oldValue, value);
            _floatParams[name] = value;
            if (changed)
                OnFloatChanged?.Invoke(name, value);
        }

        /// <summary>
        /// 获取字符串参数（支持退化到ContextPool）
        /// </summary>
        public float GetFloat(string name, float defaultValue = 0f)
        {
            if (_floatParams.TryGetValue(name, out float value))
                return value;
            
            // 退化到ContextPool
            if (_fallbackContextPool != null)
            {
                var contextValue = _fallbackContextPool.GetValue(name);
                if (contextValue is float floatVal)
                    return floatVal;
                if (contextValue is int intVal)
                    return intVal;
            }
            
            return defaultValue;
        }
        
        /// <summary>
        /// SetFloat StateParameter重载 - 根据EnumValue自动选择路径
        /// </summary>
        public void SetFloat(StateParameter param, float value)
        {
            if (param.EnumValue != StateDefaultFloatParameter.None)
                SetDefaultFloat(param.EnumValue, value);
            else
                SetFloat(param.StringValue, value);
        }
        
        /// <summary>
        /// GetFloat StateParameter重载 - 根据EnumValue自动选择路径
        /// </summary>
        public float GetFloat(StateParameter param, float defaultValue = 0f)
        {
            if (param.EnumValue != StateDefaultFloatParameter.None)
                return GetDefaultFloat(param.EnumValue, defaultValue);
            else
                return GetFloat(param.StringValue, defaultValue);
        }

        public bool HasFloat(string name) => _floatParams.ContainsKey(name);
        
        #endregion

        #region Int Parameters
        public void SetInt(string name, int value)
        {
            bool changed = !_intParams.TryGetValue(name, out int oldValue) || oldValue != value;
            _intParams[name] = value;
            if (changed)
                OnIntChanged?.Invoke(name, value);
        }

        public int GetInt(string name, int defaultValue = 0)
        {
            return _intParams.TryGetValue(name, out int value) ? value : defaultValue;
        }

        public bool HasInt(string name) => _intParams.ContainsKey(name);
        #endregion

        #region Bool Parameters
        public void SetBool(string name, bool value)
        {
            bool changed = !_boolParams.TryGetValue(name, out bool oldValue) || oldValue != value;
            _boolParams[name] = value;
            if (changed)
                OnBoolChanged?.Invoke(name, value);
        }

        public bool GetBool(string name, bool defaultValue = false)
        {
            return _boolParams.TryGetValue(name, out bool value) ? value : defaultValue;
        }

        public bool HasBool(string name) => _boolParams.ContainsKey(name);
        #endregion

        #region Trigger Parameters
        public void SetTrigger(string name)
        {
            _activeTriggers.Add(name);
            OnTriggerFired?.Invoke(name);
        }

        public bool GetTrigger(string name)
        {
            return _activeTriggers.Contains(name);
        }

        public void ResetTrigger(string name)
        {
            _activeTriggers.Remove(name);
        }

        public void ResetAllTriggers()
        {
            _activeTriggers.Clear();
        }
        #endregion

        #region String Parameters
        public void SetString(string name, string value)
        {
            _stringParams[name] = value;
        }

        public string GetString(string name, string defaultValue = "")
        {
            return _stringParams.TryGetValue(name, out string value) ? value : defaultValue;
        }

        public bool HasString(string name) => _stringParams.ContainsKey(name);
        #endregion

        #region Entity Parameters
        public void SetEntity(string name, UnityEngine.Object entity)
        {
            _entityParams[name] = entity;
        }

        public T GetEntity<T>(string name) where T : UnityEngine.Object
        {
            return _entityParams.TryGetValue(name, out var entity) ? entity as T : null;
        }

        public UnityEngine.Object GetEntity(string name)
        {
            return _entityParams.TryGetValue(name, out var entity) ? entity : null;
        }

        public bool HasEntity(string name) => _entityParams.ContainsKey(name);
        #endregion

        #region Curve Parameters (for IK)
        public void SetCurve(string name, AnimationCurve curve)
        {
            _curveParams[name] = curve;
        }

        public AnimationCurve GetCurve(string name)
        {
            return _curveParams.TryGetValue(name, out var curve) ? curve : null;
        }

        public float EvaluateCurve(string name, float time, float defaultValue = 0f)
        {
            if (_curveParams.TryGetValue(name, out var curve) && curve != null)
                return curve.Evaluate(time);
            return defaultValue;
        }

        public bool HasCurve(string name) => _curveParams.ContainsKey(name);
        #endregion

        /// <summary>
        /// 清空所有参数
        /// </summary>
        public void Clear()
        {
            _floatParams.Clear();
            _intParams.Clear();
            _boolParams.Clear();
            _stringParams.Clear();
            _entityParams.Clear();
            _curveParams.Clear();
            _activeTriggers.Clear();
        }

        /// <summary>
        /// 每帧更新 - 重置触发器、速度限制、运动状态更新
        /// </summary>
        public void Update()
        {
            // 触发器在下一帧自动重置
            _activeTriggers.Clear();
            
            // ===== 速度限制和运动状态更新 =====
            // 计算水平速度（SpeedX/Z由移动系统控制，这里只处理速度限制）
            float horizontalSpeed = Mathf.Sqrt(SpeedX * SpeedX + SpeedZ * SpeedZ);
            
            // 根据是否按下冲刺键来限制速度
            float maxSpeed = IsSprintKeyPressed > 0.5f ? SprintSpeedThreshold : WalkSpeedThreshold;
            
            // 如果超过最大速度，进行限制
            if (horizontalSpeed > maxSpeed)
            {
                float scale = maxSpeed / horizontalSpeed;
                SpeedX *= scale;
                SpeedZ *= scale;
                horizontalSpeed = maxSpeed;
            }
            
            // 更新综合速度
            Speed = horizontalSpeed;
            
            // 更新运动状态标记（用于动画混合）
            if (horizontalSpeed > 0.01f)
            {
                if (IsSprintKeyPressed > 0.5f)
                {
                    IsSprinting = 1f;
                    IsRunning = 0f;
                    IsWalking = 0f;
                }
                else if (horizontalSpeed > RunSpeedThreshold * 0.8f)
                {
                    IsSprinting = 0f;
                    IsRunning = 1f;
                    IsWalking = 0f;
                }
                else
                {
                    IsSprinting = 0f;
                    IsRunning = 0f;
                    IsWalking = 1f;
                }
            }
            else
            {
                // 速度接近0时，清除所有运动状态
                IsWalking = 0f;
                IsRunning = 0f;
                IsSprinting = 0f;
            }
            
            // 更新时间戳
            lastUpdateTime = Time.time;
        }

        /// <summary>
        /// 拷贝参数到另一个上下文
        /// </summary>
        public void CopyTo(StateMachineContext target)
        {
            // 拷贝枚举参数字段
            target.SpeedX = SpeedX;
            target.SpeedY = SpeedY;
            target.SpeedZ = SpeedZ;
            target.AimYaw = AimYaw;
            target.AimPitch = AimPitch;
            target.Speed = Speed;
            target.IsGrounded = IsGrounded;
            target.WalkSpeedThreshold = WalkSpeedThreshold;
            target.RunSpeedThreshold = RunSpeedThreshold;
            target.SprintSpeedThreshold = SprintSpeedThreshold;
            target.IsWalking = IsWalking;
            target.IsRunning = IsRunning;
            target.IsSprinting = IsSprinting;
            target.IsCrouching = IsCrouching;
            target.IsSliding = IsSliding;
            target.IsSprintKeyPressed = IsSprintKeyPressed;
            
            foreach (var kv in _floatParams) target.SetFloat(kv.Key, kv.Value);
            foreach (var kv in _intParams) target.SetInt(kv.Key, kv.Value);
            foreach (var kv in _boolParams) target.SetBool(kv.Key, kv.Value);
            foreach (var kv in _stringParams) target.SetString(kv.Key, kv.Value);
            foreach (var kv in _entityParams) target.SetEntity(kv.Key, kv.Value);
            foreach (var kv in _curveParams) target.SetCurve(kv.Key, kv.Value);
            foreach (var trigger in _activeTriggers) target.SetTrigger(trigger);
        }
        
        #region 共享数据管理（原StateMachineContext功能）
        
        /// <summary>
        /// 设置共享数据
        /// </summary>
        public void SetData<T>(string key, T value)
        {
            _sharedData[key] = value;
        }

        /// <summary>
        /// 获取共享数据
        /// </summary>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_sharedData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }
        
        /// <summary>
        /// 检查共享数据是否存在
        /// </summary>
        public bool HasData(string key)
        {
            return _sharedData.ContainsKey(key);
        }
        
        /// <summary>
        /// 移除共享数据
        /// </summary>
        public bool RemoveData(string key)
        {
            return _sharedData.Remove(key);
        }

        /// <summary>
        /// 添加运行时标记
        /// </summary>
        public void AddFlag(string flag)
        {
            _runtimeFlags.Add(flag);
        }

        /// <summary>
        /// 移除运行时标记
        /// </summary>
        public void RemoveFlag(string flag)
        {
            _runtimeFlags.Remove(flag);
        }

        /// <summary>
        /// 检查运行时标记
        /// </summary>
        public bool HasFlag(string flag)
        {
            return _runtimeFlags.Contains(flag);
        }
        
        /// <summary>
        /// 清空所有运行时标记
        /// </summary>
        public void ClearFlags()
        {
            _runtimeFlags.Clear();
        }

       
        
        #endregion
    }
}
