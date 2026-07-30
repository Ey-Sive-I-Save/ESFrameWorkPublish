using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 状态机上下文 - 管理所有可变参数
    /// 
    /// 设计原则：
    /// 1. 整个状态机共享一个StateMachineContext
    /// 2. 枚举参数使用数组索引，零开销直接访问（史上最强性能）
    /// 3. 运行时只接受已声明的强类型参数，不提供字符串回退。
    /// </summary>
    public class StateMachineContext
    {

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

        // Project-level animation inputs. Kept in the typed core store, not the string fallback.
        public float WeaponEquipWeight;
        public float UpperBodyWeight;
        public float WeaponFirePulse;
        public float WeaponInHandWeight;
        public float FootSupportShare;

        // ===== 可扩展默认参数（Int/Bool 分离枚举） =====
        private int[] _defaultEnumIntValues;
        private bool[] _defaultEnumBoolValues;
        
        // 参数链事件（统一 Link 标准）
        public readonly LinkReceiveChannelPool<StateDefaultFloatParameter, Link_StateContext_DefaultFloatChange> LinkRCL_DefaultFloat
            = new LinkReceiveChannelPool<StateDefaultFloatParameter, Link_StateContext_DefaultFloatChange>();

        public readonly LinkReceiveChannelPool<StateDefaultIntParameter, Link_StateContext_DefaultIntChange> LinkRCL_DefaultInt
            = new LinkReceiveChannelPool<StateDefaultIntParameter, Link_StateContext_DefaultIntChange>();

        public readonly LinkReceiveChannelPool<StateDefaultBoolParameter, Link_StateContext_DefaultBoolChange> LinkRCL_DefaultBool
            = new LinkReceiveChannelPool<StateDefaultBoolParameter, Link_StateContext_DefaultBoolChange>();

        public StateMachineContext()
        {
            // 初始化元数据
            contextID = Guid.NewGuid().ToString();
            creationTime = Time.time;
            lastUpdateTime = Time.time;
            _sharedData = new Dictionary<string, object>(32);
            _runtimeFlags = new HashSet<string>();
            
            // Stable enum values are intentionally sparse (1001/2001 ranges); per-machine data
            // uses the catalog's deterministic dense RuntimeKey instead of reserving those gaps.
            _defaultEnumIntValues = new int[StateDefaultNumericParameterCatalog.IntRuntimeKeyCount + 1];
            _defaultEnumBoolValues = new bool[StateDefaultNumericParameterCatalog.BoolRuntimeKeyCount + 1];
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
                case StateDefaultFloatParameter.MoveX: previous = SpeedX; changed = !Mathf.Approximately(previous, value); SpeedX = value; break;
                case StateDefaultFloatParameter.VerticalSpeed: previous = SpeedY; changed = !Mathf.Approximately(previous, value); SpeedY = value; break;
                case StateDefaultFloatParameter.MoveZ: previous = SpeedZ; changed = !Mathf.Approximately(previous, value); SpeedZ = value; break;
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
                case StateDefaultFloatParameter.ClimbX: previous = ClimbHorizontal; changed = !Mathf.Approximately(previous, value); ClimbHorizontal = value; break;
                case StateDefaultFloatParameter.ClimbY: previous = ClimbVertical; changed = !Mathf.Approximately(previous, value); ClimbVertical = value; break;
                case StateDefaultFloatParameter.WeaponEquipWeight: previous = WeaponEquipWeight; changed = !Mathf.Approximately(previous, value); WeaponEquipWeight = value; break;
                case StateDefaultFloatParameter.UpperBodyWeight: previous = UpperBodyWeight; changed = !Mathf.Approximately(previous, value); UpperBodyWeight = value; break;
                case StateDefaultFloatParameter.WeaponFirePulse: previous = WeaponFirePulse; changed = !Mathf.Approximately(previous, value); WeaponFirePulse = value; break;
                case StateDefaultFloatParameter.WeaponInHandWeight: previous = WeaponInHandWeight; changed = !Mathf.Approximately(previous, value); WeaponInHandWeight = value; break;
                case StateDefaultFloatParameter.FootSupportShare: previous = FootSupportShare; changed = !Mathf.Approximately(previous, value); FootSupportShare = value; break;
            }

            if (changed && enableChangeEvents && enableDefaultParamEvents)
            {
                LinkRCL_DefaultFloat.SendLink(param, new Link_StateContext_DefaultFloatChange
                {
                    Value_Pre = previous,
                    Value_Now = value
                });

            }
        }

        public void NotifyDefaultFloatChanged(StateDefaultFloatParameter param)
        {
            if (!enableChangeEvents || !enableDefaultParamEvents)
                return;

            float value = GetDefaultFloat(param);
            LinkRCL_DefaultFloat.SendLink(param, new Link_StateContext_DefaultFloatChange { Value_Pre = value, Value_Now = value });

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
                case StateDefaultFloatParameter.MoveX: return SpeedX;
                case StateDefaultFloatParameter.VerticalSpeed: return SpeedY;
                case StateDefaultFloatParameter.MoveZ: return SpeedZ;
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
                case StateDefaultFloatParameter.ClimbX: return ClimbHorizontal;
                case StateDefaultFloatParameter.ClimbY: return ClimbVertical;
                case StateDefaultFloatParameter.WeaponEquipWeight: return WeaponEquipWeight;
                case StateDefaultFloatParameter.UpperBodyWeight: return UpperBodyWeight;
                case StateDefaultFloatParameter.WeaponFirePulse: return WeaponFirePulse;
                case StateDefaultFloatParameter.WeaponInHandWeight: return WeaponInHandWeight;
                case StateDefaultFloatParameter.FootSupportShare: return FootSupportShare;
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

        #endregion

        /// <summary>
        /// 清空所有参数
        /// </summary>
        public void Clear()
        {

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
            RefreshMotionDerivedParameters();
            
            // 更新时间戳
            lastUpdateTime = Time.time;
        }

        /// <summary>
        /// Starts one state-machine tick. Legacy string triggers are pulse values and expire
        /// before the next tick. Motion derivation deliberately does not happen here.
        /// </summary>
        internal void BeginStateMachineTick()
        {
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
            target.WeaponEquipWeight = WeaponEquipWeight;
            target.UpperBodyWeight = UpperBodyWeight;
            target.WeaponFirePulse = WeaponFirePulse;
            target.WeaponInHandWeight = WeaponInHandWeight;
            target.FootSupportShare = FootSupportShare;

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

