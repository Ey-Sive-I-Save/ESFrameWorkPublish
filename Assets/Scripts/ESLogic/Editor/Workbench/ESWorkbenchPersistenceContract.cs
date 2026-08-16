#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>高级工作台的作者态保存适配器。底座不依赖任何具体领域资产。</summary>
    public interface IESWorkbenchPersistenceAdapter<TAsset> where TAsset : UnityEngine.Object
    {
        bool TrySave(TAsset asset, SerializedObject serializedObject, out string message);
    }
}
#endif
