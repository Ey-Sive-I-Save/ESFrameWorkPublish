#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ES.EditorInternal;

namespace ES
{
    [Serializable]
    public sealed class ESWorkbenchIntegrationTestAsset : ScriptableObject
    {
        public string workspaceId = "ugc.integration.test";
        public string title = "UGC 世界综合案例";
        public int revision = 1;
        public string lastCommit = "未提交";
        public string activeModule = "地形";
        public bool previewEnabled = true;
        public bool inputValid = true;
        public bool preflightCompleted;
        public bool simulatedFailure;
        public int terrainBrushSize = 4;
        public float terrainHeight = 0.5f;
        public int materialLayerCount = 3;
        public int vegetationDensity = 680;
        public int prefabInstanceCount = 128;
        public float navMeshSlope = 42f;
        public bool weatherEnabled = true;
        public int streamingRadius = 2;
        public bool collisionEnabled = true;
        public int ugcBudget = 10000;
        public List<string> eventLog = new List<string>();
    }

    internal sealed class ESWorkbenchIntegrationTestPersistenceAdapter : IESWorkbenchPersistenceAdapter<ESWorkbenchIntegrationTestAsset>
    {
        public bool TrySave(ESWorkbenchIntegrationTestAsset asset, SerializedObject serializedObject, out string message)
        {
            serializedObject?.ApplyModifiedProperties();
            if (asset == null) { message = "测试资产不存在。"; return false; }
            if (!asset.preflightCompleted) { message = "尚未执行预检，测试提交被阻断。"; return false; }
            if (!asset.inputValid) { message = "测试输入未通过预检，保存被阻断。"; return false; }
            if (asset.simulatedFailure) { message = "已模拟提交失败：正式输出阶段不可用。"; return false; }
            asset.revision = Mathf.Max(1, asset.revision) + 1;
            asset.lastCommit = "test-commit-r" + asset.revision;
            asset.eventLog.Add("提交成功：" + asset.lastCommit);
            EditorUtility.SetDirty(asset);
            message = "测试提交成功（内存快照，未写入项目资产）。";
            return true;
        }
    }

    /// <summary>
    /// 纯底座集成测试工作台：混合 World 类模块，验证多页面、多插槽、事务、预览和失败恢复。
    /// 该窗口只使用 HideAndDontSave 内存资产，不写入正式地图、Scene 或资源管线。
    /// </summary>
    public sealed class ESWorkbenchIntegrationTestWindow : ESWorkbenchWindowBase<ESWorkbenchIntegrationTestWindow, ESWorkbenchIntegrationTestAsset>
    {
        private static readonly IESWorkbenchPersistenceAdapter<ESWorkbenchIntegrationTestAsset> PersistenceAdapter = new ESWorkbenchIntegrationTestPersistenceAdapter();
        private ESWorkbenchIntegrationTestAsset testAsset => ESWorkbench_Asset;
        private SerializedObject serializedAsset => ESWorkbench_SerializedAsset;
        private string[] moduleNames;

        [MenuItem("【ES】/验证与诊断/工作台/底座综合集成测试", false, 270)]
        public static void Open()
        {
            OpenWindow();
        }

        protected override IESWorkbenchPersistenceAdapter<ESWorkbenchIntegrationTestAsset> ESWorkbench_PersistenceAdapter => PersistenceAdapter;
        protected override string ESWorkbench_WorkbenchId => "workbench.integration-test";
        protected override List<ESWorkbenchModuleKind> ESWorkbench_DefaultModules => new List<ESWorkbenchModuleKind>
        {
            ESWorkbenchModuleKind.Overview,
            ESWorkbenchModuleKind.Terrain,
            ESWorkbenchModuleKind.Material,
            ESWorkbenchModuleKind.Vegetation,
            ESWorkbenchModuleKind.Prefab,
            ESWorkbenchModuleKind.Navigation,
            ESWorkbenchModuleKind.WaterWeather,
            ESWorkbenchModuleKind.Streaming,
            ESWorkbenchModuleKind.Collision,
            ESWorkbenchModuleKind.UGC
        };

        protected override void ESWorkbench_AdjustModules(List<ESWorkbenchModuleKind> modules)
        {
            // 综合测试默认覆盖全部标准模块；派生测试窗口可以在此处删除、插入或排序模块。
        }
        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 工作台底座综合集成测试", "纯测试：模拟 World 类专业工作台的多模块编辑闭环。");
        protected override string ESWindow_Subtitle => "纯内存测试 · 地形、材质、植被、Prefab、导航、天气、流式、碰撞、UGC";
        protected override Vector2 ESWindow_MinSize => new Vector2(980f, 620f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1280f, 820f);
        protected override string ESWindow_PageStableId => "workbench.integration-test";
        protected override string ESWindow_PageTitle => "工作台底座综合集成测试";
        protected override string ESWindow_PageKeywords => "工作台 底座 World 地形 材质 植被 Prefab 导航 天气 流式 碰撞 UGC 测试";

        protected override void ESWindow_BuildPageActions(ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction("integration.preflight", "执行预检", "验证测试输入和 UGC 配额。", _ => RunPreflight()).WithPriority(100));
            actions.Add(new ESMenuTreePageAction("integration.commit", "提交测试快照", "执行内存提交，不写入正式资产。", _ => ESWorkbench_Save()).WithPriority(90));
            actions.Add(new ESMenuTreePageAction("integration.fail", "模拟失败", "模拟正式输出失败并测试恢复路径。", _ => SimulateFailure()).WithPriority(80));
        }

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            moduleNames = ESWorkbench_GetActiveModuleDisplayNames();
            if (testAsset == null)
            {
                ESWorkbenchIntegrationTestAsset asset = CreateInstance<ESWorkbenchIntegrationTestAsset>();
                asset.hideFlags = HideFlags.HideAndDontSave;
                asset.eventLog.Add("创建纯内存测试资产");
                ESWorkbench_BindAsset(asset);
            }
            RegisterPages();
            ESWorkbench_SelectPage(ESWorkbench_ActiveModules.Count > 0
                ? GetPageId(ESWorkbench_ActiveModules[0])
                : "overview");
        }

        protected override void ESWindow_OnHostDisable()
        {
            ESWorkbenchIntegrationTestAsset asset = testAsset;
            base.ESWindow_OnHostDisable();
            if (asset != null) DestroyImmediate(asset);
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            if (testAsset == null) return;
            serializedAsset.Update();
            SyncActiveModuleFromPage();
            ESWorkbench_DrawHero(testAsset.title, "底座扩展合同测试 · 纯内存、不写入正式资产", ESWorkbench_IsDirty ? "有未提交变更" : "已提交");
            using (new EditorGUILayout.HorizontalScope())
            {
                ESWorkbench_DrawMetric("模块数量", moduleNames == null ? "0" : moduleNames.Length.ToString());
                ESWorkbench_DrawMetric("Revision", testAsset.revision.ToString());
                ESWorkbench_DrawMetric("当前模块", testAsset.activeModule);
                ESWorkbench_DrawMetric("预览", testAsset.previewEnabled ? "已启用" : "已关闭", testAsset.previewEnabled ? ESStatusKind.Ready : ESStatusKind.Warning);
            }
            ESWorkbench_DrawStandardLayout(DrawInspectorPanel);
            bool changed = serializedAsset.hasModifiedProperties;
            serializedAsset.ApplyModifiedProperties();
            if (changed) ESWorkbench_MarkDirty("integration." + testAsset.activeModule, ESWorkbenchDirtyFlags.Authoring);
        }

        private void RegisterPages()
        {
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Overview))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("overview", "总览", "当前测试状态与模块闭环", ESWorkbenchDirtyFlags.Authoring, DrawOverview, drawDiagnostics: DrawDiagnostics, drawFooter: DrawFooter));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Terrain))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("terrain", "地形", "高度刷和 Unity Terrain 类预览", ESWorkbenchDirtyFlags.Authoring, DrawTerrain, drawPreview: DrawPreview, drawDiagnostics: DrawDiagnostics));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Material))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("materials", "材质层", "地表层与高度/坡度规则", ESWorkbenchDirtyFlags.Authoring, DrawMaterials));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Vegetation))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("vegetation", "植被 / 细节", "密度与细节层", ESWorkbenchDirtyFlags.Authoring, DrawVegetation));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Prefab))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("prefabs", "Prefab 散布", "批量对象布局", ESWorkbenchDirtyFlags.Authoring, DrawPrefabs, drawPreview: DrawPreview));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Navigation))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("navigation", "导航 / AI", "可行走坡度和烘焙状态", ESWorkbenchDirtyFlags.Authoring, DrawNavigation, drawDiagnostics: DrawDiagnostics));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.WaterWeather))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("environment", "水体 / 天气", "环境表现组合", ESWorkbenchDirtyFlags.Authoring, DrawEnvironment));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Streaming))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("streaming", "地形块流式", "加载半径和区块预算", ESWorkbenchDirtyFlags.Authoring, DrawStreaming));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.Collision))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("collision", "碰撞 / 物理", "碰撞策略和物理材质", ESWorkbenchDirtyFlags.Authoring, DrawCollision));
            if (ESWorkbench_IsModuleEnabled(ESWorkbenchModuleKind.UGC))
                ESWorkbench_RegisterPage(new ESWorkbenchPageDefinition("build", "构建 / UGC", "预检、提交、失败恢复", ESWorkbenchDirtyFlags.Build, DrawBuild, drawFooter: DrawFooter));
        }

        private void DrawInspectorPanel()
        {
            GUILayout.Label("当前模块检查器", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox(ESWorkbench_Status, ESWorkbench_StatusType);
            EditorGUILayout.LabelField("工作区", testAsset.workspaceId);
            EditorGUILayout.LabelField("模块", testAsset.activeModule);
            EditorGUILayout.LabelField("Revision", testAsset.revision.ToString());
            EditorGUILayout.LabelField("最近提交", testAsset.lastCommit);
            int selectedModule = Mathf.Max(0, Array.IndexOf(moduleNames, testAsset.activeModule));
            int nextModule = EditorGUILayout.Popup("模块选择", selectedModule, moduleNames);
            if (nextModule != selectedModule)
            {
                ESWorkbenchModuleKind module = ESWorkbench_ActiveModules[nextModule];
                testAsset.activeModule = ESWorkbench_GetModuleDisplayName(module);
                ESWorkbench_SelectPage(GetPageId(module));
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("执行模块动作", GUILayout.Height(28f))) RunModuleAction();
            if (GUILayout.Button(testAsset.previewEnabled ? "关闭预览" : "打开预览", GUILayout.Height(26f))) testAsset.previewEnabled = !testAsset.previewEnabled;
            if (GUILayout.Button("执行预检", GUILayout.Height(26f))) RunPreflight();
            if (GUILayout.Button("提交测试快照", GUILayout.Height(26f))) ESWorkbench_Save();
            if (GUILayout.Button("模拟失败 / 恢复", GUILayout.Height(26f))) SimulateFailure();
        }

        private void DrawOverview()
        {
            GUILayout.Label("World 类综合体验（纯测试）", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("同一工作台内混合多个专业模块，验证底座页面注册、状态、预览、提交和恢复。所有数据只存在于内存。", MessageType.Info);
            DrawProperty("title");
            DrawProperty("activeModule");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric("地形高度", testAsset.terrainHeight.ToString("0.00"), ESStatusKind.Modified);
                DrawMetric("材质层", testAsset.materialLayerCount.ToString(), ESStatusKind.Ready);
                DrawMetric("Prefab", testAsset.prefabInstanceCount.ToString(), testAsset.prefabInstanceCount > testAsset.ugcBudget ? ESStatusKind.Error : ESStatusKind.Ready);
                DrawMetric("导航坡度", testAsset.navMeshSlope.ToString("0") + "°", ESStatusKind.Ready);
            }
        }

        private void DrawTerrain()
        {
            DrawModuleTitle("地形编辑", "模拟高度笔刷、Terrain 后端预览和局部参数。");
            DrawProperty("terrainBrushSize");
            DrawProperty("terrainHeight");
            DrawProperty("previewEnabled");
        }

        private void DrawMaterials() { DrawModuleTitle("地形材质层", "模拟按高度、坡度和区域组合材质。", "materialLayerCount"); }
        private void DrawVegetation() { DrawModuleTitle("植被 / 细节", "模拟植被密度、细节层和生态参数。", "vegetationDensity"); }
        private void DrawPrefabs() { DrawModuleTitle("Prefab 批量散布", "模拟散布实例、预算和预览。", "prefabInstanceCount"); }
        private void DrawNavigation() { DrawModuleTitle("导航 / AI 烘焙", "模拟可行走坡度和 NavMesh 烘焙配置。", "navMeshSlope"); }
        private void DrawEnvironment() { DrawModuleTitle("水体 / 天气", "模拟环境开关和天气预览。", "weatherEnabled"); }
        private void DrawStreaming() { DrawModuleTitle("地形块流式", "模拟区块加载半径和运行时加载策略。", "streamingRadius"); }
        private void DrawCollision() { DrawModuleTitle("碰撞 / 物理", "模拟 TerrainCollider 和物理材质策略。", "collisionEnabled"); }

        private void DrawBuild()
        {
            DrawModuleTitle("构建 / UGC", "这是纯测试闭环：预检 → 编辑 → 预览 → 提交 → 恢复。", "ugcBudget");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("预检", GUILayout.Height(28f))) RunPreflight();
                if (GUILayout.Button("提交", GUILayout.Height(28f))) ESWorkbench_Save();
                if (GUILayout.Button("模拟失败", GUILayout.Height(28f))) SimulateFailure();
            }
            EditorGUILayout.HelpBox("正式项目不会被写入；提交只增加内存 Revision 和事件日志。", MessageType.None);
        }

        private void DrawPreview()
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("统一预览插槽", ESEditorPresentation.HeaderStyle);
                EditorGUILayout.HelpBox(testAsset.previewEnabled ? "预览已启用：这里代表 Terrain / Prefab / 运行时数据的统一预览区域。" : "预览已关闭。", testAsset.previewEnabled ? MessageType.Info : MessageType.Warning);
                Rect bar = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(bar, ESEditorPresentation.GetDepthBackground(1));
                    EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(testAsset.terrainHeight), bar.height), ESEditorPresentation.MapHeightHighColor);
                }
            }
        }

        private void DrawDiagnostics()
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUILayout.Label("统一诊断插槽", ESEditorPresentation.HeaderStyle);
                bool budgetOk = testAsset.prefabInstanceCount <= testAsset.ugcBudget;
                EditorGUILayout.HelpBox(budgetOk ? "预算检查通过。" : "Prefab 数量超过 UGC 预算。", budgetOk ? MessageType.Info : MessageType.Warning);
                EditorGUILayout.LabelField("事件日志", testAsset.eventLog == null ? "0" : testAsset.eventLog.Count.ToString());
            }
        }

        private void DrawFooter()
        {
            GUILayout.Space(8f);
            GUILayout.Label("下一步：修改参数后执行预检，再提交测试快照。", ESEditorPresentation.MetaStyle);
        }

        private void DrawModuleTitle(string title, string subtitle, string property = null)
        {
            GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
            GUILayout.Label(subtitle, ESEditorPresentation.SubtitleStyle);
            if (!string.IsNullOrWhiteSpace(property)) DrawProperty(property);
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedAsset?.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(property.displayName));
        }

        private void DrawMetric(string label, string value, ESStatusKind status)
        {
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle, GUILayout.MinWidth(120f), GUILayout.Height(54f)))
            {
                GUILayout.Label(label, ESEditorPresentation.MetaStyle);
                Color previous = GUI.color;
                GUI.color = ESEditorPresentation.GetStatusAccent(0, status);
                GUILayout.Label(value, ESEditorPresentation.HeaderStyle);
                GUI.color = previous;
            }
        }

        private void RunPreflight()
        {
            bool budgetOk = testAsset.prefabInstanceCount <= Mathf.Max(1, testAsset.ugcBudget);
            testAsset.inputValid = budgetOk && testAsset.materialLayerCount > 0 && testAsset.terrainBrushSize > 0;
            testAsset.preflightCompleted = true;
            testAsset.simulatedFailure = false;
            testAsset.eventLog.Add(testAsset.inputValid ? "预检通过" : "预检失败：参数或 UGC 预算无效");
            ESWorkbench_MarkDirty("integration.preflight", ESWorkbenchDirtyFlags.Build);
            ESWorkbench_SetStatus(testAsset.inputValid ? "预检通过，可以提交测试快照。" : "预检失败，请修正参数后重试。", testAsset.inputValid ? MessageType.Info : MessageType.Error);
        }

        private void RunModuleAction()
        {
            testAsset.eventLog.Add("执行模块动作：" + testAsset.activeModule);
            ESWorkbench_MarkDirty("integration." + testAsset.activeModule, ESWorkbenchDirtyFlags.Authoring);
            ESWorkbench_SetStatus("已执行模块动作：" + testAsset.activeModule, MessageType.Info);
        }

        private void SimulateFailure()
        {
            testAsset.simulatedFailure = true;
            testAsset.inputValid = true;
            testAsset.eventLog.Add("模拟失败：提交阶段阻断");
            ESWorkbench_SetStatus("已模拟失败；请重新预检或关闭失败模拟后再提交。", MessageType.Error);
        }

        private void SyncActiveModuleFromPage()
        {
            ESWorkbenchModuleKind? module = GetModuleForPageId(ESWorkbench_SelectedPageId);
            if (module.HasValue && ESWorkbench_IsModuleEnabled(module.Value))
                testAsset.activeModule = ESWorkbench_GetModuleDisplayName(module.Value);
        }

        private static string GetPageId(ESWorkbenchModuleKind module)
        {
            switch (module)
            {
                case ESWorkbenchModuleKind.Overview: return "overview";
                case ESWorkbenchModuleKind.Terrain: return "terrain";
                case ESWorkbenchModuleKind.Material: return "materials";
                case ESWorkbenchModuleKind.Vegetation: return "vegetation";
                case ESWorkbenchModuleKind.Prefab: return "prefabs";
                case ESWorkbenchModuleKind.Navigation: return "navigation";
                case ESWorkbenchModuleKind.WaterWeather: return "environment";
                case ESWorkbenchModuleKind.Streaming: return "streaming";
                case ESWorkbenchModuleKind.Collision: return "collision";
                case ESWorkbenchModuleKind.UGC: return "build";
                default: return "overview";
            }
        }

        private static ESWorkbenchModuleKind? GetModuleForPageId(string pageId)
        {
            switch (pageId)
            {
                case "overview": return ESWorkbenchModuleKind.Overview;
                case "terrain": return ESWorkbenchModuleKind.Terrain;
                case "materials": return ESWorkbenchModuleKind.Material;
                case "vegetation": return ESWorkbenchModuleKind.Vegetation;
                case "prefabs": return ESWorkbenchModuleKind.Prefab;
                case "navigation": return ESWorkbenchModuleKind.Navigation;
                case "environment": return ESWorkbenchModuleKind.WaterWeather;
                case "streaming": return ESWorkbenchModuleKind.Streaming;
                case "collision": return ESWorkbenchModuleKind.Collision;
                case "build": return ESWorkbenchModuleKind.UGC;
                default: return null;
            }
        }
    }
}
#endif
