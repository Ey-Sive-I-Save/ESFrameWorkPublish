using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;


namespace ES
{

    #region 灯光设置工具
    [Serializable]
    public class Page_LightingSettings : ESWindowPageBase
    {
        [Title("灯光设置工具", "批量调整灯光属性", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择带有Light组件的GameObject，\n设置灯光属性，\n点击应用按钮批量修改";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                var targets = SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren);
                int lightCount = targets.Count(obj => obj != null && obj.GetComponent<Light>() != null);
                string filter = (useTypeFilter ? $"类型={filterType}" : "类型不限") + ", " + (useNameFilter ? $"名称包含“{nameFilter}”" : "名称不限");
                return $"当前选择: {selectedCount} 个对象 | 实际目标: {targets.Count} 个 | 命中灯光: {lightCount} 个 | 过滤: {filter}";
            }
        }

        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        [LabelText("灯光类型"), Space(5)]
        public LightType lightType = LightType.Point;

        [LabelText("颜色"), Space(5)]
        public Color lightColor = Color.white;

        [LabelText("强度"), Range(0f, 10f), Space(5)]
        public float intensity = 1f;

        [LabelText("范围"), Range(0f, 100f), Space(5)]
        public float range = 10f;

        [LabelText("阴影类型"), Space(5)]
        public LightShadows shadowType = LightShadows.None;

        [LabelText("烘焙模式"), Space(5)]
        public LightmapBakeType bakeType = LightmapBakeType.Realtime;

        [FoldoutGroup("条件过滤", expanded: false)]
        [LabelText("启用类型过滤"), Space(5)]
        public bool useTypeFilter = false;

        [FoldoutGroup("条件过滤")]
        [LabelText("过滤灯光类型"), ShowIf("@useTypeFilter"), Space(5)]
        public LightType filterType = LightType.Point;

        [FoldoutGroup("条件过滤")]
        [LabelText("启用名称过滤"), Space(5)]
        public bool useNameFilter = false;

        [FoldoutGroup("条件过滤")]
        [LabelText("名称包含"), ShowIf("@useNameFilter"), Space(5)]
        public string nameFilter = "";

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string lightSearch = "";
        private int lightPreviewPageIndex;
        private const int LightPreviewPageSize = 12;

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            int lightCount = GetFilteredSelectedLights().Count;
            SimpleToolsPanelUtility.DrawToolHeader(
                "灯光批量设置",
                "用于统一选区内 Light 参数、随机化灯光表现、批量添加 Light，或把当前加载场景里的灯光转为烘焙模式。",
                SimpleToolsMaturity.Upgrading,
                "灯光会影响画面、烘焙结果和运行时性能；“转为烘焙”会扫描已加载场景，不只处理当前选区。");
            SimpleToolsPanelUtility.DrawLargeListGuard(lightCount, "灯光");
            DrawLightingActionPanel();
            DrawLightPreviewPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近灯光操作", lastResultSummary, lastResultDetail);
        }

        private void DrawLightingActionPanel()
        {
            var selectedLights = GetFilteredSelectedLights();
            int bakedCount = selectedLights.Count(light => light != null && light.lightmapBakeType == LightmapBakeType.Baked);
            int realtimeCount = selectedLights.Count(light => light != null && light.lightmapBakeType == LightmapBakeType.Realtime);
            int shadowCount = selectedLights.Count(light => light != null && light.shadows != LightShadows.None);

            SimpleToolsPanelUtility.DrawSectionTitle("核心流程", "先确认选区灯光，再选择统一写入、随机化、补组件或全场景转烘焙。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"选区命中: {selectedLights.Count}",
                    $"实时: {realtimeCount}",
                    $"烘焙: {bakedCount}",
                    $"有阴影: {shadowCount}",
                    $"过滤: {(useTypeFilter ? filterType.ToString() : "类型不限")}");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("应用设置", SimpleToolsActionTone.Warning, 30, GUILayout.Width(92)))
                        ApplyLightingSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("随机化", SimpleToolsActionTone.Primary, 30, GUILayout.Width(92)))
                        ApplyRandomLightingSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("补 Light", SimpleToolsActionTone.Success, 30, GUILayout.Width(92)))
                        AddLightComponents();
                    if (SimpleToolsPanelUtility.DrawActionButton("转烘焙", SimpleToolsActionTone.Danger, 30, GUILayout.Width(92)))
                        ConvertAllToBaked();
                    if (SimpleToolsPanelUtility.DrawActionButton("重置参数", SimpleToolsActionTone.Neutral, 30, GUILayout.Width(92)))
                        ResetToDefaults();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawLightPreviewPanel()
        {
            var lights = GetFilteredSelectedLights();
            SimpleToolsPanelUtility.DrawSectionTitle("选区灯光预览", "按对象路径、灯光类型、烘焙模式搜索；大选区自动分页。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    lightSearch = EditorGUILayout.TextField(lightSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        lightSearch = string.Empty;
                        lightPreviewPageIndex = 0;
                    }
                }

                if (lights.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前选区没有命中的灯光。请先选择带 Light 的对象，或开启包含子对象。");
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("对象路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180));
                    EditorGUILayout.LabelField("类型", EditorStyles.miniBoldLabel, GUILayout.Width(64));
                    EditorGUILayout.LabelField("强度", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    EditorGUILayout.LabelField("范围", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    EditorGUILayout.LabelField("烘焙", EditorStyles.miniBoldLabel, GUILayout.Width(72));
                    GUILayout.Space(48);
                }

                foreach (var light in SimpleToolsPanelUtility.PageItems(lights, ref lightPreviewPageIndex, LightPreviewPageSize, out _))
                    DrawLightPreviewRow(light);

                SimpleToolsPanelUtility.DrawPager(ref lightPreviewPageIndex, lights.Count, LightPreviewPageSize);
            }
        }

        private List<Light> GetFilteredSelectedLights()
        {
            var targets = SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren)
                .Select(obj => obj != null ? obj.GetComponent<Light>() : null)
                .Where(light => light != null)
                .Where(light => !useTypeFilter || light.type == filterType)
                .Where(light => light.gameObject != null && NameMatches(light.gameObject.name));

            if (!string.IsNullOrWhiteSpace(lightSearch))
            {
                string keyword = lightSearch.Trim();
                targets = targets.Where(light =>
                    ContainsIgnoreCase(SimpleToolsSafetyUtility.GetHierarchyPath(light.gameObject), keyword) ||
                    ContainsIgnoreCase(light.type.ToString(), keyword) ||
                    ContainsIgnoreCase(light.lightmapBakeType.ToString(), keyword));
            }

            return targets.Distinct().ToList();
        }

        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(keyword) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawLightPreviewRow(Light light)
        {
            if (light == null || light.gameObject == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(SimpleToolsSafetyUtility.GetHierarchyPath(light.gameObject), EditorStyles.miniLabel, GUILayout.MinWidth(180));
                EditorGUILayout.LabelField(light.type.ToString(), EditorStyles.miniLabel, GUILayout.Width(64));
                EditorGUILayout.LabelField(light.intensity.ToString("0.##"), EditorStyles.miniLabel, GUILayout.Width(52));
                EditorGUILayout.LabelField(light.range.ToString("0.##"), EditorStyles.miniLabel, GUILayout.Width(52));
                EditorGUILayout.LabelField(light.lightmapBakeType.ToString(), EditorStyles.miniLabel, GUILayout.Width(72));
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Selection.activeGameObject = light.gameObject;
                    EditorGUIUtility.PingObject(light.gameObject);
                }
            }
        }

        [FoldoutGroup("随机化属性", expanded: false)]
        [LabelText("随机强度范围"), Space(5)]
        public Vector2 randomIntensityRange = new Vector2(0.5f, 2f);

        [FoldoutGroup("随机化属性")]
        [LabelText("随机颜色范围-MIN"), Space(5)]
        public Color randomColorMin = Color.white * 0.5f;

        [FoldoutGroup("随机化属性")]
        [LabelText("随机颜色范围-MAX"), Space(5)]
        public Color randomColorMax = Color.white;

        [FoldoutGroup("4. 旧按钮入口", Expanded = false)]
        [Button("应用灯光设置", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void ApplyLightingSettings()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);

            if (!ValidateNameFilter())
                return;
            NormalizeRandomRanges();

            // 过滤对象
            var filteredObjects = allObjects.Where(obj =>
            {
                var light = obj.GetComponent<Light>();
                if (light == null) return false;
                if (useTypeFilter && light.type != filterType) return false;
                if (!NameMatches(obj.name)) return false;
                return true;
            }).ToList();

            if (filteredObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有符合条件的灯光对象！", "确定");
                return;
            }

            // 操作预览
            string previewMessage = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(SimpleToolsSafetyUtility.GetHierarchyPath), 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认应用灯光设置",
                filteredObjects.Count,
                "修改以下灯光：\n" + previewMessage,
                "会覆盖目标 Light 的类型、颜色、强度、范围、阴影和烘焙模式。"))
                return;

            int modifiedCount = 0;
            Undo.SetCurrentGroupName("Apply Lighting Settings");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < filteredObjects.Count; i++)
                {
                    var obj = filteredObjects[i];
                    EditorUtility.DisplayProgressBar("应用灯光设置", $"修改: {obj.name}", (float)i / filteredObjects.Count);

                    var light = obj.GetComponent<Light>();
                    if (light != null)
                    {
                        Undo.RecordObject(light, "Modify Light");

                        light.type = lightType;
                        light.color = lightColor;
                        light.intensity = intensity;
                        light.range = range;
                        light.shadows = shadowType;
                        light.lightmapBakeType = bakeType;

                        EditorUtility.SetDirty(light);
                        modifiedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(filteredObjects);
            lastResultSummary = $"灯光设置完成: 修改 {modifiedCount} 个 | 目标 {filteredObjects.Count} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(obj => obj.name), 12);
            EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个灯光组件！", "确定");
        }

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("应用随机灯光设置", ButtonHeight = 34), GUIColor(0.25f, 0.62f, 0.45f)]
        public void ApplyRandomLightingSettings()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);

            if (!ValidateNameFilter())
                return;

            // 过滤对象
            var filteredObjects = allObjects.Where(obj =>
            {
                var light = obj.GetComponent<Light>();
                if (light == null) return false;
                if (useTypeFilter && light.type != filterType) return false;
                if (!NameMatches(obj.name)) return false;
                return true;
            }).ToList();

            if (filteredObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有符合条件的灯光对象！", "确定");
                return;
            }

            // 操作预览
            string previewMessage = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(SimpleToolsSafetyUtility.GetHierarchyPath), 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认随机灯光设置",
                filteredObjects.Count,
                "随机修改以下灯光：\n" + previewMessage,
                "会批量随机化目标 Light 的颜色和强度，并覆盖范围、阴影、烘焙模式。"))
                return;

            int modifiedCount = 0;
            Undo.SetCurrentGroupName("Apply Random Lighting Settings");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < filteredObjects.Count; i++)
                {
                    var obj = filteredObjects[i];
                    EditorUtility.DisplayProgressBar("应用随机灯光设置", $"修改: {obj.name}", (float)i / filteredObjects.Count);

                    var light = obj.GetComponent<Light>();
                    if (light != null)
                    {
                        Undo.RecordObject(light, "Modify Light Randomly");

                        light.type = lightType;
                        light.color = new Color(
                            UnityEngine.Random.Range(randomColorMin.r, randomColorMax.r),
                            UnityEngine.Random.Range(randomColorMin.g, randomColorMax.g),
                            UnityEngine.Random.Range(randomColorMin.b, randomColorMax.b),
                            UnityEngine.Random.Range(randomColorMin.a, randomColorMax.a)
                        );
                        light.intensity = UnityEngine.Random.Range(randomIntensityRange.x, randomIntensityRange.y);
                        light.range = range;
                        light.shadows = shadowType;
                        light.lightmapBakeType = bakeType;

                        EditorUtility.SetDirty(light);
                        modifiedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(filteredObjects);
            lastResultSummary = $"随机灯光完成: 修改 {modifiedCount} 个 | 目标 {filteredObjects.Count} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(obj => obj.name), 12);
            EditorUtility.DisplayDialog("成功", $"成功随机修改 {modifiedCount} 个灯光组件！", "确定");
        }

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("批量添加Light组件", ButtonHeight = 34), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void AddLightComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            // 过滤对象（只添加没有Light的）
            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var filteredObjects = allObjects.Where(obj => obj != null && obj.GetComponent<Light>() == null).ToList();

            if (filteredObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "所有选中的对象已有Light组件！", "确定");
                return;
            }

            // 操作预览
            string previewMessage = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(SimpleToolsSafetyUtility.GetHierarchyPath), 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认添加 Light 组件",
                filteredObjects.Count,
                "为以下对象添加 Light：\n" + previewMessage,
                "会给没有 Light 的目标对象新增组件，并写入当前灯光参数。"))
                return;

            int addedCount = 0;
            Undo.SetCurrentGroupName("Add Light Components");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < filteredObjects.Count; i++)
                {
                    var obj = filteredObjects[i];
                    EditorUtility.DisplayProgressBar("添加Light组件", $"添加: {obj.name}", (float)i / filteredObjects.Count);

                    var light = Undo.AddComponent<Light>(obj);
                    light.type = lightType;
                    light.color = lightColor;
                    light.intensity = intensity;
                    light.range = range;
                    light.shadows = shadowType;
                    light.lightmapBakeType = bakeType;
                    EditorUtility.SetDirty(light);
                    addedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(filteredObjects);
            lastResultSummary = $"添加 Light 完成: 添加 {addedCount} 个 | 目标 {filteredObjects.Count} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(filteredObjects.Select(obj => obj.name), 12);
            EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个Light组件！", "确定");
        }

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("将所有灯光转为烘焙", ButtonHeight = 34), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        public void ConvertAllToBaked()
        {
            if (!ValidateNameFilter())
                return;

            var allLights = GetLoadedSceneLights();
            var filteredLights = allLights.Where(light =>
            {
                if (light == null || light.gameObject == null) return false;
                if (useTypeFilter && light.type != filterType) return false;
                if (!NameMatches(light.gameObject.name)) return false;
                return true;
            }).ToList();

            if (filteredLights.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有符合条件的灯光！", "确定");
                return;
            }

            // 操作预览
            string previewMessage = SimpleToolsSafetyUtility.JoinPreview(filteredLights.Select(light => light != null && light.gameObject != null ? SimpleToolsSafetyUtility.GetHierarchyPath(light.gameObject) : null), 12);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认转换为烘焙灯光",
                filteredLights.Count,
                "转换以下已加载场景灯光：\n" + previewMessage,
                "该操作扫描当前已加载场景，不限当前选区，会把命中灯光的 LightmapBakeType 改为 Baked。"))
                return;

            int convertedCount = 0;
            Undo.SetCurrentGroupName("Convert Lights to Baked");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                for (int i = 0; i < filteredLights.Count; i++)
                {
                    var light = filteredLights[i];
                    EditorUtility.DisplayProgressBar("转换灯光", $"转换: {light.gameObject.name}", (float)i / filteredLights.Count);

                    Undo.RecordObject(light, "Convert to Baked");
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    EditorUtility.SetDirty(light);
                    convertedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(filteredLights.Select(light => light != null ? light.gameObject : null));
            lastResultSummary = $"转烘焙完成: 转换 {convertedCount} 个 | 命中 {filteredLights.Count} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(filteredLights.Select(light => light != null && light.gameObject != null ? light.gameObject.name : null), 12);
            EditorUtility.DisplayDialog("成功", $"成功转换 {convertedCount} 个灯光为烘焙模式！", "确定");
        }

        private List<Light> GetLoadedSceneLights()
        {
            var lights = new List<Light>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;

                    lights.AddRange(root.GetComponentsInChildren<Light>(true));
                }
            }

            return lights.Where(light => light != null).Distinct().ToList();
        }

        private void NormalizeRandomRanges()
        {
            if (randomIntensityRange.x > randomIntensityRange.y)
                randomIntensityRange = new Vector2(randomIntensityRange.y, randomIntensityRange.x);

            Color min = randomColorMin;
            Color max = randomColorMax;
            randomColorMin = new Color(
                Mathf.Min(min.r, max.r),
                Mathf.Min(min.g, max.g),
                Mathf.Min(min.b, max.b),
                Mathf.Min(min.a, max.a));

            randomColorMax = new Color(
                Mathf.Max(min.r, max.r),
                Mathf.Max(min.g, max.g),
                Mathf.Max(min.b, max.b),
                Mathf.Max(min.a, max.a));
        }

        private bool ValidateNameFilter()
        {
            if (!useNameFilter || !string.IsNullOrWhiteSpace(nameFilter))
                return true;

            EditorUtility.DisplayDialog("名称过滤为空", "已启用名称过滤，请输入要匹配的名称片段，或关闭名称过滤。", "知道了");
            return false;
        }

        private bool NameMatches(string objectName)
        {
            return !useNameFilter ||
                   (!string.IsNullOrEmpty(objectName) &&
                    objectName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("重置为默认设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ResetToDefaults()
        {
            includeChildren = true;
            lightType = LightType.Point;
            lightColor = Color.white;
            intensity = 1f;
            range = 10f;
            shadowType = LightShadows.None;
            bakeType = LightmapBakeType.Realtime;
            useTypeFilter = false;
            filterType = LightType.Point;
            useNameFilter = false;
            nameFilter = "";
            randomIntensityRange = new Vector2(0.5f, 2f);
            randomColorMin = Color.white * 0.5f;
            randomColorMax = Color.white;
        }
    }
    #endregion

}
