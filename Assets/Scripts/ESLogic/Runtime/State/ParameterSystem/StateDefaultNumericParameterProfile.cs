using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 默认 Float 参数赋值项。
    /// </summary>
    [Serializable]
    public struct StateDefaultFloatAssignment
    {
        public StateDefaultFloatParameter parameter;
        public float value;
    }

    /// <summary>
    /// 默认 Int 参数赋值项。
    /// </summary>
    [Serializable]
    public struct StateDefaultIntAssignment
    {
        public StateDefaultIntParameter parameter;
        public int value;
    }

    /// <summary>
    /// 默认 Bool 参数赋值项。
    /// </summary>
    [Serializable]
    public struct StateDefaultBoolAssignment
    {
        public StateDefaultBoolParameter parameter;
        public bool value;
    }

    /// <summary>
    /// 状态机默认参数配置资产（Float/Int/Bool 分离枚举）。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/State/Default Parameter Profile", fileName = "StateDefaultNumericParameterProfile")]
    public class StateDefaultNumericParameterProfile : ScriptableObject
    {
        [Header("Default Float Parameters")]
        public List<StateDefaultFloatAssignment> floats = new List<StateDefaultFloatAssignment>();

        [Header("Default Int Parameters")]
        public List<StateDefaultIntAssignment> ints = new List<StateDefaultIntAssignment>();

        [Header("Default Bool Parameters")]
        public List<StateDefaultBoolAssignment> bools = new List<StateDefaultBoolAssignment>();

        /// <summary>
        /// 将配置应用到 StateMachine（用于运行时或初始化阶段）。
        /// </summary>
        public void ApplyTo(StateMachine machine)
        {
            if (machine == null)
                return;

            for (int i = 0; i < floats.Count; i++)
            {
                var item = floats[i];
                if (item.parameter == StateDefaultFloatParameter.None)
                    continue;

                machine.SetFloat(item.parameter, item.value);
            }

            for (int i = 0; i < ints.Count; i++)
            {
                var item = ints[i];
                if (item.parameter == StateDefaultIntParameter.None)
                    continue;

                machine.SetInt(item.parameter, item.value);
            }

            for (int i = 0; i < bools.Count; i++)
            {
                var item = bools[i];
                if (item.parameter == StateDefaultBoolParameter.None)
                    continue;

                machine.SetBool(item.parameter, item.value);
            }
        }

        /// <summary>
        /// 将配置应用到 StateMachineContext（用于上下文重建/克隆后回填）。
        /// </summary>
        public void ApplyTo(StateMachineContext context)
        {
            if (context == null)
                return;

            for (int i = 0; i < floats.Count; i++)
            {
                var item = floats[i];
                if (item.parameter == StateDefaultFloatParameter.None)
                    continue;

                context.SetDefaultFloat(item.parameter, item.value);
            }

            for (int i = 0; i < ints.Count; i++)
            {
                var item = ints[i];
                if (item.parameter == StateDefaultIntParameter.None)
                    continue;

                context.SetDefaultInt(item.parameter, item.value);
            }

            for (int i = 0; i < bools.Count; i++)
            {
                var item = bools[i];
                if (item.parameter == StateDefaultBoolParameter.None)
                    continue;

                context.SetDefaultBool(item.parameter, item.value);
            }
        }
    }
}