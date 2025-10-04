using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /*Cache 非常简单，他是要求具有生命周期的With能够支持缓存一些数据
     由于单一职责原则，通常是存储键值对即可，直接以已经序列化的共享逻辑单元当做键即可
     */

    //普通键值对 适用于啥呢，其实都不太使用
    public interface ICacheKeyValueForOutputOpeation<OP,Value,Flag> where OP : IOperation
    {

        public Dictionary<OP, Value> GetCache(Flag flag=default);
        
    } 

    public interface ICacheSafeKeyGroupForOutputOpeation<OP, Value, Flag> where OP : IOperation
    {
        public SafeKeyGroup<OP,Value> GetCache(Flag flag = default);
    }
}
