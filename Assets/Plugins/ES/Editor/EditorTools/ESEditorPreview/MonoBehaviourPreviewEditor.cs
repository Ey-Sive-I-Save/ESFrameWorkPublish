using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    // 所有 MonoBehaviour 在没有特定定制编辑器时，自动套用这个泛型基类
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MonoBehaviourPreviewEditor : BasePreviewEditor<MonoBehaviour>
    {
       
    }
  
}
