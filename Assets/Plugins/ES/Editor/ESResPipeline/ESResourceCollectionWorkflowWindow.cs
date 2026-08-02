using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资产收集、ConfigKey 查询和 ResourcePlan 构建前检查的统一入口。
    /// 目标是让“资产在哪里、对应哪个 Key、是否已烘焙”在一个窗口内完成确认。
    /// </summary>
    public sealed class ESResourceCollectionWorkflowWindow : EditorWindow
    {
        private struct PlanField
        {
            public string FieldName;
            public ESAssetReferKind Kind;
            public string DisplayName;

            public PlanField(string fieldName, ESAssetReferKind kind, string displayName)
            {
                FieldName = fieldName;
                Kind = kind;
                DisplayName = displayName;
            }
        }

        private sealed class Issue
        {
            public UnityEngine.Object Owner;
            public string Title;
            public string Message;
            public bool Error;
        }

        private static readonly PlanField[] ResourcePlanFields =
        {
            new PlanField("prefabs", ESAssetReferKind.Prefab, "Prefab"),
            new PlanField("sprites", ESAssetReferKind.Sprite, "Sprite"),
            new PlanField("audioClips", ESAssetReferKind.AudioClip, "AudioClip"),
            new PlanField("animationClips", ESAssetReferKind.AnimationClip, "AnimationClip"),
            new PlanField("animatorControllers", ESAssetReferKind.AnimatorController, "AnimatorController"),
            new PlanField("materials", ESAssetReferKind.Material, "Material"),
            new PlanField("meshes", ESAssetReferKind.Mesh, "Mesh"),
            new PlanField("textures", ESAssetReferKind.Texture, "Texture"),
            new PlanField("rawAssets", ESAssetReferKind.Raw, "Raw"),
            new PlanField("texture2Ds", ESAssetReferKind.Texture2D, "Texture2D"),
            new PlanField("spriteAtlases", ESAssetReferKind.SpriteAtlas, "SpriteAtlas"),
            new PlanField("avatars", ESAssetReferKind.Avatar, "Avatar"),
            new PlanField("playableAssets", ESAssetReferKind.PlayableAsset, "PlayableAsset"),
            new PlanField("scriptableObjects", ESAssetReferKind.ScriptableObject, "ScriptableObject"),
            new PlanField("timelineAssets", ESAssetReferKind.TimelineAsset, "TimelineAsset"),
            new PlanField("videoClips", ESAssetReferKind.VideoClip, "VideoClip"),
            new PlanField("terrainDatas", ESAssetReferKind.TerrainData, "TerrainData")
        };

        [SerializeField] private UnityEngine.Object selectedAsset;
        [SerializeField] private ESResourcePlanInfo targetPlan;
        [SerializeField] private Vector2 scrollPosition;
        private readonly List<Issue> issues = new List<Issue>();
        private string scanSummary = "尚未扫描 ResourcePlan。";
        private string catalogSummary = "尚未检查 Catalog。";
        private string workflowStatus = string.Empty;
        private string issueSearch = string.Empty;
        private bool showWarnings = true;
        private double stageStatusExpiresAt;
        private int cachedCatalogCount;
        private int cachedManifestCount;
        private bool cachedPlanReady;
        private bool cachedPublished;

        [MenuItem("【ES】/资源与发布/资源收集/资源收集工作流", false, 2201)]
        public static void Open()
        {
            ESResourceCollectionWorkflowWindow window = GetWindow<ESResourceCollectionWorkflowWindow>();
            window.titleContent = new GUIContent("ES收集与Key");
            window.minSize = new Vector2(640f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            SyncFromSelection();
            InvalidateStageStatus();
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            SyncFromSelection();
            Repaint();
        }

        private void SyncFromSelection()
        {
            if (Selection.activeObject is ESResourcePlanInfo selectedPlan)
            {
                targetPlan = selectedPlan;
                return;
            }
            if (Selection.activeObject != null)
                selectedAsset = Selection.activeObject;
        }

        private void OnGUI()
        {
            DrawToolbar();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawWorkflowGuide();
            DrawSelectedAssetSection();
            DrawPipelineSection();
            DrawIssueSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawWorkflowGuide()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("推荐操作顺序", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("1. 从 Project 选中或拖入资产；2. 确认它属于当前 Library；3. 选择目标 ResourcePlan；4. 扫描通过后再烘焙和构建。", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("不需要先记住 EnumKey。优先从资产开始，系统会自动找到或生成对应 Key。", EditorStyles.miniLabel);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("收集 → Catalog → ConfigKey → ResourcePlan → 构建", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新注册表", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    ESAssetCatalogKeyPicker.Invalidate();
                    InvalidateStageStatus();
                    AssetDatabase.Refresh();
                    Repaint();
                }
                if (GUILayout.Button("打开资源窗口", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                    ESResWindow.TryOpenWindow();
            }
        }

        private void DrawSelectedAssetSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("① 从资产配置 Key", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawCollectionTarget();
                DrawAssetDropArea();

                UnityEngine.Object next = EditorGUILayout.ObjectField("资产", selectedAsset, typeof(UnityEngine.Object), false);
                if (next != selectedAsset)
                    selectedAsset = next;

                if (selectedAsset == null)
                {
                    EditorGUILayout.HelpBox("从 Project 拖入资产，或直接在 Project 里选中资产。推荐从资产开始，窗口会自动显示它所属的 Library、类型和 Key。", MessageType.Info);
                    return;
                }

                ESAssetReferKind kind = ESAssetPage.DetermineKind(selectedAsset);
                EditorGUILayout.LabelField("资产类型", ESAssetConfigKeyDrawerBase.ResolveKindDisplayName(kind));
                string path = AssetDatabase.GetAssetPath(selectedAsset);
                EditorGUILayout.LabelField("路径", path);

                if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                {
                    EditorGUILayout.HelpBox("该对象不是可配置的 ES 业务资源类型。", MessageType.Warning);
                    return;
                }

                if (!ESAssetCatalogKeyPicker.TryFindByAsset(kind, selectedAsset, out ESAssetCatalogKeyPicker.Candidate candidate))
                {
                    EditorGUILayout.HelpBox("当前资产尚未在 ESAssetRegistry 中找到。先把资产拖入 Library 的 Book，或打开资源窗口进行收集。", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(ResolveCollectionTarget() == null))
                            if (GUILayout.Button("加入当前收集 Library"))
                                CollectAssets(new[] { selectedAsset });
                        if (GUILayout.Button("定位资源窗口"))
                            ESResWindow.TryOpenWindow();
                    }
                    return;
                }

                string key = !string.IsNullOrWhiteSpace(candidate.stringKey) ? candidate.stringKey : candidate.enumKey.ToString();
                EditorGUILayout.LabelField("收集状态", candidate.isBaked ? "已收集并已烘焙 Catalog" : "已收集，等待烘焙 Catalog");
                EditorGUILayout.LabelField("Library / Book", candidate.libraryName + " / " + candidate.pageName);
                EditorGUILayout.LabelField("最终 Key", key);
                EditorGUILayout.LabelField("枚举 Key（内部）", candidate.enumKey.ToString());
                EditorGUILayout.LabelField("字符串 Key（内部）", string.IsNullOrEmpty(candidate.stringKey) ? "—" : candidate.stringKey);
                int keyMatchCount = ESAssetCatalogKeyPicker.CountKeyMatches(kind, candidate.enumKey, candidate.stringKey);
                if (keyMatchCount > 1)
                    EditorGUILayout.HelpBox("当前 ConfigKey 同时映射到 " + keyMatchCount + " 个资产。请先在 Library 中消除重复 Key，否则运行时解析结果不唯一。", MessageType.Error);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制 Key"))
                    {
                        EditorGUIUtility.systemCopyBuffer = key;
                        ShowNotification(new GUIContent("Key 已复制"));
                    }
                    if (GUILayout.Button("复制 ConfigKey 摘要"))
                    {
                        EditorGUIUtility.systemCopyBuffer = "Kind=" + kind + ", EnumKey=" + candidate.enumKey + ", StringKey=" + (candidate.stringKey ?? string.Empty);
                        ShowNotification(new GUIContent("ConfigKey 摘要已复制"));
                    }
                    if (GUILayout.Button("定位 Library 页面"))
                        LocateRegistryPage(kind, candidate);
                    if (GUILayout.Button("Ping 资产"))
                    {
                        Selection.activeObject = selectedAsset;
                        EditorGUIUtility.PingObject(selectedAsset);
                    }
                }
            }
        }

        private void DrawCollectionTarget()
        {
            ESAssetLibrary target = ResolveCollectionTarget();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("当前收集 Library", target != null ? target.Name : "未设置", GUILayout.MinWidth(180f));
                if (target != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                }
                int selectedCount = Selection.objects.Count(IsCollectableAsset);
                using (new EditorGUI.DisabledScope(target == null || selectedCount == 0))
                    if (GUILayout.Button("收集当前选择(" + selectedCount + ")", EditorStyles.miniButton, GUILayout.Width(108f)))
                        CollectAssets(Selection.objects);
                if (GUILayout.Button("资源窗口设置", EditorStyles.miniButton, GUILayout.Width(90f)))
                    ESResWindow.TryOpenWindow();
            }
            if (target == null)
                EditorGUILayout.HelpBox("请先在资源窗口选择一个“当前收集 Library”。未设置时不会自动决定资源归属。", MessageType.Warning);
        }

        private void DrawAssetDropArea()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "拖入一个或多个资产：自动加入当前收集 Library", EditorStyles.helpBox);
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition)
                || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
                return;

            bool canCollect = ResolveCollectionTarget() != null && DragAndDrop.objectReferences.Any(IsCollectableAsset);
            DragAndDrop.visualMode = canCollect ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (current.type == EventType.DragPerform && canCollect)
            {
                DragAndDrop.AcceptDrag();
                CollectAssets(DragAndDrop.objectReferences);
            }
            current.Use();
        }

        private static ESAssetLibrary ResolveCollectionTarget()
        {
            return ESGlobalResToolsSupportConfig.ActiveCollectLibrary;
        }

        private static bool IsCollectableAsset(UnityEngine.Object asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(asset)))
                return false;
            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            return kind != ESAssetReferKind.None && kind != ESAssetReferKind.Other;
        }

        private bool CollectAssets(IEnumerable<UnityEngine.Object> source)
        {
            ESAssetLibrary library = ResolveCollectionTarget();
            if (library == null)
            {
                workflowStatus = "收集失败：尚未设置当前收集 Library。";
                return false;
            }

            UnityEngine.Object[] assets = (source ?? Array.Empty<UnityEngine.Object>())
                .Where(IsCollectableAsset)
                .Distinct()
                .ToArray();
            if (assets.Length == 0)
            {
                workflowStatus = "没有可收集的资源对象。";
                return false;
            }
            int alreadyRegistered = assets.Count(IsRegisteredAsset);
            assets = assets.Where(asset => !IsRegisteredAsset(asset)).ToArray();
            if (assets.Length == 0)
            {
                workflowStatus = "所选资产均已存在于 Library 注册表，本次未重复收集。";
                return true;
            }
            if (assets.Length > 1 && !EditorUtility.DisplayDialog(
                    "批量收集资产",
                    "将 " + assets.Length + " 个资产加入 Library【" + library.Name + "】的默认 Book？",
                    "收集",
                    "取消"))
                return false;

            Undo.RecordObject(library, "Collect Assets To Active Library");
            library.EditorOnly_DragAssetsToBooks(assets);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            ESAssetCatalogKeyPicker.Invalidate();
            InvalidateStageStatus();
            selectedAsset = assets[0];
            workflowStatus = "已收集 " + assets.Length + " 个资产到 Library【" + library.Name + "】"
                + (alreadyRegistered > 0 ? "，已登记资产跳过 " + alreadyRegistered + " 个" : string.Empty)
                + "；现在可直接配置 Key，构建前再统一烘焙。";
            Repaint();
            return true;
        }

        private static bool IsRegisteredAsset(UnityEngine.Object asset)
        {
            if (!IsCollectableAsset(asset))
                return false;
            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            return identity.IsValid && ESAssetRegistry.TryGetByAssetIdentity(kind, identity.guid, identity.localFileId, out _);
        }

        private static void LocateRegistryPage(ESAssetReferKind kind, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            if (candidate == null || !ESAssetRegistry.TryGetByAssetIdentity(kind, candidate.guid, candidate.localFileId, out ESAssetPage page))
            {
                Debug.LogWarning("[ESRes][Workflow] 当前 Key 没有对应的 Library 页面。");
                ESResWindow.TryOpenWindow();
                return;
            }

            if (ESAssetReferEditorBridge.OpenRegistryPage != null)
                ESAssetReferEditorBridge.OpenRegistryPage(page);
            else
                ESResWindow.TryOpenWindow();
        }

        private void DrawPipelineSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("② 加入计划并检查构建", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("先选择目标 ResourcePlan，再把当前选中的资产加入计划。扫描没有错误后，才执行烘焙、规划和发布。", MessageType.Info);
                DrawPipelineStageStatus();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("检查资源计划", "扫描所有 ResourcePlan 中的空 Key、重复 Key 和失效引用"), GUILayout.Height(28f)))
                        ScanResourcePlans();
                    if (GUILayout.Button(new GUIContent("同步过期 Key", "把所有 SO 中已绑定源资产的最新 Key 同步回快照；手填 Key 不会被改动"), GUILayout.Height(28f)))
                    {
                        int synchronized = ESResourcePlanConfigKeySynchronizer.SynchronizeAll();
                        workflowStatus = "已同步过期 ConfigKey：" + synchronized + " 项。";
                        ScanResourcePlans();
                    }
                    if (GUILayout.Button(new GUIContent("检查烘焙结果", "检查 Catalog 数量、条目和错误"), GUILayout.Height(28f)))
                        ScanCatalogs();
                    if (GUILayout.Button(new GUIContent("烘焙引用", "把 Library 注册信息写入可供运行时读取的 Catalog"), GUILayout.Height(28f)))
                        StartBake();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("规划并标记 AB", "根据烘焙结果生成资源包规划并写入 AB 标签"), GUILayout.Height(28f)))
                        StartPlan();
                    showWarnings = EditorGUILayout.ToggleLeft("显示警告", showWarnings, GUILayout.Width(90f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    targetPlan = (ESResourcePlanInfo)EditorGUILayout.ObjectField("目标 ResourcePlan", targetPlan, typeof(ESResourcePlanInfo), false);
                    using (new EditorGUI.DisabledScope(targetPlan == null))
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44f)))
                        {
                            Selection.activeObject = targetPlan;
                            EditorGUIUtility.PingObject(targetPlan);
                        }
                }
                int selectedCount = Selection.objects.Count(IsCollectableAsset);
                using (new EditorGUI.DisabledScope(targetPlan == null || selectedCount == 0))
                    if (GUILayout.Button("收集并加入 ResourcePlan（" + selectedCount + "）", GUILayout.Height(28f)))
                        AddSelectionToResourcePlan();
                if (targetPlan != null)
                    EditorGUILayout.LabelField("目标计划", BuildPlanEntrySummary(targetPlan), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Plan 扫描", scanSummary, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Catalog 检查", catalogSummary, EditorStyles.miniLabel);
                if (!string.IsNullOrWhiteSpace(workflowStatus))
                    EditorGUILayout.HelpBox(workflowStatus, workflowStatus.Contains("失败") ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawPipelineStageStatus()
        {
            RefreshStageStatusIfNeeded();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStageBadge("① Catalog", cachedCatalogCount > 0, cachedCatalogCount + " 个");
                DrawStageBadge("② 规划", cachedPlanReady, cachedPlanReady ? "已生成" : "未生成");
                DrawStageBadge("③ AB", cachedManifestCount > 0, cachedManifestCount + " 个库");
                DrawStageBadge("④ 发布", cachedPublished, cachedPublished ? "已有本地发布" : "未发布");
            }
        }

        private void RefreshStageStatusIfNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < stageStatusExpiresAt)
                return;

            string platform = ESAssetPipelineIO.PlatformName;
            cachedCatalogCount = CountPipelineFiles(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName);
            string planPath = Path.Combine(ESAssetPipelineIO.PlanRoot(platform), ESAssetPipelineIO.PlanFileName);
            cachedPlanReady = File.Exists(planPath);
            cachedManifestCount = CountPipelineFiles(ESAssetPipelineIO.StagingRoot(platform), ESAssetPipelineIO.BundleManifestFileName);
            string publishedRoot = ESAssetPipelineIO.LocalTestRoot(platform);
            cachedPublished = File.Exists(Path.Combine(publishedRoot, ESAssetPipelineIO.ReleaseManifestFileName));
            stageStatusExpiresAt = now + 2d;
        }

        private static int CountPipelineFiles(string root, string fileName)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return 0;
            try
            {
                return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Count();
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private void InvalidateStageStatus()
        {
            stageStatusExpiresAt = 0d;
        }

        private static void DrawStageBadge(string title, bool ready, string detail)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = ready ? new Color(0.55f, 0.95f, 0.62f) : new Color(1f, 0.78f, 0.48f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(110f)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
            }
            GUI.backgroundColor = previous;
        }

        private void AddSelectionToResourcePlan()
        {
            if (targetPlan == null)
                return;

            UnityEngine.Object[] assets = Selection.objects.Where(IsCollectableAsset).Distinct().ToArray();
            UnityEngine.Object[] unregisteredAssets = assets.Where(asset => !IsRegisteredAsset(asset)).ToArray();
            if (unregisteredAssets.Length > 0 && !CollectAssets(unregisteredAssets))
                return;

            int added = 0;
            int duplicate = 0;
            var failures = new List<string>();
            Undo.RecordObject(targetPlan, "Add Assets To ResourcePlan");

            foreach (UnityEngine.Object asset in assets)
            {
                ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
                PlanField? mapping = FindPlanField(kind);
                if (!mapping.HasValue)
                {
                    failures.Add(asset.name + "：ResourcePlan 暂不支持类型 " + kind);
                    continue;
                }
                if (!ESAssetCatalogKeyPicker.TryFindByAsset(kind, asset, out ESAssetCatalogKeyPicker.Candidate candidate))
                {
                    failures.Add(asset.name + "：尚未收集到 Library");
                    continue;
                }

                FieldInfo listField = typeof(ESResourcePlanInfo).GetField(mapping.Value.FieldName, BindingFlags.Instance | BindingFlags.Public);
                IList list = listField?.GetValue(targetPlan) as IList;
                if (list == null)
                {
                    failures.Add(asset.name + "：无法访问 Plan 列表 " + mapping.Value.FieldName);
                    continue;
                }
                if (ContainsPlanKey(list, candidate))
                {
                    duplicate++;
                    continue;
                }

                Type entryType = list.GetType().GetGenericArguments()[0];
                object entry = Activator.CreateInstance(entryType);
                FieldInfo keyField = entryType.GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object key = keyField?.GetValue(entry);
                if (key == null)
                {
                    failures.Add(asset.name + "：无法创建 ConfigKey");
                    continue;
                }
                ApplyCandidateToKey(key, candidate);
                list.Add(entry);
                added++;
            }

            EditorUtility.SetDirty(targetPlan);
            AssetDatabase.SaveAssets();
            workflowStatus = "ResourcePlan【" + targetPlan.name + "】新增 " + added + "，重复跳过 " + duplicate
                + (failures.Count > 0 ? "，失败 " + failures.Count + "：" + string.Join("；", failures) : "。" );
            Selection.activeObject = targetPlan;
            EditorGUIUtility.PingObject(targetPlan);
        }

        private static string BuildPlanEntrySummary(ESResourcePlanInfo plan)
        {
            if (plan == null)
                return "未选择";
            int total = 0;
            foreach (PlanField field in ResourcePlanFields)
            {
                FieldInfo listField = typeof(ESResourcePlanInfo).GetField(field.FieldName, BindingFlags.Instance | BindingFlags.Public);
                if (listField?.GetValue(plan) is IList list)
                    total += list.Count;
            }
            total += plan.prefabPrewarms?.Count ?? 0;
            return plan.name + " · 资源条目 " + total;
        }

        private static PlanField? FindPlanField(ESAssetReferKind kind)
        {
            for (int i = 0; i < ResourcePlanFields.Length; i++)
                if (ResourcePlanFields[i].Kind == kind)
                    return ResourcePlanFields[i];
            return null;
        }

        private static bool ContainsPlanKey(IList list, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            foreach (object entry in list)
            {
                if (entry == null) continue;
                FieldInfo keyField = entry.GetType().GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object key = keyField?.GetValue(entry);
                if (key == null) continue;
                int enumKey = ReadEnumKey(key);
                string stringKey = ReadField<string>(key, "stringKey") ?? string.Empty;
                if (enumKey == candidate.enumKey && string.Equals(stringKey, candidate.stringKey ?? string.Empty, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ApplyCandidateToKey(object key, ESAssetCatalogKeyPicker.Candidate candidate)
        {
            FieldInfo enumField = FindField(key.GetType(), "enumKey");
            if (enumField != null)
                enumField.SetValue(key, Enum.ToObject(enumField.FieldType, candidate.enumKey));
            SetField(key, "stringKey", candidate.stringKey ?? string.Empty);
            SetField(key, "guid", candidate.guid ?? string.Empty);
            SetField(key, "localFileId", candidate.localFileId);
            SetField(key, "assetTypeName", candidate.assetTypeName ?? string.Empty);
            SetField(key, "editorPath", candidate.assetPath ?? string.Empty);
            SetField(key, "editorOnly", ESAssetPipelineIO.IsEditorOnly(candidate.assetPath, ESAssetCatalogKeyPicker.ResolveAsset(candidate)));
        }

        private static int ReadEnumKey(object key)
        {
            FieldInfo field = FindField(key.GetType(), "enumKey");
            object value = field?.GetValue(key);
            return value != null ? Convert.ToInt32(value) : 0;
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            object value = field?.GetValue(target);
            return value is T typed ? typed : default;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FindField(target.GetType(), fieldName)?.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private void StartBake()
        {
            try
            {
                if (!ScanResourcePlans())
                {
                    scanSummary = "ResourcePlan ConfigKey validation failed. Resolve or synchronize stale bound Keys before baking.";
                    return;
                }
                ESAssetReferenceBaker.Bake();
                InvalidateStageStatus();
                scanSummary = "已启动烘焙长任务；完成后重新扫描 Plan。";
            }
            catch (Exception exception)
            {
                scanSummary = "烘焙启动失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void StartPlan()
        {
            try
            {
                if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("规划并标记 AB", "会读取当前烘焙结果并修改 ES 管理的 AB 标签。继续吗？", "执行", "取消"))
                {
                    ESAssetBundleBuildPlanner.PlanAndMark();
                    InvalidateStageStatus();
                }
            }
            catch (Exception exception)
            {
                scanSummary = "规划失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void ScanCatalogs()
        {
            int catalogs = 0;
            int assets = 0;
            int errors = 0;
            int warnings = 0;
            if (Directory.Exists(ESAssetPipelineIO.BakeRoot))
            {
                foreach (string path in Directory.GetFiles(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName, SearchOption.AllDirectories))
                {
                    catalogs++;
                    try
                    {
                        ESAssetLibraryCatalog catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path);
                        assets += catalog?.assets?.Count ?? 0;
                        errors += catalog?.errors?.Count ?? 0;
                        warnings += catalog?.warnings?.Count ?? 0;
                    }
                    catch (Exception exception)
                    {
                        errors++;
                        issues.Add(new Issue { Title = "Catalog", Message = path + "：" + exception.Message, Error = true });
                    }
                }
            }
            catalogSummary = "Catalog=" + catalogs + "，资产条目=" + assets + "，错误=" + errors + "，警告=" + warnings;
            Repaint();
        }

        private bool ScanResourcePlans()
        {
            ESAssetCatalogKeyPicker.RefreshForValidation();
            issues.Clear();
            int planCount = 0;
            int entryCount = 0;
            int errors = 0;
            int warnings = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ESResourcePlanInfo"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ESResourcePlanInfo plan = AssetDatabase.LoadAssetAtPath<ESResourcePlanInfo>(path);
                if (plan == null) continue;
                planCount++;
                SerializedObject serialized = new SerializedObject(plan);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (PlanField field in ResourcePlanFields)
                {
                    SerializedProperty list = serialized.FindProperty(field.FieldName);
                    if (list == null || !list.isArray) continue;
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        SerializedProperty element = list.GetArrayElementAtIndex(i);
                        if (field.FieldName == "prefabPrewarms") continue;
                        SerializedProperty key = element.FindPropertyRelative("key");
                        if (key == null) continue;
                        entryCount++;
                        int enumKey = key.FindPropertyRelative("enumKey")?.intValue ?? 0;
                        string stringKey = key.FindPropertyRelative("stringKey")?.stringValue ?? string.Empty;
                        bool required = element.FindPropertyRelative("required")?.boolValue ?? true;
                        string identity = field.Kind + "|" + enumKey + "|" + stringKey;
                        if (enumKey == 0 && string.IsNullOrWhiteSpace(stringKey))
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "ConfigKey 为空。", Error = required });
                            continue;
                        }
                        if (!seen.Add(identity))
                        {
                            warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "同一计划内重复的 ConfigKey。", Error = false });
                        }
                        int keyMatchCount = ESAssetCatalogKeyPicker.CountKeyMatches(field.Kind, enumKey, stringKey);
                        ESAssetCatalogKeyPicker.Candidate authority = ESAssetCatalogKeyPicker.FindCurrent(field.Kind, key);
                        if (ESAssetCatalogKeyPicker.IsBoundSourceMissing(key, authority))
                        {
                            errors++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / " + field.DisplayName + "[" + i + "]",
                                Message = "已绑定的源资产不在当前 Library/Catalog。请重新选择或收集该资产。",
                                Error = true
                            });
                            continue;
                        }
                        if (authority != null && ESAssetCatalogKeyPicker.IsStale(key, authority))
                        {
                            errors++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / " + field.DisplayName + "[" + i + "]",
                                Message = "Bound source Key changed: " + ESConfigKeyMatch.Describe(enumKey, stringKey)
                                    + " -> " + ESConfigKeyMatch.Describe(authority.enumKey, authority.stringKey) + ". Sync this reference before baking.",
                                Error = true
                            });
                            continue;
                        }
                        if (keyMatchCount == 0)
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = (required ? "必需" : "可选") + " ConfigKey 在当前 Library 注册表/Catalog 中无法解析。", Error = required });
                        }
                        else if (keyMatchCount > 1)
                        {
                            errors++;
                            issues.Add(new Issue { Owner = plan, Title = plan.name + " / " + field.DisplayName + "[" + i + "]", Message = "ConfigKey 同时映射到 " + keyMatchCount + " 个资产，运行时解析存在歧义。", Error = true });
                        }
                    }
                }

                SerializedProperty prewarms = serialized.FindProperty("prefabPrewarms");
                if (prewarms != null && prewarms.isArray)
                {
                    for (int i = 0; i < prewarms.arraySize; i++)
                    {
                        SerializedProperty element = prewarms.GetArrayElementAtIndex(i);
                        SerializedProperty data = element.FindPropertyRelative("data");
                        bool required = element.FindPropertyRelative("required")?.boolValue ?? true;
                        if (data != null && data.objectReferenceValue == null)
                        {
                            if (required) errors++; else warnings++;
                            issues.Add(new Issue
                            {
                                Owner = plan,
                                Title = plan.name + " / PrefabPrewarm[" + i + "]",
                                Message = "预热配置为空。",
                                Error = required
                            });
                        }
                    }
                }
            }
            scanSummary = "Plan=" + planCount + "，Key 条目=" + entryCount + "，错误=" + errors + "，警告=" + warnings;
            Repaint();
            return errors == 0;
        }

        private void DrawIssueSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("③ 修复检查问题", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                issueSearch = EditorGUILayout.TextField("筛选", issueSearch);
                IEnumerable<Issue> visible = issues.Where(item => item != null
                    && (showWarnings || item.Error)
                    && (string.IsNullOrWhiteSpace(issueSearch)
                        || (item.Title?.IndexOf(issueSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                        || (item.Message?.IndexOf(issueSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0));
                int count = 0;
                foreach (Issue issue in visible)
                {
                    count++;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent icon = EditorGUIUtility.IconContent(issue.Error ? "console.erroricon" : "console.warnicon");
                        GUILayout.Label(icon, GUILayout.Width(18f), GUILayout.Height(18f));
                        EditorGUILayout.LabelField(issue.Title + "：" + issue.Message, EditorStyles.miniLabel);
                        if (issue.Owner != null && GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(42f)))
                        {
                            Selection.activeObject = issue.Owner;
                            EditorGUIUtility.PingObject(issue.Owner);
                        }
                    }
                }
                if (count == 0)
                    EditorGUILayout.LabelField("没有扫描结果。先点击“扫描 ResourcePlan”或“检查 Catalog”。", EditorStyles.miniLabel);
            }
        }
    }
}
