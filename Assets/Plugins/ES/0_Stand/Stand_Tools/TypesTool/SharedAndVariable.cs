using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public class SharedAndVariable
    {

        #region 应用方法
        /// <summary>
        /// 常规数据应用,完全相同的Shared,Variable,并且Variable是完成了IDeepClone的Class类型
        /// </summary>
        /// <typeparam name="Shared"></typeparam>
        /// <typeparam name="Variable"></typeparam>
        /// <param name="applier">应用者</param>
        /// <param name="from">数据来源</param>
        public static void ApplyFrom<Shared, Variable>(ISharedAndVariable<Shared, Variable> applier, ISharedAndVariable<Shared, Variable> from)
            where Variable : class, IDeepClone<Variable>, new()
        {
            applier.SharedData = from.SharedData;
            applier.VariableData ??= new Variable();
            if (from.VariableData != null)
            {
                applier.VariableData.DeepCloneFrom(from.VariableData);
            }
            else
            {
                from.VariableData = new Variable();
            }
        }

        /// <summary>
        /// 动态数据应用,具有继承关系的Shared,Variable,并且Variable是完成了IDeepClone的Class类型
        /// </summary>
        /// <typeparam name="SharedApplier"></typeparam>
        /// <typeparam name="VariableApplier"></typeparam>
        /// <typeparam name="SharedFrom"></typeparam>
        /// <typeparam name="VariableFrom"></typeparam>
        /// <param name="applier"></param>
        /// <param name="from"></param>
        public static void ApplyFromDynamic<SharedApplier, VariableApplier, SharedFrom, VariableFrom>(ISharedAndVariable<SharedApplier, VariableApplier> applier, ISharedAndVariable<SharedFrom, VariableFrom> from)
    where SharedFrom : SharedApplier where VariableFrom : class, VariableApplier, IDeepClone<VariableFrom>, new()
          where VariableApplier : class, IDeepClone<VariableApplier>, new()
        {
            applier.SharedData = from.SharedData;
            applier.VariableData ??= new VariableApplier();
            if (from.VariableData != null)
            {
                applier.VariableData.DeepCloneFrom(from.VariableData);
            }
            else
            {
                from.VariableData = new VariableFrom();
            }
        }

        /// <summary>
        /// 结构体Variable数据应用,完全相同的Shared,Variable,并且Variable是结构体
        /// </summary>
        /// <typeparam name="Shared"></typeparam>
        /// <typeparam name="Variable"></typeparam>
        /// <param name="applier"></param>
        /// <param name="from"></param>
        public static void ApplyFromStruct<Shared, Variable>(ISharedAndVariable<Shared, Variable> applier, ISharedAndVariable<Shared, Variable> from)
            where Variable : struct
        {
            applier.SharedData = from.SharedData;
            applier.VariableData = from.VariableData;
        }

        /// <summary>
        /// 结构体Variable动态数据应用,具有继承关系的Shared
        /// </summary>
        /// <typeparam name="Shared"></typeparam>
        /// <typeparam name="Variable"></typeparam>
        /// <param name="applier"></param>
        /// <param name="from"></param>
        public static void ApplyFromStructDynamic<SharedApplier, SharedFrom, Variable>(ISharedAndVariable<SharedApplier, Variable> applier, ISharedAndVariable<SharedFrom, Variable> from)
             where SharedFrom : SharedApplier
            where Variable : struct
        {
            applier.SharedData = from.SharedData;
            applier.VariableData = from.VariableData;
        }

        #endregion


        #region 演示
        public class Samples
        {
            public class 共享数据1
            {
                public string 共享名字;
                public int 共享类型;
            }
            public class 变量数据1 : IDeepClone<变量数据1>
            {
                public string 变量标志;
                public float 变量血量;

                public void DeepCloneFrom(变量数据1 t)
                {
                    变量标志 = t.变量标志;
                    变量血量 = t.变量血量;
                }
            }

            public class 假如这是一个SO数据1 : ISharedAndVariable<共享数据1, 变量数据1>
            {
                public 共享数据1 SharedData { get; set; }
                public 变量数据1 VariableData { get; set ; }
            }

            public class 假如这是一个场景对象 : ISharedAndVariable<共享数据1, 变量数据1>
            {
                public 共享数据1 SharedData { get; set; }
                public 变量数据1 VariableData { get; set; }

                void 初始化(假如这是一个SO数据1 from)
                {
                    SharedAndVariable.ApplyFrom(this,from);
                }
            }
        }
        #endregion
    }
}