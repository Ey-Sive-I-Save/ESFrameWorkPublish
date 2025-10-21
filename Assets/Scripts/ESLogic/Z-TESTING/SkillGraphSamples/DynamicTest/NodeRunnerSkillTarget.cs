using ES;
using ES.SkillSample;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;


namespace ES.SkillSample
{
    [CreateNodeRunnerSoMenu(NodeEnvironment.SKill, "触发目标源", "常规起始目标(:玩家本体)")]
    public class NodeRunnerSkillTarget : NodeRunnerSupport_TargetSKill<OOP_EESKILLTarget_UseOnON>
    {
        public override Type DefaultValueType()
        {
            return typeof(OOPSKILL_Hurt2);
        }

        public override string GetNameForT()
        {
            return Value?.Name ?? "未定义";
        }
    }
    [Serializable]
    public abstract class OOP_EESKILLTarget_UseOnON : OOPEES_Target
    {
        public abstract string Name { get; }
    }
    [Serializable, TypeRegistryItem("玩家本体目标")]
    public class OOPSKILL_Hurt2 : OOP_EESKILLTarget_UseOnON
    {
        public override string Name => "玩家本体目标";
        public float damage;
        public bool isRange;
    }
    [Serializable, TypeRegistryItem("玩家身边的实体")]
    public class OOPSKILL_GiveBuff2 : OOP_EESKILLTarget_UseOnON
    {
        public override string Name => "玩家身边的实体";
        public int buffID;
        public float time = 10;
    }
    [Serializable, TypeRegistryItem("玩家缓冲池中的实体")]
    public class OOPSKILL_Control2 : OOP_EESKILLTarget_UseOnON
    {
        public override string Name => "玩家缓冲池中的实体";
        public float timeControl;
        public int level;
    }


}
