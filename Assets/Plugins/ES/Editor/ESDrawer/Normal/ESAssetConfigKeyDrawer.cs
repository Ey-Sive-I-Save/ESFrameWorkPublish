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
        private const int AdvancedLineCount = 9;
        private const float PanelPadding = 6f;
        private static readonly Color PanelAccent = new Color(0.22f, 0.78f, 1f, 0.95f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = 5 + (property.isExpanded ? AdvancedLineCount : 0);
            return lines * Line + (lines - 1) * Gap + PanelPadding * 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

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

            Rect row = NextLine(ref position);
            DrawHeader(row, ResolveTitle(property, label), "Asset ConfigKey · " + ResolveKind());

            row = NextLine(ref position);
            const string enumLabel = "枚举 Key";
            Rect contentRect = EditorGUI.PrefixLabel(row, new GUIContent(enumLabel));
            DrawActionRow(contentRect, out Rect selectorRect, out Rect clearRect, out Rect locateRect);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(selectorRect, enumKey, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                ClearAssetIdentity(property);
                Apply(property);
            }

            using (new EditorGUI.DisabledScope(!HasAnyValue(property)))
            {
                if (GUI.Button(clearRect, new GUIContent("×", "清空配置"), EditorStyles.miniButton))
                    Clear(property);
                if (GUI.Button(locateRect, new GUIContent("定位", "在 Project 中定位资源"), EditorStyles.miniButton))
                    Locate(property);
            }

            row = NextLine(ref position);
            Rect stringRect = EditorGUI.PrefixLabel(row, new GUIContent("字符串 Key", "可直接输入，也可从当前 Catalog 的同类型资源中选择。"));
            DrawStringSelectionRow(stringRect, out Rect stringInputRect, out Rect stringSelectRect);
            EditorGUI.BeginChangeCheck();
            string editedStringKey = EditorGUI.DelayedTextField(stringInputRect, stringKey.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                stringKey.stringValue = editedStringKey?.Trim() ?? string.Empty;
                ClearAssetIdentity(property);
                Apply(property);
            }
            if (GUI.Button(stringSelectRect, StringSelectContent(stringSelectRect), EditorStyles.miniButton))
                ESAssetCatalogKeyPicker.ShowMenu(stringSelectRect, ResolveKind(), property);

            row = NextLine(ref position);
            Rect objectRect = EditorGUI.PrefixLabel(row, new GUIContent("资产(备选)", "拖入已收集资产以反向同步 ConfigKey"));
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
                else if (ESAssetCatalogKeyPicker.TryFindByAsset(ResolveKind(), selectedAsset, out ESAssetCatalogKeyPicker.Candidate selectedCandidate))
                    ESAssetCatalogKeyPicker.ApplyCandidate(property, selectedCandidate);
                else
                    Debug.LogWarning("[ESRes][ConfigKey] 选择的资源尚未进入当前 Catalog，无法同步 ConfigKey：" + selectedAsset.name, selectedAsset);
            }

            string summary = current != null
                ? $"类型：{current.typeDisplayName} · 来源：{current.libraryName}/{current.pageName} · Key：{current.stringKey}"
                : BuildFallbackSummary(property, ResolveKind());
            property.isExpanded = EditorGUI.Foldout(NextLine(ref position), property.isExpanded, summary, true, EditorStyles.foldout);

            if (property.isExpanded)
            {
                DrawReadOnly(ref position, "Enum Key", enumKey);
                DrawReadOnly(ref position, "String Key", stringKey);
                DrawReadOnly(ref position, "GUID", property.FindPropertyRelative("guid"));
                DrawReadOnly(ref position, "Local File Id", property.FindPropertyRelative("localFileId"));
                DrawReadOnly(ref position, "类型", property.FindPropertyRelative("assetTypeName"));
                DrawReadOnly(ref position, "地址", property.FindPropertyRelative("address"));
                DrawReadOnly(ref position, "分组", property.FindPropertyRelative("groupName"));
                DrawReadOnly(ref position, "编辑器路径", property.FindPropertyRelative("editorPath"));
                Rect flags = NextLine(ref position);
                using (new EditorGUI.DisabledScope(true))
                {
                    float half = (flags.width - Gap) * 0.5f;
                    EditorGUI.PropertyField(new Rect(flags.x, flags.y, half, flags.height), property.FindPropertyRelative("editorOnly"), new GUIContent("Editor Only"));
                    EditorGUI.PropertyField(new Rect(flags.x + half + Gap, flags.y, half, flags.height), property.FindPropertyRelative("alwaysLoaded"), new GUIContent("Always Loaded"));
                }
            }

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
            string key = property.FindPropertyRelative("stringKey").stringValue;
            return HasAnyValue(property) ? $"类型：{kind} · Key：{key} · 未匹配当前 Catalog" : "高级信息";
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

    /// <summary>只读取阶段①的 Catalog，供业务配置安全选择键；不修改 Library 或 RuntimeKey。</summary>
    internal static class ESAssetCatalogKeyPicker
    {
        internal sealed class Candidate
        {
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
        }

        private static readonly Dictionary<ESAssetReferKind, List<Candidate>> CandidatesByKind = new Dictionary<ESAssetReferKind, List<Candidate>>();
        private static bool loaded;

        public static Candidate FindCurrent(ESAssetReferKind kind, SerializedProperty property)
        {
            if (!loaded) Reload();
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates)) return null;
            string guid = property.FindPropertyRelative("guid").stringValue;
            long localFileId = property.FindPropertyRelative("localFileId").longValue;
            int enumKey = property.FindPropertyRelative("enumKey").intValue;
            string stringKey = property.FindPropertyRelative("stringKey").stringValue;
            if (!string.IsNullOrEmpty(guid))
            {
                Candidate identityMatch = candidates.FirstOrDefault(item => item.guid == guid && item.localFileId == localFileId);
                if (identityMatch != null) return identityMatch;
            }
            return candidates.FirstOrDefault(item => item.enumKey == enumKey && string.Equals(item.stringKey, stringKey, StringComparison.Ordinal));
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
            Reload();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                || !CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates)) return false;
            candidate = candidates.FirstOrDefault(item => item.guid == guid && item.localFileId == localFileId);
            return candidate != null;
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
                case ESAssetReferKind.ScriptableObject: return typeof(ScriptableObject);
                case ESAssetReferKind.PlayableAsset: return typeof(UnityEngine.Playables.PlayableAsset);
                case ESAssetReferKind.TimelineAsset: return typeof(UnityEngine.Playables.PlayableAsset);
                default: return typeof(UnityEngine.Object);
            }
        }

        public static void ShowMenu(Rect position, ESAssetReferKind kind, SerializedProperty property)
        {
            Reload();
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("清空"), false, () => ClearCandidate(property));
            menu.AddSeparator(string.Empty);
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates) || candidates.Count == 0)
                menu.AddDisabledItem(new GUIContent("没有可选资源，请先在资源总控中烘焙 Catalog"));
            else
                foreach (Candidate candidate in candidates)
                {
                    Candidate captured = candidate;
                    UnityEngine.Object asset = LoadAsset(candidate);
                    menu.AddItem(new GUIContent(candidate.menuLabel, asset != null ? AssetPreview.GetMiniThumbnail(asset) : null), false, () => ApplyCandidate(property, captured));
                }
            menu.DropDown(position);
        }

        public static UnityEngine.Object ResolveAsset(SerializedProperty property)
        {
            string guid = property.FindPropertyRelative("guid").stringValue;
            long localFileId = property.FindPropertyRelative("localFileId").longValue;
            string path = !string.IsNullOrEmpty(guid) ? AssetDatabase.GUIDToAssetPath(guid) : property.FindPropertyRelative("editorPath").stringValue;
            if (string.IsNullOrEmpty(path)) return null;
            if (localFileId == 0) return AssetDatabase.LoadMainAssetAtPath(path);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long fileId)
                    && assetGuid == guid && fileId == localFileId) return asset;
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        private static void Reload()
        {
            loaded = true;
            CandidatesByKind.Clear();
            if (!Directory.Exists(ESAssetPipelineIO.BakeRoot)) return;
            foreach (string path in Directory.GetFiles(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName, SearchOption.AllDirectories))
            {
                ESAssetLibraryCatalog catalog;
                try { catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path); }
                catch { continue; }
                if (catalog == null || catalog.errors.Count > 0) continue;
                foreach (ESAssetCatalogEntry entry in catalog.assets.Where(item => item != null && item.isBusinessAsset))
                {
                    if (!Enum.TryParse(entry.kind, out ESAssetReferKind kind) || kind == ESAssetReferKind.None) continue;
                    if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey)) continue;
                    if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                        CandidatesByKind.Add(kind, candidates = new List<Candidate>());
                    candidates.Add(new Candidate
                    {
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
                        menuLabel = BuildMenuLabel(entry, kind)
                    });
                }
            }
            foreach (List<Candidate> candidates in CandidatesByKind.Values)
                candidates.Sort((left, right) => string.CompareOrdinal(left.menuLabel, right.menuLabel));
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

        private static string BuildMenuLabel(ESAssetCatalogEntry entry, ESAssetReferKind kind)
        {
            string assetName = ResolveAssetName(entry);
            string typeName = ResolveTypeDisplayName(entry.assetTypeName, kind);
            string relativePath = entry.assetPath.StartsWith("Assets/", StringComparison.Ordinal) ? entry.assetPath.Substring(7) : entry.assetPath;
            relativePath = relativePath.Replace("/", " › ");
            string key = !string.IsNullOrEmpty(entry.stringKey) ? entry.stringKey : entry.enumKey.ToString();
            return $"ConfigKey/{key} · {assetName} · {typeName} · {entry.libraryName} › {entry.pageName} · {relativePath}";
        }

        private static UnityEngine.Object LoadAsset(Candidate candidate)
        {
            if (candidate.localFileId == 0) return AssetDatabase.LoadMainAssetAtPath(candidate.assetPath);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(candidate.assetPath))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long fileId) && fileId == candidate.localFileId) return asset;
            return null;
        }

        public static void ApplyCandidate(SerializedProperty property, Candidate candidate)
        {
            property.serializedObject.Update();
            property.FindPropertyRelative("enumKey").intValue = candidate.enumKey;
            property.FindPropertyRelative("stringKey").stringValue = candidate.stringKey ?? string.Empty;
            property.FindPropertyRelative("guid").stringValue = candidate.guid ?? string.Empty;
            property.FindPropertyRelative("localFileId").longValue = candidate.localFileId;
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
}
