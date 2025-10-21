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

        public virtual NodeEnvironment environment => NodeEnvironment.None;

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

        public INodeRunner CopyNodeRunner(INodeRunner runnerIN)
        {
#if UNITY_EDITOR
            if (runnerIN != null)
            {
                if (runnerIN is ScriptableObject so)
                {
                    var soChild = ScriptableObject.Instantiate(so);
                    if (soChild is NodeRunnerSO runner)
                    {
                        soChild.name = "zesnode-" + runner.GetTitle();
                        nodeRunners.Add(runner);
                        AssetDatabase.AddObjectToAsset(soChild, ESDesignUtility.SafeEditor.Wrap_GetAssetPath(this));
                        EditorUtility.SetDirty(this);
                        AssetDatabase.Refresh();
                        AssetDatabase.SaveAssets();
                        return runner;
                    }
                }
                return null;
            }
#endif
            return null;
        }

        public IEnumerable<INodeRunner> GetAllNodes()
        {
            
            return nodeRunners;
        }

        

        protected virtual void InitNodes()
        {
            
        }

        public void RemoveRunner(INodeRunner runner)
        {
            if (runner is NodeRunnerSO ro)
            {
                if (nodeRunners.Contains(ro))
                {
                    if (runner is UnityEngine.Object uo)
                    {
                        UnityEngine.Object.DestroyImmediate(uo,true);
                    }
                    nodeRunners.Remove(ro);
                    EditorUtility.SetDirty(this);
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }

    public abstract class NodeContainerSO<_Fill> : NodeContainerSO
    {
        //填充物，用来匹配调用参数
        public _Fill fill;
        
        public void Init(_Fill fill)
        {
            this.fill = fill;
            InitNodes();
        }
        
    }
}

