using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    [CreateAssetMenu(fileName ="contain", menuName ="测试So/contaimn")]
    public class NodeContainerSO : ScriptableObject, INodeContainer
    {
        [SerializeReference,LabelText("缓存全部子"),ShowInInspector,ReadOnly,FoldoutGroup("内置数据")]
        public List<NodeRunnerSO> nodeRunners = new List<NodeRunnerSO>();

#if UNITY_EDITOR
        private static int legacyEditorWriteScopeDepth;

        /// <summary>
        /// Explicit opt-in required by the historical NodeRunner editor only. This prevents
        /// arbitrary editor or business code from mutating graph sub-assets through this
        /// legacy container while the stable graph model is still being rebuilt.
        /// </summary>
        public static IDisposable BeginLegacyEditorWriteScope()
        {
            legacyEditorWriteScopeDepth++;
            return new LegacyEditorWriteScopeToken();
        }

        private static bool IsLegacyEditorWriteAllowed => legacyEditorWriteScopeDepth > 0;

        private sealed class LegacyEditorWriteScopeToken : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                if (legacyEditorWriteScopeDepth > 0)
                    legacyEditorWriteScopeDepth--;
            }
        }
#endif

        public virtual NodeEnvironment environment => NodeEnvironment.None;

        public INodeRunner AddNodeByType(Type t)
        {
            if (t.IsAbstract) return null;
#if UNITY_EDITOR
            if (!IsLegacyEditorWriteAllowed)
            {
                Debug.LogWarning("NodeRunner legacy asset write blocked: use an explicit legacy editor write scope.");
                return null;
            }

            if (t.IsSubclassOf(typeof(ScriptableObject)))
            {
                var soChild=ScriptableObject.CreateInstance(t);
                if(soChild is NodeRunnerSO runner)
                {
                    soChild.name = "zesnode-"+runner.GetTitle();
                    string assetPath = ESDesignUtility.SafeEditor.Wrap_GetAssetPath(this);
                    if (!AssetDatabase.Contains(this) || string.IsNullOrEmpty(assetPath))
                    {
                        UnityEngine.Object.DestroyImmediate(soChild);
                        Debug.LogWarning("NodeRunner legacy asset write blocked: container is not a saved Unity asset.");
                        return null;
                    }

                    Undo.RecordObject(this, "Add legacy NodeRunner");
                    nodeRunners.Add(runner );
                    AssetDatabase.AddObjectToAsset(soChild, assetPath);
                    Undo.RegisterCreatedObjectUndo(soChild, "Add legacy NodeRunner");
                    EditorUtility.SetDirty(this);
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
            if (!IsLegacyEditorWriteAllowed)
            {
                Debug.LogWarning("NodeRunner legacy asset write blocked: use an explicit legacy editor write scope.");
                return null;
            }

            if (runnerIN != null)
            {
                if (runnerIN is ScriptableObject so)
                {
                    var soChild = ScriptableObject.Instantiate(so);
                    if (soChild is NodeRunnerSO runner)
                    {
                        soChild.name = "zesnode-" + runner.GetTitle();
                        string assetPath = ESDesignUtility.SafeEditor.Wrap_GetAssetPath(this);
                        if (!AssetDatabase.Contains(this) || string.IsNullOrEmpty(assetPath))
                        {
                            UnityEngine.Object.DestroyImmediate(soChild);
                            Debug.LogWarning("NodeRunner legacy asset write blocked: container is not a saved Unity asset.");
                            return null;
                        }

                        Undo.RecordObject(this, "Copy legacy NodeRunner");
                        nodeRunners.Add(runner);
                        AssetDatabase.AddObjectToAsset(soChild, assetPath);
                        Undo.RegisterCreatedObjectUndo(soChild, "Copy legacy NodeRunner");
                        EditorUtility.SetDirty(this);
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
#if UNITY_EDITOR
            if (!IsLegacyEditorWriteAllowed)
            {
                Debug.LogWarning("NodeRunner legacy asset write blocked: use an explicit legacy editor write scope.");
                return;
            }
#endif

            if (runner is NodeRunnerSO ro)
            {
                if (nodeRunners.Contains(ro))
                {
                    if (runner is UnityEngine.Object uo)
                    {
                        #if UNITY_EDITOR
                        Undo.RecordObject(this, "Remove legacy NodeRunner");
                        Undo.DestroyObjectImmediate(uo);
                        #else
                        UnityEngine.Object.DestroyImmediate(uo,true);
                        #endif
                    }
                    nodeRunners.Remove(ro);
                    #if UNITY_EDITOR
                    EditorUtility.SetDirty(this);
                    AssetDatabase.SaveAssets();
                    #endif
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

