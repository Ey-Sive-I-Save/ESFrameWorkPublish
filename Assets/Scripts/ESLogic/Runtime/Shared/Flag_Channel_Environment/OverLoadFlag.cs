using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /*OverLoadFlag 
     * 重载辨识
     * 的作用非常简单，只是为了创建避免同名不依赖参数的重载
     * 为每类可能重复的同名行为定义了无损失的参数支持，让重载得以成立*/
    public class OverLoadFlag<This> where This : OverLoadFlag<This>
    {
        public static This flag;
    }
    //应用场景
    public interface Example__IAttackEnable<FlagOnly>
    {
        void Attack(FlagOnly flagOnly);
    }
    public class MelleAttackFlag : OverLoadFlag<MelleAttackFlag>
    { }
    public class RangeAttackFlag : OverLoadFlag<RangeAttackFlag>
    { }
    public abstract class Example_IAMABanMan :
        Example__IAttackEnable<MelleAttackFlag>,
        Example__IAttackEnable<RangeAttackFlag>
    {
        public abstract void Attack(MelleAttackFlag flagOnly);
        public abstract void Attack(RangeAttackFlag flagOnly);
        void Test()
        {
            this.Attack(MelleAttackFlag.flag);
            this.Attack(RangeAttackFlag.flag);
        }
    }

}

