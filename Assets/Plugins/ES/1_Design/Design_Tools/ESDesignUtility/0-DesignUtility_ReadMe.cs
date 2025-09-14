/*global using KVM = ESStaticUtility;*/
using System;
using System.Collections;

    


namespace ES
{
    //ESDesignUtility 是一系列静态处理方法，用于解耦的功能合集

    /*  XX ESDesignUtility 设计层工具包 ，
     *   使用时 ： ESStaticUtility.ColorSelector.XXX
     *  ##ColorSelector        存储大量自定义颜色 主要用于编辑器美化
     *  ##Couroutine           提供了一些快速创建协程的方法
     *  ##Creator              提供简单的深拷贝方案和创建其他数据
     *  ##DataApply            提供一句完成把Data应用到目标身上，做到代码简化
     *  ##DataInfoPointer      使用键取出某个数据源(通常是So类型)
     *  ##ESBack               大型 "返回" 逻辑 %比如获得实体周围的其他实体
     *  ##ESCodeTree           可视化多态序列化的代码生成辅助功能(详见CodeGen/Define/ESCodeTree)
     *  ##ESProcess            大型的 基于Link体系 的 事件处理 流
     *  ##Foreach              常用的循环操作 或者 递归手段
     *  ##Function             常用的 数学/容器/字符串 操作(一般配合特定枚举)
     *  ##KeyPointer           取键器 通常配合编辑器扩展使用
     *  ##Matcher              进行键值匹配 或者解析 曲线/数据 
     *  ##Reflection           简化反射操作
     *  ##SafeEditor           提供运行时可调用但是已经被预编辑功能的编辑器功能，或者只是完成了简化
     *  ##SimpleScriptMaker    简易的脚本生成工具(编辑器模式下使用)
     *  ##Sorter               数值/空间 上的 排序 操作
     *  ##TransformSetter      对变换的比较复杂的操作
     *  此外 被代码生成的枚举是可能的---在ESFrameWork / CodeGen / Enum 寻找
     */
    public static partial class ESDesignUtility
    {
        #region  找键器(考虑过时)
     /*   public static T FindByIKey<T, Key>(IEnumerable<T> ts, Key key) where T : IWithKey<Key> where Key : IKey
        {
            if (ts != null)
            {
                foreach (var i in ts)
                {
                    if (i.key.Equals(key)) return i;
                }
            }
            return default(T);
        }
        *//* public static CopyToWhere FindByKey<CopyToWhere, TypeSelect_>(IEnumerable<CopyToWhere> ts, TypeSelect_ key) where CopyToWhere : IWithKey<object> where TypeSelect_ : IKey
         {
             if (ts != null)
             {
                 foreach (var i in ts)
                 {
                     if (i.key.Equals(key)) return i;
                 }
             }
             return default(CopyToWhere);
         }*//*
        public static T FindByAKey<T, Key>(IEnumerable<T> ts, Key key) where T : IWithKey<IKey<Key>>
        {
            if (ts != null)
            {
                foreach (var i in ts)
                {
                    // Debug.Log($"Compare{i},{i.key},{key}");
                    if (i.key.Equals(key)) return i;
                }
            }
            return default(T);
        }
        public static bool ContainsByIKey<T, Key>(IEnumerable<T> ts, Key key) where T : IWithKey<Key> where Key : IKey
        {
            if (ts != null)
            {
                foreach (var i in ts)
                {
                    if (i.key.Equals(key)) return true;
                }
            }
            return false;
        }
        public static bool ContainsByAKey<T, Key>(IEnumerable<T> ts, Key key) where T : IWithKey<IKey<Key>>
        {
            if (ts != null)
            {
                foreach (var i in ts)
                {
                    if (i.key.Equals(key)) return true;
                }
            }
            return false;
        }*/
        #endregion
        
        static void test()
        {
            
        }
       

       

        
    }
}
