using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface IDeepClone
    {
        void DeepCloneFrom(object from);
    }
    /// <summary>
    /// 可深拷贝 自己实现一个最高性能的深拷贝功能 -->
    /// </summary>
    /// <typeparam name="This">逆变为This</typeparam>
    public interface IDeepClone<in This> : IDeepClone where This : IDeepClone<This>
    {
        void DeepCloneFrom(This t);
        void IDeepClone.DeepCloneFrom(object from)
        {
            DeepCloneFrom((This)from);
        }
    }

    #region 例子
    public class TestDeepClone : IDeepClone<TestDeepClone>
    {
        public void DeepCloneFrom(TestDeepClone t)
        {
            throw new System.NotImplementedException();
        }
    }

    #endregion
}
