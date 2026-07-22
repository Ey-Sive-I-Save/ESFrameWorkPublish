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
    public class ESAssetLibrary : LibrarySoBase<ESAssetBook>
    {
        [NonSerialized]
        private readonly Dictionary<ESAssetReferKind, List<ESAssetPage>> _pagesByKind = new Dictionary<ESAssetReferKind, List<ESAssetPage>>(32);

        [NonSerialized]
        private readonly Dictionary<int, ESAssetPage> _pageByEnumKey = new Dictionary<int, ESAssetPage>(256);

        [NonSerialized]
        private readonly Dictionary<string, ESAssetPage> _pageByStringKey = new Dictionary<string, ESAssetPage>(256);

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
        public ESAssetBook DefaultTerrainDataBook = new ESAssetBook() { Name = "Default TerrainData Book", Desc = "ESAssetReferTerrainData" };

        [LabelText("Can Build")]
        public bool ContainsBuild = true;

        [ESBoolOption("Remote Download", "Local Library")]
        public bool IsNet = true;

        public override void OnEditorApply()
        {
            base.OnEditorApply();
            Refresh();
#if UNITY_EDITOR
            NormalizePagesEditor();
            ESAssetRegistry.BuildFromAssetLibrary(this);
#endif
        }

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
            NormalizePagesEditor();
            ESAssetRegistry.BuildFromAssetLibrary(this);
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
            var kind = ESAssetPage.DetermineKind(page.OB);
            if (page.Kind != kind)
            {
                page.Kind = kind;
                changed = true;
            }

            if (string.IsNullOrEmpty(page.StringKey))
            {
                page.StringKey = !string.IsNullOrEmpty(page.Name) ? page.Name : page.OB.name;
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

        public bool TryGetPageByEnumKey(int enumKey, out ESAssetPage page)
        {
            EnsureFastIndex();
            return _pageByEnumKey.TryGetValue(enumKey, out page);
        }

        public bool TryGetPageByStringKey(string stringKey, out ESAssetPage page)
        {
            EnsureFastIndex();
            if (string.IsNullOrEmpty(stringKey))
            {
                page = null;
                return false;
            }

            return _pageByStringKey.TryGetValue(stringKey, out page);
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

            if (page.EnumKey != 0 && !_pageByEnumKey.ContainsKey(page.EnumKey))
            {
                _pageByEnumKey.Add(page.EnumKey, page);
            }

            var stringKey = string.IsNullOrEmpty(page.StringKey) ? page.Name : page.StringKey;
            if (!string.IsNullOrEmpty(stringKey) && !_pageByStringKey.ContainsKey(stringKey))
            {
                _pageByStringKey.Add(stringKey, page);
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

    [Serializable]
    public struct ESAssetRecord
    {
        public ESAssetReferKind kind;
        public int enumKey;
        public string stringKey;
        public string guid;
        public long localFileId;
        public string assetPath;
        public int runtimeKey;
        public string assetName;
        public Type assetType;
        public string libraryName;
        public string bookName;
    }

    public sealed class ESAssetTable
    {
        private readonly List<ESAssetRecord> records;
        private readonly Dictionary<int, int> slotByRuntimeKey;
        private readonly Dictionary<int, int> slotByEnumKey;
        private readonly Dictionary<string, int> slotByStringKey;
        private readonly Dictionary<string, int> slotByGuid;

        public ESAssetTable(int capacity = 256)
        {
            records = new List<ESAssetRecord>(capacity);
            slotByRuntimeKey = new Dictionary<int, int>(capacity);
            slotByEnumKey = new Dictionary<int, int>(capacity);
            slotByStringKey = new Dictionary<string, int>(capacity);
            slotByGuid = new Dictionary<string, int>(capacity);
        }

        public int Count => records.Count;
        public IReadOnlyList<ESAssetRecord> Records => records;

        public void Clear()
        {
            records.Clear();
            slotByRuntimeKey.Clear();
            slotByEnumKey.Clear();
            slotByStringKey.Clear();
            slotByGuid.Clear();
        }

        public void Load(IReadOnlyList<ESAssetRecord> sourceRecords)
        {
            Clear();
            if (sourceRecords == null)
                return;

            for (int i = 0; i < sourceRecords.Count; i++)
            {
                Register(sourceRecords[i], true);
            }
        }

        public bool Register(ESAssetRecord record, bool allowOverride = true)
        {
            if (record.runtimeKey == 0)
                return false;

            if (TryFindSlot(record, out int slot))
            {
                if (!allowOverride)
                    return false;

                Replace(slot, record);
                return true;
            }

            slot = records.Count;
            records.Add(record);
            Bind(slot, record);
            return true;
        }

        public bool Remove(int runtimeKey)
        {
            if (!slotByRuntimeKey.TryGetValue(runtimeKey, out int slot))
                return false;

            records.RemoveAt(slot);
            RebuildIndex();
            return true;
        }

        public bool TryGet(int runtimeKey, out ESAssetRecord record)
        {
            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int slot))
            {
                record = records[slot];
                return true;
            }

            record = default;
            return false;
        }

        public bool TryGetByEnum(int enumKey, out ESAssetRecord record)
        {
            if (slotByEnumKey.TryGetValue(enumKey, out int slot))
            {
                record = records[slot];
                return true;
            }

            record = default;
            return false;
        }

        public bool TryGetByString(string stringKey, out ESAssetRecord record)
        {
            if (!string.IsNullOrEmpty(stringKey) && slotByStringKey.TryGetValue(stringKey, out int slot))
            {
                record = records[slot];
                return true;
            }

            record = default;
            return false;
        }

        public bool TryGetByGuid(string guid, out ESAssetRecord record)
        {
            if (!string.IsNullOrEmpty(guid) && slotByGuid.TryGetValue(guid, out int slot))
            {
                record = records[slot];
                return true;
            }

            record = default;
            return false;
        }

        private bool TryFindSlot(ESAssetRecord record, out int slot)
        {
            if (record.runtimeKey != 0 && slotByRuntimeKey.TryGetValue(record.runtimeKey, out slot))
                return true;
            if (record.enumKey != 0 && slotByEnumKey.TryGetValue(record.enumKey, out slot))
                return true;
            if (!string.IsNullOrEmpty(record.stringKey) && slotByStringKey.TryGetValue(record.stringKey, out slot))
                return true;
            if (!string.IsNullOrEmpty(record.guid) && slotByGuid.TryGetValue(record.guid, out slot))
                return true;

            slot = -1;
            return false;
        }

        private void Replace(int slot, ESAssetRecord record)
        {
            records[slot] = record;
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            slotByRuntimeKey.Clear();
            slotByEnumKey.Clear();
            slotByStringKey.Clear();
            slotByGuid.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                Bind(i, records[i]);
            }
        }

        private void Bind(int slot, ESAssetRecord record)
        {
            if (record.runtimeKey != 0)
                slotByRuntimeKey[record.runtimeKey] = slot;
            if (record.enumKey != 0)
                slotByEnumKey[record.enumKey] = slot;
            if (!string.IsNullOrEmpty(record.stringKey))
                slotByStringKey[record.stringKey] = slot;
            if (!string.IsNullOrEmpty(record.guid))
                slotByGuid[record.guid] = slot;
        }
    }

    public static class ESAssetRegistry
    {
        public const int DefaultStringRuntimeKeyStart = 30000;

        private static readonly List<ESAssetRecord> records = new List<ESAssetRecord>(256);
        private static readonly ESAssetTable table = new ESAssetTable(256);
        private static int nextStringRuntimeKey = DefaultStringRuntimeKeyStart;

        public static ESAssetTable Table => table;
        public static IReadOnlyList<ESAssetRecord> Records => records;

        public static void Clear()
        {
            records.Clear();
            table.Clear();
            nextStringRuntimeKey = DefaultStringRuntimeKeyStart;
        }

        public static ESAssetRecord[] BuildFromAssetLibrary(ESAssetLibrary library, bool clearBeforeBuild = false)
        {
            if (clearBeforeBuild)
                Clear();

            if (library == null)
                return Array.Empty<ESAssetRecord>();

            library.NormalizePagesEditor();
            foreach (var book in library.GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;

                for (int i = 0; i < book.pages.Count; i++)
                {
                    RegisterAsset((ESAssetPage)book.pages[i], library.Name, book.Name);
                }
            }

            return records.ToArray();
        }

        [Obsolete("Use BuildFromAssetLibrary instead.")]
        public static ESAssetRecord[] BuildFromLibrary(ResLibrary library, bool clearBeforeBuild = false)
        {
            return BuildFromAssetLibrary(library, clearBeforeBuild);
        }

        public static ESAssetRecord[] BuildFromAssetLibraries(IReadOnlyList<ESAssetLibrary> libraries, bool clearBeforeBuild = true)
        {
            if (clearBeforeBuild)
                Clear();

            if (libraries == null)
                return records.ToArray();

            for (int i = 0; i < libraries.Count; i++)
            {
                BuildFromAssetLibrary(libraries[i], false);
            }

            return records.ToArray();
        }

        [Obsolete("Use BuildFromAssetLibraries instead.")]
        public static ESAssetRecord[] BuildFromLibraries(IReadOnlyList<ResLibrary> libraries, bool clearBeforeBuild = true)
        {
            if (clearBeforeBuild)
                Clear();

            if (libraries == null)
                return records.ToArray();

            for (int i = 0; i < libraries.Count; i++)
            {
                BuildFromAssetLibrary(libraries[i], false);
            }

            return records.ToArray();
        }

        public static bool RegisterAsset(ESAssetPage page)
        {
            return RegisterAsset(page, null, null);
        }

        public static bool RegisterAsset(ESAssetPage page, string libraryName, string bookName)
        {
            if (page == null || page.OB == null)
                return false;

            ESAssetReferKind kind = page.Kind;
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                kind = ESAssetPage.DetermineKind(page.OB);

            string stringKey = string.IsNullOrEmpty(page.StringKey) ? page.Name : page.StringKey;
            return RegisterAsset(page.OB, kind, page.EnumKey, stringKey, libraryName, bookName);
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

            string assetPath = AssetDatabase.GetAssetPath(asset);
            string guid = null;
            long localFileId = 0;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localFileId);
            if (string.IsNullOrEmpty(stringKey))
                stringKey = asset.name;

            ESAssetRecord record = new ESAssetRecord
            {
                kind = kind,
                enumKey = enumKey,
                stringKey = stringKey,
                guid = guid,
                localFileId = localFileId,
                assetPath = assetPath,
                runtimeKey = BakeRuntimeKey(enumKey, stringKey),
                assetName = asset.name,
                assetType = asset.GetType(),
                libraryName = libraryName,
                bookName = bookName
            };

            UpsertRecord(record);
            return true;
#else
            return false;
#endif
        }

        public static bool RemoveAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return false;

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].guid == guid)
                {
                    int runtimeKey = records[i].runtimeKey;
                    records.RemoveAt(i);
                    table.Remove(runtimeKey);
                    return true;
                }
            }

            return false;
        }

        public static bool RemoveAsset(ESAssetPage page)
        {
#if UNITY_EDITOR
            if (page?.OB == null)
                return false;

            string path = AssetDatabase.GetAssetPath(page.OB);
            return RemoveAsset(string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path));
#else
            return false;
#endif
        }

        public static bool RenameAsset(ESAssetRecord record, string newName)
        {
            if (record.runtimeKey == 0)
                return false;

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].runtimeKey == record.runtimeKey)
                {
                    record.assetName = newName;
                    records[i] = record;
                    table.Register(record, true);
                    return true;
                }
            }

            return false;
        }

        public static bool RenameStringKey(ESAssetRecord record, string newStringKey)
        {
            if (record.runtimeKey == 0 || string.IsNullOrEmpty(newStringKey))
                return false;

            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].runtimeKey == record.runtimeKey)
                {
                    record.stringKey = newStringKey;
                    record.runtimeKey = BakeRuntimeKey(record.enumKey, newStringKey);
                    records[i] = record;
                    table.Load(records);
                    return true;
                }
            }

            return false;
        }

        private static void UpsertRecord(ESAssetRecord record)
        {
            for (int i = 0; i < records.Count; i++)
            {
                ESAssetRecord existing = records[i];
                if (existing.runtimeKey == record.runtimeKey
                    || (!string.IsNullOrEmpty(record.guid) && existing.guid == record.guid)
                    || (!string.IsNullOrEmpty(record.stringKey) && existing.stringKey == record.stringKey))
                {
                    records[i] = record;
                    table.Register(record, true);
                    return;
                }
            }

            records.Add(record);
            table.Register(record, true);
        }

        private static int BakeRuntimeKey(int enumKey, string stringKey)
        {
            if (enumKey != 0)
                return enumKey;

            if (!string.IsNullOrEmpty(stringKey) && table.TryGetByString(stringKey, out ESAssetRecord existing))
                return existing.runtimeKey;

            return nextStringRuntimeKey++;
        }
    }
}
