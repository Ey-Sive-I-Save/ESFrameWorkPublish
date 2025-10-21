using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES.SkillSample
{
    [CreateNodeRunnerSoMenu(NodeEnvironment.SKill,"目标实体操作","常规操作组1(:造成伤害)")]
    public class NodeRunnerSkillOperation : NodeRunnerSupport_OpeationSkill<OOP_EESKILL_UseOnON>
    {
        public override Type DefaultValueType()
        {
            return typeof(OOPSKILL_Hurt);
        }

        public override string GetNameForT()
        {
            return Value?.Name ?? "未定义";
        }
    }
    [Serializable]
    public abstract class OOP_EESKILL_UseOnON : OOP_EESkill
    {
        public abstract string Name { get; }
    }
    [Serializable,TypeRegistryItem("造成伤害")]
    public class OOPSKILL_Hurt : OOP_EESKILL_UseOnON
    {
        public override string Name => "造成伤害";
        public float damage;
        public bool isRange;
    }
    [Serializable, TypeRegistryItem("给与Buff")] 
    public class OOPSKILL_GiveBuff : OOP_EESKILL_UseOnON
    {
        public override string Name => "给与Buff";
        public int buffID;
        public float time = 10;
    }
    [Serializable, TypeRegistryItem("控制")]
    public class OOPSKILL_Control : OOP_EESKILL_UseOnON
    {
        public override string Name => "控制";
        public float timeControl;
        public int level;
    }
}
