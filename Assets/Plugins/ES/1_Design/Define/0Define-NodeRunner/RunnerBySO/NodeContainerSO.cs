using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CreateAssetMenu(fileName ="contain", menuName ="测试So/contaimn")]
    public class NodeContainerSO : ScriptableObject, INodeContainer
    {
        [SerializeReference,LabelText("缓存全部子"),ShowInInspector,ReadOnly,FoldoutGroup("内置数据")]
        public List<NodeRunnerSO> nodeRunners = new List<NodeRunnerSO>();
        public INodeRunner AddNodeByType(Type t)
        {
            if (t.IsAbstract) return null;
#if UNITY_EDITOR
            if (t.IsSubclassOf(typeof(ScriptableObject)))
            {
                var soChild=ScriptableObject.CreateInstance(t);
                if(soChild is NodeRunnerSO runner)
                {
                    soChild.name = "zesnode-"+runner.GetTitle();
                    nodeRunners.Add(runner );
                    AssetDatabase.AddObjectToAsset(soChild,ESDesignUtility.SafeEditor.Wrap_GetAssetPath(this));
                    EditorUtility.SetDirty(this);
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                    return runner;
                } 
            }
#endif
            return null;
        }

        public IEnumerable<INodeRunner> GetAllNodes()
        {
            
            return nodeRunners;
        }

        public void RemoveRunner(INodeRunner runner)
        {
            if (runner is NodeRunnerSO ro)
            {
                if (nodeRunners.Contains(ro))
                {
                    if (runner is UnityEngine.Object uo)
                    {
                        UnityEngine.Object.DestroyImmediate(uo);
                    }
                    nodeRunners.Remove(ro);
                    EditorUtility.SetDirty(this);
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}

