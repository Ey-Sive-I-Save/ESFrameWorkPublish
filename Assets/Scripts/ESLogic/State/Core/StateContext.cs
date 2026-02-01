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
        TempCost,       // 临时代价值
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
    /// 1. 整个状态机共享一个StateContext
    /// 2. 枚举参数使用显式字段，零开销直接访问
    /// 3. 字符串参数使用字典，支持退化到ContextPool
    /// 4. 统一的Get/Set方法，支持枚举和字符串
    /// </summary>
    public class StateContext
    {
        // ==================== 枚举参数 - 显式字段（零开销） ====================
        public float SpeedX;
        public float SpeedY;
        public float SpeedZ;
        public float AimYaw;
        public float AimPitch;
        public float Speed;
        public float IsGrounded;
        
        // ==================== 字符串参数 - 字典存储（支持退化） ====================
        private Dictionary<string, float> _floatParams;
        private Dictionary<string, int> _intParams;
        private Dictionary<string, bool> _boolParams;
        private Dictionary<string, string> _stringParams;
        private Dictionary<string, UnityEngine.Object> _entityParams;
        private Dictionary<string, AnimationCurve> _curveParams;
        private HashSet<string> _activeTriggers;
        private Dictionary<string, float> _tempCosts;

        // 退化到Entity的ContextPool（仅字符串参数）
        private ContextPool _fallbackContextPool;

        // 参数变更事件
        public event Action<string, float> OnFloatChanged;
        public event Action<string, int> OnIntChanged;
        public event Action<string, bool> OnBoolChanged;
        public event Action<string> OnTriggerFired;

        public StateContext(ContextPool fallbackPool = null)
        {
            _floatParams = new Dictionary<string, float>();
            _intParams = new Dictionary<string, int>();
            _boolParams = new Dictionary<string, bool>();
            _stringParams = new Dictionary<string, string>();
            _entityParams = new Dictionary<string, UnityEngine.Object>();
            _curveParams = new Dictionary<string, AnimationCurve>();
            _activeTriggers = new HashSet<string>();
            _tempCosts = new Dictionary<string, float>();
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
        /// 设置默认枚举参数（零开销，直接字段访问，硬编码映射）
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
            }
        }
        
        /// <summary>
        /// 获取默认枚举参数（零开销，直接字段访问，无退化）
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

        #region Temp Cost Parameters
        public void SetTempCost(string name, float cost)
        {
            _tempCosts[name] = Mathf.Clamp01(cost);
        }

        public float GetTempCost(string name, float defaultValue = 0f)
        {
            return _tempCosts.TryGetValue(name, out float value) ? value : defaultValue;
        }

        public void ConsumeTempCost(string name, float amount)
        {
            if (_tempCosts.TryGetValue(name, out float current))
            {
                _tempCosts[name] = Mathf.Clamp01(current + amount);
            }
            else
            {
                _tempCosts[name] = Mathf.Clamp01(amount);
            }
        }

        public void ReturnTempCost(string name, float amount)
        {
            if (_tempCosts.TryGetValue(name, out float current))
            {
                _tempCosts[name] = Mathf.Clamp01(current - amount);
            }
        }

        public void ClearTempCost(string name)
        {
            _tempCosts.Remove(name);
        }
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
            _tempCosts.Clear();
        }

        /// <summary>
        /// 每帧更新 - 重置触发器等
        /// </summary>
        public void Update()
        {
            // 触发器在下一帧自动重置
            _activeTriggers.Clear();
        }

        /// <summary>
        /// 拷贝参数到另一个上下文
        /// </summary>
        public void CopyTo(StateContext target)
        {
            foreach (var kv in _floatParams) target.SetFloat(kv.Key, kv.Value);
            foreach (var kv in _intParams) target.SetInt(kv.Key, kv.Value);
            foreach (var kv in _boolParams) target.SetBool(kv.Key, kv.Value);
            foreach (var kv in _stringParams) target.SetString(kv.Key, kv.Value);
            foreach (var kv in _entityParams) target.SetEntity(kv.Key, kv.Value);
            foreach (var kv in _curveParams) target.SetCurve(kv.Key, kv.Value);
            foreach (var trigger in _activeTriggers) target.SetTrigger(trigger);
            foreach (var kv in _tempCosts) target.SetTempCost(kv.Key, kv.Value);
        }
    }
}
