using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public class OutputOpeationSettleFlag : OverLoadFlag<OutputOpeationSettleFlag>
    {

    }
    [TypeRegistryItem("布尔结算结果值"), Serializable]
    public class SettlementBool : Settlement<bool, SettleOperationBool, SettlementBool>
    {

    }
    public abstract class OutputOperationSettle<On, From, With, ValueType_> : IOutputOperation<On, From, With>
    {
        public abstract void TryOperation(On on, From from, With with);
        public abstract void TryCancel(On on, From from, With with);
    }


    

   /* [Serializable, TypeRegistryItem("结算输出-测试专属-攻击力")]
    public class OutputOpeationBuffSettle_Test_Attack : OutputOpeationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Attack;
        }
    }
    [Serializable, TypeRegistryItem("结算输出-测试专属-暴击率")]
    public class OutputOpeationBuffSettle_Test_Cri : OutputOpeationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Cri;
        }
    }
    [Serializable, TypeRegistryItem("结算输出-测试专属-防御力")]
    public class OutputOpeationBuffSettle_Test_Defend : OutputOpeationFloat_EEB
    {
        public override SettlementFloat GetSettlement(Entity on, Entity from, EntityState_Buff with)
        {
            return on.VariableData.Defend;
        }
    }*/
}
