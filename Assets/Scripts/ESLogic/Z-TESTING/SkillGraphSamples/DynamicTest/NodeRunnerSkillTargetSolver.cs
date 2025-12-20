using ES;
using ES.SkillSample;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES.SkillSample
{
    [CreateNodeRunnerSoMenu(NodeEnvironment.SKill, "目标源筛选", "目标筛选(:单目标转为多目标)")]
    public class NodeRunnerSkillTargetSolver : NodeRunnerSupport_TargetSolver<OOP_EESKILLTargetSolver_UseOnON>
    {
        public override Type DefaultValueType()
        {
            return typeof(OOPSKILL_Hurt4);
        }

        public override string GetNameForT()
        {
            return Value?.Name ?? "未定义";
        }
    }
    [Serializable]
    public abstract class OOP_EESKILLTargetSolver_UseOnON : OOPEES_OnSolver
    {
        public abstract string Name { get; }
        public override void Solver(OOPEES_Target trigger)
        {
            
        }
    }
    [Serializable, TypeRegistryItem("单目标转为多目标")]
    public class OOPSKILL_Hurt4 : OOP_EESKILLTargetSolver_UseOnON
    {
        public override string Name => "单目标转为多目标";
        public float damage;
        public bool isRange;
    }
    [Serializable, TypeRegistryItem("多目标里离单目标最近的作为单目标")]
    public class OOPSKILL_GiveBuff4 : OOP_EESKILLTargetSolver_UseOnON
    {
        public override string Name => "多目标里离单目标最近的作为单目标";
        public int buffID;
        public float time = 10;
    }
    [Serializable, TypeRegistryItem("多目标剔除单目标")]
    public class OOPSKILL_Control4 : OOP_EESKILLTargetSolver_UseOnON
    {
        public override string Name => "多目标剔除单目标";
        public float timeControl;
        public int level;
    }

    [Serializable, TypeRegistryItem("从多目标中剔除单目标的友好阵营")]
    public class OOPSKILL_Control4w : OOP_EESKILLTargetSolver_UseOnON
    {
        public override string Name => "多目标剔除单目标";
        public float timeControl;
        public int level;
    }

}
