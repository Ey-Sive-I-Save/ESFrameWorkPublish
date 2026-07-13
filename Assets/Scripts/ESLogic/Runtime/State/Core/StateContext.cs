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
        private const int DefaultParamDictionaryCapacity = 32;
        private const int DefaultTriggerSetCapacity = 16;

        public bool enableChangeEvents = true;
        public bool enableDefaultParamEvents = true;
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
        
        // ===== 历史平均速度 (16-17) =====
        /// <summary>前0.5秒局部空间横向平均速度（急停时动画方向保持用）</summary>
        public float AvgSpeedX;
        /// <summary>前0.5秒局部空间前后平均速度（急停时动画方向保持用）</summary>
        public float AvgSpeedZ;
        
        // ===== 攀爬参数 (18-19) =====
        /// <summary>攀爬时沿墙面的水平输入（-1=左, 0=静止, 1=右）</summary>
        public float ClimbHorizontal;
        /// <summary>攀爬时沿墙面的垂直输入（-1=下, 0=静止, 1=上）</summary>
        public float ClimbVertical;

        // ===== 可扩展默认参数（Int/Bool 分离枚举） =====
        private int[] _defaultEnumIntValues;
        private bool[] _defaultEnumBoolValues;
        
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

        // 参数链事件（统一 Link 标准）
        public readonly LinkReceiveChannelPool<StateDefaultFloatParameter, Link_StateContext_DefaultFloatChange> LinkRCL_DefaultFloat
            = new LinkReceiveChannelPool<StateDefaultFloatParameter, Link_StateContext_DefaultFloatChange>();

        public readonly LinkReceiveChannelPool<StateDefaultIntParameter, Link_StateContext_DefaultIntChange> LinkRCL_DefaultInt
            = new LinkReceiveChannelPool<StateDefaultIntParameter, Link_StateContext_DefaultIntChange>();

        public readonly LinkReceiveChannelPool<StateDefaultBoolParameter, Link_StateContext_DefaultBoolChange> LinkRCL_DefaultBool
            = new LinkReceiveChannelPool<StateDefaultBoolParameter, Link_StateContext_DefaultBoolChange>();

        public readonly LinkReceiveChannelPool<string, Link_ContextEvent_FloatChange> LinkRCL_Float
            = new LinkReceiveChannelPool<string, Link_ContextEvent_FloatChange>();

        public readonly LinkReceiveChannelPool<string, Link_ContextEvent_IntChange> LinkRCL_Int
            = new LinkReceiveChannelPool<string, Link_ContextEvent_IntChange>();

        public readonly LinkReceiveChannelPool<string, Link_ContextEvent_BoolChange> LinkRCL_Bool
            = new LinkReceiveChannelPool<string, Link_ContextEvent_BoolChange>();

        public readonly LinkReceiveChannelPool<string, Link_ContextEvent_StringChange> LinkRCL_String
            = new LinkReceiveChannelPool<string, Link_ContextEvent_StringChange>();

        public readonly LinkReceiveChannelPool<string, Link_ContextEvent_UnityObjectChange> LinkRCL_Entity
            = new LinkReceiveChannelPool<string, Link_ContextEvent_UnityObjectChange>();

        public readonly LinkReceiveChannelPool<string, Link_StateContext_CurveChange> LinkRCL_Curve
            = new LinkReceiveChannelPool<string, Link_StateContext_CurveChange>();

        public readonly LinkReceiveChannelPool<string, Link_StateContext_TriggerFired> LinkRCL_Trigger
            = new LinkReceiveChannelPool<string, Link_StateContext_TriggerFired>();

        public StateMachineContext(ContextPool fallbackPool = null)
        {
            // 初始化元数据
            contextID = Guid.NewGuid().ToString();
            creationTime = Time.time;
            lastUpdateTime = Time.time;
            _sharedData = new Dictionary<string, object>(DefaultParamDictionaryCapacity);
            _runtimeFlags = new HashSet<string>();
            
            // 初始化参数字典
            _floatParams = new Dictionary<string, float>(DefaultParamDictionaryCapacity);
            _intParams = new Dictionary<string, int>(DefaultParamDictionaryCapacity);
            _boolParams = new Dictionary<string, bool>(DefaultParamDictionaryCapacity);
            _stringParams = new Dictionary<string, string>(DefaultParamDictionaryCapacity);
            _entityParams = new Dictionary<string, UnityEngine.Object>(DefaultParamDictionaryCapacity);
            _curveParams = new Dictionary<string, AnimationCurve>(DefaultParamDictionaryCapacity);
            _activeTriggers = new HashSet<string>(DefaultTriggerSetCapacity);
            _fallbackContextPool = fallbackPool;

            _defaultEnumIntValues = new int[StateDefaultNumericParameterCatalog.MaxIntParameterValue + 1];
            _defaultEnumBoolValues = new bool[StateDefaultNumericParameterCatalog.MaxBoolParameterValue + 1];
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
            bool changed = false;
            float previous = 0f;
            switch (param)
            {
                case StateDefaultFloatParameter.SpeedX: previous = SpeedX; changed = !Mathf.Approximately(previous, value); SpeedX = value; break;
                case StateDefaultFloatParameter.SpeedY: previous = SpeedY; changed = !Mathf.Approximately(previous, value); SpeedY = value; break;
                case StateDefaultFloatParameter.SpeedZ: previous = SpeedZ; changed = !Mathf.Approximately(previous, value); SpeedZ = value; break;
                case StateDefaultFloatParameter.AimYaw: previous = AimYaw; changed = !Mathf.Approximately(previous, value); AimYaw = value; break;
                case StateDefaultFloatParameter.AimPitch: previous = AimPitch; changed = !Mathf.Approximately(previous, value); AimPitch = value; break;
                case StateDefaultFloatParameter.Speed: previous = Speed; changed = !Mathf.Approximately(previous, value); Speed = value; break;
                case StateDefaultFloatParameter.IsGrounded: previous = IsGrounded; changed = !Mathf.Approximately(previous, value); IsGrounded = value; break;
                case StateDefaultFloatParameter.WalkSpeedThreshold: previous = WalkSpeedThreshold; changed = !Mathf.Approximately(previous, value); WalkSpeedThreshold = value; break;
                case StateDefaultFloatParameter.RunSpeedThreshold: previous = RunSpeedThreshold; changed = !Mathf.Approximately(previous, value); RunSpeedThreshold = value; break;
                case StateDefaultFloatParameter.SprintSpeedThreshold: previous = SprintSpeedThreshold; changed = !Mathf.Approximately(previous, value); SprintSpeedThreshold = value; break;
                case StateDefaultFloatParameter.IsWalking: previous = IsWalking; changed = !Mathf.Approximately(previous, value); IsWalking = value; break;
                case StateDefaultFloatParameter.IsRunning: previous = IsRunning; changed = !Mathf.Approximately(previous, value); IsRunning = value; break;
                case StateDefaultFloatParameter.IsSprinting: previous = IsSprinting; changed = !Mathf.Approximately(previous, value); IsSprinting = value; break;
                case StateDefaultFloatParameter.IsCrouching: previous = IsCrouching; changed = !Mathf.Approximately(previous, value); IsCrouching = value; break;
                case StateDefaultFloatParameter.IsSliding: previous = IsSliding; changed = !Mathf.Approximately(previous, value); IsSliding = value; break;
                case StateDefaultFloatParameter.AvgSpeedX: previous = AvgSpeedX; changed = !Mathf.Approximately(previous, value); AvgSpeedX = value; break;
                case StateDefaultFloatParameter.AvgSpeedZ: previous = AvgSpeedZ; changed = !Mathf.Approximately(previous, value); AvgSpeedZ = value; break;
                case StateDefaultFloatParameter.ClimbHorizontal: previous = ClimbHorizontal; changed = !Mathf.Approximately(previous, value); ClimbHorizontal = value; break;
                case StateDefaultFloatParameter.ClimbVertical: previous = ClimbVertical; changed = !Mathf.Approximately(previous, value); ClimbVertical = value; break;
            }

            if (changed && enableChangeEvents && enableDefaultParamEvents)
            {
                LinkRCL_DefaultFloat.SendLink(param, new Link_StateContext_DefaultFloatChange
                {
                    Value_Pre = previous,
                    Value_Now = value
                });

                if (TryGetDefaultFloatName(param, out string name))
                {
                    LinkRCL_Float.SendLink(name, new Link_ContextEvent_FloatChange
                    {
                        Value_Pre = previous,
                        Value_Now = value,
                        Create = false,
                        Remove = false
                    });
                }
            }
        }

        public void NotifyDefaultFloatChanged(StateDefaultFloatParameter param)
        {
            if (!enableChangeEvents || !enableDefaultParamEvents)
                return;

            float value = GetDefaultFloat(param);
            LinkRCL_DefaultFloat.SendLink(param, new Link_StateContext_DefaultFloatChange { Value_Pre = value, Value_Now = value });

            if (TryGetDefaultFloatName(param, out string name))
            {
                LinkRCL_Float.SendLink(name, new Link_ContextEvent_FloatChange
                {
                    Value_Pre = value,
                    Value_Now = value,
                    Create = false,
                    Remove = false
                });
            }
        }

        /// <summary>
        /// 设置默认 Int 枚举参数（强类型，非法枚举值会被忽略）。
        /// </summary>
        public void SetDefaultInt(StateDefaultIntParameter param, int value)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
                return;

            int previous = _defaultEnumIntValues[index];
            if (previous == value)
                return;

            _defaultEnumIntValues[index] = value;

            if (enableChangeEvents && enableDefaultParamEvents)
            {
                LinkRCL_DefaultInt.SendLink(param, new Link_StateContext_DefaultIntChange
                {
                    Value_Pre = previous,
                    Value_Now = value
                });

                if (StateDefaultNumericParameterCatalog.TryGetName(param, out string name))
                {
                    LinkRCL_Int.SendLink(name, new Link_ContextEvent_IntChange
                    {
                        Value_Pre = previous,
                        Value_Now = value,
                        Create = false,
                        Remove = false
                    });
                }
            }
        }

        /// <summary>
        /// 获取默认 Int 枚举参数。
        /// </summary>
        public int GetDefaultInt(StateDefaultIntParameter param, int defaultValue = 0)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
                return defaultValue;

            return _defaultEnumIntValues[index];
        }

        /// <summary>
        /// 尝试获取默认 Int 枚举参数。
        /// </summary>
        public bool TryGetDefaultInt(StateDefaultIntParameter param, out int value)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
            {
                value = default;
                return false;
            }

            value = _defaultEnumIntValues[index];
            return true;
        }

        public bool HasDefaultInt(StateDefaultIntParameter param)
        {
            return StateDefaultNumericParameterCatalog.IsDefined(param);
        }

        /// <summary>
        /// 设置默认 Bool 枚举参数（强类型，非法枚举值会被忽略）。
        /// </summary>
        public void SetDefaultBool(StateDefaultBoolParameter param, bool value)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
                return;

            bool previous = _defaultEnumBoolValues[index];
            if (previous == value)
                return;

            _defaultEnumBoolValues[index] = value;

            if (enableChangeEvents && enableDefaultParamEvents)
            {
                LinkRCL_DefaultBool.SendLink(param, new Link_StateContext_DefaultBoolChange
                {
                    Value_Pre = previous,
                    Value_Now = value
                });

                if (StateDefaultNumericParameterCatalog.TryGetName(param, out string name))
                {
                    LinkRCL_Bool.SendLink(name, new Link_ContextEvent_BoolChange
                    {
                        Value_Pre = previous,
                        Value_Now = value,
                        Create = false,
                        Remove = false
                    });
                }
            }
        }

        /// <summary>
        /// 获取默认 Bool 枚举参数。
        /// </summary>
        public bool GetDefaultBool(StateDefaultBoolParameter param, bool defaultValue = false)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
                return defaultValue;

            return _defaultEnumBoolValues[index];
        }

        /// <summary>
        /// 尝试获取默认 Bool 枚举参数。
        /// </summary>
        public bool TryGetDefaultBool(StateDefaultBoolParameter param, out bool value)
        {
            if (!StateDefaultNumericParameterCatalog.TryGetIndex(param, out int index))
            {
                value = default;
                return false;
            }

            value = _defaultEnumBoolValues[index];
            return true;
        }

        public bool HasDefaultBool(StateDefaultBoolParameter param)
        {
            return StateDefaultNumericParameterCatalog.IsDefined(param);
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
                case StateDefaultFloatParameter.AvgSpeedX: return AvgSpeedX;
                case StateDefaultFloatParameter.AvgSpeedZ: return AvgSpeedZ;
                case StateDefaultFloatParameter.ClimbHorizontal: return ClimbHorizontal;
                case StateDefaultFloatParameter.ClimbVertical: return ClimbVertical;
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
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            bool existed = _floatParams.TryGetValue(name, out float oldValue);
            bool changed = !existed || !Mathf.Approximately(oldValue, value);
            _floatParams[name] = value;
            if (changed && enableChangeEvents)
            {
                LinkRCL_Float.SendLink(name, new Link_ContextEvent_FloatChange
                {
                    Value_Pre = oldValue,
                    Value_Now = value,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        /// <summary>
        /// 获取字符串参数（支持退化到ContextPool）
        /// </summary>
        public float GetFloat(string name, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

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
            {
                SetDefaultFloat(param.EnumValue, value);
            }
            else
            {
                if (string.IsNullOrEmpty(param.StringValue))
                    return;
                SetFloat(param.StringValue, value);
            }
        }
        
        /// <summary>
        /// GetFloat StateParameter重载 - 根据EnumValue自动选择路径
        /// </summary>
        public float GetFloat(StateParameter param, float defaultValue = 0f)
        {
            if (param.EnumValue != StateDefaultFloatParameter.None)
            {
                return GetDefaultFloat(param.EnumValue, defaultValue);
            }
            else
            {
                if (string.IsNullOrEmpty(param.StringValue))
                    return defaultValue;
                return GetFloat(param.StringValue, defaultValue);
            }
        }

        public bool TryGetFloat(string name, out float value)
        {
            if (string.IsNullOrEmpty(name))
            {
                value = default;
                return false;
            }

            if (_floatParams.TryGetValue(name, out value))
                return true;

            if (_fallbackContextPool != null)
            {
                var contextValue = _fallbackContextPool.GetValue(name);
                if (contextValue is float floatVal)
                {
                    value = floatVal;
                    return true;
                }

                if (contextValue is int intVal)
                {
                    value = intVal;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public bool TryGetFloat(StateParameter param, out float value)
        {
            if (param.EnumValue != StateDefaultFloatParameter.None)
            {
                value = GetDefaultFloat(param.EnumValue);
                return true;
            }

            if (string.IsNullOrEmpty(param.StringValue))
            {
                value = default;
                return false;
            }

            return TryGetFloat(param.StringValue, out value);
        }

        public bool HasFloat(string name) => _floatParams.ContainsKey(name);

        public bool HasFloat(StateParameter param)
        {
            if (param.EnumValue != StateDefaultFloatParameter.None)
                return true;

            return !string.IsNullOrEmpty(param.StringValue) && _floatParams.ContainsKey(param.StringValue);
        }
        
        #endregion

        #region Int Parameters
        public void SetInt(string name, int value)
        {
            if (string.IsNullOrEmpty(name))
                return;

            bool existed = _intParams.TryGetValue(name, out int oldValue);
            bool changed = !existed || oldValue != value;
            _intParams[name] = value;
            if (changed && enableChangeEvents)
            {
                LinkRCL_Int.SendLink(name, new Link_ContextEvent_IntChange
                {
                    Value_Pre = oldValue,
                    Value_Now = value,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        public int GetInt(string name, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

            return _intParams.TryGetValue(name, out int value) ? value : defaultValue;
        }

        public bool HasInt(string name) => !string.IsNullOrEmpty(name) && _intParams.ContainsKey(name);

        #endregion

        #region Bool Parameters
        public void SetBool(string name, bool value)
        {
            if (string.IsNullOrEmpty(name))
                return;

            bool existed = _boolParams.TryGetValue(name, out bool oldValue);
            bool changed = !existed || oldValue != value;
            _boolParams[name] = value;
            if (changed && enableChangeEvents)
            {
                LinkRCL_Bool.SendLink(name, new Link_ContextEvent_BoolChange
                {
                    Value_Pre = oldValue,
                    Value_Now = value,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        public bool GetBool(string name, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

            return _boolParams.TryGetValue(name, out bool value) ? value : defaultValue;
        }

        public bool HasBool(string name) => !string.IsNullOrEmpty(name) && _boolParams.ContainsKey(name);

        #endregion

        #region Trigger Parameters
        public void SetTrigger(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            _activeTriggers.Add(name);
            if (enableChangeEvents)
            {
                LinkRCL_Trigger.SendLink(name, new Link_StateContext_TriggerFired { FiredTime = Time.time });
            }
        }

        public bool GetTrigger(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return _activeTriggers.Contains(name);
        }

        public void ResetTrigger(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

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
            if (string.IsNullOrEmpty(name))
                return;

            bool existed = _stringParams.TryGetValue(name, out string oldValue);
            bool changed = !existed || oldValue != value;
            _stringParams[name] = value;
            if (changed && enableChangeEvents)
            {
                LinkRCL_String.SendLink(name, new Link_ContextEvent_StringChange
                {
                    Value_Pre = oldValue,
                    Value_Now = value,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        public string GetString(string name, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

            return _stringParams.TryGetValue(name, out string value) ? value : defaultValue;
        }

        public bool HasString(string name) => !string.IsNullOrEmpty(name) && _stringParams.ContainsKey(name);
        #endregion

        #region Entity Parameters
        public void SetEntity(string name, UnityEngine.Object entity)
        {
            if (string.IsNullOrEmpty(name))
                return;

            bool existed = _entityParams.TryGetValue(name, out var oldValue);
            bool changed = !existed || oldValue != entity;
            _entityParams[name] = entity;
            if (changed && enableChangeEvents)
            {
                LinkRCL_Entity.SendLink(name, new Link_ContextEvent_UnityObjectChange
                {
                    Value_Pre = oldValue,
                    Value_Now = entity,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        public T GetEntity<T>(string name) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return _entityParams.TryGetValue(name, out var entity) ? entity as T : null;
        }

        public UnityEngine.Object GetEntity(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return _entityParams.TryGetValue(name, out var entity) ? entity : null;
        }

        public bool HasEntity(string name) => !string.IsNullOrEmpty(name) && _entityParams.ContainsKey(name);
        #endregion

        #region Curve Parameters (for IK)
        public void SetCurve(string name, AnimationCurve curve)
        {
            if (string.IsNullOrEmpty(name))
                return;

            bool existed = _curveParams.TryGetValue(name, out var oldValue);
            bool changed = !existed || oldValue != curve;
            _curveParams[name] = curve;
            if (changed && enableChangeEvents)
            {
                LinkRCL_Curve.SendLink(name, new Link_StateContext_CurveChange
                {
                    Value_Pre = oldValue,
                    Value_Now = curve,
                    Create = !existed,
                    Remove = false
                });
            }
        }

        public AnimationCurve GetCurve(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return _curveParams.TryGetValue(name, out var curve) ? curve : null;
        }

        public float EvaluateCurve(string name, float time, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(name))
                return defaultValue;

            if (_curveParams.TryGetValue(name, out var curve) && curve != null)
                return curve.Evaluate(time);
            return defaultValue;
        }

        public bool HasCurve(string name) => !string.IsNullOrEmpty(name) && _curveParams.ContainsKey(name);
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

            if (_defaultEnumIntValues != null)
            {
                Array.Clear(_defaultEnumIntValues, 0, _defaultEnumIntValues.Length);
            }

            if (_defaultEnumBoolValues != null)
            {
                Array.Clear(_defaultEnumBoolValues, 0, _defaultEnumBoolValues.Length);
            }
        }

        /// <summary>
        /// 每帧更新 - 重置触发器、速度限制、运动状态更新
        /// </summary>
        public void Update()
        {
            // 触发器在下一帧自动重置
            _activeTriggers.Clear();
            RefreshMotionDerivedParameters();
            
            // 更新时间戳
            lastUpdateTime = Time.time;
        }

        public void ApplyMotionSpeedXZ(float localSpeedX, float localSpeedZ)
        {
            SpeedX = localSpeedX;
            SpeedZ = localSpeedZ;
            RefreshMotionDerivedParametersWithoutClamping();
        }

        private void RefreshMotionDerivedParameters()
        {
            float horizontalSpeed = Mathf.Sqrt(SpeedX * SpeedX + SpeedZ * SpeedZ);

            float maxSpeed = IsSprintKeyPressed > 0.5f ? SprintSpeedThreshold : WalkSpeedThreshold;
            if (horizontalSpeed > maxSpeed)
            {
                float scale = maxSpeed / horizontalSpeed;
                SpeedX *= scale;
                SpeedZ *= scale;
                horizontalSpeed = maxSpeed;
            }

            Speed = horizontalSpeed;

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
                IsWalking = 0f;
                IsRunning = 0f;
                IsSprinting = 0f;
            }
        }

        private void RefreshMotionDerivedParametersWithoutClamping()
        {
            float horizontalSpeed = Mathf.Sqrt(SpeedX * SpeedX + SpeedZ * SpeedZ);

            Speed = horizontalSpeed;

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
                IsWalking = 0f;
                IsRunning = 0f;
                IsSprinting = 0f;
            }
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
            target.AvgSpeedX = AvgSpeedX;
            target.AvgSpeedZ = AvgSpeedZ;
            target.ClimbHorizontal = ClimbHorizontal;
            target.ClimbVertical = ClimbVertical;

            if (target._defaultEnumIntValues == null || target._defaultEnumIntValues.Length != _defaultEnumIntValues.Length)
            {
                target._defaultEnumIntValues = new int[_defaultEnumIntValues.Length];
            }

            if (target._defaultEnumBoolValues == null || target._defaultEnumBoolValues.Length != _defaultEnumBoolValues.Length)
            {
                target._defaultEnumBoolValues = new bool[_defaultEnumBoolValues.Length];
            }

            Array.Copy(_defaultEnumIntValues, target._defaultEnumIntValues, _defaultEnumIntValues.Length);
            Array.Copy(_defaultEnumBoolValues, target._defaultEnumBoolValues, _defaultEnumBoolValues.Length);
            
            bool originalEvents = target.enableChangeEvents;
            target.enableChangeEvents = false;
            foreach (var kv in _floatParams) target.SetFloat(kv.Key, kv.Value);
            foreach (var kv in _intParams) target.SetInt(kv.Key, kv.Value);
            foreach (var kv in _boolParams) target.SetBool(kv.Key, kv.Value);
            foreach (var kv in _stringParams) target.SetString(kv.Key, kv.Value);
            foreach (var kv in _entityParams) target.SetEntity(kv.Key, kv.Value);
            foreach (var kv in _curveParams) target.SetCurve(kv.Key, kv.Value);
            foreach (var trigger in _activeTriggers) target.SetTrigger(trigger);
            target.enableChangeEvents = originalEvents;
        }

        private static bool TryGetDefaultFloatName(StateDefaultFloatParameter param, out string name)
        {
            return StateDefaultFloatParameterUtility.TryGetName(param, out name);
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
