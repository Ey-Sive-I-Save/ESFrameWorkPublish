using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
  这组专门用来写 调用多个或者按条件调用 的 扩展模块
 */
/* [Serializable, TypeRegistryItem("0A扩展-转换作用源")]
    public class OutputOperationUI_Expand_InvokeNUM_2 : IOutputOperationUI
    {  

        public void TryOperation(ESUIElementCore on, ESUIElementCore from, ILink_UI_OperationOptions with)
        {

        }

        public void TryCancel(ESUIElementCore on, ESUIElementCore from, ILink_UI_OperationOptions with)
        {

        }

      
    }*/

namespace ES
{
    [Serializable, TypeRegistryItem("0C扩展-操作两个")]
    public class OutputOperationUI_Expand_InvokeNUM_2 : IOutputOperationUI
    {
        [SerializeReference, LabelText("第一个")] public IOutputOperationUI operation1;
        [SerializeReference, LabelText("第二个")] public IOutputOperationUI operation2;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            operation1?.TryOperation(on, from, with);
            operation2?.TryOperation(on, from, with);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            operation1?.TryCancel(on, from, with);
            operation2?.TryCancel(on, from, with);
        }
    }


    [Serializable, TypeRegistryItem("0C扩展-操作三个")]
    public class OutputOperationUI_Expand_InvokeNUM_3 : IOutputOperationUI
    {
        [SerializeReference, LabelText("第一个")] public IOutputOperationUI operation1;
        [SerializeReference, LabelText("第二个")] public IOutputOperationUI operation2;
        [SerializeReference, LabelText("第三个")] public IOutputOperationUI operation3;

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            operation1?.TryOperation(on, from, with);
            operation2?.TryOperation(on, from, with);
            operation3?.TryOperation(on, from, with);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            operation1?.TryCancel(on, from, with);
            operation2?.TryCancel(on, from, with);
            operation3?.TryOperation(on, from, with);
        }
    }

    [Serializable, TypeRegistryItem("0C扩展-概率调用(死概率-低级)")]
    public class OutputOperationUI_Expand_InvokeIfP : IOutputOperationUI
    {
        [LabelText("执行概率")] public float P = 0.5f;
        [SerializeReference, LabelText("执行操作")] public IOutputOperationUI operation;
        [LabelText("可取消")] public bool CanCancel = false;

        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            if (UnityEngine.Random.value < P)
            operation?.TryOperation(on, from, with);
        }
        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
           if(CanCancel)operation?.TryCancel(on, from, with);
        }
    }
}
