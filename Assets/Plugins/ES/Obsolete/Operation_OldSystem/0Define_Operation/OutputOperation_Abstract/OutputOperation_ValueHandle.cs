/*
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
    只是想操作一个�?可以�?(Support+Target) 也可以为了性能直接去ValueEntry
    [Serializable]
    public abstract class OutputOperationValue<Target,Logic, ValueType, OperationType, HandleOptions> : IOutputOperation<Target,Logic>
    {
        public abstract void TryCancel(Target target,Logic logic);

        public abstract void TryOperation(Target target,Logic logic);
    }
    [Serializable]
    public abstract class OutputOperationValue_CompositeWithSupportAndTarget<Target,Logic, ValueType, OperationType, HandleOptions,Support> :
        OutputOperationValue<Target,Logic, ValueType, OperationType, HandleOptions>
        where Support:SupportOperation<Target,Logic, OperationType, HandleOptions>,new()
        where Target : ValueEntryOperation<Target,Logic, ValueType, OperationType, HandleOptions>
    {
        [SerializeReference, LabelText("数据支持")] public Support support=new Support();

        [SerializeReference, LabelText("数据支持")] public Target target;
        public override void TryOperation(Target target,Logic logic)
        {
            var value = support.GetOpeationValue(target,logic);
            target.HandleValueEntryOpeation(target,logic,value, support.GetOperationOptions);
        }
        public override void TryCancel(Target target,Logic logic)
        {
            var value = support.GetOpeationValue(target, logic);
            target.HandleValueEntryCancel(target, logic, value, support.GetOperationOptions);
        }
    }


}


*/