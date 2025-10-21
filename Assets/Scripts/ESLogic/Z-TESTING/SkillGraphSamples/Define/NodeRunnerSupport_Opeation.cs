using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES.SkillSample {
    public abstract class NodeRunnerSupport_OpeationSkill<Operation> : 
        NodeRunnerSupport_Opeation<ES.SkillSample.Entity,Operation,NodeContainerSkill>
        where Operation : OOP_EESkill
    {
        public override List<NodePort> GetOutputNodes()
        {
            return null;
        }
    }
}
