using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
#if UNITY_EDITOR
    /// <summary>由 ES_Editor 注册具体窗口导航，避免 ES_Stand 反向依赖编辑器程序集。</summary>
    public static class ESAssetReferEditorBridge
    {
        public static Action<ESAssetPage> OpenRegistryPage;
    }
#endif

    public enum ESAssetReferInputMode
    {
        Asset = 0,
        Key = 1
    }

    #region 抽象基类
    
    /// <summary>
    /// ESAssetRefer 抽象基类 - ES 资源系统的便捷引用工具
    /// 
    /// 这是业务层的统一类型安全引用，依赖 AssetTable 与 IESAssetRuntimeProvider 完成加载和持有管理。
    /// 不直接接触下载、BundleKey、Manifest 或底层引用计数。
    /// 
    /// 设计原则：
    /// 1. 不创建临时 Loader，使用传入的 Loader 或全局 Loader  
    /// 2. 不管理引用计数，交由 ESResSource 管理
    /// 3. 作为辅助工具提供便捷的编辑器体验和加载接口
    /// </summary>
    [Serializable]
    public abstract class ESAssetReferBase
    {
#if UNITY_EDITOR
        [NonSerialized] private Action<string> _editorBeginChange;
        [NonSerialized] private Action _editorCommitChange;
#endif
        [SerializeField, HideInInspector]
        protected string _guid = "";

        [SerializeField, HideInInspector]
        protected long _localFileId;

        [SerializeField, HideInInspector]
        protected ESAssetReferKind _assetKind;

        [SerializeField, HideInInspector]
        protected int _resolvedEnumKey;

        [SerializeField, HideInInspector]
        protected string _resolvedStringKey = "";

        [SerializeField, HideInInspector]
        protected ESAssetReferInputMode _inputMode;

        public string GUID => _guid;
        public long LocalFileId => _localFileId;
        public bool IsSubAsset => _localFileId != 0;
        public ESAssetIdentity AssetIdentity => new ESAssetIdentity(_guid, _localFileId);
        public ESAssetReferKind AssetKind => _assetKind;
        public int ResolvedEnumKey => _resolvedEnumKey;
        public string ResolvedStringKey => _resolvedStringKey;
        public ESAssetReferInputMode InputMode => _inputMode;
        public bool HasResolvedAssetTableKey => _assetKind != ESAssetReferKind.None && (_resolvedEnumKey != 0 || !string.IsNullOrEmpty(_resolvedStringKey));
        public abstract Type AssetBaseType { get; }
        public bool IsValid => !string.IsNullOrEmpty(_guid) && _localFileId >= 0;
        public abstract void Release();
        public abstract bool SupportsGameCorePreload { get; }
        public abstract UniTask PreloadAsync(CancellationToken cancellationToken = default);

#if UNITY_EDITOR
        internal void ConfigureEditorPersistence(Action<string> beginChange, Action commitChange)
        {
            _editorBeginChange = beginChange;
            _editorCommitChange = commitChange;
        }

        protected void BeginEditorChange(string actionName) => _editorBeginChange?.Invoke(actionName);
        protected void CommitEditorChange() => _editorCommitChange?.Invoke();
#endif

        /// <summary>供不关心具体 T 的扫描、预加载与诊断系统读取当前已解析的业务键。</summary>
        public bool TryGetResolvedAssetTableKey(out ESAssetReferKind kind, out int enumKey, out string stringKey)
        {
            kind = _assetKind;
            enumKey = _resolvedEnumKey;
            stringKey = _resolvedStringKey;
            return HasResolvedAssetTableKey;
        }

        protected void SetAssetIdentity(string guid, long localFileId)
        {
            _guid = guid ?? string.Empty;
            _localFileId = localFileId;
        }

        protected void SetResolvedAssetTableKey(ESAssetReferKind kind, int enumKey, string stringKey)
        {
            _assetKind = kind;
            _resolvedEnumKey = enumKey;
            _resolvedStringKey = stringKey ?? string.Empty;
        }

        protected void ClearResolvedAssetTableKey(ESAssetReferKind fallbackKind)
        {
            _assetKind = fallbackKind;
            _resolvedEnumKey = 0;
            _resolvedStringKey = string.Empty;
        }

        protected void SetInputMode(ESAssetReferInputMode inputMode) => _inputMode = inputMode;

        /// <summary>仅供资源烘焙生成启动预热清单，不用于业务运行时修改引用。</summary>
        public void InitializeGeneratedReference(string guid, long localFileId, ESAssetReferKind kind, int enumKey, string stringKey)
        {
            SetAssetIdentity(guid, localFileId);
            SetResolvedAssetTableKey(kind, enumKey, stringKey);
            SetInputMode(ESAssetReferInputMode.Key);
        }

        public abstract void Draw();
        
        /// <summary>
        /// 验证资源是否存在
        /// </summary>
        public bool Validate()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_guid))
                return false;
            if (_localFileId == 0)
                return ESStandUtility.SafeEditor.LoadAssetByGUIDString(_guid) != null;

            string path = AssetDatabase.GUIDToAssetPath(_guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                    && string.Equals(guid, _guid, StringComparison.Ordinal) && localFileId == _localFileId)
                    return true;
            return false;
#else
            return !string.IsNullOrEmpty(_guid);
#endif
        }
    }

    #endregion

    #region 核心泛型实现

    /// <summary>
    /// ESAssetRefer 泛型实现
    /// 提供类型安全的资源引用，依赖 ES 资源系统完成加载
    /// </summary>
    [Serializable]
    public abstract class ESAssetRefer<T> : ESAssetReferBase where T : UnityEngine.Object
    {
        [NonSerialized] private IDisposable runtimeHandle;
        #region 编辑器支持
        
#if UNITY_EDITOR
        [NonSerialized]
        private bool _editorValidationDone;
        [NonSerialized]
        private bool _editorShowRegistryDetails;
        
        [NonSerialized]
        private UnityEngine.Object _editorAsset;

        private static readonly string[] InputModeLabels = { "资产", "Key" };
#endif

        public override Type AssetBaseType => typeof(T);
        public override bool SupportsGameCorePreload => true;

        /// <summary>
        /// 在 Inspector 中绘制 - 添加特殊符号 @ 表明这是便捷引用工具
        /// </summary>
        [OnInspectorGUI]
        public override void Draw()
        {
#if UNITY_EDITOR
            // 每个 Refer 实例在 Inspector 载入后只验证一次；不要在每帧 Repaint 中反复查 AssetDatabase。
            if (!_editorValidationDone)
            {
                _editorAsset = ResolveEditorAsset();
                if (_editorAsset is T)
                    TryResolveCollectedKey(_editorAsset);
                else if (_editorAsset != null)
                    UnityEngine.Debug.LogWarning($"[ESAssetRefer] 已保存的资产类型不匹配：期望 {typeof(T).Name}，实际 {_editorAsset.GetType().Name}。", _editorAsset);
                _editorValidationDone = true;
            }

            ESAssetReferInputMode newMode = (ESAssetReferInputMode)GUILayout.Toolbar((int)_inputMode, InputModeLabels, EditorStyles.miniButton);
            if (newMode != _inputMode)
            {
                BeginEditorChange("切换 ESAssetRefer 输入模式");
                SetInputMode(newMode);
                CommitEditorChange();
            }

            if (_inputMode == ESAssetReferInputMode.Asset)
                DrawAssetInput();
            else
                DrawKeyInput();

            DrawIdentitySummary();
            DrawRegistryPanel();
#endif
        }

#if UNITY_EDITOR
        private UnityEngine.Object ResolveEditorAsset()
        {
            return ResolveEditorAsset(_guid, _localFileId);
        }

        private static UnityEngine.Object ResolveEditorAsset(string targetGuid, long targetLocalFileId)
        {
            if (string.IsNullOrEmpty(targetGuid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(targetGuid);
            if (string.IsNullOrEmpty(path)) return null;
            if (targetLocalFileId == 0) return AssetDatabase.LoadMainAssetAtPath(path);
            foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath(path))
                if (candidate != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string candidateGuid, out long candidateLocalFileId)
                    && string.Equals(candidateGuid, targetGuid, StringComparison.Ordinal) && candidateLocalFileId == targetLocalFileId)
                    return candidate;
            return null;
        }

        private void SetEditorAssetIdentity(UnityEngine.Object asset)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId))
            {
                SetAssetIdentity(string.Empty, 0);
                return;
            }
            SetAssetIdentity(guid, AssetDatabase.IsSubAsset(asset) ? localFileId : 0);
        }

        private void TryResolveCollectedKey(UnityEngine.Object asset)
        {
            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            if (!IsAssetTableSupportedKind(kind))
            {
                ClearResolvedAssetTableKey(kind);
                return;
            }
            if (ESAssetRegistry.TryGetByAssetIdentity(kind, _guid, _localFileId, out ESAssetPage page))
            {
                SetResolvedAssetTableKey(page.Kind, page.EnumKey, page.EffectiveStringKey);
                return;
            }
            ClearResolvedAssetTableKey(kind);
        }

        // ES_Stand 不能反向依赖 ES_Logic；这里仅声明 Stand 侧可通过桥接 AssetTable 处理的分类。
        private static bool IsAssetTableSupportedKind(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab:
                case ESAssetReferKind.Sprite:
                case ESAssetReferKind.AudioClip:
                case ESAssetReferKind.AnimationClip:
                case ESAssetReferKind.AnimatorController:
                case ESAssetReferKind.Material:
                case ESAssetReferKind.Mesh:
                case ESAssetReferKind.Texture:
                case ESAssetReferKind.Texture2D:
                case ESAssetReferKind.SpriteAtlas:
                case ESAssetReferKind.Avatar:
                case ESAssetReferKind.PlayableAsset:
                case ESAssetReferKind.TimelineAsset:
                case ESAssetReferKind.VideoClip:
                case ESAssetReferKind.TerrainData:
                case ESAssetReferKind.ScriptableObject:
                    return true;
                default:
                    return false;
            }
        }

        private void DrawAssetInput()
        {
            UnityEngine.Object newAsset = EditorGUILayout.ObjectField(_editorAsset, typeof(T), false);
            if (newAsset == _editorAsset)
                return;

            if (newAsset != null && !(newAsset is T))
            {
                UnityEngine.Debug.LogWarning($"[ESAssetRefer] 资产类型不匹配：需要 {typeof(T).Name}，但拖入的是 {newAsset.GetType().Name}");
                return;
            }

            _editorAsset = newAsset;
            BeginEditorChange("修改 ESAssetRefer 资产");
            if (newAsset != null)
            {
                SetEditorAssetIdentity(newAsset);
                TryAutoCollectAsset(newAsset);
                TryResolveCollectedKey(newAsset);
            }
            else
            {
                SetAssetIdentity(string.Empty, 0);
                ClearResolvedAssetTableKey(ESAssetReferKind.None);
            }

            _editorValidationDone = true;
            CommitEditorChange();
        }

        private void DrawKeyInput()
        {
            ESAssetReferKind kind = GetKeyPickerKind();
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
            {
                EditorGUILayout.HelpBox("此引用没有专属 AssetTable 分类，请使用资产模式并由 GUID 精确加载。", MessageType.Info);
                return;
            }

            ESAssetReferKeyCache cache = ESAssetReferKeyCache.Get(kind);
            if (cache.Count == 0)
            {
                EditorGUILayout.HelpBox("没有已注入的收集资产。请先收集并 Bake / 刷新 Library。", MessageType.Warning);
                return;
            }

            ESAssetPage current = null;
            string currentLabel = null;
            for (int i = 0; i < cache.Count; i++)
            {
                ESAssetPage page = cache.Pages[i];
                if (page != null && page.EnumKey == _resolvedEnumKey && string.Equals(page.EffectiveStringKey, _resolvedStringKey, StringComparison.Ordinal))
                {
                    current = page;
                    currentLabel = cache.SelectionLabels[i];
                    break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("ConfigKey");
            if (GUILayout.Button(current == null ? "搜索并选择 Key..." : currentLabel,
                    EditorStyles.popup))
            {
                ESAssetReferKeyPickerWindow.Open(kind, current, ApplyKeyPage);
            }
            EditorGUILayout.EndHorizontal();
        }

        private ESAssetReferKind GetKeyPickerKind()
        {
            if (_assetKind != ESAssetReferKind.None && _assetKind != ESAssetReferKind.Other)
                return _assetKind;

            Type type = typeof(T);
            if (type == typeof(GameObject)) return ESAssetReferKind.Prefab;
            if (type == typeof(Sprite)) return ESAssetReferKind.Sprite;
            if (type == typeof(AudioClip)) return ESAssetReferKind.AudioClip;
            if (type == typeof(AnimationClip)) return ESAssetReferKind.AnimationClip;
            if (type == typeof(RuntimeAnimatorController)) return ESAssetReferKind.AnimatorController;
            if (type == typeof(Material)) return ESAssetReferKind.Material;
            if (type == typeof(Mesh)) return ESAssetReferKind.Mesh;
            if (type == typeof(Texture)) return ESAssetReferKind.Texture;
            if (type == typeof(Texture2D)) return ESAssetReferKind.Texture2D;
            if (type == typeof(UnityEngine.U2D.SpriteAtlas)) return ESAssetReferKind.SpriteAtlas;
            if (type == typeof(Avatar)) return ESAssetReferKind.Avatar;
            if (type == typeof(UnityEngine.Playables.PlayableAsset)) return ESAssetReferKind.PlayableAsset;
            if (type == typeof(UnityEngine.Timeline.TimelineAsset)) return ESAssetReferKind.TimelineAsset;
            if (type == typeof(UnityEngine.Video.VideoClip)) return ESAssetReferKind.VideoClip;
            if (type == typeof(TerrainData)) return ESAssetReferKind.TerrainData;
            if (type == typeof(ScriptableObject)) return ESAssetReferKind.ScriptableObject;
            return ESAssetReferKind.None;
        }

        private void ApplyKeyPage(ESAssetPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.AssetGuid))
                return;

            UnityEngine.Object resolvedAsset = ResolveEditorAsset(page.AssetGuid, page.LocalFileId);
            if (resolvedAsset == null || !(resolvedAsset is T))
            {
                UnityEngine.Debug.LogWarning($"[ESAssetRefer] ConfigKey 对应资产类型不匹配：期望 {typeof(T).Name}，实际 {(resolvedAsset == null ? "<missing>" : resolvedAsset.GetType().Name)}。");
                return;
            }

            BeginEditorChange("选择 ESAssetRefer Key");
            SetResolvedAssetTableKey(page.Kind, page.EnumKey, page.EffectiveStringKey);
            SetAssetIdentity(page.AssetGuid, page.LocalFileId);
            _editorAsset = resolvedAsset;
            _editorValidationDone = true;
            CommitEditorChange();
        }

        private void DrawIdentitySummary()
        {
            string source = HasResolvedAssetTableKey ? "AssetTable" : "GUID Fallback";
            EditorGUILayout.LabelField(source, HasResolvedAssetTableKey ? _resolvedStringKey : "未解析业务 Key", EditorStyles.miniLabel);
        }

        private void DrawRegistryPanel()
        {
            ESAssetReferKind kind = GetKeyPickerKind();
            if (_editorAsset is MonoScript || !IsAssetTableSupportedKind(kind) || string.IsNullOrEmpty(_guid))
                return;

            bool registered = ESAssetRegistry.TryGetByAssetIdentity(kind, _guid, _localFileId, out ESAssetPage page);
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = _localFileId == 0 ? new Color(0.25f, 0.52f, 0.72f, 1f) : new Color(0.55f, 0.35f, 0.72f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = oldColor;

            string registrySummary = registered
                ? "注册信息 · " + page.SourceBook + " / " + page.Name
                : "注册信息 · 未注册";
            _editorShowRegistryDetails = EditorGUILayout.Foldout(
                _editorShowRegistryDetails,
                registrySummary,
                true,
                EditorStyles.foldoutHeader);
            if (!_editorShowRegistryDetails)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(_localFileId == 0 ? "资产身份" : "子资产身份", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("类型", _editorAsset != null ? _editorAsset.GetType().Name : typeof(T).Name);
            EditorGUILayout.LabelField("GUID", _guid);
            if (_localFileId != 0)
                EditorGUILayout.LabelField("Local File ID", _localFileId.ToString());

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_localFileId == 0 ? "定位资产" : "准确定位子资产", EditorStyles.miniButtonLeft))
                PingExactAsset();
            if (GUILayout.Button("复制完整身份", EditorStyles.miniButtonRight))
                EditorGUIUtility.systemCopyBuffer = _guid + ":" + _localFileId;
            EditorGUILayout.EndHorizontal();

            if (!registered)
            {
                EditorGUILayout.HelpBox("该资产尚未注册到对应类型的 Library。", MessageType.Info);
                if (GUILayout.Button("注册当前资产"))
                    RegisterCurrentAsset();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("注册 Page", page.SourceBook + " / " + page.Name, EditorStyles.miniBoldLabel);
            string stringKey = EditorGUILayout.DelayedTextField("String Key", page.StringKey ?? string.Empty);
            if (!string.Equals(stringKey, page.StringKey, StringComparison.Ordinal))
                RenameStringKey(page, stringKey);

            Type enumType = ESAssetReferEnumTypeCache.Get(kind);
            if (enumType != null)
            {
                Enum selected = EditorGUILayout.EnumPopup("Enum Key", (Enum)Enum.ToObject(enumType, page.EnumKey));
                int enumKey = Convert.ToInt32(selected);
                if (enumKey != page.EnumKey)
                    RenameEnumKey(page, enumKey);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位 Page", EditorStyles.miniButtonLeft))
            {
                if (ESAssetReferEditorBridge.OpenRegistryPage != null)
                    ESAssetReferEditorBridge.OpenRegistryPage(page);
                else
                    EditorApplication.ExecuteMenuItem(MenuItemPathDefine.RESOURCE_WINDOW_PATH);
            }
            if (GUILayout.Button("定位 Library", EditorStyles.miniButtonMid))
                PingSourceLibrary(page);
            using (new EditorGUI.DisabledScope(enumType == null))
            {
                if (GUILayout.Button("定位枚举", EditorStyles.miniButtonMid))
                    OpenEnumMember(enumType, page.EnumKey);
                if (GUILayout.Button("枚举扩容", EditorStyles.miniButtonRight))
                    ESEnumScriptJump.OpenEnumAppendPosition(enumType);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void PingExactAsset()
        {
            UnityEngine.Object asset = ResolveEditorAsset(_guid, _localFileId);
            if (asset == null)
            {
                Debug.LogWarning("[ESRes][Inspector] 无法解析资产身份：" + _guid + ":" + _localFileId);
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void RegisterCurrentAsset()
        {
            if (_editorAsset == null)
                return;
            ESAssetLibrary library = ESGlobalResToolsSupportConfig.CollectAssetToRecommendedLibrary(
                _editorAsset, showConfirmDialog: false, silent: false);
            if (library == null)
            {
                Debug.LogWarning("[ESRes][Register] 当前资产没有匹配的 Library 收集规则。", _editorAsset);
                return;
            }
            library.InjectToAssetRegistryEditor();
            BeginEditorChange("注册并同步 ESAssetRefer");
            TryResolveCollectedKey(_editorAsset);
            CommitEditorChange();
        }

        private void RenameStringKey(ESAssetPage page, string newKey)
        {
            newKey = newKey == null ? string.Empty : newKey.Trim();
            if (string.IsNullOrEmpty(newKey))
            {
                Debug.LogWarning("[ESRes][Register] String Key 不能为空。", _editorAsset);
                return;
            }
            ESAssetLibrary library = RecordLibraryUndo(page, "修改资源 String Key");
            BeginEditorChange("同步 ESAssetRefer String Key");
            if (ESAssetRegistry.RenameStringKey(page, newKey))
            {
                SetResolvedAssetTableKey(page.Kind, page.EnumKey, page.EffectiveStringKey);
                PersistLibraryAndRefer(library);
            }
        }

        private void RenameEnumKey(ESAssetPage page, int newKey)
        {
            ESAssetLibrary library = RecordLibraryUndo(page, "修改资源 Enum Key");
            BeginEditorChange("同步 ESAssetRefer Enum Key");
            if (ESAssetRegistry.RenameEnumKey(page, newKey))
            {
                SetResolvedAssetTableKey(page.Kind, page.EnumKey, page.EffectiveStringKey);
                PersistLibraryAndRefer(library);
            }
        }

        private void PersistLibraryAndRefer(ESAssetLibrary library)
        {
            if (library != null)
                EditorUtility.SetDirty(library);
            CommitEditorChange();
            AssetDatabase.SaveAssets();
        }

        private static ESAssetLibrary RecordLibraryUndo(ESAssetPage page, string action)
        {
            if (page == null || string.IsNullOrEmpty(page.SourceLibrary))
                return null;
            string path = AssetDatabase.GUIDToAssetPath(page.SourceLibrary);
            ESAssetLibrary library = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(path);
            if (library != null)
                Undo.RecordObject(library, action);
            return library;
        }

        private static void PingSourceLibrary(ESAssetPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.SourceLibrary))
                return;
            string path = AssetDatabase.GUIDToAssetPath(page.SourceLibrary);
            ESAssetLibrary library = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(path);
            if (library == null)
                return;
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }

        private static void OpenEnumMember(Type enumType, int enumKey)
        {
            string memberName = Enum.GetName(enumType, enumKey);
            if (string.IsNullOrEmpty(memberName))
                ESEnumScriptJump.OpenEnumAppendPosition(enumType);
            else
                ESEnumScriptJump.OpenEnumMember(enumType, memberName);
        }
#endif

        #endregion

        #region 新版 Provider API

        /// <summary>
        /// 新版唯一加载入口：主资源与子资源均通过 GUID + LocalFileId 直接寻址。
        /// 调用方持有返回的 Handle，并在不再使用时 Dispose。
        /// </summary>
        internal UniTask<ESRuntimeAssetHandle<T>> LoadWithProviderAsync(IESAssetRuntimeProvider provider, CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider), "[ESRes][Load] Runtime Provider 不能为空。");
            if (!IsValid) throw new InvalidOperationException("[ESRes][Load] ESAssetRefer 缺少有效 GUID/LocalFileId。");
            return IsSubAsset
                ? provider.LoadSubAssetAsync<T>(AssetIdentity, cancellationToken)
                : provider.LoadMainAssetAsync<T>(AssetIdentity, cancellationToken);
        }

        /// <summary>默认业务入口：Owner 销毁时自动释放，同一 Owner 内同一资产只持有一次。</summary>
        public UniTask<T> LoadAsync(Component owner, CancellationToken cancellationToken = default)
            => ESAssets.LoadAsync(this, owner, cancellationToken);

        public override async UniTask PreloadAsync(CancellationToken cancellationToken = default)
        {
            await LoadAsync(cancellationToken);
        }

        /// <summary>
        /// 新链同步入口：只返回已就绪缓存，绝不阻塞加载 AssetBundle 或网络请求。
        /// AssetTable 与 Provider 缓存均为 O(1) 查找；预热后的正常命中/未命中路径不产生托管分配。
        /// </summary>
        public bool TryLoad(out T asset)
        {
            IESAssetRuntimeProvider provider = ESAssets.RuntimeBackend;
            if (provider != null && IsValid)
                return provider.TryGetLoaded(AssetIdentity, out asset);

            asset = null;
            return false;
        }

        /// <summary>
        /// 常规业务入口：已解析业务键时优先通过类型化 AssetTable O(1) 获取；
        /// 未收集或表未就绪时，使用完整 GUID 身份回退到 Provider。
        /// </summary>
        internal UniTask<T> LoadAsync(IESAssetRuntimeProvider provider, CancellationToken cancellationToken = default)
        {
            if (runtimeHandle != null && TryLoad(out T ready))
                return UniTask.FromResult(ready);
            return LoadAndRetainAsync(provider, cancellationToken);
        }

        private async UniTask<T> LoadAndRetainAsync(IESAssetRuntimeProvider provider, CancellationToken cancellationToken)
        {
            Release();
            ESRuntimeAssetHandle<T> handle = await LoadWithProviderAsync(provider, cancellationToken);
            runtimeHandle = handle;
            return handle.Asset;
        }

        /// <summary>默认无显式持有入口：全局驻留至显式资源安全点，调用者不需要 Owner、Scope 或 Release。</summary>
        public UniTask<T> LoadAsync(CancellationToken cancellationToken = default)
            => ESAssets.LoadAsync(this, cancellationToken);

        public override void Release()
        {
            runtimeHandle?.Dispose();
            runtimeHandle = null;
        }

        internal bool TryGetReady(IESAssetRuntimeProvider provider, out T asset)
        {
            if (provider != null && IsValid) return provider.TryGetLoaded(AssetIdentity, out asset);
            asset = null;
            return false;
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
                UnityEngine.Debug.LogError($"[ESAssetRefer] 资产收集状态检测失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
#endif

        #endregion

    }

    #endregion

    #region 预定义类型 - 开箱即用

    /// <summary>
    /// 未建立专属业务分类的 Unity 对象引用。
    /// 它仍保存 GUID + LocalFileId 并通过 Provider 精确加载；不要在业务 SO 中使用泛型基类。
    /// </summary>
    [Serializable]
    public sealed class ESAssetReferUnityObject : ESAssetRefer<UnityEngine.Object>
    {
    }

    [Serializable]
    public sealed class ESAssetReferScriptableObject : ESAssetRefer<ScriptableObject>
    {
    }

    /// <summary>
    /// 预制体资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferPrefab : ESAssetRefer<GameObject>
    {
    }

    /// <summary>
    /// 音频资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferAudioClip : ESAssetRefer<AudioClip>
    {
        /// <summary>
        /// 加载后播放；Owner 销毁时自动结束资源持有。
        /// </summary>
        public async UniTask<AudioClip> PlayAsync(AudioSource source, Component owner, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            AudioClip clip = await LoadAsync(owner, cancellationToken);
            source.clip = clip;
            source.Play();
            return clip;
        }
    }

    /// <summary>
    /// 材质资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferMaterial : ESAssetRefer<Material>
    {
    }

    /// <summary>
    /// Sprite资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferSprite : ESAssetRefer<Sprite>
    {
        /// <summary>
        /// 已就绪热路径：O(1) 查询，不创建闭包、Task、Loader 或临时集合。
        /// </summary>
        public bool TryApplyToImage(UnityEngine.UI.Image image)
        {
            if (image == null || !TryLoad(out Sprite sprite)) return false;
            image.sprite = sprite;
            return true;
        }

        /// <summary>
        /// 新版异步便捷入口。主 Sprite 与切图子 Sprite 使用同一调用方式；Owner 销毁时自动结束持有。
        /// </summary>
        public async UniTask<Sprite> ApplyToImageAsync(UnityEngine.UI.Image image, Component owner, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Sprite sprite = await LoadAsync(owner, cancellationToken);
            image.sprite = sprite;
            return sprite;
        }

        /// <summary>全局驻留版本；适合不会随单个 Owner 销毁的常驻 UI。</summary>
        public async UniTask<Sprite> ApplyToImageAsync(UnityEngine.UI.Image image, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            Sprite sprite = await LoadAsync(cancellationToken);
            image.sprite = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// 2D贴图资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferTexture2D : ESAssetRefer<Texture2D>
    {
    }

    /// <summary>
    /// 贴图资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferTexture : ESAssetRefer<Texture>
    {
    }

    /// <summary>
    /// 动画剪辑资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferAnimationClip : ESAssetRefer<AnimationClip>
    {
    }

    /// <summary>
    /// 动画控制器资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferAnimatorController : ESAssetRefer<RuntimeAnimatorController>
    {
    }

    /// <summary>
    /// SpriteAtlas资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferSpriteAtlas : ESAssetRefer<UnityEngine.U2D.SpriteAtlas>
    {
    }

    /// <summary>
    /// ES_Logic 对类型化 AssetTable 的桥接。Stand 只定义协议，避免反向程序集依赖。
    /// </summary>
    /// <summary>
    /// Avatar资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferAvatar : ESAssetRefer<Avatar>
    {
    }

    /// <summary>
    /// 视频剪辑资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferVideoClip : ESAssetRefer<UnityEngine.Video.VideoClip>
    {
    }

    /// <summary>
    /// Timeline资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferTimelineAsset : ESAssetRefer<UnityEngine.Timeline.TimelineAsset>
    {
    }

    /// <summary>
    /// PlayableAsset资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferPlayableAsset : ESAssetRefer<UnityEngine.Playables.PlayableAsset>
    {
    }

    /// <summary>
    /// Mesh资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferMesh : ESAssetRefer<Mesh>
    {
    }

    /// <summary>
    /// TerrainData资源引用
    /// </summary>
    [Serializable]
    public class ESAssetReferTerrainData : ESAssetRefer<TerrainData>
    {
    }

    /// <summary>
    /// 场景资产引用
    /// Scene 不是运行时 UnityEngine.Object 资产类型，因此单独保存 GUID 并按场景资源加载。
    /// </summary>
    [Serializable]
    public class ESAssetReferScene : ESAssetReferBase
    {
        [SerializeField, HideInInspector]
        private string _sceneName = "";

#if UNITY_EDITOR
        [NonSerialized] private bool _editorValidationDone;
        [NonSerialized] private UnityEngine.Object _editorScene;
        private static readonly string[] SceneInputModeLabels = { "资产", "Key" };
#endif

        public string SceneName => _sceneName;
        public override Type AssetBaseType => typeof(UnityEngine.Object);
        public override bool SupportsGameCorePreload => false;

        [OnInspectorGUI]
        public override void Draw()
        {
#if UNITY_EDITOR
            ESAssetReferInputMode newMode = (ESAssetReferInputMode)GUILayout.Toolbar((int)_inputMode, SceneInputModeLabels, EditorStyles.miniButton);
            if (newMode != _inputMode)
            {
                BeginEditorChange("切换场景引用输入模式");
                SetInputMode(newMode);
                CommitEditorChange();
            }
            if (_inputMode == ESAssetReferInputMode.Key)
            {
                DrawSceneKeyInput();
                return;
            }

            if (!_editorValidationDone)
            {
                _editorScene = string.IsNullOrEmpty(_guid) ? null : ESStandUtility.SafeEditor.LoadAssetByGUIDString(_guid);
                if (_editorScene is SceneAsset && ESAssetRegistry.TryGetByAssetIdentity(ESAssetReferKind.Scene, _guid, 0, out ESAssetPage currentPage))
                    SetResolvedAssetTableKey(currentPage.Kind, currentPage.EnumKey, currentPage.EffectiveStringKey);
                _editorValidationDone = true;
            }

            var newScene = EditorGUILayout.ObjectField(_editorScene, typeof(SceneAsset), false);
            if (newScene == _editorScene)
            {
                return;
            }

            if (newScene == null)
            {
                BeginEditorChange("清空场景引用");
                _guid = "";
                _sceneName = "";
                _editorScene = null;
                ClearResolvedAssetTableKey(ESAssetReferKind.None);
                CommitEditorChange();
                return;
            }

            BeginEditorChange("修改场景引用");
            _editorScene = newScene;
            SetAssetIdentity(ESStandUtility.SafeEditor.GetAssetGUID(newScene), 0);
            ESGlobalResToolsSupportConfig.CollectAssetToRecommendedLibrary(newScene, showConfirmDialog: true, silent: false);
            if (ESAssetRegistry.TryGetByAssetIdentity(ESAssetReferKind.Scene, _guid, 0, out ESAssetPage page))
                SetResolvedAssetTableKey(page.Kind, page.EnumKey, page.EffectiveStringKey);
            else
                ClearResolvedAssetTableKey(ESAssetReferKind.Scene);
            string path = AssetDatabase.GetAssetPath(newScene);
            _sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            _editorValidationDone = true;
            CommitEditorChange();
#endif
        }

#if UNITY_EDITOR
        private void DrawSceneKeyInput()
        {
            ESAssetReferKeyCache cache = ESAssetReferKeyCache.Get(ESAssetReferKind.Scene);
            if (cache.Count == 0)
            {
                EditorGUILayout.HelpBox("没有已注入的场景 ConfigKey。请先收集并 Bake / 刷新 Library。", MessageType.Warning);
                return;
            }

            ESAssetPage current = null;
            string currentLabel = null;
            for (int i = 0; i < cache.Count; i++)
            {
                ESAssetPage page = cache.Pages[i];
                if (page != null && page.EnumKey == _resolvedEnumKey && string.Equals(page.EffectiveStringKey, _resolvedStringKey, StringComparison.Ordinal))
                {
                    current = page;
                    currentLabel = cache.SelectionLabels[i];
                    break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Scene Key");
            if (GUILayout.Button(current == null ? "搜索并选择场景 Key..." : currentLabel, EditorStyles.popup))
                ESAssetReferKeyPickerWindow.Open(ESAssetReferKind.Scene, current, ApplyScenePage);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(HasResolvedAssetTableKey ? "AssetTable" : "GUID Fallback",
                HasResolvedAssetTableKey ? _resolvedStringKey : "未解析业务 Key", EditorStyles.miniLabel);
        }

        private void ApplyScenePage(ESAssetPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.AssetGuid))
                return;
            BeginEditorChange("选择场景引用 Key");
            SetResolvedAssetTableKey(ESAssetReferKind.Scene, page.EnumKey, page.EffectiveStringKey);
            SetAssetIdentity(page.AssetGuid, 0);
            _sceneName = string.IsNullOrEmpty(page.AssetPath) ? page.Name : System.IO.Path.GetFileNameWithoutExtension(page.AssetPath);
            _editorScene = ESStandUtility.SafeEditor.LoadAssetByGUIDString(page.AssetGuid);
            _editorValidationDone = true;
            CommitEditorChange();
        }
#endif

        internal UniTask<ESRuntimeSceneHandle> LoadWithProviderAsync(IESAssetRuntimeProvider provider, LoadSceneMode mode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!IsValid) throw new InvalidOperationException("ESAssetReferScene 缺少有效 GUID。");
            return provider.LoadSceneAsync(AssetIdentity, mode, cancellationToken);
        }

        /// <summary>高级场景租约入口；仅 Level/Map 场景服务可使用，普通业务不得持有 Scene Handle。</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UniTask<ESRuntimeSceneHandle> LoadAsync(LoadSceneMode mode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            IESAssetRuntimeProvider provider = ESAssets.RuntimeBackend;
            if (provider == null)
                return UniTask.FromException<ESRuntimeSceneHandle>(new InvalidOperationException("ESAssetReferScene 尚未接入 ESRuntimeDataAssetLoadingService。"));
            return LoadWithProviderAsync(provider, mode, cancellationToken);
        }

        // Scene 的卸载由调用方持有的 ESRuntimeSceneHandle 决定；此处保持统一 Refer 查询接口。
        public override void Release() { }

        public override UniTask PreloadAsync(CancellationToken cancellationToken = default)
            => UniTask.CompletedTask;

        public bool IsLoaded()
        {
            return !string.IsNullOrEmpty(_sceneName) && SceneManager.GetSceneByName(_sceneName).isLoaded;
        }
    }

    #endregion

    #region 编辑器自定义绘制

#if UNITY_EDITOR
    internal sealed class ESAssetReferKeyCache
    {
        private static readonly Dictionary<ESAssetReferKind, ESAssetReferKeyCache> ByKind = new Dictionary<ESAssetReferKind, ESAssetReferKeyCache>(24);
        private int version = -1;
        private ESAssetReferKind kind;
        private ESAssetPage[] pages = Array.Empty<ESAssetPage>();
        private string[] searchTexts = Array.Empty<string>();
        private string[] selectionLabels = Array.Empty<string>();
        private string[] detailLabels = Array.Empty<string>();

        public ESAssetPage[] Pages => pages;
        public string[] SearchTexts => searchTexts;
        public string[] SelectionLabels => selectionLabels;
        public string[] DetailLabels => detailLabels;
        public int Count => pages.Length;
        public ESAssetReferKind Kind => kind;

        public static ESAssetReferKeyCache Get(ESAssetReferKind kind)
        {
            if (!ByKind.TryGetValue(kind, out ESAssetReferKeyCache cache))
            {
                cache = new ESAssetReferKeyCache { kind = kind };
                ByKind.Add(kind, cache);
            }
            cache.RefreshIfNeeded();
            return cache;
        }

        private void RefreshIfNeeded()
        {
            if (version == ESAssetRegistry.Version)
                return;

            IReadOnlyList<ESAssetPage> source = ESAssetRegistry.GetPagesByKind(kind);
            int count = source?.Count ?? 0;
            pages = new ESAssetPage[count];
            searchTexts = new string[count];
            selectionLabels = new string[count];
            detailLabels = new string[count];
            for (int i = 0; i < count; i++)
            {
                ESAssetPage page = source[i];
                pages[i] = page;
                searchTexts[i] = page == null
                    ? string.Empty
                    : ((page.Name ?? string.Empty) + "\n" + (page.EffectiveStringKey ?? string.Empty) + "\n"
                       + page.EnumKey + "\n" + (page.SourceBook ?? string.Empty) + "\n" + (page.SourceLibrary ?? string.Empty)).ToLowerInvariant();
                selectionLabels[i] = page == null ? "<missing>" : page.Name + "  |  " + page.EffectiveStringKey;
                detailLabels[i] = page == null ? string.Empty : "String: " + page.EffectiveStringKey + "    Enum: " + page.EnumKey + "    Page: " + page.SourceBook;
            }
            version = ESAssetRegistry.Version;
        }
    }

    internal sealed class ESAssetReferKeyPickerWindow : EditorWindow
    {
        private const float RowHeight = 38f;
        private readonly List<int> filteredIndices = new List<int>(128);
        private ESAssetReferKeyCache cache;
        private Action<ESAssetPage> onSelected;
        private ESAssetPage current;
        private Vector2 scroll;
        private string search = string.Empty;
        private string appliedSearch;
        private int cacheVersion = -1;

        public static void Open(ESAssetReferKind kind, ESAssetPage current, Action<ESAssetPage> onSelected)
        {
            ESAssetReferKeyPickerWindow window = CreateInstance<ESAssetReferKeyPickerWindow>();
            window.titleContent = new GUIContent("选择 " + kind + " Key");
            window.minSize = new Vector2(480f, 340f);
            window.cache = ESAssetReferKeyCache.Get(kind);
            window.current = current;
            window.onSelected = onSelected;
            window.RebuildFilter();
            window.ShowAuxWindow();
            window.Focus();
        }

        private void OnGUI()
        {
            if (cache == null)
            {
                Close();
                return;
            }

            EditorGUILayout.Space(4);
            GUI.SetNextControlName("ESAssetReferKeySearch");
            string nextSearch = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
                search = nextSearch;
            if (!string.Equals(appliedSearch, search, StringComparison.Ordinal) || cacheVersion != ESAssetRegistry.Version)
            {
                cache = ESAssetReferKeyCache.Get(cache.Kind);
                RebuildFilter();
            }

            Rect viewport = EditorGUILayout.BeginVertical();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float totalHeight = filteredIndices.Count * RowHeight;
            Rect contentRect = GUILayoutUtility.GetRect(1f, totalHeight, GUILayout.ExpandWidth(true));
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int visible = Mathf.CeilToInt(position.height / RowHeight) + 2;
            int last = Mathf.Min(filteredIndices.Count, first + visible);
            for (int row = first; row < last; row++)
            {
                ESAssetPage page = cache.Pages[filteredIndices[row]];
                if (page == null)
                    continue;
                Rect rowRect = new Rect(contentRect.x, contentRect.y + row * RowHeight, contentRect.width, RowHeight - 2f);
                if (ReferenceEquals(page, current))
                    EditorGUI.DrawRect(rowRect, new Color(0.20f, 0.48f, 0.72f, 0.25f));
                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    onSelected?.Invoke(page);
                    Close();
                    GUIUtility.ExitGUI();
                }
                Rect nameRect = new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - 12f, 18f);
                Rect keyRect = new Rect(nameRect.x, nameRect.y + 17f, nameRect.width, 16f);
                EditorGUI.LabelField(nameRect, page.Name, EditorStyles.boldLabel);
                EditorGUI.LabelField(keyRect, cache.DetailLabels[filteredIndices[row]], EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                Close();
            if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                EditorGUI.FocusTextInControl("ESAssetReferKeySearch");
        }

        private void RebuildFilter()
        {
            filteredIndices.Clear();
            string term = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim().ToLowerInvariant();
            for (int i = 0; i < cache.Count; i++)
                if (term.Length == 0 || cache.SearchTexts[i].Contains(term))
                    filteredIndices.Add(i);
            appliedSearch = search;
            cacheVersion = ESAssetRegistry.Version;
        }
    }

    internal static class ESAssetReferEnumTypeCache
    {
        private static readonly Dictionary<ESAssetReferKind, Type> Types = Build();

        public static Type Get(ESAssetReferKind kind)
        {
            Types.TryGetValue(kind, out Type type);
            return type;
        }

        private static Dictionary<ESAssetReferKind, Type> Build()
        {
            Dictionary<ESAssetReferKind, Type> result = new Dictionary<ESAssetReferKind, Type>(20);
            TypeCache.TypeCollection types = TypeCache.GetTypesWithAttribute<ESEnumScriptAttribute>();
            foreach (Type type in types)
            {
                if (!type.IsEnum)
                    continue;
                string name = type.Name;
                foreach (ESAssetReferKind kind in Enum.GetValues(typeof(ESAssetReferKind)))
                    if (name == "ESAssetRefer" + kind + "EnumKey")
                        result[kind] = type;
            }
            return result;
        }
    }

    /// <summary>
    /// ESAssetRefer 的 Odin 自定义绘制器
    /// 添加特殊符号 @ 表明这是 ES 资源系统的便捷引用工具
    /// </summary>
    public class ESAssetReferDrawer : OdinValueDrawer<ESAssetReferBase>
    {
        private UnityEngine.Object[] changeTargets;
        private static GUIStyle atMarkStyle;
        private static GUIStyle assetKindStyle;
        private static GUIStyle stateStyle;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = this.ValueEntry.SmartValue;
            
            if (value == null)
            {
                EditorGUILayout.HelpBox("资源引用为 null", MessageType.Error);
                return;
            }

            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            // 金色 @ 表明这是 ES 资源引用；类型单独强调，隐藏外层 Label 后仍可一眼识别 Scene / Prefab 等类别。
            string state = value.HasResolvedAssetTableKey ? "Table" : "GUID";
            string assetKind = ResolveAssetKindDisplay(value);
            GUILayout.Label("@", AtMarkStyle, GUILayout.Width(18f));
            GUILayout.Label(assetKind, AssetKindStyle, GUILayout.MinWidth(48f), GUILayout.ExpandWidth(false));
            GUILayout.Label("[" + state + "]", StateStyle, GUILayout.ExpandWidth(true));
            
            // 快速定位按钮
            if (!string.IsNullOrEmpty(value.GUID))
            {
                if (GUILayout.Button("复制", EditorStyles.miniButtonLeft, GUILayout.Width(38)))
                    EditorGUIUtility.systemCopyBuffer = value.GUID + (value.LocalFileId == 0 ? string.Empty : ":" + value.LocalFileId);

                if (GUILayout.Button(EditorIcons.ArrowRight.Raw, EditorStyles.miniButtonRight, GUILayout.Width(24)))
                {
                    var asset = ResolveExactAsset(value.GUID, value.LocalFileId);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            value.ConfigureEditorPersistence(BeginChange, CommitChange);
            value.Draw();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel = previousIndentLevel;
        }

        private static GUIStyle AtMarkStyle
        {
            get
            {
                if (atMarkStyle == null)
                    atMarkStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15,
                        normal = { textColor = new Color(1f, 0.72f, 0.16f) }
                    };
                return atMarkStyle;
            }
        }

        private static GUIStyle AssetKindStyle
        {
            get
            {
                if (assetKindStyle == null)
                    assetKindStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = new Color(1f, 0.78f, 0.28f) }
                    };
                return assetKindStyle;
            }
        }

        private static GUIStyle StateStyle
        {
            get
            {
                if (stateStyle == null)
                    stateStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.68f, 0.72f, 0.78f) : new Color(0.34f, 0.38f, 0.44f) }
                    };
                return stateStyle;
            }
        }

        private static string ResolveAssetKindDisplay(ESAssetReferBase value)
        {
            if (value.AssetKind != ESAssetReferKind.None && value.AssetKind != ESAssetReferKind.Other)
                return value.AssetKind.ToString();

            string typeName = value.GetType().Name;
            const string prefix = "ESAssetRefer";
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
                typeName = typeName.Substring(prefix.Length);
            return string.IsNullOrEmpty(typeName) ? "Asset" : typeName;
        }

        private void BeginChange(string actionName)
        {
            if (changeTargets == null)
            {
                var weakTargets = Property.Tree.WeakTargets;
                List<UnityEngine.Object> targets = new List<UnityEngine.Object>(weakTargets.Count);
                for (int i = 0; i < weakTargets.Count; i++)
                    if (weakTargets[i] is UnityEngine.Object target)
                        targets.Add(target);
                changeTargets = targets.ToArray();
            }
            if (changeTargets.Length > 0)
                Undo.RecordObjects(changeTargets, actionName);
        }

        private void CommitChange()
        {
            ValueEntry.SmartValue = ValueEntry.SmartValue;
            if (changeTargets != null)
                for (int i = 0; i < changeTargets.Length; i++)
                    if (changeTargets[i] != null)
                        EditorUtility.SetDirty(changeTargets[i]);
            Property.Tree.ApplyChanges();
        }

        private static UnityEngine.Object ResolveExactAsset(string guid, long localFileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;
            if (localFileId == 0)
                return AssetDatabase.LoadMainAssetAtPath(path);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assets[i], out string candidateGuid, out long candidateId)
                    && candidateGuid == guid && candidateId == localFileId)
                    return assets[i];
            return null;
        }
    }
#endif

    #endregion
}
