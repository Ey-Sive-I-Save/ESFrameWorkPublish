using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ES
{
    #region 抽象基类
    
    /// <summary>
    /// ESResRefer 抽象基类 - ES 资源系统的便捷引用工具
    /// 
    /// 这是一个辅助性工具，依赖 ESResLoader 和 ESResSource 完成资源加载和引用计数管理
    /// 不具有独立的引用计数权限，所有引用计数由 ES 资源系统管理
    /// 
    /// 设计原则：
    /// 1. 不创建临时 Loader，使用传入的 Loader 或全局 Loader  
    /// 2. 不管理引用计数，交由 ESResSource 管理
    /// 3. 作为辅助工具提供便捷的编辑器体验和加载接口
    /// </summary>
    [Serializable]
    public abstract class ESResReferABBase
    {
        [SerializeField, HideInInspector]
        protected string _guid = "";

        public string GUID => _guid;
        public abstract Type AssetBaseType { get; }
        public bool IsValid => !string.IsNullOrEmpty(_guid);

        public abstract void Draw();
        
        /// <summary>
        /// 验证资源是否存在
        /// </summary>
        public bool Validate()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_guid))
                return false;
                
            var asset = ESStandUtility.SafeEditor.LoadAssetByGUIDString(_guid);
            return asset != null;
#else
            return !string.IsNullOrEmpty(_guid);
#endif
        }
    }

    #endregion

    #region 核心泛型实现

    /// <summary>
    /// ESResRefer 泛型实现
    /// 提供类型安全的资源引用，依赖 ES 资源系统完成加载
    /// </summary>
    [Serializable]
    public class ESResRefer<T> : ESResReferABBase where T : UnityEngine.Object
    {
        #region 编辑器支持
        
#if UNITY_EDITOR
        [NonSerialized]
        private bool _needRefresh = true;
        
        [NonSerialized]
        private UnityEngine.Object _editorAsset;
#endif

        public override Type AssetBaseType => typeof(T);

        /// <summary>
        /// 在 Inspector 中绘制 - 添加特殊符号 @ 表明这是便捷引用工具
        /// </summary>
        [OnInspectorGUI]
        public override void Draw()
        {
#if UNITY_EDITOR
            // 刷新编辑器资产引用
            if (_needRefresh || _editorAsset == null)
            {
                _editorAsset = ESStandUtility.SafeEditor.LoadAssetByGUIDString(_guid);
                _needRefresh = false;
            }

            // 绘制对象字段 - 完全模拟原生 Unity 体验
            var newAsset = EditorGUILayout.ObjectField(_editorAsset, typeof(T), false);
            
            if (newAsset != _editorAsset)
            {
                // 类型验证：确保拖入的资产类型匹配
                if (newAsset != null && !(newAsset is T))
                {
                    UnityEngine.Debug.LogWarning($"[ESResRefer] 资产类型不匹配：需要 {typeof(T).Name}，但拖入的是 {newAsset.GetType().Name}");
                    return;
                }
                
                _editorAsset = newAsset;
                
                if (newAsset != null)
                {
                    _guid = ESStandUtility.SafeEditor.GetAssetGUID(newAsset);
                    
                    // 拖入资产时自动检测并提示收集状态
                    TryAutoCollectAsset(newAsset);
                }
                else
                {
                    _guid = "";
                }
                
                _needRefresh = true;
            }
#endif
        }

        #endregion

        #region 核心加载 API - 依赖 ES 资源系统

        /// <summary>
        /// 异步加载资源 - 使用指定 Loader（推荐）
        /// </summary>
        /// <param name="loader">资源加载器，如果为 null 则使用全局 Loader</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="autoStartLoading">是否立即开始加载，默认true。如果批量加载请设为false，手动调用loader.LoadAllAsync()</param>
        public void LoadAsync(ESResLoader loader, Action<bool, T> onComplete, bool autoStartLoading = true)
        {
            if (!IsValid)
            {
                UnityEngine.Debug.LogError("[ESResRefer] 无效的资源引用，GUID为空");
                onComplete?.Invoke(false, default(T));
                return;
            }

            // 使用传入的 Loader 或全局 Loader
            var targetLoader = loader ?? ESResMaster.GlobalResLoader;

            targetLoader.AddAsset2LoadByGUIDSourcer(_guid, (success, source) =>
            {
                if (success && source.Asset is T asset)
                {
                    onComplete?.Invoke(true, asset);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[ESResRefer] 加载失败: GUID={_guid}");
                    onComplete?.Invoke(false, default(T));
                }
            });

            // 只有在 autoStartLoading 为 true 时才触发加载
            if (autoStartLoading)
            {
                targetLoader.LoadAllAsync();
            }
        }

        /// <summary>
        /// 异步加载资源 - 使用全局 Loader
        /// </summary>
        /// <param name="autoStartLoading">是否立即开始加载，默认true</param>
        public void LoadAsync(Action<bool, T> onComplete, bool autoStartLoading = true)
        {
            LoadAsync(null, onComplete, autoStartLoading);
        }

        /// <summary>
        /// 异步加载 - Task 版本
        /// </summary>
        public Task<T> LoadAsyncTask(ESResLoader loader = null)
        {
            var tcs = new TaskCompletionSource<T>();
            
            LoadAsync(loader, (success, asset) =>
            {
                tcs.SetResult(success ? asset : default(T));
            });
            
            return tcs.Task;
        }

        /// <summary>
        /// 同步加载资源 - 使用指定 Loader
        /// </summary>
        public bool LoadSync(ESResLoader loader, out T asset)
        {
            asset = default(T);

            if (!IsValid)
            {
                UnityEngine.Debug.LogError("[ESResRefer] 无效的资源引用，GUID为空");
                return false;
            }

            // 使用传入的 Loader 或全局 Loader
            var targetLoader = loader ?? ESResMaster.GlobalResLoader;
            
            bool success = false;
            T tempAsset = default(T);
            
            targetLoader.AddAsset2LoadByGUIDSourcer(_guid, (b, source) =>
            {
                if (b && source.Asset is T t)
                {
                    tempAsset = t;
                    success = true;
                }
            });
            
            targetLoader.LoadAll_Sync();
            
            asset = tempAsset;
            
            if (!success)
            {
                UnityEngine.Debug.LogError($"[ESResRefer] 同步加载失败: GUID={_guid}");
            }
            
            return success;
        }

        /// <summary>
        /// 同步加载 - 使用全局 Loader
        /// </summary>
        public bool LoadSync(out T asset)
        {
            return LoadSync(null, out asset);
        }

        /// <summary>
        /// 获取已加载的资源（从 ES 系统查询）
        /// 注意：此方法不会触发加载，仅返回已加载的资源
        /// </summary>
        public T GetLoadedAsset()
        {
            if (!IsValid) return default(T);

            if (ESResMaster.GlobalAssetKeys.TryGetESResKeyByGUID(_guid, out var key))
            {
                var source = ESResMaster.ResTable.GetAssetResByKey(key);
                if (source != null && source.State == ResSourceState.Ready)
                {
                    return source.Asset as T;
                }
            }

            return default(T);
        }

        #endregion

        #region 便捷方法

        /// <summary>
        /// 实例化GameObject（仅用于GameObject类型）
        /// </summary>
        public void InstantiateAsync(Action<GameObject> onComplete, Transform parent = null, ESResLoader loader = null)
        {
            if (typeof(T) != typeof(GameObject))
            {
                UnityEngine.Debug.LogError("[ESResRefer] InstantiateAsync只能用于GameObject类型");
                onComplete?.Invoke(null);
                return;
            }

            LoadAsync(loader, (success, asset) =>
            {
                if (success && asset != null)
                {
                    var go = UnityEngine.Object.Instantiate(asset as GameObject, parent);
                    onComplete?.Invoke(go);
                }
                else
                {
                    onComplete?.Invoke(null);
                }
            });
        }

        #endregion

        #region 编辑器自动收集
        
#if UNITY_EDITOR
        /// <summary>
        /// 检测资产收集状态并提示
        /// 集成 ESGlobalResToolsSupportConfig 的自动收集功能
        /// </summary>
        /// <param name="asset">资产对象</param>
        private void TryAutoCollectAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            
            try
            {
                // 调用全局配置的收集方法
                var collectedLibrary = ESGlobalResToolsSupportConfig.CollectAssetToRecommendedLibrary(
                    asset, 
                    showConfirmDialog: true,  // 拖入时弹窗确认
                    silent: false             // 输出日志
                );
                
                // CollectAssetToRecommendedLibrary 已经处理所有逻辑：
                // - 类型判断
                // - 去重检查
                // - 优先级查找
                // - 弹窗确认
                // - 实际收集
                // - 日志输出
            }
            catch (System.Exception ex)
            {
                // 仅在出现异常时输出错误
                UnityEngine.Debug.LogError($"[ESResRefer] 资产收集状态检测失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
#endif

        #endregion

        #region 兼容旧版API

        [Obsolete("请使用 LoadAsync(loader, callback) 替代")]
        public virtual void TryLoadByLoaderASync(ESResLoader loader, Action<T> Use, bool atLastOrFirst = true)
        {
            LoadAsync(loader, (success, asset) =>
            {
                if (success) Use?.Invoke(asset);
            });
        }

        [Obsolete("请使用 LoadSync(loader, out asset) 替代")]
        public virtual bool TryLoadByLoaderSync(ESResLoader loader, out T tuse, bool atLastOrFirst = true)
        {
            return LoadSync(loader, out tuse);
        }

        #endregion
    }

    #endregion

    #region 预定义类型 - 开箱即用

    /// <summary>
    /// 通用资源引用
    /// </summary>
    [Serializable]
    public class ESResRefer : ESResRefer<UnityEngine.Object>
    {
    }

    /// <summary>
    /// 预制体资源引用
    /// </summary>
    [Serializable]
    public class ESResReferPrefab : ESResRefer<GameObject>
    {
    }

    /// <summary>
    /// 音频资源引用
    /// </summary>
    [Serializable]
    public class ESResReferAudioClip : ESResRefer<AudioClip>
    {
        /// <summary>
        /// 播放音频（便捷方法）
        /// </summary>
        public void Play(AudioSource source, ESResLoader loader = null, Action onComplete = null)
        {
            LoadAsync(loader, (success, clip) =>
            {
                if (success && source != null)
                {
                    source.clip = clip;
                    source.Play();
                }
                onComplete?.Invoke();
            });
        }
    }

    /// <summary>
    /// 材质资源引用
    /// </summary>
    [Serializable]
    public class ESResReferMat : ESResRefer<Material>
    {
    }

    /// <summary>
    /// Sprite资源引用
    /// </summary>
    [Serializable]
    public class ESResReferSprite : ESResRefer<Sprite>
    {
        /// <summary>
        /// 应用到Image组件（便捷方法）
        /// </summary>
        public void ApplyToImage(UnityEngine.UI.Image image, ESResLoader loader = null, Action onComplete = null)
        {
            LoadAsync(loader, (success, sprite) =>
            {
                if (success && image != null)
                {
                    image.sprite = sprite;
                }
                onComplete?.Invoke();
            });
        }
    }

    /// <summary>
    /// 2D贴图资源引用
    /// </summary>
    [Serializable]
    public class ESResReferTexture2D : ESResRefer<Texture2D>
    {
    }

    /// <summary>
    /// 贴图资源引用
    /// </summary>
    [Serializable]
    public class ESResReferTexture : ESResRefer<Texture>
    {
    }

    /// <summary>
    /// ScriptableObject资源引用
    /// </summary>
    [Serializable]
    public class ESResReferScriptableObject : ESResRefer<ScriptableObject>
    {
    }

    /// <summary>
    /// 动画剪辑资源引用
    /// </summary>
    [Serializable]
    public class ESResReferAnimationClip : ESResRefer<AnimationClip>
    {
    }

    /// <summary>
    /// 动画控制器资源引用
    /// </summary>
    [Serializable]
    public class ESResReferAnimatorController : ESResRefer<RuntimeAnimatorController>
    {
    }

    /// <summary>
    /// Shader资源引用
    /// </summary>
    [Serializable]
    public class ESResReferShader : ESResRefer<Shader>
    {
    }

    /// <summary>
    /// 字体资源引用
    /// </summary>
    [Serializable]
    public class ESResReferFont : ESResRefer<Font>
    {
    }

    /// <summary>
    /// 视频剪辑资源引用
    /// </summary>
    [Serializable]
    public class ESResReferVideoClip : ESResRefer<UnityEngine.Video.VideoClip>
    {
    }

    /// <summary>
    /// TextAsset资源引用（文本、JSON、XML等）
    /// </summary>
    [Serializable]
    public class ESResReferTextAsset : ESResRefer<TextAsset>
    {
    }

    /// <summary>
    /// Mesh资源引用
    /// </summary>
    [Serializable]
    public class ESResReferMesh : ESResRefer<Mesh>
    {
    }

    #endregion

    #region 编辑器自定义绘制

#if UNITY_EDITOR
    /// <summary>
    /// ESResRefer 的 Odin 自定义绘制器
    /// 添加特殊符号 @ 表明这是 ES 资源系统的便捷引用工具
    /// </summary>
    public class ESResReferDrawer : OdinValueDrawer<ESResReferABBase>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = this.ValueEntry.SmartValue;
            
            if (value == null)
            {
                EditorGUILayout.HelpBox("资源引用为 null", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            
            // 添加特殊图标和前缀 @ 表明这是便捷引用工具
            var customLabel = new GUIContent($"@ {label.text}", EditorIcons.StarPointer.Raw, "ES资源系统便捷引用");
            EditorGUILayout.LabelField(customLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 20));
            
            // 绘制对象字段
            value.Draw();
            
            // 快速定位按钮
            if (!string.IsNullOrEmpty(value.GUID))
            {
                if (GUILayout.Button(EditorIcons.ArrowRight.Raw, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    var asset = ESStandUtility.SafeEditor.LoadAssetByGUIDString(value.GUID);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
#endif

    #endregion
}
