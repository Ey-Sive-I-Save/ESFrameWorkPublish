using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Operation 存储接口标记。
    /// 由 IOpSupporter 提供具体存储，用于保存 Operation 运行期数据。
    /// </summary>
    public interface IOpStore
    {
    }

    /// <summary>字典形式的 Operation 存储。</summary>
    public interface IOpStoreDictionary<TOperation, TValue, TFlag> : IOpStore
    {
        SafeDictionary<TOperation, TValue> GetFromOpStore(TFlag flag = default);
    }

    /// <summary>一对多分组形式的 Operation 存储。</summary>
    public interface IOpStoreKeyGroup<TOperation, TValue, TFlag> : IOpStore
    {
        SafeKeyGroup<TOperation, TValue> GetFromOpStore(TFlag flag = default);
    }
}
