using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface ISharedAndVariable<_Shared, _Variable> 
    {
        _Shared SharedData { get; set; }
        _Variable VariableData { get; set; }
    }

    #region 示例
    public class ExampleSharedAndVariable : ISharedAndVariable<object, ExampleDeepCloneData>
    {
        public object SharedData { get; set; }
        public ExampleDeepCloneData VariableData { get ; set ; }
    }
    #endregion
}
