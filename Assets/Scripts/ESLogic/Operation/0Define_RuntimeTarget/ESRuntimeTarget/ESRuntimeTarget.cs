using ES;
using ES.SkillSample;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /*
      一次性存储最常可能需要的作用目标，来达到通用的技能构筑功能
    
    */
    
    public struct ESRuntimeTarget : IRuntimeTarget
    {
       public Entity entityTarget;
    }

  
}
