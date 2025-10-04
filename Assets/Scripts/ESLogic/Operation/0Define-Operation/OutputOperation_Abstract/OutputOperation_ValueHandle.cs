using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace ES
{
    /*只是想操作一个值 可以用 (Support+Target) 也可以为了性能直接去Target*/
    [Serializable]
    public abstract class OutputOperationValue<On, From, With, ValueType, OperationType, HandleOptions> : IOutputOperation<On, From, With>
    {
        public abstract void TryCancel(On on, From from, With with);

        public abstract void TryOperation(On on, From from, With with);
    }
    [Serializable]
    public abstract class OutputOperationValue_CompositeWithSupportAndTarget<On, From, With, ValueType, OperationType, HandleOptions,Support, Target> :
        OutputOperationValue<On, From, With, ValueType, OperationType, HandleOptions>
        where Support:SupportOperation<On, From, With, OperationType, HandleOptions>,new()
        where Target : TargetOperation<On, From, With, ValueType, OperationType, HandleOptions>
    {
        [SerializeReference, LabelText("数据支持")] public Support support=new Support();

        [SerializeReference, LabelText("数据支持")] public Target target;
        public override void TryOperation(On on, From from, With with)
        {
            var value = support.GetOpeationValue(on,from,with);
            target.TargetOpeation(on,from,with,value, support.GetOperationOptions);
        }
        public override void TryCancel(On on, From from, With with)
        {
            var value = support.GetOpeationValue(on, from, with);
            target.TargetCancel(on, from, with, value, support.GetOperationOptions);
        }
    }


}

