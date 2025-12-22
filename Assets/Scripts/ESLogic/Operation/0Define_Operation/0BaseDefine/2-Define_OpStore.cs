using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /*OpStore 非常简单，他是要求具有生命周期的RuntimeLogic能够支持操作类型缓存一些数据
     由于单一职责原则，通常是存储键值对即可，直接以已经序列化的共享逻辑单元当做键即可
     */

    //普通键值对 适用于啥呢，其实都不太使用
    public interface IOpStoreKeyValueForOutputOpeation<OP,Value,Flag> where OP : IOperation
    {

        public Dictionary<OP, Value> GetFromOpStore(Flag flag=default);
        
    } 
    public interface IOpStoreSafeKeyGroupForOutputOpeation<OP, Value, Flag> where OP : IOperation
    {
        public SafeKeyGroup<OP,Value> GetFromOpStore(Flag flag = default);
    }
}
