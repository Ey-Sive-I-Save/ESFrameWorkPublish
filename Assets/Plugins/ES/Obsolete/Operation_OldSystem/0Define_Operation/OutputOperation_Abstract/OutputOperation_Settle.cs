/*
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public class OutputOperationSettleFlag : OverLoadFlag<OutputOperationSettleFlag>
    {

    }
    [TypeRegistryItem("布尔结算结果�?), Serializable]
    public class SettlementBool : Settlement<bool, SettleOperationBool, SettlementBool>
    {

    }
    public abstract class OutputOperationSettle<Target,Logic, ValueType_> : IOutputOperation<Target,Logic>
    {
        public abstract void TryOperation(Target target,Logic logic);
        public abstract void TryCancel(Target target,Logic logic);
    }


    

   /* [Serializable, TypeRegistryItem("结算输出-测试专属-攻击�?)]
    public class OutputOperationBuffSettle_Test_Attack : OutputOperationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Attack;
        }
    }
    [Serializable, TypeRegistryItem("结算输出-测试专属-暴击�?)]
    public class OutputOperationBuffSettle_Test_Cri : OutputOperationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Cri;
        }
    }
    [Serializable, TypeRegistryItem("结算输出-测试专属-防御�?)]
    public class OutputOperationBuffSettle_Test_Defend : OutputOperationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Defend;
        }
    }
}

*/