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

    #region 测试泛型
    public class TestSharedAndVariable : ISharedAndVariable<object, TestDeepClone>
    {
        public object SharedData { get; set; }
        public TestDeepClone VariableData { get ; set ; }
    }
    #endregion
}