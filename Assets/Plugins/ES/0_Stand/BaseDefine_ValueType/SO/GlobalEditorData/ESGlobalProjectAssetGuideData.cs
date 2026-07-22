using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    /// <summary>
    /// Project-level asset responsibility and editor helper data.
    /// Store project ownership/readme data here instead of scattering it across EditorPrefs or ad-hoc files.
    /// Serialized storage is GUID-keyed through Odin dictionary serialization.
    /// </summary>
    [CreateAssetMenu(fileName = "ESGlobalProjectAssetGuideData", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "项目资产职责提示数据")]
    [ESOnlyEditorSO("项目资产职责提示数据只服务编辑器协作和资产说明，不应进入运行时构建或AB资源包。")]
    public partial class ESGlobalProjectAssetGuideData : ESEditorGlobalSo<ESGlobalProjectAssetGuideData>
    {
        public const string DefaultAssetPath = "Assets/ESNormalAssets/Data/GlobalData/ProjectAssetGuide/ESGlobalProjectAssetGuideData.asset";

        [TabGroup("职责提示")]
        [DisplayAsString(FontSize = 28, Alignment = TextAlignment.Center)]
        [HideLabel]
        [GUIColor(0.25f, 0.85f, 0.55f)]
        [PropertyOrder(-100)]
        public string editorTitle = "项目资产职责提示 / ReadMe GlobalData";

        [TabGroup("职责提示")]
        [InfoBox("Project asset responsibility and ReadMe data. Records are keyed by GUID; path/name/type are cached display data.")]
        [PropertyOrder(-99)]
        public bool showGuideUsage = true;

        [Serializable]
        public sealed class AssetGuideRecord
        {
            [PropertyOrder(-20)]
            [HorizontalGroup("Top", Width = 0.35f), LabelText("职责标题")]
            [GUIColor(1f, 0.82f, 0.28f)]
            [OnValueChanged(nameof(MarkManuallyEdited))]
            public string roleTitle;

            [PropertyOrder(-19)]
            [HorizontalGroup("Top"), LabelText("Owner System")]
            [GUIColor(0.45f, 0.85f, 1f)]
            [OnValueChanged(nameof(MarkManuallyEdited))]
            public string ownerSystem;

            [LabelText("GUID"), ReadOnly]
            public string guid;

            [LabelText("路径"), ReadOnly]
            public string assetPath;

            [LabelText("Asset Name"), ReadOnly]
            public string assetName;

            [LabelText("类型"), ReadOnly]
            public string assetTypeName;

            [LabelText("标签")]
            [OnValueChanged(nameof(MarkManuallyEdited), true)]
            public List<string> tags = new List<string>();

            [PropertyOrder(-18)]
            [LabelText("职责提示"), TextArea(3, 7)]
            [GUIColor(0.75f, 1f, 0.75f)]
            [OnValueChanged(nameof(MarkManuallyEdited))]
            public string responsibilityHint;

            [PropertyOrder(-17)]
            [LabelText("ReadMe"), TextArea(5, 14)]
            [OnValueChanged(nameof(MarkManuallyEdited))]
            public string readMe;

            [PropertyOrder(-16)]

            [LabelText("人工编辑保护")]

            [ReadOnly]

            public bool isManuallyEdited;


            [LabelText("最后扫描 UTC Ticks"), ReadOnly]
            public long lastScanUtcTicks;

            [NonSerialized]
            private UnityEngine.Object cachedAsset;

            public void MarkManuallyEdited()
            {
                isManuallyEdited = true;
            }

            [Button("解除人工编辑保护", ButtonSizes.Small)]
            public void ClearManuallyEdited()
            {
                isManuallyEdited = false;
            }

#if UNITY_EDITOR
            public void RefreshFromAssetPath(string path)
            {
                assetPath = NormalizeAssetPath(path);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                assetName = string.IsNullOrEmpty(assetPath) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(assetPath);

                Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                assetTypeName = type != null ? type.FullName : string.Empty;
                lastScanUtcTicks = DateTime.UtcNow.Ticks;
                cachedAsset = null;
            }

            [Button("Ping", ButtonSizes.Small)]
            private void PingAsset()
            {
                UnityEngine.Object asset = GetAsset();
                if (asset != null)
                    EditorGUIUtility.PingObject(asset);
            }

            public UnityEngine.Object GetAsset()
            {
                if (cachedAsset != null)
                    return cachedAsset;

                if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
                    assetPath = AssetDatabase.GUIDToAssetPath(guid);

                cachedAsset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
                return cachedAsset;
            }
#endif
        }

        [Serializable]
        public sealed class SceneHierarchyExpansionRecord
        {
            [LabelText("场景 GUID"), ReadOnly]
            public string sceneGuid;

            [LabelText("场景路径"), ReadOnly]
            public string scenePath;

            [LabelText("保存时间"), ReadOnly]
            public long savedUtcTicks;

            [LabelText("展开对象路径"), ListDrawerSettings(ShowIndexLabels = true)]
            public List<string> expandedTransformPaths = new List<string>();
        }

        [TabGroup("职责提示")]
        [InfoBox("Stores project asset responsibility, ReadMe, tags and owner system by GUID.")]
        [Searchable]
        [DictionaryDrawerSettings(KeyLabel = "GUID", ValueLabel = "职责记录")]
        [OdinSerialize]
        public Dictionary<string, AssetGuideRecord> assetGuideByGuid = new Dictionary<string, AssetGuideRecord>();

        [HideInInspector]
        public List<AssetGuideRecord> assetGuides = new List<AssetGuideRecord>();

        [TabGroup("Scene Editor State")]
        [InfoBox("Project-level scene editor state. Scene hierarchy expansion state is owned here instead of EditorPrefs.")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "scenePath", ShowPaging = true, NumberOfItemsPerPage = 20)]
        public List<SceneHierarchyExpansionRecord> sceneHierarchyExpansionRecords = new List<SceneHierarchyExpansionRecord>();

        [TabGroup("设置"), LabelText("标题字号"), Range(14, 36)]
        public int displayTitleFontSize = 22;

        [TabGroup("设置"), LabelText("正文字号"), Range(10, 24)]
        public int displayHintFontSize = 14;

        [TabGroup("设置"), LabelText("标题颜色")]
        public Color displayTitleColor = new Color(1f, 0.82f, 0.28f, 1f);

        [TabGroup("设置"), LabelText("系统颜色")]
        public Color displayOwnerColor = new Color(0.45f, 0.85f, 1f, 1f);

        [TabGroup("设置"), LabelText("正文颜色")]
        public Color displayHintColor = new Color(0.78f, 1f, 0.78f, 1f);
        [TabGroup("设置"), LabelText("扫描 Packages")]
        public bool scanPackages = false;

        [TabGroup("设置"), LabelText("保存场景展开状态时自动写入资产")]
        public bool saveSceneExpansionAssetImmediately = true;

        [NonSerialized]
        private int lastStandardGuideMatchedCount;

        [NonSerialized]
        private int lastStandardGuideTotalCount;

        [NonSerialized]
        private Dictionary<string, SceneHierarchyExpansionRecord> sceneExpansionByGuid;

        public void RebuildCache()
        {
            EnsureAssetGuideDictionary();

            sceneExpansionByGuid = new Dictionary<string, SceneHierarchyExpansionRecord>(StringComparer.OrdinalIgnoreCase);
            if (sceneHierarchyExpansionRecords != null)
            {
                for (int i = 0; i < sceneHierarchyExpansionRecords.Count; i++)
                {
                    SceneHierarchyExpansionRecord record = sceneHierarchyExpansionRecords[i];
                    if (record == null || string.IsNullOrEmpty(record.sceneGuid))
                        continue;

                    sceneExpansionByGuid[record.sceneGuid] = record;
                }
            }
        }

        public bool TryGetGuide(string guid, out AssetGuideRecord record)
        {
            record = null;
            EnsureAssetGuideDictionary();

            return !string.IsNullOrEmpty(guid) && assetGuideByGuid.TryGetValue(NormalizeGuid(guid), out record);
        }

        public bool TryGetSceneExpansion(string sceneGuid, out SceneHierarchyExpansionRecord record)
        {
            record = null;
            if (sceneExpansionByGuid == null)
                RebuildCache();

            return !string.IsNullOrEmpty(sceneGuid) && sceneExpansionByGuid.TryGetValue(sceneGuid, out record);
        }

        public void SetSceneExpansion(string sceneGuid, string scenePath, List<string> expandedTransformPaths)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return;

            if (sceneExpansionByGuid == null)
                RebuildCache();

            if (!sceneExpansionByGuid.TryGetValue(sceneGuid, out SceneHierarchyExpansionRecord record))
            {
                record = new SceneHierarchyExpansionRecord { sceneGuid = sceneGuid };
                sceneHierarchyExpansionRecords.Add(record);
                sceneExpansionByGuid[sceneGuid] = record;
            }

            record.scenePath = NormalizeAssetPath(scenePath);
            record.savedUtcTicks = DateTime.UtcNow.Ticks;
            record.expandedTransformPaths.Clear();
            if (expandedTransformPaths != null)
                record.expandedTransformPaths.AddRange(expandedTransformPaths);
        }

        public bool ClearSceneExpansion(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
                return false;

            int removed = sceneHierarchyExpansionRecords.RemoveAll(i => i != null && string.Equals(i.sceneGuid, sceneGuid, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
                RebuildCache();

            return removed > 0;
        }

        private void EnsureAssetGuideDictionary()
        {
            bool changed = false;
            if (assetGuideByGuid == null)
            {
                assetGuideByGuid = new Dictionary<string, AssetGuideRecord>();
                changed = true;
            }
            else if (!ReferenceEquals(assetGuideByGuid.Comparer, EqualityComparer<string>.Default))
            {
                assetGuideByGuid = new Dictionary<string, AssetGuideRecord>(assetGuideByGuid);
                changed = true;
            }

            if (assetGuides == null || assetGuides.Count == 0)
            {
#if UNITY_EDITOR
                if (changed)
                    EditorUtility.SetDirty(this);
#endif
                return;
            }

            for (int i = 0; i < assetGuides.Count; i++)
            {
                AssetGuideRecord record = assetGuides[i];
                if (record == null || string.IsNullOrEmpty(record.guid))
                    continue;

                assetGuideByGuid[NormalizeGuid(record.guid)] = record;
            }

            assetGuides.Clear();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        private static string NormalizeGuid(string guid)
        {
            return string.IsNullOrWhiteSpace(guid) ? string.Empty : guid.Trim().ToLowerInvariant();
        }

#if UNITY_EDITOR
        public static bool TryFindExistingData(out ESGlobalProjectAssetGuideData data)
        {
            data = Instance;
            if (data != null)
                return true;

            List<ESGlobalProjectAssetGuideData> indexedData = ESEditorSO.SOS.GetNewGroupOfType<ESGlobalProjectAssetGuideData>();
            if (indexedData != null && indexedData.Count > 0)
            {
                data = indexedData.FirstOrDefault(item => item != null && item.HasConfirm) ?? indexedData.FirstOrDefault(item => item != null);
                if (data != null)
                {
                    data.TryConfirmSwitchThis();
                    data.RebuildCache();
                    return true;
                }
            }

            return false;
        }

        public static ESGlobalProjectAssetGuideData GetOrCreateData()
        {
            if (TryFindExistingData(out ESGlobalProjectAssetGuideData data))
                return data;

            string folder = System.IO.Path.GetDirectoryName(DefaultAssetPath).Replace('\\', '/');
            EnsureFolder(folder);

            data = CreateInstance<ESGlobalProjectAssetGuideData>();
            AssetDatabase.CreateAsset(data, DefaultAssetPath);
            data.TryConfirmSwitchThis();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        public AssetGuideRecord GetOrCreateGuide(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return GetOrCreateGuideByPath(path);
        }

        public AssetGuideRecord GetOrCreateGuideByPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return null;

            EnsureAssetGuideDictionary();
            guid = NormalizeGuid(guid);

            if (!assetGuideByGuid.TryGetValue(guid, out AssetGuideRecord record))
            {
                record = new AssetGuideRecord();
                assetGuideByGuid[guid] = record;
            }

            record.RefreshFromAssetPath(assetPath);
            return record;
        }

        [TabGroup("职责提示"), Button("扫描项目资产索引", ButtonSizes.Medium), GUIColor(0.45f, 0.85f, 1f)]
        public void ScanProjectAssets()
        {
            string[] searchRoots = scanPackages ? new[] { "Assets", "Packages" } : new[] { "Assets" };
            string[] guids = AssetDatabase.FindAssets(string.Empty, searchRoots);
            int changedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(path))
                    continue;

                AssetGuideRecord record = GetOrCreateGuideByPath(path);
                if (record != null)
                    changedCount++;
            }

            RebuildCache();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESGlobalProjectAssetGuideData] 已扫描资产索引：{changedCount}");
        }

        [TabGroup("职责提示"), Button("填充标准职责提示", ButtonSizes.Medium), GUIColor(0.35f, 0.95f, 0.65f)]
        public void FillStandardAssetGuideResponsibilities()
        {
            int changedCount = ApplyStandardAssetGuideResponsibilities(true, false);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESGlobalProjectAssetGuideData] 已填充标准职责提示：Changed={changedCount} Matched={lastStandardGuideMatchedCount} Total={lastStandardGuideTotalCount}");
        }

        [TabGroup("职责提示"), Button("重写标准职责提示", ButtonSizes.Medium), GUIColor(1f, 0.72f, 0.28f)]
        public void RewriteStandardAssetGuideResponsibilities()
        {
            int changedCount = ApplyStandardAssetGuideResponsibilities(true, true);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESGlobalProjectAssetGuideData] 已重写标准职责提示：Changed={changedCount} Matched={lastStandardGuideMatchedCount} Total={lastStandardGuideTotalCount}");
        }

        [TabGroup("职责提示"), Button("清除全部人工编辑保护", ButtonSizes.Medium), GUIColor(1f, 0.45f, 0.35f)]
        public void ClearAllManualEditProtection()
        {
            EnsureAssetGuideDictionary();
            int changedCount = 0;
            foreach (KeyValuePair<string, AssetGuideRecord> pair in assetGuideByGuid)
            {
                AssetGuideRecord record = pair.Value;
                if (record == null || !record.isManuallyEdited)
                    continue;

                record.isManuallyEdited = false;
                changedCount++;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESGlobalProjectAssetGuideData] 已清除人工编辑保护：{changedCount}");
        }

        [TabGroup("职责提示"), Button("清理失效资产索引", ButtonSizes.Medium), GUIColor(1f, 0.75f, 0.45f)]
        public void RemoveMissingAssetGuides()
        {
            EnsureAssetGuideDictionary();
            List<string> removeGuids = new List<string>();
            int refreshedPathCount = 0;
            foreach (KeyValuePair<string, AssetGuideRecord> pair in assetGuideByGuid)
            {
                AssetGuideRecord record = pair.Value;
                string guid = NormalizeGuid(record != null && !string.IsNullOrEmpty(record.guid) ? record.guid : pair.Key);
                string currentPath = string.IsNullOrEmpty(guid) ? string.Empty : NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(currentPath))
                {
                    removeGuids.Add(pair.Key);
                    continue;
                }

                if (record != null && !string.Equals(NormalizeAssetPath(record.assetPath), currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    record.RefreshFromAssetPath(currentPath);
                    refreshedPathCount++;
                }
            }

            for (int i = 0; i < removeGuids.Count; i++)
                assetGuideByGuid.Remove(removeGuids[i]);

            int removed = removeGuids.Count;
            RebuildCache();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESGlobalProjectAssetGuideData] Removed missing guide records: {removed}, refreshed stale paths: {refreshedPathCount}");
            Debug.Log($"[ESGlobalProjectAssetGuideData] 已清理失效资产索引：{removed}");
        }

        private int ApplyStandardAssetGuideResponsibilities(bool createMissingImportantRecords, bool overwriteExisting)
        {
            EnsureAssetGuideDictionary();

            if (createMissingImportantRecords)
            {
                EnsureStandardGuideRecords();
                EnsureRecordsForAllStandardRuleMatches();
            }

            int changedCount = 0;
            int matchedCount = 0;
            foreach (KeyValuePair<string, AssetGuideRecord> pair in assetGuideByGuid)
            {
                AssetGuideRecord record = pair.Value;
                if (record == null)
                    continue;

                record.guid = string.IsNullOrEmpty(record.guid) ? pair.Key : NormalizeGuid(record.guid);
                if (string.IsNullOrEmpty(record.assetPath) && !string.IsNullOrEmpty(record.guid))
                    record.RefreshFromAssetPath(AssetDatabase.GUIDToAssetPath(record.guid));

                if (string.IsNullOrEmpty(record.assetPath))
                    continue;

                if (record.isManuallyEdited)
                    continue;

                bool isFolder = AssetDatabase.IsValidFolder(record.assetPath);
                StandardGuideContent content = CreateStandardGuideContent(record.assetPath, isFolder);
                if (content == null)
                    continue;

                matchedCount++;
                bool changed = false;
                changed |= SetStandardValue(ref record.ownerSystem, content.ownerSystem, StandardGuideField.OwnerSystem, overwriteExisting);
                changed |= SetStandardValue(ref record.roleTitle, content.roleTitle, StandardGuideField.RoleTitle, overwriteExisting);
                changed |= SetStandardValue(ref record.responsibilityHint, content.responsibilityHint, StandardGuideField.ResponsibilityHint, overwriteExisting);
                changed |= SetStandardValue(ref record.readMe, content.readMe, StandardGuideField.ReadMe, overwriteExisting);
                changed |= AddMissingTags(record.tags, content.tags);

                if (changed)
                    changedCount++;
            }

            lastStandardGuideMatchedCount = matchedCount;
            lastStandardGuideTotalCount = assetGuideByGuid.Count;
            return changedCount;
        }

        private void EnsureStandardGuideRecords()
        {
            string[] importantPaths =
            {
                "Assets/Plugins/ES",
                "Assets/Plugins/ES/0_Stand",
                "Assets/Plugins/ES/1_Design",
                "Assets/Plugins/ES/Editor",
                "Assets/Plugins/ES/Editor/ESTrackView",
                "Assets/Plugins/ES/Editor/EditorTools",
                "Assets/Plugins/ES/ThirdParty",
                "Assets/Scripts/ESLogic",
                "Assets/Scripts/ESLogic/Runtime",
                "Assets/Scripts/ESLogic/Runtime/Skill",
                "Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence",
                "Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip",
                "Assets/Scripts/ESLogic/Runtime/Operation",
                "Assets/Scripts/ESLogic/Runtime/Operation/Operations",
                "Assets/Scripts/ESLogic/Runtime/Operation/Expressions",
                "Assets/Scripts/ESLogic/Runtime/Operation/ExpressionSources",
                "Assets/Scripts/ESLogic/Runtime/Entity",
                "Assets/Scripts/ESLogic/Runtime/Entity/Entity",
                "Assets/Scripts/ESLogic/Runtime/State",
                "Assets/Scripts/ESLogic/Runtime/Data",
                "Assets/Scripts/ESLogic/Runtime/Data/For_Info",
                "Assets/Scripts/ESLogic/Runtime/EditorPreview",
                "Assets/Scripts/ESLogic/Editor",
                "Assets/ESNormalAssets/Data/GlobalData/ProjectAssetGuide/ESGlobalProjectAssetGuideData.asset"
            };

            for (int i = 0; i < importantPaths.Length; i++)
            {
                string path = importantPaths[i];
                if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                    GetOrCreateGuideByPath(path);
            }
        }

        private void EnsureRecordsForAllStandardRuleMatches()
        {
            string[] searchRoots =
            {
                "Assets/Plugins/ES",
                "Assets/Scripts/ESLogic",
                "Assets/ESNormalAssets/Data/GlobalData/ProjectAssetGuide"
            };

            for (int i = 0; i < searchRoots.Length; i++)
            {
                string root = searchRoots[i];
                if (AssetDatabase.IsValidFolder(root))
                    EnsureFolderGuideRecordsRecursive(root);
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, searchRoots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(path) || CreateStandardGuideContent(path, AssetDatabase.IsValidFolder(path)) == null)
                    continue;

                GetOrCreateGuideByPath(path);
            }
        }

        private void EnsureFolderGuideRecordsRecursive(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (CreateStandardGuideContent(folderPath, true) != null)
                GetOrCreateGuideByPath(folderPath);

            string absoluteFolder = Path.GetFullPath(folderPath);
            if (!Directory.Exists(absoluteFolder))
                return;

            string[] subFolders = Directory.GetDirectories(absoluteFolder);
            for (int i = 0; i < subFolders.Length; i++)
            {
                string assetPath = NormalizeAssetPath(ToProjectAssetPath(subFolders[i]));
                if (!string.IsNullOrEmpty(assetPath))
                    EnsureFolderGuideRecordsRecursive(assetPath);
            }
        }

        private static string ToProjectAssetPath(string absolutePath)
        {
            absolutePath = NormalizeAssetPath(absolutePath);
            string projectPath = NormalizeAssetPath(Directory.GetCurrentDirectory());
            if (!absolutePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return absolutePath.Substring(projectPath.Length).TrimStart('/');
        }

        private static StandardGuideRule FindStandardGuideRule(string assetPath, bool isFolder)
        {
            assetPath = NormalizeAssetPath(assetPath);
            StandardGuideRule bestRule = null;
            for (int i = 0; i < StandardGuideRules.Length; i++)
            {
                StandardGuideRule rule = StandardGuideRules[i];
                if (!rule.Matches(assetPath, isFolder))
                    continue;

                if (bestRule == null || rule.pathPrefix.Length > bestRule.pathPrefix.Length)
                    bestRule = rule;
            }

            return bestRule;
        }

        private static StandardGuideContent CreateStandardGuideContent(string assetPath, bool isFolder)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (TryCreateHardcodedCommonScriptGuideContent(assetPath, out StandardGuideContent hardcodedContent))
                return hardcodedContent;

            StandardGuideRule rule = FindStandardGuideRule(assetPath, isFolder);
            if (rule != null)
                return new StandardGuideContent(rule.ownerSystem, rule.roleTitle, rule.responsibilityHint, rule.readMe, rule.tags);

            return null;
        }

        private sealed class StandardGuideContent
        {
            public readonly string ownerSystem;
            public readonly string roleTitle;
            public readonly string responsibilityHint;
            public readonly string readMe;
            public readonly string[] tags;

            public StandardGuideContent(string ownerSystem, string roleTitle, string responsibilityHint, string readMe, string[] tags)
            {
                this.ownerSystem = ownerSystem;
                this.roleTitle = roleTitle;
                this.responsibilityHint = responsibilityHint;
                this.readMe = readMe;
                this.tags = tags;
            }
        }

        private enum StandardGuideField
        {
            OwnerSystem,
            RoleTitle,
            ResponsibilityHint,
            ReadMe
        }

        private static bool SetStandardValue(ref string value, string newValue, StandardGuideField field, bool overwriteExisting)
        {
            if (string.IsNullOrWhiteSpace(newValue))
                return false;

            if (!overwriteExisting && !string.IsNullOrWhiteSpace(value) && !IsKnownStandardValue(value, field))
                return false;

            if (string.Equals(value, newValue, StringComparison.Ordinal))
                return false;

            value = newValue;
            return true;
        }

        private static bool IsKnownStandardValue(string value, StandardGuideField field)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            for (int i = 0; i < StandardGuideRules.Length; i++)
            {
                StandardGuideRule rule = StandardGuideRules[i];
                string standardValue = field switch
                {
                    StandardGuideField.OwnerSystem => rule.ownerSystem,
                    StandardGuideField.RoleTitle => rule.roleTitle,
                    StandardGuideField.ResponsibilityHint => rule.responsibilityHint,
                    StandardGuideField.ReadMe => rule.readMe,
                    _ => string.Empty
                };

                if (string.Equals(value, standardValue, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool AddMissingTags(List<string> targetTags, string[] sourceTags)
        {
            if (sourceTags == null || sourceTags.Length == 0 || targetTags == null)
                return false;

            bool changed = false;
            for (int i = 0; i < sourceTags.Length; i++)
            {
                string tag = sourceTags[i];
                if (string.IsNullOrWhiteSpace(tag) || targetTags.Contains(tag))
                    continue;

                targetTags.Add(tag);
                changed = true;
            }

            return changed;
        }

        private sealed class StandardGuideRule
        {
            public enum MatchKind
            {
                Any,
                FolderOnly,
                FileOnly
            }

            public readonly string pathPrefix;
            public readonly string ownerSystem;
            public readonly string roleTitle;
            public readonly string responsibilityHint;
            public readonly string readMe;
            public readonly string[] tags;
            public readonly MatchKind matchKind;

            public StandardGuideRule(string pathPrefix, string ownerSystem, string roleTitle, string responsibilityHint, string readMe, params string[] tags)
                : this(pathPrefix, MatchKind.FolderOnly, ownerSystem, roleTitle, responsibilityHint, readMe, tags)
            {
            }

            public StandardGuideRule(string pathPrefix, MatchKind matchKind, string ownerSystem, string roleTitle, string responsibilityHint, string readMe, params string[] tags)
            {
                this.pathPrefix = NormalizeAssetPath(pathPrefix);
                this.matchKind = matchKind;
                this.ownerSystem = ownerSystem;
                this.roleTitle = roleTitle;
                this.responsibilityHint = responsibilityHint;
                this.readMe = readMe;
                this.tags = tags;
            }

            public bool Matches(string assetPath, bool isFolder)
            {
                if (matchKind == MatchKind.FolderOnly && !isFolder)
                    return false;

                if (matchKind == MatchKind.FileOnly && isFolder)
                    return false;

                return assetPath.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static readonly StandardGuideRule[] StandardGuideRules =
        {
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/OperationTypeRegistryNames.cs", StandardGuideRule.MatchKind.FileOnly, "Operation/TypeRegister", "Operation type registry constants", "Hardcoded file responsibility: declares operation menu/category names for SerializeReference type selection.", "Maintain explicit operation categories here. Do not use dynamic responsibility generation for this file.", "Operation", "TypeRegister", "EditorMenu"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/IHandleValueOperation.cs", StandardGuideRule.MatchKind.FileOnly, "Operation/Common", "Value operation protocol", "Hardcoded file responsibility: defines the common protocol for operations that handle value input/output.", "Keep the protocol narrow and stable.", "Operation", "Interface", "Value"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/OutputOperationBuffer.cs", StandardGuideRule.MatchKind.FileOnly, "Operation/Common", "Operation output buffer", "Hardcoded file responsibility: stores operation output values and intermediate execution results.", "Watch reuse and cleanup to avoid high-frequency GC.", "Operation", "Buffer", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/ESOutputOp.cs", StandardGuideRule.MatchKind.FileOnly, "Operation/Common", "Output operation base", "Hardcoded file responsibility: base structure for operations that write results to target packs, services, or buffers.", "Keep this abstract; concrete effects belong in concrete operation classes.", "Operation", "Output", "Base"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/Examples/", StandardGuideRule.MatchKind.FileOnly, "Operation/Example", "Operation example scripts", "Hardcoded file responsibility: examples for validating runtime target and operation execution links.", "Examples are for debugging and teaching, not long-term production skill dependencies.", "Operation", "Example"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations/README", StandardGuideRule.MatchKind.FileOnly, "Operation/Docs", "Operation docs", "Hardcoded file responsibility: documents operation folder organization and extension rules.", "Docs explain rules only; they do not carry runtime logic.", "Operation", "Docs"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/ExpressionSources", "Operation/Expression", "Expression source folder", "Folder responsibility: stores direct-value plus expression-source wrappers.", "Folder rule only. Script duties must be hardcoded separately.", "Operation", "ExpressionSource", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Expressions", "Operation/Expression", "Expression folder", "Folder responsibility: stores runtime value expressions used by operation and skill logic.", "Folder rule only. Script duties must be hardcoded separately.", "Operation", "Expression", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation/Operations", "Operation", "Operation implementation folder", "Folder responsibility: stores atomic effect operation implementations.", "Folder rule only. Script duties must be hardcoded separately.", "Operation", "Runtime", "TypeRegister"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Operation", "Operation", "Operation runtime layer", "Folder responsibility: owns runtime targets, services, expressions, selectors and effect execution protocols.", "Folder rule only. Script duties must be hardcoded separately.", "Operation", "RuntimeLogic"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence", "Skill Sequence", "Skill sequence folder", "Folder responsibility: stores formal skill sequence tracks, clips and runtime structures.", "Folder rule only. Script duties must be hardcoded separately.", "Skill", "Sequence", "Track"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip/Tools", "Skill Sequence", "Track sampling and editor play tools folder", "Folder responsibility: stores editor preview, sampling and sequence play support.", "Folder rule only. Script duties must be hardcoded separately.", "Skill", "Sampler", "Preview"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Skill/TrackItemAndClip", "Skill Sequence", "Legacy track and clip compatibility folder", "Folder responsibility: keeps legacy skill track/clip/sampler support.", "Folder rule only. Script duties must be hardcoded separately.", "Skill", "Track", "Compatibility"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Skill", "Skill", "Skill runtime and configuration folder", "Folder responsibility: connects entity, target, timeline and operation logic for skills.", "Folder rule only. Script duties must be hardcoded separately.", "Skill", "EntityStateSkill"),
            new StandardGuideRule("Assets/Plugins/ES/Editor/ESTrackView", "Editor/TrackView", "TrackView editor folder", "Folder responsibility: owns the custom timeline editor UI and interactions.", "Folder rule only. Script duties must be hardcoded separately.", "Editor", "TrackView", "UIElements"),
            new StandardGuideRule("Assets/Plugins/ES/Editor/EditorTools/ESEditorInspector", "Editor/Inspector", "Inspector extension folder", "Folder responsibility: owns asset/folder inspector extensions and quick asset information.", "Folder rule only. Script duties must be hardcoded separately.", "Editor", "Inspector", "AssetGuide"),
            new StandardGuideRule("Assets/Plugins/ES/Editor/EditorTools", "Editor Tools", "Editor tools folder", "Folder responsibility: stores production editor utilities.", "Folder rule only. Script duties must be hardcoded separately.", "Editor", "Tools"),
            new StandardGuideRule("Assets/Plugins/ES/Editor/ESMenuTreeWindow", "Editor/MenuTree", "ES menu tree window folder", "Folder responsibility: organizes centralized editor tool windows.", "Folder rule only. Script duties must be hardcoded separately.", "Editor", "Window"),
            new StandardGuideRule("Assets/Plugins/ES/Editor", "Editor", "ES editor extension folder", "Folder responsibility: stores editor-only ES framework extensions.", "Folder rule only. Script duties must be hardcoded separately.", "Editor"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/EditorPreview", "Editor Preview", "Runtime-visible editor preview helper folder", "Folder responsibility: stores preview target and preview resource lifecycle helpers.", "Folder rule only. Script duties must be hardcoded separately.", "Preview", "Lifecycle"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State", "Entity/State", "Entity state domain folder", "Folder responsibility: connects entity state, animation state and skill preview/play flow.", "Folder rule only. Script duties must be hardcoded separately.", "Entity", "State", "Skill"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Entity/Entity", "Entity", "Entity body and domain folder", "Folder responsibility: stores entity body, core, domain and transform mapping structures.", "Folder rule only. Script duties must be hardcoded separately.", "Entity", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Entity/Interaction", "Entity/Interaction", "Entity interaction folder", "Folder responsibility: stores interactable object behavior and interaction examples.", "Folder rule only. Script duties must be hardcoded separately.", "Entity", "Interaction"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Entity", "Entity", "Entity runtime folder", "Folder responsibility: owns entity-related runtime capabilities.", "Folder rule only. Script duties must be hardcoded separately.", "Entity", "RuntimeLogic"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/State", "State", "State and animation state folder", "Folder responsibility: stores state machine, state animation data and animation blending logic.", "Folder rule only. Script duties must be hardcoded separately.", "State", "Animation"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Data/For_Info", "DataInfo", "Business data Info/Group/Pack folder", "Folder responsibility: stores business data definitions, groups and packs.", "Folder rule only. Script duties must be hardcoded separately.", "Data", "Info"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Data", "Data", "Runtime data folder", "Folder responsibility: stores runtime data definitions and configuration structures.", "Folder rule only. Script duties must be hardcoded separately.", "Data", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/GameManager", "GameManager", "Game manager folder", "Folder responsibility: stores game-level coordination and domain entry logic.", "Folder rule only. Script duties must be hardcoded separately.", "GameManager", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime/Features", "Feature", "Reusable feature folder", "Folder responsibility: stores reusable runtime feature modules.", "Folder rule only. Script duties must be hardcoded separately.", "Feature", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Runtime", "RuntimeLogic", "ESLogic runtime root", "Folder responsibility: root of project runtime logic.", "Folder rule only. Script duties must be hardcoded separately.", "Runtime"),
            new StandardGuideRule("Assets/Scripts/ESLogic/Editor", "ESLogic Editor", "ESLogic editor folder", "Folder responsibility: stores editor tools tied to ESLogic business systems.", "Folder rule only. Script duties must be hardcoded separately.", "Editor", "ESLogic"),
            new StandardGuideRule("Assets/Scripts/ESLogic", "ESLogic", "Project logic root", "Folder responsibility: root folder for project business logic.", "Folder rule only. Script duties must be hardcoded separately.", "ESLogic"),
            new StandardGuideRule("Assets/Plugins/ES/0_Stand", "ES Stand", "ES standard base folder", "Folder responsibility: stores base definitions, global SO data and standard protocols.", "Folder rule only. Script duties must be hardcoded separately.", "Framework", "Stand"),
            new StandardGuideRule("Assets/Plugins/ES/1_Design", "ES Design", "ES design abstraction folder", "Folder responsibility: stores design-time abstractions shared by editor and runtime logic.", "Folder rule only. Script duties must be hardcoded separately.", "Framework", "Design"),
            new StandardGuideRule("Assets/Plugins/ES/ThirdParty", "ThirdParty", "Third-party library folder", "Folder responsibility: stores third-party dependencies and redirect assets.", "Folder rule only. Script duties must be hardcoded separately.", "ThirdParty"),
            new StandardGuideRule("Assets/Plugins/ES/3_Examples", "Examples", "ES examples folder", "Folder responsibility: stores examples and demonstrations.", "Folder rule only. Script duties must be hardcoded separately.", "Example"),
            new StandardGuideRule("Assets/Plugins/ES/Obsolete", "Obsolete", "Obsolete systems folder", "Folder responsibility: stores deprecated systems kept for reference and migration.", "Folder rule only. Script duties must be hardcoded separately.", "Obsolete"),
            new StandardGuideRule("Assets/Plugins/ES", "ES Framework", "ES framework plugin root", "Folder responsibility: root of ES framework plugin assets.", "Folder rule only. Script duties must be hardcoded separately.", "Framework"),
            new StandardGuideRule("Assets/ESNormalAssets/Data/GlobalData/ProjectAssetGuide", "GlobalData", "Project asset guide data folder", "Folder responsibility: stores global project asset guide data.", "Folder rule only. Script duties must be hardcoded separately.", "GlobalData", "AssetGuide")
        };

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "打开职责提示数据", false, 0)]
        public static void OpenDataMenu()
        {
            Selection.activeObject = GetOrCreateData();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "扫描项目资产索引", false, 10)]
        public static void ScanProjectAssetsMenu()
        {
            GetOrCreateData().ScanProjectAssets();
        }

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "填充标准职责提示", false, 20)]
        public static void FillStandardAssetGuideResponsibilitiesMenu()
        {
            GetOrCreateData().FillStandardAssetGuideResponsibilities();
        }

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "重写标准职责提示", false, 30)]
        public static void RewriteStandardAssetGuideResponsibilitiesMenu()
        {
            GetOrCreateData().RewriteStandardAssetGuideResponsibilities();
        }

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "清除全部人工编辑保护", false, 40)]
        public static void ClearAllManualEditProtectionMenu()
        {
            GetOrCreateData().ClearAllManualEditProtection();
        }

        [MenuItem(MenuItemPathDefine.PROJECT_ASSETS_PATH + "清理失效索引并刷新旧路径", false, 50)]
        public static void RemoveMissingAssetGuidesMenu()
        {
            GetOrCreateData().RemoveMissingAssetGuides();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
#endif
    }
}

