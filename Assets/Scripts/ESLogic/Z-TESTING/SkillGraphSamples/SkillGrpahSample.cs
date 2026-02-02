using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES.SkillSample {
    /*[CreateAssetMenu(fileName = "contain", menuName = "测试So/contaimn")]
    public class SkillGrpahSample : NodeContainerSO<OOPEES_Target,OOP_EESkill,Filler>
    {
          
    }*/

    public class Entity_SKill
    {

    }
    public struct Filler 
    {
        public Entity on;
        public Entity by;
        public Entity_SKill skill;
    }
    public class OOP_EESkill : IOperation<Entity, Entity_SKill>
    {
        public void HandleByFill(Filler filler)
        {
            TryOperation(filler.on, filler.by, filler.skill);
        }

        public void TryCancel(Entity on, Entity from, Entity_SKill with)
        {
            
        }

        public void TryOperation(Entity on, Entity from, Entity_SKill with)
        {
            
        }
    }
    public class OOPEES_Target
    {
        //操作目标
        public Entity single;//原始为player-->
        public List<Entity> entities = new List<Entity>();

        //输出两个端口
        

    }
    //对Trigger 进行重整
    public abstract class OOPEES_OnSolver
    {
        //覆写？
        public bool write;
        //操作目标
        public Entity single;//原始为player
        public List<Entity> entities = new List<Entity>();
        public abstract void Solver(OOPEES_Target trigger);
    }
   
}
