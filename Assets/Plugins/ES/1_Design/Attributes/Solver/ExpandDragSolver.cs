using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    /*ESDragToFieldSolver 用于拖动资源到字段框内，进行一个非直接赋值的解析事件
     对目标进行值变动并且保存
     */
    #region 拖动解算
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ESDragToFieldSolverAttribute: Attribute
    {
        public ESDragToFieldSolverOptions solverOptions;
        public ESDragToFieldSolverAttribute()
        {
            solverOptions = ESDragToFieldSolverOptions.SimpleAssetToABSearchKey;
        }
        public ESDragToFieldSolverAttribute(ESDragToFieldSolverOptions op)
        {
            solverOptions = op;
        }
    }
    public enum ESDragToFieldSolverOptions
    {
        [InspectorName("常规资产变AB查询键")] SimpleAssetToABSearchKey,
        [InspectorName("物体变UnityEvent新调用")] UnityEventNewInvoke,
    }
    #endregion
}

