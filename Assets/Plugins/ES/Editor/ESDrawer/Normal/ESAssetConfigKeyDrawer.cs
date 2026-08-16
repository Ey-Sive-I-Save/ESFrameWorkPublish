using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ES;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("ES.ContentRegistration.Editor.Tests")]

namespace ES.EditorInternal
{
    public abstract class ESAssetConfigKeyDrawerBase : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 2f;
        private const int AdvancedLineCount = 7;
        private const float PanelPadding = 6f;
        private static readonly Color PanelAccent = new Color(0.22f, 0.78f, 1f, 0.95f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ESAssetCatalogKeyPicker.Candidate current = ESAssetCatalogKeyPicker.FindCurrent(ResolveKind(), property);
            bool needsAttention = ESAssetCatalogKeyPicker.IsBoundSourceMissing(property, current)
                || ESAssetCatalogKeyPicker.HasLibraryKeyConflict(current);
            int lines = 5 + (needsAttention ? 1 : 0) + (property.isExpanded ? AdvancedLineCount : 0);
            return lines * Line + (lines - 1) * Gap + PanelPadding * 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int previousIndent = EditorGUI.indentLevel;
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.27f, 76f, 108f);

            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(position.x + 2f, position.y + 2f, Mathf.Max(0f, position.width - 4f), 2f), PanelAccent);
            position = new Rect(
                position.x + PanelPadding,
                position.y + PanelPadding,
                Mathf.Max(0f, position.width - PanelPadding * 2f),
                Mathf.Max(0f, position.height - PanelPadding * 2f));

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            ESAssetCatalogKeyPicker.Candidate current = ESAssetCatalogKeyPicker.FindCurrent(ResolveKind(), property);
            if (SynchronizeBoundKey(property, current))
            {
                property.serializedObject.Update();
                enumKey = property.FindPropertyRelative("enumKey");
                stringKey = property.FindPropertyRelative("stringKey");
                current = ESAssetCatalogKeyPicker.FindCurrent(ResolveKind(), property);
            }

            bool isBoundSourceMissing = ESAssetCatalogKeyPicker.IsBoundSourceMissing(property, current);
            bool hasLibraryKeyConflict = ESAssetCatalogKeyPicker.HasLibraryKeyConflict(current);
            bool isBoundToRegisteredSource = current != null && ESAssetCatalogKeyPicker.HasBoundIdentity(property);
            bool hasKeyConflict = current != null
                && ESAssetCatalogKeyPicker.CountKeyMatches(ResolveKind(), current.enumKey, current.stringKey) > 1;

            Rect row = NextLine(ref position);
            DrawHeader(row, ResolveTitle(property, label), "资源配置键 · " + ResolveKindDisplayName(ResolveKind()));

            row = NextLine(ref position);
            string enumLabel = isBoundToRegisteredSource ? "枚举 Key（自动同步）" : "枚举 Key（可选）";
            Rect contentRect = EditorGUI.PrefixLabel(row, new GUIContent(enumLabel,
                isBoundToRegisteredSource ? "关联资产存在时由 AssetLibrary 自动维护" : "通常不需要手填；从资产或下拉列表选择即可"));
            DrawActionRow(contentRect, out Rect selectorRect, out Rect clearRect, out Rect locateRect);
            using (new EditorGUI.DisabledScope(isBoundToRegisteredSource))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(selectorRect, enumKey, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    ClearAssetIdentity(property);
                    Apply(property);
                }
            }

            using (new EditorGUI.DisabledScope(!HasAnyValue(property)))
            {
                if (GUI.Button(clearRect, new GUIContent("×", "清空配置"), EditorStyles.miniButton))
                    Clear(property);
                if (GUI.Button(locateRect, new GUIContent("定位", "在 Project 中定位资源"), EditorStyles.miniButton))
                    Locate(property);
            }

            row = NextLine(ref position);
            Rect stringRect = EditorGUI.PrefixLabel(row, new GUIContent(
                isBoundToRegisteredSource ? "字符串 Key（自动同步）" : "字符串 Key（可选）",
                isBoundToRegisteredSource ? "关联资产存在时由 AssetLibrary 自动维护" : "需要稳定文本地址时使用；也可从项目内 Library/Catalog 选择。"));
            DrawStringSelectionRow(stringRect, out Rect stringInputRect, out Rect stringSelectRect);
            using (new EditorGUI.DisabledScope(isBoundToRegisteredSource))
            {
                EditorGUI.BeginChangeCheck();
                string editedStringKey = EditorGUI.DelayedTextField(stringInputRect, stringKey.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    stringKey.stringValue = editedStringKey ?? string.Empty;
                    ClearAssetIdentity(property);
                    Apply(property);
                }
            }
            if (GUI.Button(stringSelectRect, StringSelectContent(stringSelectRect), EditorStyles.miniButton))
                ESAssetCatalogKeyPicker.ShowMenu(stringSelectRect, ResolveKind(), property);

            row = NextLine(ref position);
            Rect objectRect = EditorGUI.PrefixLabel(row, new GUIContent("关联资产（推荐）", "优先拖入资产；未收集时可自动加入当前 Library 并同步 Key"));
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object selectedAsset = EditorGUI.ObjectField(objectRect, current != null ? ESAssetCatalogKeyPicker.ResolveAsset(current) : ESAssetCatalogKeyPicker.ResolveAsset(property),
                ESAssetCatalogKeyPicker.ResolveAssetType(ResolveKind()), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedAsset == null)
                {
                    ClearAssetIdentity(property);
                    Apply(property);
                }
                else if (ESAssetCatalogKeyPicker.TryFindByAsset(ResolveKind(), selectedAsset, out ESAssetCatalogKeyPicker.Candidate selectedCandidate)
                         && !ESAssetCatalogKeyPicker.HasLibraryKeyConflict(selectedCandidate))
                    ESAssetCatalogKeyPicker.ApplyCandidate(property, selectedCandidate);
                else if (selectedCandidate != null && ESAssetCatalogKeyPicker.HasLibraryKeyConflict(selectedCandidate))
                    Debug.LogError("[ESRes][ConfigKey] 无法同步 ConfigKey：资产在多个 Library 中配置了不同 Key，请先消除 Library 冲突。", selectedAsset);
                else if (ESAssetCatalogKeyPicker.TryOpenRegistrationWorkflow(ResolveKind(), selectedAsset, out string registrationError))
                    Debug.Log("[ESRes][ConfigKey] 已打开统一内容注册入口。提交成功后重新选择该资产以绑定 ConfigKey。", selectedAsset);
                else
                    Debug.LogWarning("[ESRes][ConfigKey] 无法打开注册入口：" + selectedAsset.name + "。" + registrationError, selectedAsset);
            }

            if (isBoundSourceMissing || hasLibraryKeyConflict)
            {
                row = NextLine(ref position);
                Rect syncLabel = EditorGUI.PrefixLabel(row, new GUIContent(
                    hasLibraryKeyConflict ? "Library Key conflict" : "Bound source missing",
                    hasLibraryKeyConflict
                        ? "The same asset has different Keys in multiple Libraries; resolve the Library conflict before synchronizing."
                        : "The bound asset is no longer in any project Library/Catalog; reselect or register it."));
                EditorGUI.HelpBox(syncLabel,
                    hasLibraryKeyConflict
                        ? "同一资产在多个 Library 中配置了不同 Key。请先统一 Library Key，ConfigKey 才会自动同步。"
                        : "已绑定的源资产不在项目内任何 Library/Catalog。请重新选择或收集该资产。",
                    MessageType.Warning);
            }

            string summary = current != null
                ? $"{(hasLibraryKeyConflict ? "⚠ Library Key 冲突 · " : (hasKeyConflict ? "⚠ Key 冲突 · " : string.Empty))}{(current.isBaked ? "已烘焙" : "已注册待烘焙")} · {current.typeDisplayName} · {current.libraryName}/{current.pageName} · Key：{ResolveCandidateKey(current)}"
                : BuildFallbackSummary(property, ResolveKind());
            property.isExpanded = EditorGUI.Foldout(NextLine(ref position), property.isExpanded, summary, true, EditorStyles.foldout);

            if (property.isExpanded)
            {
                DrawReadOnly(ref position, "GUID", property.FindPropertyRelative("guid"));
                DrawReadOnly(ref position, "子资产 FileId", property.FindPropertyRelative("localFileId"));
                DrawReadOnly(ref position, "类型", property.FindPropertyRelative("assetTypeName"));
                DrawReadOnly(ref position, "地址", property.FindPropertyRelative("address"));
                DrawReadOnly(ref position, "分组", property.FindPropertyRelative("groupName"));
                DrawReadOnly(ref position, "编辑器路径", property.FindPropertyRelative("editorPath"));
                Rect flags = NextLine(ref position);
                using (new EditorGUI.DisabledScope(true))
                {
                    float half = (flags.width - Gap) * 0.5f;
                    EditorGUI.PropertyField(new Rect(flags.x, flags.y, half, flags.height), property.FindPropertyRelative("editorOnly"), new GUIContent("仅编辑器"));
                    EditorGUI.PropertyField(new Rect(flags.x + half + Gap, flags.y, half, flags.height), property.FindPropertyRelative("alwaysLoaded"), new GUIContent("常驻加载"));
                }
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUI.indentLevel = previousIndent;
            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();
        protected abstract ESAssetReferKind ResolveKind();

        private static void DrawHeader(Rect rect, string title, string subtitle)
        {
            Rect marker = new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f);
            EditorGUI.DrawRect(marker, PanelAccent);
            Rect titleRect = new Rect(marker.xMax + 5f, rect.y, Mathf.Max(0f, rect.width - marker.width - 5f), rect.height);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = PanelAccent;
            EditorGUI.LabelField(titleRect, title + "  ·  " + subtitle, style);
        }

        private static string ResolveTitle(SerializedProperty property, GUIContent label)
        {
            if (HasVisibleLabel(label)) return label.text;
            return property != null && !string.IsNullOrWhiteSpace(property.displayName)
                ? property.displayName
                : "资源 Key";
        }

        internal static string ResolveKindDisplayName(ESAssetReferKind kind)
        {
            System.Reflection.FieldInfo field = typeof(ESAssetReferKind).GetField(kind.ToString());
            var attribute = field != null
                ? Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute
                : null;
            return attribute != null && !string.IsNullOrWhiteSpace(attribute.displayName)
                ? attribute.displayName
                : kind.ToString();
        }

        private static void DrawStringSelectionRow(Rect rect, out Rect input, out Rect select)
        {
            float selectWidth = rect.width >= 150f ? 42f : 24f;
            select = new Rect(rect.xMax - selectWidth, rect.y, selectWidth, rect.height);
            input = new Rect(rect.x, rect.y, Mathf.Max(20f, select.x - Gap - rect.x), rect.height);
        }

        private static GUIContent StringSelectContent(Rect rect)
        {
            return new GUIContent(rect.width >= 40f ? "选择" : "▼", "从 Catalog 中选择字符串 Key 和对应资源");
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect rect = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return rect;
        }

        private static bool HasVisibleLabel(GUIContent label)
        {
            return label != null && !string.IsNullOrWhiteSpace(label.text);
        }

        private static void DrawActionRow(Rect rect, out Rect selector, out Rect clear, out Rect locate)
        {
            float clearWidth = 22f;
            float locateWidth = rect.width >= 180f ? 42f : 24f;
            locate = new Rect(rect.xMax - locateWidth, rect.y, locateWidth, rect.height);
            clear = new Rect(locate.x - Gap - clearWidth, rect.y, clearWidth, rect.height);
            selector = new Rect(rect.x, rect.y, Mathf.Max(20f, clear.x - Gap - rect.x), rect.height);
        }

        private static void DrawReadOnly(ref Rect position, string label, SerializedProperty value)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(NextLine(ref position), value, new GUIContent(label));
        }

        private static bool HasAnyValue(SerializedProperty property)
        {
            return property.FindPropertyRelative("enumKey").intValue != 0
                || !string.IsNullOrEmpty(property.FindPropertyRelative("stringKey").stringValue)
                || !string.IsNullOrEmpty(property.FindPropertyRelative("guid").stringValue);
        }

        private static string ResolveCurrentAssetName(SerializedProperty property)
        {
            string path = property.FindPropertyRelative("editorPath").stringValue;
            return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
        }

        private static string BuildFallbackSummary(SerializedProperty property, ESAssetReferKind kind)
        {
            int enumValue = property.FindPropertyRelative("enumKey").intValue;
            string key = property.FindPropertyRelative("stringKey").stringValue;
            if (enumValue != 0)
                return $"枚举模式 · {ResolveKindDisplayName(kind)} · Enum={enumValue} · 未匹配 Catalog";
            if (!string.IsNullOrWhiteSpace(key))
                return $"字符串模式 · {ResolveKindDisplayName(kind)} · {key} · 未匹配 Catalog";
            return "未配置 · 可使用枚举、字符串或拖入资产";
        }

        private static string ResolveCandidateKey(ESAssetCatalogKeyPicker.Candidate candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate.stringKey)
                ? candidate.stringKey
                : candidate.enumKey.ToString();
        }

        private static void Clear(SerializedProperty property)
        {
            property.FindPropertyRelative("enumKey").intValue = 0;
            property.FindPropertyRelative("stringKey").stringValue = string.Empty;
            ClearAssetIdentity(property);
            Apply(property);
        }

        private static void ClearAssetIdentity(SerializedProperty property)
        {
            property.FindPropertyRelative("guid").stringValue = string.Empty;
            property.FindPropertyRelative("localFileId").longValue = 0;
            property.FindPropertyRelative("assetTypeName").stringValue = string.Empty;
            property.FindPropertyRelative("address").stringValue = string.Empty;
            property.FindPropertyRelative("groupName").stringValue = string.Empty;
            property.FindPropertyRelative("editorPath").stringValue = string.Empty;
            property.FindPropertyRelative("editorOnly").boolValue = false;
            property.FindPropertyRelative("alwaysLoaded").boolValue = false;
        }

        private static bool SynchronizeBoundKey(SerializedProperty property, ESAssetCatalogKeyPicker.Candidate source)
        {
            if (property == null || property.hasMultipleDifferentValues || source == null
                || ESAssetCatalogKeyPicker.HasLibraryKeyConflict(source)
                || !ESAssetCatalogKeyPicker.IsStale(property, source))
                return false;

            ESAssetCatalogKeyPicker.ApplyCandidate(property, source);
            return true;
        }

        private static void Locate(SerializedProperty property)
        {
            UnityEngine.Object asset = ESAssetCatalogKeyPicker.ResolveAsset(property);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        internal static void Apply(SerializedProperty property)
        {
            property.serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
                if (target != null) EditorUtility.SetDirty(target);
        }
    }

    /// <summary>合并项目内全部 AssetLibrary、编辑器注册表与阶段① Catalog，供业务配置安全选择键；不修改 RuntimeKey。</summary>
    internal static class ESAssetCatalogKeyPicker
    {
        internal sealed class Candidate
        {
            public ESAssetReferKind kind;
            public int enumKey;
            public string stringKey;
            public string guid;
            public long localFileId;
            public string assetPath;
            public string assetTypeName;
            public string typeDisplayName;
            public string libraryName;
            public string pageName;
            public string assetName;
            public string menuLabel;
            public UnityEngine.Object asset;
            public bool isBaked;
            public bool isLibrarySource;
            public bool hasLibraryKeyConflict;
        }

        private sealed class CandidateSource
        {
            private readonly Dictionary<ESAssetReferKind, List<Candidate>> candidatesByKind = new Dictionary<ESAssetReferKind, List<Candidate>>();

            public IEnumerable<ESAssetReferKind> Kinds => candidatesByKind.Keys;

            public void Add(Candidate candidate)
            {
                if (candidate == null)
                    return;
                if (!candidatesByKind.TryGetValue(candidate.kind, out List<Candidate> candidates))
                    candidatesByKind.Add(candidate.kind, candidates = new List<Candidate>());
                candidates.Add(candidate);
            }

            public IReadOnlyList<Candidate> Get(ESAssetReferKind kind)
                => candidatesByKind.TryGetValue(kind, out List<Candidate> candidates)
                    ? candidates
                    : Array.Empty<Candidate>();
        }

        private static readonly Dictionary<ESAssetReferKind, List<Candidate>> CandidatesByKind = new Dictionary<ESAssetReferKind, List<Candidate>>();
        private static readonly Dictionary<string, CandidateSource> LibraryCandidatesByPath = new Dictionary<string, CandidateSource>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CandidateSource> CatalogCandidatesByPath = new Dictionary<string, CandidateSource>(StringComparer.Ordinal);
        private static readonly Dictionary<ESAssetReferKind, Dictionary<string, Candidate>> CandidatesByIdentity = new Dictionary<ESAssetReferKind, Dictionary<string, Candidate>>();
        private static readonly Dictionary<ESAssetReferKind, Dictionary<int, List<Candidate>>> CandidatesByEnumKey = new Dictionary<ESAssetReferKind, Dictionary<int, List<Candidate>>>();
        private static readonly Dictionary<ESAssetReferKind, Dictionary<string, List<Candidate>>> CandidatesByStringKey = new Dictionary<ESAssetReferKind, Dictionary<string, List<Candidate>>>();
        private static readonly Dictionary<string, Hash128> LibraryDependencyHashes = new Dictionary<string, Hash128>(StringComparer.Ordinal);
        private static readonly HashSet<string> PendingLibraryPaths = new HashSet<string>(StringComparer.Ordinal);
        private static bool loaded;
        private static bool libraryRefreshScheduled;
        private static int loadedRegistryVersion = -1;
        private static int fullReloadCount;
        private static int incrementalLibraryReloadCount;
        private static int catalogReloadCount;

        internal static int FullReloadCount => fullReloadCount;
        internal static int IncrementalLibraryReloadCount => incrementalLibraryReloadCount;
        internal static int CatalogReloadCount => catalogReloadCount;

        public static Candidate FindCurrent(ESAssetReferKind kind, SerializedProperty property)
        {
            EnsureLoaded();
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates)) return null;
            string guid = property.FindPropertyRelative("guid").stringValue;
            long localFileId = NormalizeStoredLocalFileId(
                guid,
                property.FindPropertyRelative("localFileId").longValue,
                property.FindPropertyRelative("editorPath").stringValue);
            int enumKey = property.FindPropertyRelative("enumKey").intValue;
            string stringKey = property.FindPropertyRelative("stringKey").stringValue;
            if (!string.IsNullOrEmpty(guid))
            {
                Candidate identityMatch = null;
                if (CandidatesByIdentity.TryGetValue(kind, out Dictionary<string, Candidate> identities))
                    identities.TryGetValue(BuildIdentityKey(guid, localFileId), out identityMatch);
                // 已绑定源身份时绝不能再按 Key 猜测别的资产；源资产移除/未注册必须
                // 明确暴露为失效引用，不能静默换绑。
                return identityMatch;
            }
            return FindByKey(kind, enumKey, stringKey);
        }

        public static bool HasBoundIdentity(SerializedProperty property)
            => property != null && !string.IsNullOrEmpty(property.FindPropertyRelative("guid")?.stringValue);

        public static bool IsBoundSourceMissing(SerializedProperty property, Candidate candidate)
            => HasBoundIdentity(property) && candidate == null;

        public static bool HasLibraryKeyConflict(Candidate candidate)
            => candidate != null && candidate.hasLibraryKeyConflict;

        public static bool IsStale(SerializedProperty property, Candidate candidate)
        {
            if (property == null || candidate == null)
                return false;
            return !ESConfigKeyMatch.Matches(
                property.FindPropertyRelative("enumKey").intValue,
                property.FindPropertyRelative("stringKey").stringValue,
                candidate.enumKey,
                candidate.stringKey);
        }

        public static UnityEngine.Object ResolveAsset(Candidate candidate)
        {
            if (candidate == null) return null;
            return LoadAsset(candidate);
        }

        public static bool TryFindByAsset(ESAssetReferKind kind, UnityEngine.Object asset, out Candidate candidate)
        {
            candidate = null;
            if (asset == null) return false;
            EnsureLoaded();
            if (!ESAssetPage.TryGetAssetIdentityEditor(asset, out string guid, out long localFileId)
                || !CandidatesByIdentity.TryGetValue(kind, out Dictionary<string, Candidate> identities)) return false;
            identities.TryGetValue(BuildIdentityKey(guid, localFileId), out candidate);
            return candidate != null;
        }

        public static bool TryFindByKey(ESAssetReferKind kind, int enumKey, string stringKey, out Candidate candidate)
        {
            EnsureLoaded();
            candidate = null;
            candidate = FindByKey(kind, enumKey, stringKey);
            return candidate != null;
        }

        public static int CountKeyMatches(ESAssetReferKind kind, int enumKey, string stringKey)
        {
            EnsureLoaded();
            int count = 0;
            List<Candidate> enumCandidates = GetEnumCandidates(kind, enumKey);
            List<Candidate> stringCandidates = GetStringCandidates(kind, stringKey);
            if (enumCandidates != null)
                for (int i = 0; i < enumCandidates.Count; i++)
                    if (ESConfigKeyMatch.Matches(enumKey, stringKey, enumCandidates[i].enumKey, enumCandidates[i].stringKey)) count++;
            if (stringCandidates != null)
                for (int i = 0; i < stringCandidates.Count; i++)
                    if ((enumCandidates == null || !enumCandidates.Contains(stringCandidates[i]))
                        && ESConfigKeyMatch.Matches(enumKey, stringKey, stringCandidates[i].enumKey, stringCandidates[i].stringKey)) count++;
            return count;
        }

        public static bool TryOpenRegistrationWorkflow(
            ESAssetReferKind expectedKind,
            UnityEngine.Object asset,
            out string error)
        {
            error = string.Empty;
            if (asset == null)
            {
                error = "资产为空。";
                return false;
            }
            ESAssetReferKind actualKind = ESAssetPage.DetermineKind(asset);
            if (actualKind != expectedKind)
            {
                error = "资产类型与 ConfigKey 类型不匹配：" + actualKind + " != " + expectedKind;
                return false;
            }

            ESAssetLibrary library = ESGlobalResToolsSupportConfig.ActiveCollectLibrary;
            if (library == null)
            {
                error = "尚未设置当前收集 Library，请在资源窗口设置后重试。";
                return false;
            }

            ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(asset, library);
            return true;
        }

        public static Type ResolveAssetType(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return typeof(GameObject);
                case ESAssetReferKind.Scene: return typeof(SceneAsset);
                case ESAssetReferKind.Sprite: return typeof(Sprite);
                case ESAssetReferKind.SpriteAtlas: return typeof(UnityEngine.U2D.SpriteAtlas);
                case ESAssetReferKind.Texture:
                case ESAssetReferKind.Texture2D: return typeof(Texture);
                case ESAssetReferKind.Material: return typeof(Material);
                case ESAssetReferKind.Mesh: return typeof(Mesh);
                case ESAssetReferKind.AnimationClip: return typeof(AnimationClip);
                case ESAssetReferKind.AnimatorController: return typeof(RuntimeAnimatorController);
                case ESAssetReferKind.Avatar: return typeof(Avatar);
                case ESAssetReferKind.AudioClip: return typeof(AudioClip);
                case ESAssetReferKind.VideoClip: return typeof(UnityEngine.Video.VideoClip);
                case ESAssetReferKind.TerrainData: return typeof(TerrainData);
                case ESAssetReferKind.Raw: return typeof(TextAsset);
                case ESAssetReferKind.ScriptableObject: return typeof(ScriptableObject);
                case ESAssetReferKind.PlayableAsset: return typeof(UnityEngine.Playables.PlayableAsset);
                case ESAssetReferKind.TimelineAsset: return typeof(UnityEngine.Playables.PlayableAsset);
                default: return typeof(UnityEngine.Object);
            }
        }

        public static void ShowMenu(Rect position, ESAssetReferKind kind, SerializedProperty property)
        {
            EnsureLoaded();
            var entries = new List<ESSearchDropdown.Entry>();
            string currentGuid = property.FindPropertyRelative("guid")?.stringValue ?? string.Empty;
            long currentLocalFileId = NormalizeStoredLocalFileId(
                currentGuid,
                property.FindPropertyRelative("localFileId")?.longValue ?? 0,
                property.FindPropertyRelative("editorPath")?.stringValue);
            entries.Add(ESSearchDropdown.Entry.Item("清空当前配置", () => ClearCandidate(property), "操作"));
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates) || candidates.Count == 0)
                entries.Add(ESSearchDropdown.Entry.Disabled(
                    "没有可选资源，请先收集到 Library（无需先烘焙）"));
            else
                foreach (Candidate candidate in candidates)
                {
                    Candidate captured = candidate;
                    string groupPath = string.Join("/", new[] { candidate.libraryName, candidate.pageName }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    string key = string.IsNullOrWhiteSpace(candidate.stringKey)
                        ? "Enum " + candidate.enumKey
                        : candidate.stringKey;
                    if (HasLibraryKeyConflict(candidate))
                    {
                        entries.Add(ESSearchDropdown.Entry.Disabled(
                            "⚠ " + (string.IsNullOrWhiteSpace(candidate.assetName) ? "<未命名资产>" : candidate.assetName) + "（Library Key 冲突）",
                            groupPath,
                            "同一资产在多个 Library 中配置了不同 Key，请先统一 Library Key。"));
                        continue;
                    }
                    entries.Add(ESSearchDropdown.Entry.Item(
                        string.IsNullOrWhiteSpace(candidate.assetName) ? "<未命名资产>" : candidate.assetName,
                        () => ApplyCandidate(property, captured),
                        groupPath,
                        null,
                        subtitle: candidate.typeDisplayName + " · " + key,
                        tooltip: candidate.assetPath,
                        badge: candidate.isBaked ? "已烘焙" : "已收集",
                        selected: candidate.guid == currentGuid && candidate.localFileId == currentLocalFileId));
                }
            ESSearchDropdown.Open(position, "选择 " + ESAssetConfigKeyDrawerBase.ResolveKindDisplayName(kind) + " 资源 / Key", entries);
        }

        public static UnityEngine.Object ResolveAsset(SerializedProperty property)
        {
            string guid = property.FindPropertyRelative("guid").stringValue;
            long localFileId = NormalizeStoredLocalFileId(
                guid,
                property.FindPropertyRelative("localFileId").longValue,
                property.FindPropertyRelative("editorPath").stringValue);
            string path = !string.IsNullOrEmpty(guid) ? AssetDatabase.GUIDToAssetPath(guid) : property.FindPropertyRelative("editorPath").stringValue;
            if (string.IsNullOrEmpty(path)) return null;
            if (localFileId == 0) return AssetDatabase.LoadMainAssetAtPath(path);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long fileId)
                    && assetGuid == guid && fileId == localFileId) return asset;
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        public static void Invalidate()
        {
            loaded = false;
            loadedRegistryVersion = -1;
            PendingLibraryPaths.Clear();
            if (libraryRefreshScheduled)
                EditorApplication.delayCall -= FlushQueuedLibraryChanges;
            libraryRefreshScheduled = false;
        }

        internal static void NotifyLibraryChanged(string libraryPath)
        {
            if (!loaded || string.IsNullOrEmpty(libraryPath))
                return;

            // A synchronous registration refresh supersedes an OnValidate/import notification
            // already queued for the same Library path.
            PendingLibraryPaths.Remove(libraryPath);
            HashSet<ESAssetReferKind> affectedKinds = RefreshLibrarySource(libraryPath);
            RefreshRegistrySource();
            RebuildKinds(affectedKinds);
            incrementalLibraryReloadCount++;
        }

        internal static void QueueLibraryChanged(string libraryPath)
        {
            if (!loaded || string.IsNullOrEmpty(libraryPath))
                return;
            PendingLibraryPaths.Add(libraryPath);
            if (libraryRefreshScheduled)
                return;
            libraryRefreshScheduled = true;
            EditorApplication.delayCall += FlushQueuedLibraryChanges;
        }

        private static void FlushQueuedLibraryChanges()
        {
            EditorApplication.delayCall -= FlushQueuedLibraryChanges;
            libraryRefreshScheduled = false;
            if (!loaded || PendingLibraryPaths.Count == 0)
            {
                PendingLibraryPaths.Clear();
                return;
            }

            var affectedKinds = new HashSet<ESAssetReferKind>();
            bool refreshedAny = false;
            foreach (string path in PendingLibraryPaths)
            {
                if (!ShouldRefreshQueuedLibrary(path))
                    continue;
                affectedKinds.UnionWith(RefreshLibrarySource(path));
                refreshedAny = true;
            }
            PendingLibraryPaths.Clear();
            if (!refreshedAny)
                return;
            RefreshRegistrySource();
            RebuildKinds(affectedKinds);
            incrementalLibraryReloadCount++;
        }

        private static bool ShouldRefreshQueuedLibrary(string libraryPath)
        {
            if (!LibraryCandidatesByPath.ContainsKey(libraryPath))
                return true;
            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
            if (library == null || EditorUtility.IsDirty(library))
                return true;
            return !LibraryDependencyHashes.TryGetValue(libraryPath, out Hash128 indexedHash)
                   || indexedHash != AssetDatabase.GetAssetDependencyHash(libraryPath);
        }

        internal static void NotifyAssetPathChanged(string assetPath)
        {
            if (!loaded || string.IsNullOrEmpty(assetPath))
                return;
            if (!LibraryCandidatesByPath.ContainsKey(assetPath)
                && AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(ESAssetLibrary))
                return;
            Hash128 currentHash = AssetDatabase.GetAssetDependencyHash(assetPath);
            if (LibraryDependencyHashes.TryGetValue(assetPath, out Hash128 indexedHash)
                && indexedHash == currentHash)
                return;
            QueueLibraryChanged(assetPath);
        }

        internal static void NotifyCatalogsChanged()
        {
            if (!loaded)
                return;

            RefreshCatalogSources();
            RebuildAllIndexes();
            catalogReloadCount++;
        }

        public static void RefreshForValidation()
        {
            Invalidate();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (!loaded)
            {
                Reload();
                return;
            }
            if (loadedRegistryVersion != ESAssetRegistry.Version)
            {
                RefreshRegistrySource();
                RebuildAllIndexes();
            }
        }

        private static void Reload()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            LibraryCandidatesByPath.Clear();
            LibraryDependencyHashes.Clear();
            CatalogCandidatesByPath.Clear();
            foreach (string libraryPath in AssetDatabase.FindAssets("t:ESAssetLibrary")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(path => !string.IsNullOrEmpty(path))
                         .OrderBy(path => path, StringComparer.Ordinal))
                RefreshLibrarySource(libraryPath);
            RefreshRegistrySource();
            RefreshCatalogSources();
            RebuildAllIndexes();
            loaded = true;
            loadedRegistryVersion = ESAssetRegistry.Version;
            fullReloadCount++;
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= 200)
                Debug.LogWarning("[ESRes][ConfigKey] 编辑器候选索引冷建耗时 " + stopwatch.ElapsedMilliseconds + "ms；日常查询将复用缓存。Library=" + LibraryCandidatesByPath.Count + "，Catalog=" + CatalogCandidatesByPath.Count + "，候选=" + CandidatesByKind.Values.Sum(items => items.Count));
            else if (stopwatch.ElapsedMilliseconds >= 50)
                Debug.Log("[ESRes][ConfigKey] 编辑器候选索引冷建耗时 " + stopwatch.ElapsedMilliseconds + "ms；后续按注册/Bake 结果增量更新。", null);
        }

        private static void RefreshRegistrySource()
        {
            const string registrySource = "<registry>";
            var registryCandidates = new CandidateSource();
            foreach (ESAssetPage page in ESAssetRegistry.Pages)
            {
                if (page == null || page.Kind == ESAssetReferKind.None || page.Kind == ESAssetReferKind.Other)
                    continue;
                if (page.EnumKey == 0 && string.IsNullOrWhiteSpace(page.EffectiveStringKey))
                    continue;
                string assetPath = !string.IsNullOrWhiteSpace(page.AssetPath)
                    ? page.AssetPath
                    : AssetDatabase.GetAssetPath(page.OB);
                registryCandidates.Add(new Candidate
                {
                    kind = page.Kind,
                    enumKey = page.EnumKey,
                    stringKey = page.EffectiveStringKey,
                    guid = page.AssetGuid,
                    localFileId = page.LocalFileId,
                    assetPath = assetPath,
                    assetTypeName = page.AssetTypeName,
                    typeDisplayName = ResolveTypeDisplayName(page.AssetTypeName, page.Kind),
                    libraryName = string.IsNullOrWhiteSpace(page.SourceLibrary) ? "Registry补充" : page.SourceLibrary,
                    pageName = string.IsNullOrWhiteSpace(page.SourceBook) ? "未分组" : page.SourceBook,
                    assetName = page.OB != null ? page.OB.name : page.Name,
                    isBaked = false,
                    isLibrarySource = true,
                    asset = page.OB
                });
            }
            LibraryCandidatesByPath[registrySource] = registryCandidates;
            loadedRegistryVersion = ESAssetRegistry.Version;
        }

        private static void RefreshCatalogSources()
        {
            CatalogCandidatesByPath.Clear();
            if (!Directory.Exists(ESAssetPipelineIO.BakeRoot))
                return;

            foreach (string folder in Directory.EnumerateDirectories(ESAssetPipelineIO.BakeRoot).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (IsRecoveryCatalogDirectory(folder))
                    continue;
                string path = Path.Combine(folder, ESAssetPipelineIO.CatalogFileName);
                if (!File.Exists(path))
                    continue;
                ESAssetLibraryCatalog catalog;
                try { catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path); }
                catch { continue; }
                if (catalog == null || catalog.errors.Count > 0) continue;
                var catalogCandidates = new CandidateSource();
                foreach (ESAssetCatalogEntry entry in catalog.assets.Where(item => item != null && item.isBusinessAsset))
                {
                    if (!Enum.TryParse(entry.kind, out ESAssetReferKind kind) || kind == ESAssetReferKind.None) continue;
                    if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey)) continue;
                    catalogCandidates.Add(new Candidate
                    {
                        kind = kind,
                        enumKey = entry.enumKey,
                        stringKey = entry.stringKey,
                        guid = entry.identity.guid,
                        localFileId = entry.identity.localFileId,
                        assetPath = entry.assetPath,
                        assetTypeName = entry.assetTypeName,
                        typeDisplayName = ResolveTypeDisplayName(entry.assetTypeName, kind),
                        libraryName = entry.libraryName,
                        pageName = entry.pageName,
                        assetName = ResolveAssetName(entry),
                        isBaked = true
                    });
                }
                CatalogCandidatesByPath[path] = catalogCandidates;
            }
        }

        private static void RebuildAllIndexes()
        {
            var kinds = new HashSet<ESAssetReferKind>(CandidatesByKind.Keys);
            foreach (CandidateSource source in LibraryCandidatesByPath.Values) kinds.UnionWith(source.Kinds);
            foreach (CandidateSource source in CatalogCandidatesByPath.Values) kinds.UnionWith(source.Kinds);
            RebuildKinds(kinds);
        }

        private static void RebuildKinds(IEnumerable<ESAssetReferKind> kinds)
        {
            if (kinds == null)
                return;
            foreach (ESAssetReferKind kind in kinds.Distinct())
            {
                CandidatesByKind.Remove(kind);
                CandidatesByIdentity.Remove(kind);
                CandidatesByEnumKey.Remove(kind);
                CandidatesByStringKey.Remove(kind);
                var mergeIndexes = new Dictionary<ESAssetReferKind, Dictionary<string, Candidate>>();

                foreach (KeyValuePair<string, CandidateSource> source in LibraryCandidatesByPath
                             .Where(pair => !string.Equals(pair.Key, "<registry>", StringComparison.Ordinal))
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    foreach (Candidate candidate in source.Value.Get(kind))
                        AddOrReplaceCandidate(kind, candidate, false, mergeIndexes);
                if (LibraryCandidatesByPath.TryGetValue("<registry>", out CandidateSource registryCandidates))
                    foreach (Candidate candidate in registryCandidates.Get(kind))
                        AddRegistrySupplement(candidate, mergeIndexes);
                foreach (KeyValuePair<string, CandidateSource> source in CatalogCandidatesByPath.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    foreach (Candidate candidate in source.Value.Get(kind))
                        AddOrReplaceCandidate(kind, candidate, true, mergeIndexes);

                if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                    continue;
                foreach (Candidate candidate in candidates)
                    candidate.menuLabel = BuildMenuLabel(candidate);
                candidates.Sort((left, right) => string.CompareOrdinal(left.menuLabel, right.menuLabel));

                var identities = new Dictionary<string, Candidate>(StringComparer.Ordinal);
                var enumKeys = new Dictionary<int, List<Candidate>>();
                var stringKeys = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
                foreach (Candidate candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate.guid))
                        identities[BuildIdentityKey(candidate.guid, candidate.localFileId)] = candidate;
                    if (candidate.enumKey != 0)
                        AddLookup(enumKeys, candidate.enumKey, candidate);
                    if (!string.IsNullOrEmpty(candidate.stringKey))
                        AddLookup(stringKeys, candidate.stringKey, candidate);
                }
                CandidatesByIdentity[kind] = identities;
                CandidatesByEnumKey[kind] = enumKeys;
                CandidatesByStringKey[kind] = stringKeys;
            }
        }

        private static HashSet<ESAssetReferKind> RefreshLibrarySource(string libraryPath)
        {
            var affectedKinds = LibraryCandidatesByPath.TryGetValue(libraryPath, out CandidateSource previous)
                ? new HashSet<ESAssetReferKind>(previous.Kinds)
                : new HashSet<ESAssetReferKind>();
            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
            if (library == null)
            {
                LibraryCandidatesByPath.Remove(libraryPath);
                LibraryDependencyHashes.Remove(libraryPath);
                return affectedKinds;
            }

            string libraryName = string.IsNullOrWhiteSpace(library.Name)
                ? Path.GetFileNameWithoutExtension(libraryPath)
                : library.Name;
            var sourceCandidates = new CandidateSource();
            foreach (ESAssetBook book in library.GetAllUseableBooks())
            {
                if (book?.pages == null)
                    continue;
                foreach (ESAssetPage page in book.pages)
                    AddLibraryPageCandidate(sourceCandidates, libraryName, book.Name, page);
            }
            LibraryCandidatesByPath[libraryPath] = sourceCandidates;
            LibraryDependencyHashes[libraryPath] = AssetDatabase.GetAssetDependencyHash(libraryPath);
            affectedKinds.UnionWith(sourceCandidates.Kinds);
            return affectedKinds;
        }

        private static void AddLibraryPageCandidate(CandidateSource destination, string libraryName, string bookName, ESAssetPage page)
        {
            if (page?.OB == null)
                return;

            ESAssetReferKind kind = ESAssetPage.DetermineKind(page.OB);
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                return;

            string stringKey = page.EffectiveStringKey;
            if (page.EnumKey == 0 && string.IsNullOrWhiteSpace(stringKey))
                return;

            ESAssetPage.TryGetAssetIdentityEditor(page.OB, out string guid, out long localFileId);
            string assetPath = AssetDatabase.GetAssetPath(page.OB);
            destination.Add(new Candidate
            {
                kind = kind,
                enumKey = page.EnumKey,
                stringKey = stringKey,
                guid = guid,
                localFileId = localFileId,
                assetPath = assetPath,
                assetTypeName = page.OB.GetType().FullName,
                typeDisplayName = ResolveTypeDisplayName(page.OB.GetType().FullName, kind),
                libraryName = libraryName,
                pageName = string.IsNullOrWhiteSpace(bookName) ? "未分组" : bookName,
                assetName = page.OB.name,
                isBaked = false,
                isLibrarySource = true,
                asset = page.OB
            });
        }

        private static long NormalizeStoredLocalFileId(string guid, long localFileId, string assetPath)
        {
            if (localFileId == 0 || string.IsNullOrEmpty(guid))
                return localFileId;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                path = assetPath;
            UnityEngine.Object mainAsset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset == null || AssetDatabase.IsSubAsset(mainAsset))
                return localFileId;

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mainAsset, out string mainGuid, out long unityLocalFileId)
                && string.Equals(mainGuid, guid, StringComparison.Ordinal)
                && unityLocalFileId == localFileId)
                return 0;

            return localFileId;
        }

        private static void NormalizeCandidateMainAssetIdentity(Candidate candidate)
        {
            if (candidate?.asset == null)
                return;

            if (!AssetDatabase.IsSubAsset(candidate.asset))
            {
                candidate.localFileId = 0;
                return;
            }

            candidate.localFileId = NormalizeStoredLocalFileId(
                candidate.guid,
                candidate.localFileId,
                candidate.assetPath);
        }

        private static void AddOrReplaceCandidate(
            ESAssetReferKind kind,
            Candidate sourceCandidate,
            bool preferNew,
            Dictionary<ESAssetReferKind, Dictionary<string, Candidate>> mergeIndexes)
        {
            if (sourceCandidate == null)
                return;
            Candidate candidate = CloneForProjection(sourceCandidate);
            NormalizeCandidateMainAssetIdentity(candidate);
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                CandidatesByKind.Add(kind, candidates = new List<Candidate>());
            if (!mergeIndexes.TryGetValue(kind, out Dictionary<string, Candidate> identities))
                mergeIndexes.Add(kind, identities = new Dictionary<string, Candidate>(StringComparer.Ordinal));
            string identity = BuildIdentityKey(candidate.guid, candidate.localFileId);
            if (string.IsNullOrEmpty(candidate.guid) || !identities.TryGetValue(identity, out Candidate existing))
            {
                candidates.Add(candidate);
                if (!string.IsNullOrEmpty(candidate.guid)) identities[identity] = candidate;
                return;
            }
            if (candidate.isLibrarySource && existing.isLibrarySource
                && !ESConfigKeyMatch.Matches(existing.enumKey, existing.stringKey, candidate.enumKey, candidate.stringKey))
            {
                existing.hasLibraryKeyConflict = true;
                return;
            }

            if (preferNew && !existing.isLibrarySource)
            {
                int index = candidates.IndexOf(existing);
                if (index >= 0) candidates[index] = candidate;
                identities[identity] = candidate;
            }
            else if (preferNew)
                existing.isBaked |= candidate.isBaked;
        }

        private static void AddRegistrySupplement(
            Candidate candidate,
            Dictionary<ESAssetReferKind, Dictionary<string, Candidate>> mergeIndexes)
        {
            if (candidate == null)
                return;
            Candidate projection = CloneForProjection(candidate);
            NormalizeCandidateMainAssetIdentity(projection);
            if (mergeIndexes.TryGetValue(projection.kind, out Dictionary<string, Candidate> identities)
                && !string.IsNullOrEmpty(projection.guid)
                && identities.ContainsKey(BuildIdentityKey(projection.guid, projection.localFileId)))
                return;
            AddOrReplaceCandidate(projection.kind, projection, false, mergeIndexes);
        }

        private static Candidate CloneForProjection(Candidate source)
        {
            return new Candidate
            {
                kind = source.kind,
                enumKey = source.enumKey,
                stringKey = source.stringKey,
                guid = source.guid,
                localFileId = source.localFileId,
                assetPath = source.assetPath,
                assetTypeName = source.assetTypeName,
                typeDisplayName = source.typeDisplayName,
                libraryName = source.libraryName,
                pageName = source.pageName,
                assetName = source.assetName,
                menuLabel = source.menuLabel,
                asset = source.asset,
                isBaked = source.isBaked,
                isLibrarySource = source.isLibrarySource,
                hasLibraryKeyConflict = false
            };
        }

        private static bool IsRecoveryCatalogDirectory(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return false;
            string fullPath;
            try { fullPath = Path.GetFullPath(folder); }
            catch { return true; }
            string recoveryRoot = Path.GetFullPath(Path.Combine(ESAssetPipelineIO.BakeRoot, ".Recovery"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(recoveryRoot, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(recoveryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(recoveryRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static Candidate FindByKey(ESAssetReferKind kind, int enumKey, string stringKey)
        {
            List<Candidate> enumCandidates = GetEnumCandidates(kind, enumKey);
            if (enumCandidates != null)
                for (int i = 0; i < enumCandidates.Count; i++)
                    if (ESConfigKeyMatch.Matches(enumKey, stringKey, enumCandidates[i].enumKey, enumCandidates[i].stringKey)) return enumCandidates[i];
            List<Candidate> stringCandidates = GetStringCandidates(kind, stringKey);
            if (stringCandidates != null)
                for (int i = 0; i < stringCandidates.Count; i++)
                    if (ESConfigKeyMatch.Matches(enumKey, stringKey, stringCandidates[i].enumKey, stringCandidates[i].stringKey)) return stringCandidates[i];
            return null;
        }

        private static List<Candidate> GetEnumCandidates(ESAssetReferKind kind, int enumKey)
        {
            if (enumKey != 0 && CandidatesByEnumKey.TryGetValue(kind, out Dictionary<int, List<Candidate>> enums)
                             && enums.TryGetValue(enumKey, out List<Candidate> enumMatches))
                return enumMatches;
            return null;
        }

        private static List<Candidate> GetStringCandidates(ESAssetReferKind kind, string stringKey)
        {
            if (!string.IsNullOrEmpty(stringKey)
                && CandidatesByStringKey.TryGetValue(kind, out Dictionary<string, List<Candidate>> strings)
                && strings.TryGetValue(stringKey, out List<Candidate> stringMatches))
                return stringMatches;
            return null;
        }

        private static string BuildIdentityKey(string guid, long localFileId)
            => string.IsNullOrEmpty(guid) ? string.Empty : guid + ":" + localFileId;

        private static void AddLookup<TKey>(Dictionary<TKey, List<Candidate>> lookup, TKey key, Candidate candidate)
        {
            if (!lookup.TryGetValue(key, out List<Candidate> values))
                lookup.Add(key, values = new List<Candidate>(1));
            values.Add(candidate);
        }

        private static string ResolveAssetName(ESAssetCatalogEntry entry)
        {
            return !string.IsNullOrEmpty(entry.subAssetName) ? entry.subAssetName : Path.GetFileNameWithoutExtension(entry.assetPath);
        }

        private static string ResolveTypeDisplayName(string typeName, ESAssetReferKind kind)
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                int index = typeName.LastIndexOf('.');
                return index >= 0 ? typeName.Substring(index + 1) : typeName;
            }
            return kind.ToString();
        }

        private static string BuildMenuLabel(Candidate candidate)
        {
            string relativePath = candidate.assetPath != null && candidate.assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? candidate.assetPath.Substring(7)
                : candidate.assetPath ?? string.Empty;
            relativePath = relativePath.Replace("/", " › ");
            string key = !string.IsNullOrEmpty(candidate.stringKey) ? candidate.stringKey : candidate.enumKey.ToString();
            string state = candidate.hasLibraryKeyConflict ? "Library Key 冲突" : (candidate.isBaked ? "已烘焙" : "待烘焙");
            return $"{key} · {candidate.assetName} · {candidate.libraryName} › {candidate.pageName} · {candidate.typeDisplayName} · {state} · {relativePath}";
        }

        private static UnityEngine.Object LoadAsset(Candidate candidate)
        {
            if (candidate.asset != null) return candidate.asset;
            if (candidate.localFileId == 0)
                return candidate.asset = AssetDatabase.LoadMainAssetAtPath(candidate.assetPath);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(candidate.assetPath))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long fileId) && fileId == candidate.localFileId)
                    return candidate.asset = asset;
            return null;
        }

        public static void ApplyCandidate(SerializedProperty property, Candidate candidate, bool recordUndo = true)
        {
            if (property == null || candidate == null)
                return;

            if (recordUndo)
            {
                UnityEngine.Object[] targets = property.serializedObject.targetObjects
                    .Where(target => target != null)
                    .ToArray();
                if (targets.Length > 0)
                    Undo.RecordObjects(targets, "Synchronize AssetLibrary ConfigKey");
            }
            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = candidate.enumKey;
            property.FindPropertyRelative("stringKey").stringValue = candidate.stringKey ?? string.Empty;
            property.FindPropertyRelative("guid").stringValue = candidate.guid ?? string.Empty;
            property.FindPropertyRelative("localFileId").longValue = NormalizeStoredLocalFileId(
                candidate.guid,
                candidate.localFileId,
                candidate.assetPath);
            property.FindPropertyRelative("assetTypeName").stringValue = candidate.assetTypeName ?? string.Empty;
            property.FindPropertyRelative("editorPath").stringValue = candidate.assetPath ?? string.Empty;
            property.FindPropertyRelative("address").stringValue = string.Empty;
            property.FindPropertyRelative("groupName").stringValue = string.Empty;
            property.FindPropertyRelative("editorOnly").boolValue = ESAssetPipelineIO.IsEditorOnly(candidate.assetPath, LoadAsset(candidate));
            property.FindPropertyRelative("alwaysLoaded").boolValue = false;
            ESAssetConfigKeyDrawerBase.Apply(property);
        }

        private static void ClearCandidate(SerializedProperty property)
        {
            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = 0;
            property.FindPropertyRelative("stringKey").stringValue = string.Empty;
            property.FindPropertyRelative("guid").stringValue = string.Empty;
            property.FindPropertyRelative("localFileId").longValue = 0;
            property.FindPropertyRelative("assetTypeName").stringValue = string.Empty;
            property.FindPropertyRelative("address").stringValue = string.Empty;
            property.FindPropertyRelative("groupName").stringValue = string.Empty;
            property.FindPropertyRelative("editorPath").stringValue = string.Empty;
            property.FindPropertyRelative("editorOnly").boolValue = false;
            property.FindPropertyRelative("alwaysLoaded").boolValue = false;
            ESAssetConfigKeyDrawerBase.Apply(property);
        }
    }

    internal sealed class ESAssetCatalogKeyPickerPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            NotifyPotentialLibraryChanges(importedAssets);
            NotifyPotentialLibraryChanges(deletedAssets);
            NotifyPotentialLibraryChanges(movedAssets);
            NotifyPotentialLibraryChanges(movedFromAssetPaths);
        }

        private static void NotifyPotentialLibraryChanges(IEnumerable<string> paths)
        {
            if (paths == null)
                return;
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    continue;
                ESAssetCatalogKeyPicker.NotifyAssetPathChanged(path);
            }
        }
    }

    internal sealed class ESAssetCatalogKeyPickerInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            ESAssetReferEditorBridge.NotifyAssetLibraryChanged = library =>
            {
                string path = library != null ? AssetDatabase.GetAssetPath(library) : string.Empty;
                ESAssetCatalogKeyPicker.QueueLibraryChanged(path);
            };
        }
    }

    /// <summary>跨编辑器程序集按 ConfigKey 解析资源的最小公开入口。</summary>
    public static class ESAssetCatalogKeyResolver
    {
        public static bool TryResolveAsset(
            ESAssetReferKind kind,
            int enumKey,
            string stringKey,
            out UnityEngine.Object asset)
        {
            asset = null;
            if (!ESAssetCatalogKeyPicker.TryFindByKey(kind, enumKey, stringKey, out ESAssetCatalogKeyPicker.Candidate candidate))
                return false;

            asset = ESAssetCatalogKeyPicker.ResolveAsset(candidate);
            return asset != null;
        }
    }

    [CustomPropertyDrawer(typeof(ESAssetReferPrefabConfigKey))]
    public sealed class ESAssetReferPrefabConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferPrefabEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Prefab; }

    [CustomPropertyDrawer(typeof(ESAssetReferSceneConfigKey))]
    public sealed class ESAssetReferSceneConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSceneEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Scene; }

    [CustomPropertyDrawer(typeof(ESAssetReferSpriteConfigKey))]
    public sealed class ESAssetReferSpriteConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSpriteEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Sprite; }

    [CustomPropertyDrawer(typeof(ESAssetReferSpriteAtlasConfigKey))]
    public sealed class ESAssetReferSpriteAtlasConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSpriteAtlasEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.SpriteAtlas; }

    [CustomPropertyDrawer(typeof(ESAssetReferTextureConfigKey))]
    public sealed class ESAssetReferTextureConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTextureEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Texture; }

    [CustomPropertyDrawer(typeof(ESAssetReferTexture2DConfigKey))]
    public sealed class ESAssetReferTexture2DConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTexture2DEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Texture2D; }

    [CustomPropertyDrawer(typeof(ESAssetReferRawConfigKey))]
    public sealed class ESAssetReferRawConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferRawEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Raw; }

    [CustomPropertyDrawer(typeof(ESAssetReferMaterialConfigKey))]
    public sealed class ESAssetReferMaterialConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferMaterialEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Material; }

    [CustomPropertyDrawer(typeof(ESAssetReferMeshConfigKey))]
    public sealed class ESAssetReferMeshConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferMeshEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Mesh; }

    [CustomPropertyDrawer(typeof(ESAssetReferAnimationClipConfigKey))]
    public sealed class ESAssetReferAnimationClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAnimationClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AnimationClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferAnimatorControllerConfigKey))]
    public sealed class ESAssetReferAnimatorControllerConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAnimatorControllerEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AnimatorController; }

    [CustomPropertyDrawer(typeof(ESAssetReferAvatarConfigKey))]
    public sealed class ESAssetReferAvatarConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAvatarEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Avatar; }

    [CustomPropertyDrawer(typeof(ESAssetReferAudioClipConfigKey))]
    public sealed class ESAssetReferAudioClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAudioClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AudioClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferVideoClipConfigKey))]
    public sealed class ESAssetReferVideoClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferVideoClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.VideoClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferTimelineAssetConfigKey))]
    public sealed class ESAssetReferTimelineAssetConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTimelineAssetEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.TimelineAsset; }

    [CustomPropertyDrawer(typeof(ESAssetReferPlayableAssetConfigKey))]
    public sealed class ESAssetReferPlayableAssetConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferPlayableAssetEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.PlayableAsset; }

    [CustomPropertyDrawer(typeof(ESAssetReferTerrainDataConfigKey))]
    public sealed class ESAssetReferTerrainDataConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTerrainDataEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.TerrainData; }

    [CustomPropertyDrawer(typeof(ESAssetReferScriptableObjectConfigKey))]
    public sealed class ESAssetReferScriptableObjectConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferScriptableObjectEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.ScriptableObject; }
}
