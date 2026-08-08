using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES;
using UnityEditor;
using UnityEngine;

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
                    stringKey.stringValue = editedStringKey?.Trim() ?? string.Empty;
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
                else if (ESAssetCatalogKeyPicker.TryCollectToActiveLibrary(ResolveKind(), selectedAsset, out selectedCandidate, out string collectError))
                    ESAssetCatalogKeyPicker.ApplyCandidate(property, selectedCandidate);
                else
                    Debug.LogWarning("[ESRes][ConfigKey] 无法同步 ConfigKey：" + selectedAsset.name + "。" + collectError, selectedAsset);
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
            public bool isBaked;
            public bool isLibrarySource;
            public bool hasLibraryKeyConflict;
        }

        private static readonly Dictionary<ESAssetReferKind, List<Candidate>> CandidatesByKind = new Dictionary<ESAssetReferKind, List<Candidate>>();
        private static bool loaded;
        private static int loadedRegistryVersion = -1;

        static ESAssetCatalogKeyPicker()
        {
            // Page 的 Key 在 Inspector 中改动时，Registry 的版本不一定变化。项目资产刷新
            // 是更可靠的失效信号，避免引用抽屉继续展示旧的源 Key 快照。
            EditorApplication.projectChanged += Invalidate;
        }

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
                Candidate identityMatch = candidates.FirstOrDefault(item => item.guid == guid && item.localFileId == localFileId);
                // 已绑定源身份时绝不能再按 Key 猜测别的资产；源资产移除/未注册必须
                // 明确暴露为失效引用，不能静默换绑。
                return identityMatch;
            }
            return candidates.FirstOrDefault(item => ESConfigKeyMatch.Matches(enumKey, stringKey, item.enumKey, item.stringKey));
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
            // 拖入是显式同步动作，必须读取项目内全部 Library 的最新 Key，不能复用旧候选快照。
            Reload();
            if (!ESAssetPage.TryGetAssetIdentityEditor(asset, out string guid, out long localFileId)
                || !CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates)) return false;
            candidate = candidates.FirstOrDefault(item => item.guid == guid && item.localFileId == localFileId);
            return candidate != null;
        }

        public static bool TryFindByKey(ESAssetReferKind kind, int enumKey, string stringKey, out Candidate candidate)
        {
            EnsureLoaded();
            candidate = null;
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                return false;
            candidate = candidates.FirstOrDefault(item => ESConfigKeyMatch.Matches(enumKey, stringKey, item.enumKey, item.stringKey));
            return candidate != null;
        }

        public static int CountKeyMatches(ESAssetReferKind kind, int enumKey, string stringKey)
        {
            EnsureLoaded();
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                return 0;
            return candidates.Count(item => ESConfigKeyMatch.Matches(enumKey, stringKey, item.enumKey, item.stringKey));
        }

        public static bool TryCollectToActiveLibrary(
            ESAssetReferKind expectedKind,
            UnityEngine.Object asset,
            out Candidate candidate,
            out string error)
        {
            candidate = null;
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

            ESGlobalResToolsSupportConfig config = ESGlobalResToolsSupportConfig.Instance;
            if (config != null && config.showConfirmDialog
                && !EditorUtility.DisplayDialog(
                    "收集并配置资源",
                    "资产【" + asset.name + "】尚未收集。是否加入当前 Library【" + library.Name + "】并立即写入 ConfigKey？",
                    "收集并配置",
                    "取消"))
            {
                error = "用户取消收集。";
                return false;
            }

            Undo.RecordObject(library, "Collect Asset From ConfigKey");
            library.EditorOnly_DragAssetsToBooks(new[] { asset });
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Invalidate();
            if (TryFindByAsset(expectedKind, asset, out candidate))
                return true;

            error = "资产已提交到 Library，但注册表仍无法解析；请打开资源窗口检查目标 Book。";
            return false;
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
            // 每次主动打开选择器都重读注册表与 Catalog，刚收集或刚烘焙后无需重载 Inspector。
            Reload();
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
                    UnityEngine.Object asset = LoadAsset(candidate);
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
                        asset != null ? AssetPreview.GetMiniThumbnail(asset) : null,
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
        }

        public static void RefreshForValidation()
        {
            Invalidate();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (!loaded || loadedRegistryVersion != ESAssetRegistry.Version)
                Reload();
        }

        private static void Reload()
        {
            loaded = true;
            loadedRegistryVersion = ESAssetRegistry.Version;
            CandidatesByKind.Clear();

            // AssetLibrary 是编辑器侧 Key 的唯一真源。必须扫全项目 Library，不能只依赖
            // 当前已加载的 Registry；否则跨 Library 的已注册资产会被误判为未注册。
            AddAllAssetLibraryCandidates();

            // Registry 只补充尚未落盘或暂未被 AssetDatabase 枚举到的编辑器候选，
            // 不允许覆盖上面从真实 Library 读取的 Key。
            foreach (ESAssetPage page in ESAssetRegistry.Pages)
            {
                if (page == null || page.Kind == ESAssetReferKind.None || page.Kind == ESAssetReferKind.Other)
                    continue;
                if (page.EnumKey == 0 && string.IsNullOrWhiteSpace(page.EffectiveStringKey))
                    continue;
                string assetPath = !string.IsNullOrWhiteSpace(page.AssetPath)
                    ? page.AssetPath
                    : AssetDatabase.GetAssetPath(page.OB);
                AddOrReplaceCandidate(page.Kind, new Candidate
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
                    isLibrarySource = true
                }, false);
            }

            if (Directory.Exists(ESAssetPipelineIO.BakeRoot))
            foreach (string path in ESManagedFileIO.EnumerateFilesSafely(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName))
            {
                ESAssetLibraryCatalog catalog;
                try { catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path); }
                catch { continue; }
                if (catalog == null || catalog.errors.Count > 0) continue;
                foreach (ESAssetCatalogEntry entry in catalog.assets.Where(item => item != null && item.isBusinessAsset))
                {
                    if (!Enum.TryParse(entry.kind, out ESAssetReferKind kind) || kind == ESAssetReferKind.None) continue;
                    if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey)) continue;
                    AddOrReplaceCandidate(kind, new Candidate
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
                    }, true);
                }
            }
            foreach (List<Candidate> candidates in CandidatesByKind.Values)
            {
                foreach (Candidate candidate in candidates)
                    candidate.menuLabel = BuildMenuLabel(candidate);
                candidates.Sort((left, right) => string.CompareOrdinal(left.menuLabel, right.menuLabel));
            }
        }

        private static void AddAllAssetLibraryCandidates()
        {
            IEnumerable<string> libraryPaths = AssetDatabase.FindAssets("t:ESAssetLibrary")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .OrderBy(path => path, StringComparer.Ordinal);
            foreach (string libraryPath in libraryPaths)
            {
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
                if (library == null)
                    continue;

                string libraryName = string.IsNullOrWhiteSpace(library.Name)
                    ? Path.GetFileNameWithoutExtension(libraryPath)
                    : library.Name;
                foreach (ESAssetBook book in library.GetAllUseableBooks())
                {
                    if (book?.pages == null)
                        continue;

                    foreach (ESAssetPage page in book.pages)
                        AddLibraryPageCandidate(libraryName, book.Name, page);
                }
            }
        }

        private static void AddLibraryPageCandidate(string libraryName, string bookName, ESAssetPage page)
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
            AddOrReplaceCandidate(kind, new Candidate
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
                isLibrarySource = true
            }, false);
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
            if (candidate == null)
                return;

            candidate.localFileId = NormalizeStoredLocalFileId(
                candidate.guid,
                candidate.localFileId,
                candidate.assetPath);
        }

        private static void AddOrReplaceCandidate(ESAssetReferKind kind, Candidate candidate, bool preferNew)
        {
            if (candidate == null)
                return;
            NormalizeCandidateMainAssetIdentity(candidate);
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                CandidatesByKind.Add(kind, candidates = new List<Candidate>());

            int index = candidates.FindIndex(item =>
                !string.IsNullOrEmpty(candidate.guid)
                && item.guid == candidate.guid
                && item.localFileId == candidate.localFileId);
            if (index < 0)
            {
                candidates.Add(candidate);
                return;
            }
            Candidate existing = candidates[index];
            if (candidate.isLibrarySource && existing.isLibrarySource
                && !ESConfigKeyMatch.Matches(existing.enumKey, existing.stringKey, candidate.enumKey, candidate.stringKey))
            {
                existing.hasLibraryKeyConflict = true;
                return;
            }

            if (preferNew && !existing.isLibrarySource)
                candidates[index] = candidate;
            else if (preferNew)
                existing.isBaked |= candidate.isBaked;
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
            if (candidate.localFileId == 0) return AssetDatabase.LoadMainAssetAtPath(candidate.assetPath);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(candidate.assetPath))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long fileId) && fileId == candidate.localFileId) return asset;
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
