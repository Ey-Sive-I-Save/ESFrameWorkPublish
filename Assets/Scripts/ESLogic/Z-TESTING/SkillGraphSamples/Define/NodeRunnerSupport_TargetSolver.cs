using ES;
using ES.SkillSample;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    //相比于Target,不具备自动初始化功能，有输入端口，但是都可以应用Op上
    public abstract class NodeRunnerSupport_TargetSolver<Solver> : NodeRunnerSupport_TargetSolver<Entity, Solver,NodeContainerSkill> 
    where Solver : OOPEES_OnSolver
    { 
        

    }
}
