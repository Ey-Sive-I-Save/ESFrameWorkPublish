using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ESResRefer 子资产支持版本
    /// 
    /// 适用场景：
    /// - Sprite Atlas 中的单个 Sprite
    /// - FBX 模型中的 Mesh/Material
    /// - 多Sprite贴图中的某个Sprite
    /// - 动画控制器中的某个 AnimationClip
    /// 
    /// 使用方式：
    /// 1. 拖入主资产 (如 SpriteAtlas)
    /// 2. 在下拉菜单中选择子资产 (如某个 Sprite)
    /// 3. 运行时会加载主资产，然后返回指定的子资产
    /// </summary>
    [Serializable]
    public class ESResReferSubAsset<TMain, TSub> : ESResReferABBase 
        where TMain : UnityEngine.Object 
        where TSub : UnityEngine.Object
    {
        #region 序列化字段
        
        [SerializeField, HideInInspector]
        private string _subAssetName = "";
        
        /// <summary>
        /// 子资产名称
        /// </summary>
        public string SubAssetName => _subAssetName;
        
        /// <summary>
        /// 是否有效（主资产GUID和子资产名称都存在）
        /// </summary>
        public new bool IsValid => !string.IsNullOrEmpty(_guid) && !string.IsNullOrEmpty(_subAssetName);
        
        #endregion
        
        #region 编辑器支持
        
#if UNITY_EDITOR
        [NonSerialized]
        private bool _needRefresh = true;
        
        [NonSerialized]
        private UnityEngine.Object _editorMainAsset;
        
        [NonSerialized]
        private UnityEngine.Object _editorSubAsset;
        
        [NonSerialized]
        private List<UnityEngine.Object> _cachedSubAssets;
        
        [NonSerialized]
        private string[] _subAssetNames;
        
        [NonSerialized]
        private int _selectedSubAssetIndex = 0;
#endif

        public override Type AssetBaseType => typeof(TSub);

        [OnInspectorGUI]
        public override void Draw()
        {
#if UNITY_EDITOR
            EditorGUILayout.BeginVertical();
            
            // 1. 绘制主资产选择
            EditorGUILayout.LabelField("主资产 (Main Asset)", EditorStyles.boldLabel);
            
            // 刷新编辑器资产引用
            if (_needRefresh || _editorMainAsset == null)
            {
                _editorMainAsset = ESStandUtility.SafeEditor.LoadAssetByGUIDString(_guid);
                RefreshSubAssets();
                _needRefresh = false;
            }

            var newMainAsset = EditorGUILayout.ObjectField(_editorMainAsset, typeof(TMain), false);
            
            if (newMainAsset != _editorMainAsset)
            {
                // 类型验证
                if (newMainAsset != null && !(newMainAsset is TMain))
                {
                    UnityEngine.Debug.LogWarning($"[ESResReferSubAsset] 主资产类型不匹配：需要 {typeof(TMain).Name}，但拖入的是 {newMainAsset.GetType().Name}");
                    EditorGUILayout.EndVertical();
                    return;
                }
                
                _editorMainAsset = newMainAsset;
                
                if (newMainAsset != null)
                {
                    _guid = ESStandUtility.SafeEditor.GetAssetGUID(newMainAsset);
                    RefreshSubAssets();
                    
                    // 检测收集状态
                    TryAutoCollectAsset(newMainAsset);
                }
                else
                {
                    _guid = "";
                    _subAssetName = "";
                    _cachedSubAssets = null;
                    _subAssetNames = null;
                }
                
                _needRefresh = true;
            }
            
            // 2. 绘制子资产选择
            if (_editorMainAsset != null && _cachedSubAssets != null && _cachedSubAssets.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("子资产 (Sub Asset)", EditorStyles.boldLabel);
                
                // 确保当前选中的子资产索引有效
                if (!string.IsNullOrEmpty(_subAssetName))
                {
                    _selectedSubAssetIndex = Array.FindIndex(_subAssetNames, name => name == _subAssetName);
                    if (_selectedSubAssetIndex < 0) _selectedSubAssetIndex = 0;
                }
                
                int newIndex = EditorGUILayout.Popup("选择子资产", _selectedSubAssetIndex, _subAssetNames);
                
                if (newIndex != _selectedSubAssetIndex || string.IsNullOrEmpty(_subAssetName))
                {
                    _selectedSubAssetIndex = newIndex;
                    _subAssetName = _subAssetNames[_selectedSubAssetIndex];
                    _editorSubAsset = _cachedSubAssets[_selectedSubAssetIndex];
                }
                
                // 显示当前选中的子资产预览
                if (_editorSubAsset != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("预览");
                    EditorGUILayout.ObjectField(_editorSubAsset, typeof(TSub), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else if (_editorMainAsset != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"主资产 '{_editorMainAsset.name}' 没有找到类型为 {typeof(TSub).Name} 的子资产", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
#endif
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// 刷新子资产列表
        /// </summary>
        private void RefreshSubAssets()
        {
            _cachedSubAssets = new List<UnityEngine.Object>();
            
            if (_editorMainAsset == null)
            {
                _subAssetNames = new string[0];
                return;
            }
            
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(_editorMainAsset);
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            
            // 过滤出符合子资产类型的对象
            foreach (var asset in allAssets)
            {
                if (asset != null && asset is TSub && asset != _editorMainAsset)
                {
                    _cachedSubAssets.Add(asset);
                }
            }
            
            _subAssetNames = _cachedSubAssets.Select(a => a.name).ToArray();
            
            if (_cachedSubAssets.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"[ESResReferSubAsset] 主资产 '{_editorMainAsset.name}' 中没有找到类型为 {typeof(TSub).Name} 的子资产");
            }
        }
        
        /// <summary>
        /// 检测资产收集状态（与 ESResRefer 保持一致）
        /// </summary>
        private void TryAutoCollectAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            
            try
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
                
                if (string.IsNullOrEmpty(assetPath))
                {
                    UnityEngine.Debug.LogWarning($"[ESResReferSubAsset] 无法获取资产路径: {asset.name}");
                    return;
                }
                
                var importer = UnityEditor.AssetImporter.GetAtPath(assetPath);
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                {
                    UnityEngine.Debug.Log($"[ESResReferSubAsset] 资产已收集: {asset.name} -> AB: {importer.assetBundleName}");
                    return;
                }
                
                UnityEngine.Debug.LogWarning(
                    $"[ESResReferSubAsset] 资产尚未收集到ES系统\n" +
                    $"资产名: {asset.name}\n" +
                    $"路径: {assetPath}\n" +
                    $"请使用 ES编辑器工具 收集此资产后再使用。",
                    asset
                );
                
                bool openEditor = UnityEditor.EditorUtility.DisplayDialog(
                    "⚠️ 资产未收集",
                    $"资产 '{asset.name}' 尚未收集到ES资源系统。\n\n"
                    + $"路径: {assetPath}\n\n"
                    + "需要先使用 ES资源编辑器 收集此资产，才能在运行时加载。\n\n"
                    + "是否立即打开ES资源编辑器？",
                    "打开编辑器",
                    "稍后处理"
                );
                
                if (openEditor)
                {
                    if (!UnityEditor.EditorApplication.ExecuteMenuItem("ES/ResEditor"))
                    {
                        if (!UnityEditor.EditorApplication.ExecuteMenuItem("Tools/ES/ResEditor"))
                        {
                            UnityEngine.Debug.LogWarning("[ESResReferSubAsset] 无法找到ES资源编辑器菜单项，请手动打开。");
                        }
                    }
                    
                    UnityEditor.Selection.activeObject = asset;
                    UnityEditor.EditorGUIUtility.PingObject(asset);
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[ESResReferSubAsset] 资产收集状态检测失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
#endif

        #endregion
        
        #region 核心加载 API
        
        /// <summary>
        /// 异步加载子资产
        /// </summary>
        public void LoadAsync(ESResLoader loader, Action<bool, TSub> onComplete, bool autoStartLoading = true)
        {
            if (!IsValid)
            {
                UnityEngine.Debug.LogError("[ESResReferSubAsset] 无效的资源引用，GUID或子资产名称为空");
                onComplete?.Invoke(false, default(TSub));
                return;
            }

            var targetLoader = loader ?? ESResMaster.GlobalResLoader;

            targetLoader.AddAsset2LoadByGUIDSourcer(_guid, (success, source) =>
            {
                if (!success || source.Asset == null)
                {
                    UnityEngine.Debug.LogError($"[ESResReferSubAsset] 主资产加载失败: GUID={_guid}");
                    onComplete?.Invoke(false, default(TSub));
                    return;
                }
                
                // 主资产加载成功，查找子资产
                TSub subAsset = FindSubAsset(source.Asset);
                
                if (subAsset != null)
                {
                    onComplete?.Invoke(true, subAsset);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[ESResReferSubAsset] 子资产未找到: {_subAssetName} in {source.Asset.name}");
                    onComplete?.Invoke(false, default(TSub));
                }
            });

            if (autoStartLoading)
            {
                targetLoader.LoadAllAsync();
            }
        }
        
        /// <summary>
        /// 异步加载 - 使用全局 Loader
        /// </summary>
        public void LoadAsync(Action<bool, TSub> onComplete, bool autoStartLoading = true)
        {
            LoadAsync(null, onComplete, autoStartLoading);
        }
        
        /// <summary>
        /// Task 异步加载
        /// </summary>
        public Task<TSub> LoadAsyncTask(ESResLoader loader = null)
        {
            var tcs = new TaskCompletionSource<TSub>();
            
            LoadAsync(loader, (success, asset) =>
            {
                tcs.SetResult(success ? asset : default(TSub));
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// 同步加载子资产
        /// </summary>
        public bool LoadSync(ESResLoader loader, out TSub asset)
        {
            asset = default(TSub);

            if (!IsValid)
            {
                UnityEngine.Debug.LogError("[ESResReferSubAsset] 无效的资源引用，GUID或子资产名称为空");
                return false;
            }

            var targetLoader = loader ?? ESResMaster.GlobalResLoader;
            
            bool success = false;
            TSub tempAsset = default(TSub);
            
            targetLoader.AddAsset2LoadByGUIDSourcer(_guid, (b, source) =>
            {
                if (b && source.Asset != null)
                {
                    tempAsset = FindSubAsset(source.Asset);
                    success = tempAsset != null;
                }
            });
            
            targetLoader.LoadAll_Sync();
            
            asset = tempAsset;
            
            if (!success)
            {
                UnityEngine.Debug.LogError($"[ESResReferSubAsset] 同步加载失败: GUID={_guid}, SubAsset={_subAssetName}");
            }
            
            return success;
        }
        
        /// <summary>
        /// 同步加载 - 使用全局 Loader
        /// </summary>
        public bool LoadSync(out TSub asset)
        {
            return LoadSync(null, out asset);
        }
        
        /// <summary>
        /// 获取已加载的子资产
        /// </summary>
        public TSub GetLoadedAsset()
        {
            if (!IsValid) return default(TSub);

            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByGUID(_guid, out var key))
            {
                var source = ESResMaster.ResTable.GetAssetResByKey(key);
                if (source != null && source.State == ResSourceState.Ready && source.Asset != null)
                {
                    return FindSubAsset(source.Asset);
                }
            }

            return default(TSub);
        }
        
        /// <summary>
        /// 从主资产中查找子资产
        /// </summary>
        private TSub FindSubAsset(UnityEngine.Object mainAsset)
        {
            if (mainAsset == null || string.IsNullOrEmpty(_subAssetName))
                return default(TSub);
            
#if UNITY_EDITOR
            // 编辑器模式：使用 AssetDatabase
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mainAsset);
            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            
            foreach (var asset in allAssets)
            {
                if (asset != null && asset is TSub subAsset && asset.name == _subAssetName)
                {
                    return subAsset;
                }
            }
#else
            // 运行时模式：尝试直接转换（针对某些特殊类型）
            if (mainAsset is TSub directAsset && mainAsset.name == _subAssetName)
            {
                return directAsset;
            }
            
            // TODO: 运行时从 AssetBundle 加载子资产的逻辑
            // 这里需要根据实际的 ES 资源系统 API 来实现
            UnityEngine.Debug.LogWarning($"[ESResReferSubAsset] 运行时子资产加载尚未完全实现: {_subAssetName}");
#endif
            
            return default(TSub);
        }
        
        #endregion
    }
    
    #region 预定义子资产类型
    
    /// <summary>
    /// Sprite Atlas 中的 Sprite 引用
    /// </summary>
    [Serializable]
    public class ESResReferSpriteFromAtlas : ESResReferSubAsset<UnityEngine.U2D.SpriteAtlas, Sprite>
    {
    }
    
    /// <summary>
    /// 多Sprite贴图中的单个 Sprite 引用
    /// </summary>
    [Serializable]
    public class ESResReferSpriteFromTexture : ESResReferSubAsset<Texture2D, Sprite>
    {
    }
    
    /// <summary>
    /// FBX 模型中的 Mesh 引用
    /// </summary>
    [Serializable]
    public class ESResReferMeshFromFBX : ESResReferSubAsset<GameObject, Mesh>
    {
    }
    
    /// <summary>
    /// FBX 模型中的 Material 引用
    /// </summary>
    [Serializable]
    public class ESResReferMaterialFromFBX : ESResReferSubAsset<GameObject, Material>
    {
    }
    
    /// <summary>
    /// 动画控制器中的 AnimationClip 引用
    /// </summary>
    [Serializable]
    public class ESResReferClipFromController : ESResReferSubAsset<RuntimeAnimatorController, AnimationClip>
    {
    }
    
    #endregion
}
