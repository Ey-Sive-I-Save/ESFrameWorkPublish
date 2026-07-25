using Sirenix.OdinInspector;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    internal readonly struct ESAssetKindIntKey : IEquatable<ESAssetKindIntKey>
    {
        public readonly ESAssetReferKind Kind;
        public readonly int Value;

        public ESAssetKindIntKey(ESAssetReferKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public bool Equals(ESAssetKindIntKey other) => Kind == other.Kind && Value == other.Value;
        public override bool Equals(object obj) => obj is ESAssetKindIntKey other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Value;
    }

    internal readonly struct ESAssetKindStringKey : IEquatable<ESAssetKindStringKey>
    {
        public readonly ESAssetReferKind Kind;
        public readonly string Value;

        public ESAssetKindStringKey(ESAssetReferKind kind, string value)
        {
            Kind = kind;
            Value = value ?? string.Empty;
        }

        public bool Equals(ESAssetKindStringKey other) => Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ESAssetKindStringKey other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Value);
    }

    /// <summary>主资产以 GUID + 0 标识；独立子资产以 GUID + LocalFileId 标识。</summary>
    internal readonly struct ESAssetIdentityKey : IEquatable<ESAssetIdentityKey>
    {
        public readonly string Guid;
        public readonly long LocalFileId;

        public ESAssetIdentityKey(string guid, long localFileId)
        {
            Guid = guid ?? string.Empty;
            LocalFileId = localFileId;
        }

        public bool Equals(ESAssetIdentityKey other) => LocalFileId == other.LocalFileId && string.Equals(Guid, other.Guid, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ESAssetIdentityKey other && Equals(other);
        public override int GetHashCode() => (StringComparer.Ordinal.GetHashCode(Guid) * 397) ^ LocalFileId.GetHashCode();
    }

    public class ESAssetLibrary : LibrarySoBase<ESAssetBook>
    {
        [NonSerialized]
        private readonly Dictionary<ESAssetReferKind, List<ESAssetPage>> _pagesByKind = new Dictionary<ESAssetReferKind, List<ESAssetPage>>(32);

        [NonSerialized]
        private readonly Dictionary<ESAssetKindIntKey, ESAssetPage> _pageByEnumKey = new Dictionary<ESAssetKindIntKey, ESAssetPage>(256);

        [NonSerialized]
        private readonly Dictionary<ESAssetKindStringKey, ESAssetPage> _pageByStringKey = new Dictionary<ESAssetKindStringKey, ESAssetPage>(256);

        [NonSerialized]
        private readonly Dictionary<string, ESAssetPage> _pageByGuid = new Dictionary<string, ESAssetPage>(256);

        [NonSerialized]
        private bool _fastIndexDirty = true;

        protected override IEnumerable<ESAssetBook> GetDefaultBooks()
        {
            return _defaultBooks();
        }

        protected override void InitializeDefaultBooks()
        {
            base.InitializeDefaultBooks();

            SetupDefaultBook(DefaultPrefabBook, EditorIconType.Prefab, ESAssetCategory.Prefab);
            SetupDefaultBook(DefaultSceneBook, EditorIconType.Scene, ESAssetCategory.Scene);
            SetupDefaultBook(DefaultSpriteBook, EditorIconType.Sprite, ESAssetCategory.Sprite);
            SetupDefaultBook(DefaultTexture2DBook, EditorIconType.Texture, ESAssetCategory.Texture);
            SetupDefaultBook(DefaultTextureBook, EditorIconType.Texture, ESAssetCategory.Texture);
            SetupDefaultBook(DefaultSpriteAtlasBook, EditorIconType.Sprite, ESAssetCategory.SpriteAtlas);
            SetupDefaultBook(DefaultMaterialBook, EditorIconType.Material, ESAssetCategory.Material);
            SetupDefaultBook(DefaultMeshBook, EditorIconType.Model, ESAssetCategory.Mesh);
            SetupDefaultBook(DefaultAnimationClipBook, EditorIconType.Animation, ESAssetCategory.Animation);
            SetupDefaultBook(DefaultAnimatorControllerBook, EditorIconType.AnimatorController, ESAssetCategory.AnimatorController);
            SetupDefaultBook(DefaultAvatarBook, EditorIconType.Avatar, ESAssetCategory.Avatar);
            SetupDefaultBook(DefaultAudioClipBook, EditorIconType.AudioClip, ESAssetCategory.Audio);
            SetupDefaultBook(DefaultVideoClipBook, EditorIconType.VideoClip, ESAssetCategory.Video);
            SetupDefaultBook(DefaultTimelineAssetBook, EditorIconType.Animation, ESAssetCategory.Timeline);
            SetupDefaultBook(DefaultPlayableAssetBook, EditorIconType.File, ESAssetCategory.Playable);
            SetupDefaultBook(DefaultScriptableObjectBook, EditorIconType.ScriptableObject, ESAssetCategory.Script);
            SetupDefaultBook(DefaultTerrainDataBook, EditorIconType.Terrain, ESAssetCategory.TerrainData);
        }

        private static void SetupDefaultBook(ESAssetBook book, EditorIconType icon, ESAssetCategory category)
        {
            if (book == null)
                return;

            book._icon = icon;
            book.WritableDefaultMessageOnEditor = false;
            book.PreferredAssetCategory = category;
        }

        private IEnumerable<ESAssetBook> _defaultBooks()
        {
            if (DefaultPrefabBook != null) yield return DefaultPrefabBook;
            if (DefaultSceneBook != null) yield return DefaultSceneBook;
            if (DefaultSpriteBook != null) yield return DefaultSpriteBook;
            if (DefaultTexture2DBook != null) yield return DefaultTexture2DBook;
            if (DefaultTextureBook != null) yield return DefaultTextureBook;
            if (DefaultSpriteAtlasBook != null) yield return DefaultSpriteAtlasBook;
            if (DefaultMaterialBook != null) yield return DefaultMaterialBook;
            if (DefaultMeshBook != null) yield return DefaultMeshBook;
            if (DefaultAnimationClipBook != null) yield return DefaultAnimationClipBook;
            if (DefaultAnimatorControllerBook != null) yield return DefaultAnimatorControllerBook;
            if (DefaultAvatarBook != null) yield return DefaultAvatarBook;
            if (DefaultAudioClipBook != null) yield return DefaultAudioClipBook;
            if (DefaultVideoClipBook != null) yield return DefaultVideoClipBook;
            if (DefaultTimelineAssetBook != null) yield return DefaultTimelineAssetBook;
            if (DefaultPlayableAssetBook != null) yield return DefaultPlayableAssetBook;
            if (DefaultScriptableObjectBook != null) yield return DefaultScriptableObjectBook;
            if (DefaultTerrainDataBook != null) yield return DefaultTerrainDataBook;
        }

        [ShowInInspector, NonSerialized]
#pragma warning disable IDE0051
#pragma warning disable CS0414
        private bool ShowDefaultPrefabBook = false;
#pragma warning restore CS0414
#pragma warning restore IDE0051

        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultPrefabBook = new ESAssetBook() { Name = "Default Prefab Book", Desc = "ESAssetReferPrefab" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultSceneBook = new ESAssetBook() { Name = "Default Scene Book", Desc = "ESAssetReferScene" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultSpriteBook = new ESAssetBook() { Name = "Default Sprite Book", Desc = "ESAssetReferSprite" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultTexture2DBook = new ESAssetBook() { Name = "Default Texture2D Book", Desc = "ESAssetReferTexture2D" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultTextureBook = new ESAssetBook() { Name = "Default Texture Book", Desc = "ESAssetReferTexture" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultSpriteAtlasBook = new ESAssetBook() { Name = "Default SpriteAtlas Book", Desc = "ESAssetReferSpriteAtlas" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultMaterialBook = new ESAssetBook() { Name = "Default Material Book", Desc = "ESAssetReferMaterial" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultMeshBook = new ESAssetBook() { Name = "Default Mesh Book", Desc = "ESAssetReferMesh" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultAnimationClipBook = new ESAssetBook() { Name = "Default AnimationClip Book", Desc = "ESAssetReferAnimationClip" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultAnimatorControllerBook = new ESAssetBook() { Name = "Default AnimatorController Book", Desc = "ESAssetReferAnimatorController" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultAvatarBook = new ESAssetBook() { Name = "Default Avatar Book", Desc = "ESAssetReferAvatar" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultAudioClipBook = new ESAssetBook() { Name = "Default AudioClip Book", Desc = "ESAssetReferAudioClip" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultVideoClipBook = new ESAssetBook() { Name = "Default VideoClip Book", Desc = "ESAssetReferVideoClip" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultTimelineAssetBook = new ESAssetBook() { Name = "Default TimelineAsset Book", Desc = "ESAssetReferTimelineAsset" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultPlayableAssetBook = new ESAssetBook() { Name = "Default PlayableAsset Book", Desc = "ESAssetReferPlayableAsset" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultScriptableObjectBook = new ESAssetBook() { Name = "Default ScriptableObject Book", Desc = "ESAssetReferScriptableObject" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ESAssetBook DefaultTerrainDataBook = new ESAssetBook() { Name = "Default TerrainData Book", Desc = "ESAssetReferTerrainData" };

        [LabelText("Can Build")]
        public bool ContainsBuild = true;

        [ESBoolOption("允许热更新远端发布", "仅随包本地")]
        public bool IsNet = true;

        public override void OnEditorApply()
        {
            base.OnEditorApply();
            Refresh();
#if UNITY_EDITOR
            InjectToAssetRegistryEditor();
#endif
        }

#if UNITY_EDITOR
        private new void OnEnable()
        {
            EditorApplication.delayCall -= InjectToAssetRegistryEditor;
            EditorApplication.delayCall += InjectToAssetRegistryEditor;
        }

        private void OnValidate()
        {
            MarkFastIndexDirty();
            EditorApplication.delayCall -= InjectToAssetRegistryEditor;
            EditorApplication.delayCall += InjectToAssetRegistryEditor;
        }

        public void InjectToAssetRegistryEditor()
        {
            if (this == null)
                return;

            NormalizePagesEditor();
            ESAssetRegistry.BuildFromAssetLibrary(this);
        }
#endif

        public override void EditorOnly_DragAssetsToBooks(UnityEngine.Object[] assets)
        {
#if UNITY_EDITOR
            if (assets == null || assets.Length == 0)
                return;

            foreach (var asset in assets)
            {
                if (asset == null)
                    continue;

                var kind = ESAssetPage.DetermineKind(asset);
                var targetBook = GetDefaultBookByKind(kind);
                if (targetBook == null)
                {
                    Debug.LogWarning($"[ESAssetLibrary] Unsupported asset kind [{kind}] for [{asset.name}].");
                    continue;
                }

                targetBook.EditorOnly_DragAtArea(new[] { asset });
            }

            MarkFastIndexDirty();
            InjectToAssetRegistryEditor();
            EditorUtility.SetDirty(this);
#endif
        }

        public override void Refresh()
        {
            if (LibFolderName.IsNullOrWhitespace())
            {
                LibFolderName = IESLibrary.DefaultLibFolderName;
            }

            ESResMaster.TrySetResLibFolderName(this, LibFolderName, 0);
            MarkFastIndexDirty();
            base.Refresh();
        }

        public void MarkFastIndexDirty()
        {
            _fastIndexDirty = true;
        }

        public ESAssetBook GetDefaultBookByKind(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return DefaultPrefabBook;
                case ESAssetReferKind.Scene: return DefaultSceneBook;
                case ESAssetReferKind.Sprite: return DefaultSpriteBook;
                case ESAssetReferKind.Texture2D: return DefaultTexture2DBook;
                case ESAssetReferKind.Texture: return DefaultTextureBook;
                case ESAssetReferKind.SpriteAtlas: return DefaultSpriteAtlasBook;
                case ESAssetReferKind.Material: return DefaultMaterialBook;
                case ESAssetReferKind.Mesh: return DefaultMeshBook;
                case ESAssetReferKind.AnimationClip: return DefaultAnimationClipBook;
                case ESAssetReferKind.AnimatorController: return DefaultAnimatorControllerBook;
                case ESAssetReferKind.Avatar: return DefaultAvatarBook;
                case ESAssetReferKind.AudioClip: return DefaultAudioClipBook;
                case ESAssetReferKind.VideoClip: return DefaultVideoClipBook;
                case ESAssetReferKind.TimelineAsset: return DefaultTimelineAssetBook;
                case ESAssetReferKind.PlayableAsset: return DefaultPlayableAssetBook;
                case ESAssetReferKind.ScriptableObject: return DefaultScriptableObjectBook;
                case ESAssetReferKind.TerrainData: return DefaultTerrainDataBook;
                default: return null;
            }
        }

#if UNITY_EDITOR
        public void RebuildFastIndex()
        {
            NormalizePagesEditor();
            _pagesByKind.Clear();
            _pageByEnumKey.Clear();
            _pageByStringKey.Clear();
            _pageByGuid.Clear();

            foreach (var book in GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;

                foreach (var page in book.pages)
                {
                    AddPageToFastIndex(page);
                }
            }

            _fastIndexDirty = false;
        }
#endif

        public int NormalizePagesEditor()
        {
#if UNITY_EDITOR
            int changed = 0;
            foreach (var book in GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;

                foreach (var page in book.pages)
                {
                    if (NormalizePageEditor(page))
                        changed++;
                }
            }

            if (changed > 0)
            {
                _fastIndexDirty = true;
                EditorUtility.SetDirty(this);
            }

            return changed;
#else
            return 0;
#endif
        }

#if UNITY_EDITOR
        private static bool NormalizePageEditor(ESAssetPage page)
        {
            if (page == null || page.OB == null)
                return false;

            bool changed = false;
            if (page.RefreshAssetIdentityEditor(out _, out _, out _, out _))
            {
                changed = true;
            }

            var kind = ESAssetPage.DetermineKind(page.OB);
            if (page.Kind != kind)
            {
                page.Kind = kind;
                changed = true;
            }

            if (string.IsNullOrEmpty(page.StringKey))
            {
                page.StringKey = page.ResolveEffectiveStringKey();
                changed = true;
            }

            if (string.IsNullOrEmpty(page.Name))
            {
                page.Name = page.OB.name;
                changed = true;
            }

            return changed;
        }

#endif

#if UNITY_EDITOR
        public IReadOnlyList<ESAssetPage> GetPagesByKind(ESAssetReferKind kind)
        {
            EnsureFastIndex();
            return _pagesByKind.TryGetValue(kind, out var pages) ? pages : Array.Empty<ESAssetPage>();
        }

        public bool TryGetPageByEnumKey(ESAssetReferKind kind, int enumKey, out ESAssetPage page)
        {
            EnsureFastIndex();
            return _pageByEnumKey.TryGetValue(new ESAssetKindIntKey(kind, enumKey), out page);
        }

        public bool TryGetPageByStringKey(ESAssetReferKind kind, string stringKey, out ESAssetPage page)
        {
            EnsureFastIndex();
            if (string.IsNullOrEmpty(stringKey))
            {
                page = null;
                return false;
            }

            return _pageByStringKey.TryGetValue(new ESAssetKindStringKey(kind, stringKey), out page);
        }

        public bool TryGetPageByGuid(string guid, out ESAssetPage page)
        {
            EnsureFastIndex();
            if (string.IsNullOrEmpty(guid))
            {
                page = null;
                return false;
            }

            return _pageByGuid.TryGetValue(guid, out page);
        }

        private void EnsureFastIndex()
        {
            if (_fastIndexDirty)
            {
                RebuildFastIndex();
            }
        }

        private void AddPageToFastIndex(ESAssetPage page)
        {
            if (page == null)
                return;

            var kind = page.Kind;
#if UNITY_EDITOR
            if ((kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other) && page.OB != null)
            {
                kind = ESAssetPage.DetermineKind(page.OB);
            }
#endif

            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                return;

            if (!_pagesByKind.TryGetValue(kind, out var pages))
            {
                pages = new List<ESAssetPage>(16);
                _pagesByKind.Add(kind, pages);
            }

            pages.Add(page);

            if (page.EnumKey != 0 && !_pageByEnumKey.ContainsKey(new ESAssetKindIntKey(kind, page.EnumKey)))
            {
                _pageByEnumKey.Add(new ESAssetKindIntKey(kind, page.EnumKey), page);
            }

            var stringKey = string.IsNullOrEmpty(page.StringKey) ? page.Name : page.StringKey;
            if (!string.IsNullOrEmpty(stringKey) && !_pageByStringKey.ContainsKey(new ESAssetKindStringKey(kind, stringKey)))
            {
                _pageByStringKey.Add(new ESAssetKindStringKey(kind, stringKey), page);
            }

#if UNITY_EDITOR
            if (page.OB != null)
            {
                var path = AssetDatabase.GetAssetPath(page.OB);
                var guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid) && !_pageByGuid.ContainsKey(guid))
                {
                    _pageByGuid.Add(guid, page);
                }
            }
#endif
        }
#endif
    }

    [Obsolete("Use ESAssetLibrary.")]
    public class ResLibrary : ESAssetLibrary
    {
    }

    public sealed class ESEditorConfigAssetPageTable
    {
        private readonly List<ESAssetPage> pages;
        private readonly Dictionary<ESAssetReferKind, List<ESAssetPage>> pagesByKind;
        private readonly Dictionary<ESAssetKindIntKey, int> slotByRuntimeKey;
        private readonly Dictionary<ESAssetKindIntKey, int> slotByEnumKey;
        private readonly Dictionary<ESAssetKindStringKey, int> slotByStringKey;
        private readonly Dictionary<string, int> slotByGuid;
        private readonly Dictionary<ESAssetIdentityKey, int> slotByIdentity;

        public ESEditorConfigAssetPageTable(int capacity = 256)
        {
            pages = new List<ESAssetPage>(capacity);
            pagesByKind = new Dictionary<ESAssetReferKind, List<ESAssetPage>>(32);
            slotByRuntimeKey = new Dictionary<ESAssetKindIntKey, int>(capacity);
            slotByEnumKey = new Dictionary<ESAssetKindIntKey, int>(capacity);
            slotByStringKey = new Dictionary<ESAssetKindStringKey, int>(capacity);
            slotByGuid = new Dictionary<string, int>(capacity);
            slotByIdentity = new Dictionary<ESAssetIdentityKey, int>(capacity);
        }

        public int Count => pages.Count;
        public IReadOnlyList<ESAssetPage> Pages => pages;

        public void Clear()
        {
            pages.Clear();
            pagesByKind.Clear();
            slotByRuntimeKey.Clear();
            slotByEnumKey.Clear();
            slotByStringKey.Clear();
            slotByGuid.Clear();
            slotByIdentity.Clear();
        }

        public void Load(IReadOnlyList<ESAssetPage> sourcePages)
        {
            Clear();
            if (sourcePages == null)
                return;

            for (int i = 0; i < sourcePages.Count; i++)
            {
                Register(sourcePages[i], true);
            }
        }

        public bool Register(ESAssetPage page, bool allowOverride = true)
        {
            if (page == null || page.RuntimeKey == 0)
                return false;

            if (TryFindSlot(page, out int slot))
            {
                if (!allowOverride)
                    return false;

                Replace(slot, page);
                return true;
            }

            slot = pages.Count;
            pages.Add(page);
            Bind(slot, page);
            return true;
        }

        public bool Remove(ESAssetReferKind kind, int runtimeKey)
        {
            if (!slotByRuntimeKey.TryGetValue(new ESAssetKindIntKey(kind, runtimeKey), out int slot))
                return false;

            pages.RemoveAt(slot);
            RebuildIndex();
            return true;
        }

        public IReadOnlyList<ESAssetPage> GetPagesByKind(ESAssetReferKind kind)
        {
            return pagesByKind.TryGetValue(kind, out var kindPages) ? kindPages : Array.Empty<ESAssetPage>();
        }

        public bool TryGet(int runtimeKey, out ESAssetPage page)
        {
            if (TryGetAnyRuntime(runtimeKey, out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGet(ESAssetReferKind kind, int runtimeKey, out ESAssetPage page)
        {
            if (slotByRuntimeKey.TryGetValue(new ESAssetKindIntKey(kind, runtimeKey), out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByEnum(int enumKey, out ESAssetPage page)
        {
            if (TryGetAnyEnum(enumKey, out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByEnum(ESAssetReferKind kind, int enumKey, out ESAssetPage page)
        {
            if (slotByEnumKey.TryGetValue(new ESAssetKindIntKey(kind, enumKey), out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByString(string stringKey, out ESAssetPage page)
        {
            if (!string.IsNullOrEmpty(stringKey) && TryGetAnyString(stringKey, out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByString(ESAssetReferKind kind, string stringKey, out ESAssetPage page)
        {
            if (slotByStringKey.TryGetValue(new ESAssetKindStringKey(kind, stringKey), out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByGuid(string guid, out ESAssetPage page)
        {
            if (!string.IsNullOrEmpty(guid) && slotByGuid.TryGetValue(guid, out int slot))
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        public bool TryGetByGuid(ESAssetReferKind kind, string guid, out ESAssetPage page)
        {
            if (TryGetByGuid(guid, out page) && page.Kind == kind)
                return true;

            page = null;
            return false;
        }

        public bool TryGetByAssetIdentity(ESAssetReferKind kind, string guid, long localFileId, out ESAssetPage page)
        {
            if (!string.IsNullOrEmpty(guid)
                && slotByIdentity.TryGetValue(new ESAssetIdentityKey(guid, localFileId), out int slot)
                && pages[slot].Kind == kind)
            {
                page = pages[slot];
                return true;
            }

            page = null;
            return false;
        }

        private bool TryFindSlot(ESAssetPage page, out int slot)
        {
            if (!string.IsNullOrEmpty(page.AssetGuid)
                && slotByIdentity.TryGetValue(new ESAssetIdentityKey(page.AssetGuid, page.LocalFileId), out slot))
                return true;

            if (string.IsNullOrEmpty(page.AssetGuid))
            {
                if (page.RuntimeKey != 0 && slotByRuntimeKey.TryGetValue(new ESAssetKindIntKey(page.Kind, page.RuntimeKey), out slot))
                    return true;
                if (page.EnumKey != 0 && slotByEnumKey.TryGetValue(new ESAssetKindIntKey(page.Kind, page.EnumKey), out slot))
                    return true;
                if (!string.IsNullOrEmpty(page.EffectiveStringKey) && slotByStringKey.TryGetValue(new ESAssetKindStringKey(page.Kind, page.EffectiveStringKey), out slot))
                    return true;
            }

            slot = -1;
            return false;
        }

        private void Replace(int slot, ESAssetPage page)
        {
            pages[slot] = page;
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            slotByRuntimeKey.Clear();
            slotByEnumKey.Clear();
            slotByStringKey.Clear();
            slotByGuid.Clear();
            slotByIdentity.Clear();
            pagesByKind.Clear();
            for (int i = 0; i < pages.Count; i++)
            {
                Bind(i, pages[i]);
            }
        }

        private void Bind(int slot, ESAssetPage page)
        {
            if (page == null)
                return;

            if (!pagesByKind.TryGetValue(page.Kind, out var kindPages))
            {
                kindPages = new List<ESAssetPage>(16);
                pagesByKind.Add(page.Kind, kindPages);
            }

            kindPages.Add(page);

            if (page.RuntimeKey != 0)
                TryBind(slotByRuntimeKey, new ESAssetKindIntKey(page.Kind, page.RuntimeKey), slot);
            if (page.EnumKey != 0)
                TryBind(slotByEnumKey, new ESAssetKindIntKey(page.Kind, page.EnumKey), slot);
            if (!string.IsNullOrEmpty(page.EffectiveStringKey))
                TryBind(slotByStringKey, new ESAssetKindStringKey(page.Kind, page.EffectiveStringKey), slot);
            if (!string.IsNullOrEmpty(page.AssetGuid))
            {
                TryBind(slotByGuid, page.AssetGuid, slot);
                TryBind(slotByIdentity, new ESAssetIdentityKey(page.AssetGuid, page.LocalFileId), slot);
            }
        }

        private static void TryBind<TKey>(Dictionary<TKey, int> map, TKey key, int slot)
        {
            if (!map.ContainsKey(key))
                map.Add(key, slot);
        }

        private bool TryGetAnyRuntime(int runtimeKey, out int slot)
        {
            foreach (var pair in slotByRuntimeKey)
            {
                if (pair.Key.Value == runtimeKey)
                {
                    slot = pair.Value;
                    return true;
                }
            }
            slot = -1;
            return false;
        }

        private bool TryGetAnyEnum(int enumKey, out int slot)
        {
            foreach (var pair in slotByEnumKey)
            {
                if (pair.Key.Value == enumKey)
                {
                    slot = pair.Value;
                    return true;
                }
            }
            slot = -1;
            return false;
        }

        private bool TryGetAnyString(string stringKey, out int slot)
        {
            foreach (var pair in slotByStringKey)
            {
                if (string.Equals(pair.Key.Value, stringKey, StringComparison.Ordinal))
                {
                    slot = pair.Value;
                    return true;
                }
            }
            slot = -1;
            return false;
        }
    }

    public static class ESAssetRegistry
    {
        public const int DefaultStringRuntimeKeyStart = 30000;

        private static readonly List<ESAssetPage> pages = new List<ESAssetPage>(256);
        private static readonly ESEditorConfigAssetPageTable editorConfigQueryTable = new ESEditorConfigAssetPageTable(256);
        private static readonly List<string> warnings = new List<string>(64);
        private static readonly Dictionary<string, PageKeySnapshot> snapshotsByGuid = new Dictionary<string, PageKeySnapshot>(256);
        private static readonly HashSet<string> suppressedLibraryInjectOnce = new HashSet<string>();
        private static readonly Dictionary<ESAssetReferKind, int> nextStringRuntimeKeyByKind = new Dictionary<ESAssetReferKind, int>(32);
        private static int version;

        private struct PageKeySnapshot
        {
            public int runtimeKey;
            public int enumKey;
            public string stringKey;
            public string assetPath;
        }

        public static ESEditorConfigAssetPageTable EditorConfigQueryTable => editorConfigQueryTable;
        public static IReadOnlyList<ESAssetPage> Pages => pages;
        public static int WarningCount => warnings.Count;
        public static IReadOnlyList<string> Warnings => warnings;
        /// <summary>编辑器注册表快照版本；Key 选择器据此精确失效缓存。</summary>
        public static int Version => version;

        public static void Clear()
        {
            pages.Clear();
            editorConfigQueryTable.Clear();
            warnings.Clear();
            snapshotsByGuid.Clear();
            suppressedLibraryInjectOnce.Clear();
            nextStringRuntimeKeyByKind.Clear();
            unchecked { version++; }
        }

        public static int BuildFromAssetLibrary(ESAssetLibrary library, bool clearBeforeBuild = false, int startOrderIndex = 0)
        {
            if (clearBeforeBuild)
                Clear();

            if (library == null)
                return 0;

            library.NormalizePagesEditor();
            string libraryKey = GetLibraryRegistryKey(library);
            if (startOrderIndex == 0 && suppressedLibraryInjectOnce.Remove(libraryKey))
                return 0;

            RemovePagesBySourceLibrary(libraryKey);
            int count = 0;
            foreach (var book in library.GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;

                for (int i = 0; i < book.pages.Count; i++)
                {
                    if (RegisterAsset((ESAssetPage)book.pages[i], libraryKey, book.Name))
                        count++;
                }
            }

            return count;
        }

        [Obsolete("Use BuildFromAssetLibrary instead.")]
        public static int BuildFromLibrary(ResLibrary library, bool clearBeforeBuild = false)
        {
            return BuildFromAssetLibrary(library, clearBeforeBuild, 0);
        }

        public static int BuildFromAssetLibraries(IReadOnlyList<ESAssetLibrary> libraries, bool clearBeforeBuild = true, int startOrderIndex = 0)
        {
            if (clearBeforeBuild)
                Clear();

            if (libraries == null)
                return 0;

            int count = 0;
            for (int i = 0; i < libraries.Count; i++)
            {
                count += BuildFromAssetLibrary(libraries[i], false, startOrderIndex);
            }

            return count;
        }

        [Obsolete("Use BuildFromAssetLibraries instead.")]
        public static int BuildFromLibraries(IReadOnlyList<ResLibrary> libraries, bool clearBeforeBuild = true)
        {
            if (clearBeforeBuild)
                Clear();

            if (libraries == null)
                return 0;

            int count = 0;
            for (int i = 0; i < libraries.Count; i++)
            {
                count += BuildFromAssetLibrary(libraries[i], false, 0);
            }

            return count;
        }

        public static bool RegisterAsset(ESAssetPage page)
        {
            return RegisterAsset(page, null, null, 0);
        }

        public static bool RegisterAsset(ESAssetPage page, string libraryName, string bookName, int startOrderIndex = 0)
        {
            if (page == null || page.OB == null)
                return false;

            page.RefreshAssetIdentityEditor();
            if (string.IsNullOrEmpty(page.StringKey))
                page.StringKey = page.ResolveEffectiveStringKey();
            if (!string.IsNullOrEmpty(libraryName))
                page.SourceLibrary = libraryName;
            if (!string.IsNullOrEmpty(bookName))
                page.SourceBook = bookName;
            page.RuntimeKey = BakeRuntimeKey(page);
            WarnIfSnapshotChanged(page);
            UpsertPageByGuidAuthority(page);
            if (startOrderIndex > 0)
                MarkSourceLibraryDirty(page, startOrderIndex);
            return true;
        }

        public static bool RegisterAsset(UnityEngine.Object asset, ESAssetReferKind kind, int enumKey, string stringKey)
        {
            return RegisterAsset(asset, kind, enumKey, stringKey, null, null);
        }

        public static bool RegisterAsset(UnityEngine.Object asset, ESAssetReferKind kind, int enumKey, string stringKey, string libraryName, string bookName)
        {
#if UNITY_EDITOR
            if (asset == null || kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                return false;

            ESAssetPage page = new ESAssetPage
            {
                Name = asset.name,
                OB = asset,
                Kind = kind,
                EnumKey = enumKey,
                StringKey = string.IsNullOrEmpty(stringKey) ? asset.name : stringKey,
                SourceLibrary = libraryName ?? string.Empty,
                SourceBook = bookName ?? string.Empty
            };
            return RegisterAsset(page, libraryName, bookName, 0);
#else
            return false;
#endif
        }

        public static bool RemoveAsset(string guid, int startOrderIndex = 0)
        {
            if (string.IsNullOrEmpty(guid))
                return false;

            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].AssetGuid == guid)
                {
                    RemovePageFromSourceLibrary(pages[i], startOrderIndex + 1);
                    pages.RemoveAt(i);
                    RebuildEditorConfigQueryTable();
                    return true;
                }
            }

            return false;
        }

        public static bool RemoveAsset(ESAssetPage page, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (page?.OB == null)
                return false;

            string path = AssetDatabase.GetAssetPath(page.OB);
            return RemoveAsset(string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path), startOrderIndex);
#else
            return false;
#endif
        }

        public static bool RenameAsset(ESAssetPage page, string newName, int startOrderIndex = 0)
        {
            if (page == null)
                return false;

            if (TryFindPageIndexByGuidOrRuntime(page, out int index))
            {
                page.Name = newName;
                page.RuntimeKey = BakeRuntimeKey(page);
                WarnIfSnapshotChanged(page);
                pages[index] = page;
                RebuildEditorConfigQueryTable();
                MarkSourceLibraryDirty(page, startOrderIndex + 1);
                return true;
            }

            return false;
        }

        public static bool RenameAsset(string guid, string newName, int startOrderIndex = 0)
        {
            if (!TryGetByGuid(guid, out ESAssetPage page))
                return false;

            return RenameAsset(page, newName, startOrderIndex);
        }

        public static bool RenameAsset(UnityEngine.Object asset, string newName, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (!TryResolvePageByAsset(asset, out ESAssetPage page))
                return false;

            return RenameAsset(page, newName, startOrderIndex);
#else
            return false;
#endif
        }

        public static bool RenameStringKey(ESAssetPage page, string newStringKey, int startOrderIndex = 0)
        {
            if (page == null || string.IsNullOrEmpty(newStringKey))
                return false;

            if (TryFindPageIndexByGuidOrRuntime(page, out int index))
            {
                ESAssetPage oldPage = pages[index];
                page.StringKey = newStringKey;
                page.RuntimeKey = BakeRuntimeKey(page);
                WarnIfKeyChanged(oldPage, page);
                WarnIfSnapshotChanged(page);
                pages[index] = page;
                RebuildEditorConfigQueryTable();
                MarkSourceLibraryDirty(page, startOrderIndex + 1);
                return true;
            }

            page.StringKey = newStringKey;
            return RegisterAsset(page);
        }

        public static bool RenameStringKey(string guid, string newStringKey, int startOrderIndex = 0)
        {
            if (!TryGetByGuid(guid, out ESAssetPage page))
                return false;

            return RenameStringKey(page, newStringKey, startOrderIndex);
        }

        public static bool RenameStringKey(UnityEngine.Object asset, string newStringKey, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (!TryResolvePageByAsset(asset, out ESAssetPage page))
                return false;

            return RenameStringKey(page, newStringKey, startOrderIndex);
#else
            return false;
#endif
        }

        public static bool RenameEnumKey(ESAssetPage page, int newEnumKey, int startOrderIndex = 0)
        {
            if (page == null)
                return false;

            if (TryFindPageIndexByGuidOrRuntime(page, out int index))
            {
                ESAssetPage oldPage = pages[index];
                page.EnumKey = newEnumKey;
                page.RuntimeKey = BakeRuntimeKey(page);
                WarnIfKeyChanged(oldPage, page);
                WarnIfSnapshotChanged(page);
                pages[index] = page;
                RebuildEditorConfigQueryTable();
                MarkSourceLibraryDirty(page, startOrderIndex + 1);
                return true;
            }

            page.EnumKey = newEnumKey;
            return RegisterAsset(page);
        }

        public static bool RenameEnumKey(string guid, int newEnumKey, int startOrderIndex = 0)
        {
            if (!TryGetByGuid(guid, out ESAssetPage page))
                return false;

            return RenameEnumKey(page, newEnumKey, startOrderIndex);
        }

        public static bool RenameEnumKey(UnityEngine.Object asset, int newEnumKey, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (!TryResolvePageByAsset(asset, out ESAssetPage page))
                return false;

            return RenameEnumKey(page, newEnumKey, startOrderIndex);
#else
            return false;
#endif
        }

        public static bool RenameRuntimeKey(ESAssetPage page, int newRuntimeKey, int startOrderIndex = 0)
        {
            if (page == null
                || page.EnumKey != 0
                || newRuntimeKey < DefaultStringRuntimeKeyStart)
            {
                return false;
            }

            if (!TryFindPageIndexByGuidOrRuntime(page, out int index))
                return false;

            if (editorConfigQueryTable.TryGet(page.Kind, newRuntimeKey, out ESAssetPage conflict)
                && conflict != null
                && !ReferenceEquals(conflict, page)
                && (string.IsNullOrEmpty(page.AssetGuid) || conflict.AssetGuid != page.AssetGuid))
            {
                return false;
            }

            if (page.RuntimeKey == newRuntimeKey)
                return true;

            page.RuntimeKey = newRuntimeKey;
            pages[index] = page;
            EnsureNextStringRuntimeKeyAfter(page.Kind, newRuntimeKey);
            RebuildEditorConfigQueryTable();
            MarkSourceLibraryDirty(page, startOrderIndex + 1);
            return true;
        }

        public static bool RenameRuntimeKey(string guid, int newRuntimeKey, int startOrderIndex = 0)
        {
            if (!TryGetByGuid(guid, out ESAssetPage page))
                return false;

            return RenameRuntimeKey(page, newRuntimeKey, startOrderIndex);
        }

        public static bool RenameRuntimeKey(UnityEngine.Object asset, int newRuntimeKey, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (!TryResolvePageByAsset(asset, out ESAssetPage page))
                return false;

            return RenameRuntimeKey(page, newRuntimeKey, startOrderIndex);
#else
            return false;
#endif
        }

        public static bool RefreshAssetPath(ESAssetPage page, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (page == null || page.OB == null)
                return false;

            bool identityChanged = page.RefreshAssetIdentityEditor(out string oldGuid, out long oldLocalFileId, out string oldPath, out string oldTypeName);
            if (!TryFindPageIndexByGuidOrRuntime(page, out int index))
                return RegisterAsset(page);

            ESAssetPage oldPage = pages[index];
            page.RuntimeKey = BakeRuntimeKey(page);
            if (identityChanged)
                AddWarning("Asset identity refreshed by asset authority"
                    + " | oldGuid=" + oldGuid
                    + "/oldLocalFileId=" + oldLocalFileId
                    + "/oldPath=" + oldPath
                    + "/oldType=" + oldTypeName, oldPage, page);

            WarnIfSnapshotChanged(page);
            pages[index] = page;
            RebuildEditorConfigQueryTable();
            MarkSourceLibraryDirty(page, startOrderIndex + 1);
            return true;
#else
            return false;
#endif
        }

        public static bool RefreshAssetPath(string guid, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid) || !TryGetByGuid(guid, out ESAssetPage page))
                return false;

            if (!TryFindPageIndexByGuidOrRuntime(page, out int index))
                return false;

            ESAssetPage oldPage = pages[index];
            string oldPath = page.AssetPath;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            UnityEngine.Object loadedAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (loadedAsset != null)
                page.OB = loadedAsset;

            page.AssetGuid = guid;
            page.AssetPath = assetPath;
            if (page.OB != null)
            {
                page.Kind = ESAssetPage.DetermineKind(page.OB);
                page.AssetTypeName = page.OB.GetType().FullName;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(page.OB, out _, out long localFileId);
                page.LocalFileId = localFileId;
                if (string.IsNullOrEmpty(page.Name))
                    page.Name = page.OB.name;
                if (string.IsNullOrEmpty(page.StringKey))
                    page.StringKey = page.ResolveEffectiveStringKey();
            }

            page.RuntimeKey = BakeRuntimeKey(page);
            if (oldPath != page.AssetPath)
                AddWarning("AssetPath changed by GUID-only asset authority | oldPath=" + oldPath, oldPage, page);

            WarnIfSnapshotChanged(page);
            pages[index] = page;
            RebuildEditorConfigQueryTable();
            MarkSourceLibraryDirty(page, startOrderIndex + 1);
            return true;
#else
            return false;
#endif
        }

        public static bool RefreshAssetPath(UnityEngine.Object asset, int startOrderIndex = 0)
        {
#if UNITY_EDITOR
            if (!TryResolvePageByAsset(asset, out ESAssetPage page))
                return false;

            return RefreshAssetPath(page, startOrderIndex);
#else
            return false;
#endif
        }

        public static bool TryGet(int runtimeKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGet(runtimeKey, out page);
        }

        public static bool TryGet(ESAssetReferKind kind, int runtimeKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGet(kind, runtimeKey, out page);
        }

        public static bool TryGetByEnum(int enumKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByEnum(enumKey, out page);
        }

        public static bool TryGetByEnum(ESAssetReferKind kind, int enumKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByEnum(kind, enumKey, out page);
        }

        public static bool TryGetByString(string stringKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByString(stringKey, out page);
        }

        public static bool TryGetByString(ESAssetReferKind kind, string stringKey, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByString(kind, stringKey, out page);
        }

        public static bool TryGetByGuid(string guid, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByGuid(guid, out page);
        }

        public static bool TryGetByGuid(ESAssetReferKind kind, string guid, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByGuid(kind, guid, out page);
        }

        /// <summary>编辑器收集解析使用完整身份，避免子资产与主资源共享 GUID 时误命中。</summary>
        public static bool TryGetByAssetIdentity(ESAssetReferKind kind, string guid, long localFileId, out ESAssetPage page)
        {
            return editorConfigQueryTable.TryGetByAssetIdentity(kind, guid, localFileId, out page);
        }

        public static IReadOnlyList<ESAssetPage> GetPagesByKind(ESAssetReferKind kind)
        {
            return editorConfigQueryTable.GetPagesByKind(kind);
        }

        public static string GetWarningReport()
        {
            if (warnings.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, warnings);
        }

        private static void UpsertPageByGuidAuthority(ESAssetPage page)
        {
            if (TryFindPageIndexByGuidOrRuntime(page, out int index))
            {
                ESAssetPage existing = pages[index];
                WarnIfKeyChanged(existing, page);
                pages[index] = page;
                RebuildEditorConfigQueryTable();
                return;
            }

            WarnIfKeyConflict(page);
            pages.Add(page);
            RebuildEditorConfigQueryTable();
        }

        private static bool TryFindPageIndexByGuidOrRuntime(ESAssetPage page, out int index)
        {
            if (page != null && !string.IsNullOrEmpty(page.AssetGuid))
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    ESAssetPage existing = pages[i];
                    if (existing != null
                        && existing.AssetGuid == page.AssetGuid
                        && existing.LocalFileId == page.LocalFileId)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (page != null && page.RuntimeKey != 0)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    ESAssetPage existing = pages[i];
                    if (existing != null && existing.Kind == page.Kind && existing.RuntimeKey == page.RuntimeKey && string.IsNullOrEmpty(existing.AssetGuid))
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

#if UNITY_EDITOR
        private static bool TryResolvePageByAsset(UnityEngine.Object asset, out ESAssetPage page)
        {
            page = null;
            if (asset == null)
                return false;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId))
            {
                if (!AssetDatabase.IsSubAsset(asset))
                    localFileId = 0;

                ESAssetReferKind identityKind = ESAssetPage.DetermineKind(asset);
                if (!string.IsNullOrEmpty(guid) && TryGetByAssetIdentity(identityKind, guid, localFileId, out page))
                    return true;
            }

            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                return false;

            page = ESAssetPage.Create(asset);
            bool registered = RegisterAsset(page);
            if (registered)
                AddWarning("Asset object registered without source ESAssetLibrary; Registry mirror updated only", null, page);
            return registered;
        }
#endif

        private static void RebuildEditorConfigQueryTable()
        {
            editorConfigQueryTable.Load(pages);
            for (int i = 0; i < pages.Count; i++)
            {
                RememberSnapshot(pages[i]);
            }
            unchecked { version++; }
        }

        private static void RemovePagesBySourceLibrary(string sourceLibrary)
        {
            if (string.IsNullOrEmpty(sourceLibrary))
                return;

            for (int i = pages.Count - 1; i >= 0; i--)
            {
                if (pages[i] != null && pages[i].SourceLibrary == sourceLibrary)
                    pages.RemoveAt(i);
            }

            RebuildEditorConfigQueryTable();
        }

        private static string GetLibraryRegistryKey(ESAssetLibrary library)
        {
#if UNITY_EDITOR
            if (library != null)
            {
                string path = AssetDatabase.GetAssetPath(library);
                string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    return guid;
            }
#endif
            return library != null ? library.Name : string.Empty;
        }

        private static void MarkSourceLibraryDirty(ESAssetPage page, int startOrderIndex)
        {
#if UNITY_EDITOR
            if (page == null || string.IsNullOrEmpty(page.SourceLibrary))
                return;

            string libraryPath = AssetDatabase.GUIDToAssetPath(page.SourceLibrary);
            if (string.IsNullOrEmpty(libraryPath))
                return;

            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
            if (library == null)
                return;

            if (startOrderIndex > 0)
                suppressedLibraryInjectOnce.Add(page.SourceLibrary);

            EditorUtility.SetDirty(library);
#endif
        }

        public static void MarkSourceLibraryDirtyByPage(ESAssetPage page, int startOrderIndex = 0)
        {
            MarkSourceLibraryDirty(page, startOrderIndex);
        }

        private static bool RemovePageFromSourceLibrary(ESAssetPage page, int startOrderIndex)
        {
#if UNITY_EDITOR
            ESAssetLibrary library = LoadSourceLibrary(page);
            if (library == null)
                return false;

            bool removed = false;
            foreach (var book in library.GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;

                for (int i = book.pages.Count - 1; i >= 0; i--)
                {
                    ESAssetPage candidate = book.pages[i];
                    if (candidate == null)
                        continue;

                    candidate.RefreshAssetIdentityEditor();
                    if (!string.IsNullOrEmpty(page.AssetGuid)
                        && candidate.AssetGuid == page.AssetGuid
                        && candidate.LocalFileId == page.LocalFileId)
                    {
                        book.pages.RemoveAt(i);
                        removed = true;
                    }
                }
            }

            if (removed)
            {
                if (startOrderIndex > 0)
                    suppressedLibraryInjectOnce.Add(page.SourceLibrary);

                library.MarkFastIndexDirty();
                EditorUtility.SetDirty(library);
            }

            return removed;
#else
            return false;
#endif
        }

        private static ESAssetLibrary LoadSourceLibrary(ESAssetPage page)
        {
#if UNITY_EDITOR
            if (page == null || string.IsNullOrEmpty(page.SourceLibrary))
                return null;

            string libraryPath = AssetDatabase.GUIDToAssetPath(page.SourceLibrary);
            return string.IsNullOrEmpty(libraryPath) ? null : AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
#else
            return null;
#endif
        }

        private static void WarnIfKeyChanged(ESAssetPage oldPage, ESAssetPage newPage)
        {
            if (oldPage == null || newPage == null)
                return;

            if (oldPage.RuntimeKey != 0 && newPage.RuntimeKey != 0 && oldPage.RuntimeKey != newPage.RuntimeKey)
                AddWarning("RuntimeKey changed by GUID authority", oldPage, newPage);
            if (oldPage.EnumKey != 0 && newPage.EnumKey != 0 && oldPage.EnumKey != newPage.EnumKey)
                AddWarning("EnumKey changed by GUID authority", oldPage, newPage);
            if (!string.IsNullOrEmpty(oldPage.EffectiveStringKey)
                && !string.IsNullOrEmpty(newPage.EffectiveStringKey)
                && oldPage.EffectiveStringKey != newPage.EffectiveStringKey)
                AddWarning("StringKey changed by GUID authority", oldPage, newPage);
        }

        private static void WarnIfSnapshotChanged(ESAssetPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.AssetGuid))
                return;

            if (!snapshotsByGuid.TryGetValue(page.AssetGuid, out PageKeySnapshot old))
                return;

            string newStringKey = page.EffectiveStringKey;
            if (old.runtimeKey != 0 && page.RuntimeKey != 0 && old.runtimeKey != page.RuntimeKey)
                AddWarning("RuntimeKey changed by asset self override", SnapshotToPage(page, old), page);
            if (old.enumKey != 0 && page.EnumKey != 0 && old.enumKey != page.EnumKey)
                AddWarning("EnumKey changed by asset self override", SnapshotToPage(page, old), page);
            if (!string.IsNullOrEmpty(old.stringKey) && !string.IsNullOrEmpty(newStringKey) && old.stringKey != newStringKey)
                AddWarning("StringKey changed by asset self override", SnapshotToPage(page, old), page);
            if (!string.IsNullOrEmpty(old.assetPath) && !string.IsNullOrEmpty(page.AssetPath) && old.assetPath != page.AssetPath)
                AddWarning("AssetPath changed by asset self override", SnapshotToPage(page, old), page);
        }

        private static void RememberSnapshot(ESAssetPage page)
        {
            if (page == null || string.IsNullOrEmpty(page.AssetGuid))
                return;

            snapshotsByGuid[page.AssetGuid] = new PageKeySnapshot
            {
                runtimeKey = page.RuntimeKey,
                enumKey = page.EnumKey,
                stringKey = page.EffectiveStringKey,
                assetPath = page.AssetPath
            };
        }

        private static ESAssetPage SnapshotToPage(ESAssetPage current, PageKeySnapshot snapshot)
        {
            return new ESAssetPage
            {
                Name = current != null ? current.Name : string.Empty,
                Kind = current != null ? current.Kind : ESAssetReferKind.None,
                RuntimeKey = snapshot.runtimeKey,
                EnumKey = snapshot.enumKey,
                StringKey = snapshot.stringKey,
                AssetGuid = current != null ? current.AssetGuid : string.Empty,
                AssetPath = snapshot.assetPath
            };
        }

        private static void WarnIfKeyConflict(ESAssetPage page)
        {
            if (page == null)
                return;

            for (int i = 0; i < pages.Count; i++)
            {
                ESAssetPage existing = pages[i];
                if (existing == null)
                    continue;

                bool sameIdentity = !string.IsNullOrEmpty(page.AssetGuid)
                    && existing.AssetGuid == page.AssetGuid
                    && existing.LocalFileId == page.LocalFileId;
                if (sameIdentity)
                    continue;

                if (page.Kind == existing.Kind && page.RuntimeKey != 0 && existing.RuntimeKey == page.RuntimeKey)
                    AddWarning("RuntimeKey conflict, GUID keeps assets separated", existing, page);
                if (page.Kind == existing.Kind && page.EnumKey != 0 && existing.EnumKey == page.EnumKey)
                    AddWarning("EnumKey conflict, GUID keeps assets separated", existing, page);
                if (page.Kind == existing.Kind && !string.IsNullOrEmpty(page.EffectiveStringKey) && existing.EffectiveStringKey == page.EffectiveStringKey)
                    AddWarning("StringKey conflict, GUID keeps assets separated", existing, page);
            }
        }

        private static void AddWarning(string reason, ESAssetPage a, ESAssetPage b)
        {
            string message = "[ESAssetRegistry] " + reason
                + " | A=" + DescribePage(a)
                + " | B=" + DescribePage(b);
            if (!warnings.Contains(message))
            {
                warnings.Add(message);
                Debug.LogWarning(message);
            }
        }

        private static string DescribePage(ESAssetPage page)
        {
            if (page == null)
                return "<null>";

            return page.Kind
                + "/runtime=" + page.RuntimeKey
                + "/enum=" + page.EnumKey
                + "/string=" + page.EffectiveStringKey
                + "/guid=" + page.AssetGuid
                + "/path=" + page.AssetPath;
        }

        private static int BakeRuntimeKey(ESAssetPage page)
        {
            if (page == null)
                return 0;

            int enumKey = page.EnumKey;
            string stringKey = page.EffectiveStringKey;

            if (enumKey != 0)
                return enumKey;

            if (!string.IsNullOrEmpty(page.AssetGuid)
                && snapshotsByGuid.TryGetValue(page.AssetGuid, out PageKeySnapshot snapshot)
                && snapshot.runtimeKey >= DefaultStringRuntimeKeyStart
                && snapshot.enumKey == enumKey
                && string.Equals(snapshot.stringKey, stringKey, StringComparison.Ordinal))
            {
                EnsureNextStringRuntimeKeyAfter(page.Kind, snapshot.runtimeKey);
                return snapshot.runtimeKey;
            }

            if (page.RuntimeKey >= DefaultStringRuntimeKeyStart)
            {
                EnsureNextStringRuntimeKeyAfter(page.Kind, page.RuntimeKey);
                return page.RuntimeKey;
            }

            if (!string.IsNullOrEmpty(stringKey)
                && editorConfigQueryTable.TryGetByString(page.Kind, stringKey, out ESAssetPage existing)
                && existing.RuntimeKey >= DefaultStringRuntimeKeyStart)
            {
                EnsureNextStringRuntimeKeyAfter(page.Kind, existing.RuntimeKey);
                return existing.RuntimeKey;
            }

            int nextRuntimeKey = GetNextStringRuntimeKey(page.Kind);
            while (editorConfigQueryTable.TryGet(page.Kind, nextRuntimeKey, out _))
                nextRuntimeKey++;
            nextStringRuntimeKeyByKind[page.Kind] = nextRuntimeKey + 1;
            return nextRuntimeKey;
        }

        private static int GetNextStringRuntimeKey(ESAssetReferKind kind)
        {
            return nextStringRuntimeKeyByKind.TryGetValue(kind, out int nextRuntimeKey)
                ? nextRuntimeKey
                : DefaultStringRuntimeKeyStart;
        }

        private static void EnsureNextStringRuntimeKeyAfter(ESAssetReferKind kind, int runtimeKey)
        {
            int nextRuntimeKey = GetNextStringRuntimeKey(kind);
            if (runtimeKey >= nextRuntimeKey)
                nextStringRuntimeKeyByKind[kind] = runtimeKey + 1;
        }
    }

}
