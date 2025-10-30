using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    [Serializable, TypeRegistryItem("标准缓存池")]
    public class CacherPool 
    {
        #region 值定义
        //---------------约定类------------------
        //可用于整数计数
        public int IntValue=0;
        //可用于时间计数
        public float FloatValue = 0;

        //---------------预定义类--------------------
        //阶段专属            次数
        public int Level = 0, Times=0;
        //时间计数专属        随机值
        public float Time = 0, Random=0.5f;

        //-------------- 条目类 -------------------
        //变换集
        public CacheItem<Transform> Trans = new ();
        //坐标集
        public CacheItem<Vector3> Vectors = new();
        //旋转集
        public CacheItem<Quaternion> Direcs = new();
        //标签集
        public CacheItem<string> Tags = new();

        //-------------------委托类----------------------------
        //运行时委托
        public Action<float> OnUpdate = (when) => { };
        //退出时委托
        public Action<float> OnExit = (when) => { };
        #endregion



    }
    //常规的可用缓存条目
    public class CacheItem<T>
    {
        public T MainValue;
        public T OtherValue;
        public HashSet<T> HashSet = new HashSet<T>();
        public Queue<T> Queue= new Queue<T>();
        public void Clear(bool clearMainAndOther=true)
        {
            if (clearMainAndOther)
            {
                MainValue = default;
                OtherValue = default;
            }
            HashSet.Clear();
            Queue.Clear();
        }
    }
}
