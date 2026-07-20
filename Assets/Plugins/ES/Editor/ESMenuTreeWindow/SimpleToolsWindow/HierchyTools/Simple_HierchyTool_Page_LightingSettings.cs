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

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近灯光操作", lastResultSummary, lastResultDetail);
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
            string previewMessage = $"将修改以下 {filteredObjects.Count} 个对象：\n" +
                                   string.Join("\n", filteredObjects.Take(10).Select(obj => obj.name)) +
                                   (filteredObjects.Count > 10 ? "\n..." : "");
            if (!EditorUtility.DisplayDialog("预览修改", previewMessage, "确认应用", "取消"))
            {
                return;
            }

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
            string previewMessage = $"将随机修改以下 {filteredObjects.Count} 个对象：\n" +
                                   string.Join("\n", filteredObjects.Take(10).Select(obj => obj.name)) +
                                   (filteredObjects.Count > 10 ? "\n..." : "");
            if (!EditorUtility.DisplayDialog("预览随机修改", previewMessage, "确认应用", "取消"))
            {
                return;
            }

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
            string previewMessage = $"将为以下 {filteredObjects.Count} 个对象添加Light组件：\n" +
                                   string.Join("\n", filteredObjects.Take(10).Select(obj => obj.name)) +
                                   (filteredObjects.Count > 10 ? "\n..." : "");
            if (!EditorUtility.DisplayDialog("预览添加", previewMessage, "确认添加", "取消"))
            {
                return;
            }

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
            string previewMessage = $"将转换以下 {filteredLights.Count} 个灯光为烘焙模式：\n" +
                                   string.Join("\n", filteredLights.Take(10).Select(light => light.gameObject.name)) +
                                   (filteredLights.Count > 10 ? "\n..." : "");
            if (!EditorUtility.DisplayDialog("预览转换", previewMessage, "确认转换", "取消"))
            {
                return;
            }

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
