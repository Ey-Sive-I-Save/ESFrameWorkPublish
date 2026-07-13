using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 数值入口 Operation。
    /// 用于把某个数值按指定操作方式应用到目标对象上，并支持对应的取消逻辑。
    /// </summary>
    public interface IHandleValueOperation<Target, Logic, ValueType, OperationOptions> : IOperation
    {
        /// <summary>应用数值操作。</summary>
        void HandleValueEntryOperation(Target target, Logic logic, ValueType operationValue, OperationOptions selectType);

        /// <summary>取消数值操作。</summary>
        void HandleValueEntryCancel(Target target, Logic logic, ValueType operationValue, OperationOptions selectType);
    }
}
