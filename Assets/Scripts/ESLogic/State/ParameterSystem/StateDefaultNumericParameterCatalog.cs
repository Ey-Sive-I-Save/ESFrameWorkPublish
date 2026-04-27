using System;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// 默认数值参数目录（Float 使用 StateDefaultFloatParameter；Int/Bool 使用本文件分离枚举）。
    ///
    /// 扩展步骤：
    /// 1. 在对应枚举（StateDefaultIntParameter 或 StateDefaultBoolParameter）增加键。
    /// 2. 在 IntDefinitions 或 BoolDefinitions 补一条定义（名称/默认值）。
    /// </summary>

    /// <summary>
    /// 默认整型参数枚举。
    /// </summary>
    public enum StateDefaultIntParameter
    {
        None = 0,
        MovementMode = 1001,
        ActionPhase = 1002,
        ComboStep = 1003,
    }

    /// <summary>
    /// 默认布尔参数枚举。
    /// </summary>
    public enum StateDefaultBoolParameter
    {
        None = 0,
        IsAiming = 2001,
        IsLockingTarget = 2002,
        IsInputBlocked = 2003,
    }

    public readonly struct StateDefaultIntParameterDefinition
    {
        public readonly StateDefaultIntParameter Parameter;
        public readonly string Name;
        public readonly int DefaultInt;

        public StateDefaultIntParameterDefinition(StateDefaultIntParameter parameter, string name, int defaultInt = 0)
        {
            Parameter = parameter;
            Name = name;
            DefaultInt = defaultInt;
        }
    }

    public readonly struct StateDefaultBoolParameterDefinition
    {
        public readonly StateDefaultBoolParameter Parameter;
        public readonly string Name;
        public readonly bool DefaultBool;

        public StateDefaultBoolParameterDefinition(StateDefaultBoolParameter parameter, string name, bool defaultBool = false)
        {
            Parameter = parameter;
            Name = name;
            DefaultBool = defaultBool;
        }
    }

    public static class StateDefaultNumericParameterCatalog
    {
        private static readonly StateDefaultIntParameterDefinition[] IntDefinitions =
        {
            new StateDefaultIntParameterDefinition(StateDefaultIntParameter.MovementMode, "MovementMode"),
            new StateDefaultIntParameterDefinition(StateDefaultIntParameter.ActionPhase, "ActionPhase"),
            new StateDefaultIntParameterDefinition(StateDefaultIntParameter.ComboStep, "ComboStep"),
        };

        private static readonly StateDefaultBoolParameterDefinition[] BoolDefinitions =
        {
            new StateDefaultBoolParameterDefinition(StateDefaultBoolParameter.IsAiming, "IsAiming"),
            new StateDefaultBoolParameterDefinition(StateDefaultBoolParameter.IsLockingTarget, "IsLockingTarget"),
            new StateDefaultBoolParameterDefinition(StateDefaultBoolParameter.IsInputBlocked, "IsInputBlocked"),
        };

        private static readonly StateDefaultIntParameterDefinition[] IntDefinitionsByIndex;
        private static readonly string[] IntNamesByIndex;
        private static readonly bool[] IntExistsByIndex;

        private static readonly StateDefaultBoolParameterDefinition[] BoolDefinitionsByIndex;
        private static readonly string[] BoolNamesByIndex;
        private static readonly bool[] BoolExistsByIndex;

        private static readonly Dictionary<string, StateDefaultIntParameter> IntEnumByName;
        private static readonly Dictionary<string, StateDefaultBoolParameter> BoolEnumByName;

        public static readonly int MaxIntParameterValue;
        public static readonly int MaxBoolParameterValue;

        static StateDefaultNumericParameterCatalog()
        {
            IntEnumByName = new Dictionary<string, StateDefaultIntParameter>(IntDefinitions.Length, StringComparer.Ordinal);
            BoolEnumByName = new Dictionary<string, StateDefaultBoolParameter>(BoolDefinitions.Length, StringComparer.Ordinal);

            int maxIntValue = 0;
            for (int i = 0; i < IntDefinitions.Length; i++)
            {
                var def = IntDefinitions[i];
                IntEnumByName[def.Name] = def.Parameter;

                int intValue = (int)def.Parameter;
                if (intValue > maxIntValue)
                {
                    maxIntValue = intValue;
                }
            }

            int maxBoolValue = 0;
            for (int i = 0; i < BoolDefinitions.Length; i++)
            {
                var def = BoolDefinitions[i];
                BoolEnumByName[def.Name] = def.Parameter;

                int intValue = (int)def.Parameter;
                if (intValue > maxBoolValue)
                {
                    maxBoolValue = intValue;
                }
            }

            MaxIntParameterValue = maxIntValue;
            MaxBoolParameterValue = maxBoolValue;

            IntDefinitionsByIndex = new StateDefaultIntParameterDefinition[maxIntValue + 1];
            IntNamesByIndex = new string[maxIntValue + 1];
            IntExistsByIndex = new bool[maxIntValue + 1];
            for (int i = 0; i < IntDefinitions.Length; i++)
            {
                var def = IntDefinitions[i];
                int index = (int)def.Parameter;
                IntDefinitionsByIndex[index] = def;
                IntNamesByIndex[index] = def.Name;
                IntExistsByIndex[index] = true;
            }

            BoolDefinitionsByIndex = new StateDefaultBoolParameterDefinition[maxBoolValue + 1];
            BoolNamesByIndex = new string[maxBoolValue + 1];
            BoolExistsByIndex = new bool[maxBoolValue + 1];
            for (int i = 0; i < BoolDefinitions.Length; i++)
            {
                var def = BoolDefinitions[i];
                int index = (int)def.Parameter;
                BoolDefinitionsByIndex[index] = def;
                BoolNamesByIndex[index] = def.Name;
                BoolExistsByIndex[index] = true;
            }
        }

        public static bool TryGetDefinition(StateDefaultIntParameter parameter, out StateDefaultIntParameterDefinition definition)
        {
            int index = (int)parameter;
            if ((uint)index < (uint)IntDefinitionsByIndex.Length && IntExistsByIndex[index])
            {
                definition = IntDefinitionsByIndex[index];
                return true;
            }

            definition = default;
            return false;
        }

        public static bool IsDefined(StateDefaultIntParameter parameter)
        {
            int index = (int)parameter;
            return (uint)index < (uint)IntExistsByIndex.Length && IntExistsByIndex[index];
        }

        public static bool TryGetIndex(StateDefaultIntParameter parameter, out int index)
        {
            index = (int)parameter;
            return (uint)index < (uint)IntExistsByIndex.Length && IntExistsByIndex[index];
        }

        public static bool TryGetDefinition(StateDefaultBoolParameter parameter, out StateDefaultBoolParameterDefinition definition)
        {
            int index = (int)parameter;
            if ((uint)index < (uint)BoolDefinitionsByIndex.Length && BoolExistsByIndex[index])
            {
                definition = BoolDefinitionsByIndex[index];
                return true;
            }

            definition = default;
            return false;
        }

        public static bool IsDefined(StateDefaultBoolParameter parameter)
        {
            int index = (int)parameter;
            return (uint)index < (uint)BoolExistsByIndex.Length && BoolExistsByIndex[index];
        }

        public static bool TryGetIndex(StateDefaultBoolParameter parameter, out int index)
        {
            index = (int)parameter;
            return (uint)index < (uint)BoolExistsByIndex.Length && BoolExistsByIndex[index];
        }

        public static bool TryGetName(StateDefaultIntParameter parameter, out string name)
        {
            int index = (int)parameter;
            if ((uint)index < (uint)IntNamesByIndex.Length && IntExistsByIndex[index])
            {
                name = IntNamesByIndex[index];
                return true;
            }

            name = null;
            return false;
        }

        public static bool TryGetName(StateDefaultBoolParameter parameter, out string name)
        {
            int index = (int)parameter;
            if ((uint)index < (uint)BoolNamesByIndex.Length && BoolExistsByIndex[index])
            {
                name = BoolNamesByIndex[index];
                return true;
            }

            name = null;
            return false;
        }

        public static bool TryGetByNameAsInt(string name, out StateDefaultIntParameter parameter)
        {
            if (string.IsNullOrEmpty(name))
            {
                parameter = StateDefaultIntParameter.None;
                return false;
            }

            return IntEnumByName.TryGetValue(name, out parameter);
        }

        public static bool TryGetByNameAsBool(string name, out StateDefaultBoolParameter parameter)
        {
            if (string.IsNullOrEmpty(name))
            {
                parameter = StateDefaultBoolParameter.None;
                return false;
            }

            return BoolEnumByName.TryGetValue(name, out parameter);
        }
    }
}
