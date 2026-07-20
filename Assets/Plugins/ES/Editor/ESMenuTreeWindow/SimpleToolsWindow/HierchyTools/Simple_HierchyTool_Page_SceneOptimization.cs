#pragma warning disable CS0414 // 字段已分配但从未使用其值
#pragma warning disable CS0649 // 字段从未赋值

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
using UnityEngine.Rendering;
using System.Diagnostics;
using System.Text;
using System.Globalization;

namespace ES
{
    [Flags]
    public enum IssueCategory
    {
        [InspectorName("所有")]
        All = ~0,
        [InspectorName("对象清理")]
        ObjectCleanup = 1,
        [InspectorName("渲染优化")]
        RenderingOptimization = 2,
        [InspectorName("灯光优化")]
        LightingOptimization = 4,
        [InspectorName("网格优化")]
        MeshOptimization = 8,
        [InspectorName("材质优化")]
        MaterialOptimization = 16,
        [InspectorName("纹理优化")]
        TextureOptimization = 32,
        [InspectorName("粒子优化")]
        ParticleOptimization = 64,
        [InspectorName("音频优化")]
        AudioOptimization = 128,
        [InspectorName("物理优化")]
        PhysicsOptimization = 256
    }

    [Flags]
    public enum SeverityFilter
    {
        [InspectorName("所有")]
        All = ~0,
        [InspectorName("高")]
        High = 1,
        [InspectorName("中")]
        Medium = 2,
        [InspectorName("低")]
        Low = 4
    }

    #region 优化数据结构
    [Serializable]
    public class OptimizationIssue
    {
        [HorizontalGroup("Info")]
        [VerticalGroup("Info/Left"), LabelText("类别"), ReadOnly]
        public IssueCategory category;

        [VerticalGroup("Info/Left"), LabelText("描述"), DisplayAsString, LabelWidth(40), ReadOnly]
        public string description;

        [VerticalGroup("Info/Left"), LabelText("严重程度"), ReadOnly]
        public SeverityFilter severity; // High, Medium, Low

        [VerticalGroup("Info/Right"), LabelText("目标对象路径"), ReadOnly]
        public string targetObjectPath;

        [VerticalGroup("Info/Right"), LabelText("修复建议"), DisplayAsString, LabelWidth(60), ReadOnly]
        public string fixAction;

        [VerticalGroup("Info/Right"), LabelText("影响"), DisplayAsString, LabelWidth(40), ReadOnly]
        public int estimatedImpact; // 1-100

        [HorizontalGroup("Actions"), Button("🎯 定位对象", ButtonHeight = 25), GUIColor(0.3f, 0.8f, 0.9f)]
        public void FocusOnObject()
        {
            if (!string.IsNullOrEmpty(targetObjectPath))
            {
                GameObject obj = FindGameObjectByPath(targetObjectPath);
                if (obj != null)
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    SceneView.FrameLastActiveSceneView();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "对象已不存在或路径已改变", "确定");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "该问题没有关联的对象路径", "确定");
            }
        }

        [HorizontalGroup("Actions"), Button("📋 复制路径", ButtonHeight = 25), GUIColor(0.5f, 0.9f, 0.5f)]
        public void CopyObjectPath()
        {
            if (!string.IsNullOrEmpty(targetObjectPath))
            {
                EditorGUIUtility.systemCopyBuffer = targetObjectPath;
                UnityEngine.Debug.Log($"已复制对象路径: {targetObjectPath}");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "该问题没有关联的对象路径", "确定");
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            // Find all root objects in the active scene
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var root in roots)
            {
                if (root.name == parts[0])
                {
                    GameObject current = root;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (current == null) return null;
                        Transform child = current.transform.Find(parts[i]);
                        if (child == null) return null;
                        current = child.gameObject;
                    }
                    return current;
                }
            }
            return null;
        }
    }

    [Serializable]
    public class OptimizationReport
    {
        public DateTime timestamp;
        public string sceneName;
        public Dictionary<string, int> metrics = new Dictionary<string, int>();
        public List<OptimizationIssue> issues = new List<OptimizationIssue>();
        public long totalMemoryUsage;
        public int drawCallsEstimate;
        public float analysisTime;
    }

    [Serializable]
    public class OptimizationConfig
    {
        public string configName = "默认配置";
        public int realtimeLightThreshold = 4;
        public int emptyObjectThreshold = 10;
        public int highPolyThreshold = 10000;
        public int textureSizeThreshold = 10;
        public bool autoBackup = true;
        public bool enableLODGeneration = true;
        public bool enableTextureCompression = true;
        public bool enableMeshCombining = false;
        public bool enableStaticBatching = true;
        public bool enableOcclusionCulling = false;
    }

    [Serializable]
    public class ImportSettingChangeRecord
    {
        [LabelText("资源路径"), ReadOnly]
        public string assetPath;

        [LabelText("资源类型"), ReadOnly]
        public string importerType;

        [LabelText("修改项"), ReadOnly]
        public string changeSummary;

        [LabelText("修改前"), TextArea(2, 5), ReadOnly]
        public string beforeState;

        [LabelText("修改后"), TextArea(2, 5), ReadOnly]
        public string afterState;

        [LabelText("时间"), ReadOnly]
        public string changedAt;
    }

    [Serializable]
    public class ImportSettingRollbackData
    {
        public string generatedAt;
        public string sceneName;
        public List<ImportSettingChangeRecord> changes = new List<ImportSettingChangeRecord>();
    }
    #endregion

    #region 场景优化工具
    [Serializable]
    public class Page_SceneOptimization : ESWindowPageBase
    {
        [Title("场景优化系统", "性能分析与自动化优化解决方案", bold: true)]
        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "全面分析当前场景的性能瓶颈，\n提供优化建议并支持一键自动优化，\n生成详细的优化报告以供参考";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary =>
            $"场景: {SceneManager.GetActiveScene().name} | 对象: {totalObjects} | 问题: {detectedIssues.Count} | " +
            $"预览模式: {(previewOnly ? "开" : "关")} | 项目资产导入设置: {(allowProjectAssetImportChanges ? "允许修改" : "已保护")} | " +
            $"已记录回滚: {importSettingChanges.Count}";

        [BoxGroup("快速操作"), HorizontalGroup("快速操作/按钮"), Button("分析当前场景", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void QuickAnalyze() => AnalyzeScene();

        [BoxGroup("快速操作"), HorizontalGroup("快速操作/按钮"), Button("执行优化并确认", ButtonHeight = 34), GUIColor(0.75f, 0.58f, 0.25f)]
        public void QuickOptimize() => AutoOptimizeScene();

        [BoxGroup("快速操作"), HorizontalGroup("快速操作/按钮"), Button("导出优化报告", ButtonHeight = 34), GUIColor(0.48f, 0.48f, 0.48f)]
        public void QuickReport() => ExportDetailedReport();

        #region 配置管理
        [FoldoutGroup("配置管理"), LabelText("当前配置")]
        public OptimizationConfig currentConfig = new OptimizationConfig();

        [FoldoutGroup("配置管理"), Button("保存配置", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.9f)]
        public void SaveConfig()
        {
            string path = EditorUtility.SaveFilePanel("保存优化配置", "", "OptimizationConfig.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = JsonUtility.ToJson(currentConfig, true);
                    File.WriteAllText(path, json, Encoding.UTF8);
                    EditorUtility.DisplayDialog("成功", "配置已保存！", "确定");
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("保存失败", $"无法写入优化配置：\n{ex.Message}", "知道了");
                }
            }
        }

        [FoldoutGroup("配置管理"), Button("加载配置", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.9f)]
        public void LoadConfig()
        {
            string path = EditorUtility.OpenFilePanel("加载优化配置", "", "json");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    currentConfig = JsonUtility.FromJson<OptimizationConfig>(json);
                    if (currentConfig == null)
                    {
                        currentConfig = new OptimizationConfig();
                        EditorUtility.DisplayDialog("加载失败", "配置文件为空或格式无效。", "知道了");
                        return;
                    }
                    EditorUtility.DisplayDialog("成功", "配置已加载！", "确定");
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("加载失败", $"无法读取优化配置：\n{ex.Message}", "知道了");
                }
            }
        }
        #endregion

        #region 分析结果
        [FoldoutGroup("分析结果"), ShowInInspector, ReadOnly, LabelText("场景概览"), TextArea(8, 15)]
        public string sceneOverview = "等待分析...";

        [FoldoutGroup("分析结果"), ShowInInspector, ReadOnly, LabelText("性能报告"), TextArea(15, 25)]
        public string analysisResult = "点击'全面分析'按钮开始检测...";

        [FoldoutGroup("分析结果"), ShowInInspector, ReadOnly, LabelText("优化建议(按优先级排序)"), TextArea(12, 20)]
        public string optimizationSuggestions = "";

        [FoldoutGroup("分析结果"), ShowInInspector, ReadOnly, LabelText("预计优化收益"), TextArea(6, 12)]
        public string estimatedBenefits = "";

        [FoldoutGroup("发现的问题"), ShowInInspector, LabelText("问题类型筛选"), EnumToggleButtons, OnValueChanged("UpdateDisplayedIssues")]
        public IssueCategory categoryFilter = IssueCategory.All;

        [FoldoutGroup("发现的问题"), ShowInInspector, LabelText("严重程度筛选"), EnumToggleButtons, OnValueChanged("UpdateDisplayedIssues")]
        public SeverityFilter severityFilter = SeverityFilter.All;

        [FoldoutGroup("发现的问题"), ShowInInspector, LabelText("最小影响程度"), Range(0, 100), OnValueChanged("UpdateDisplayedIssues")]
        public int minImpact = 0;

        [FoldoutGroup("发现的问题"), HideInInspector]
        public List<OptimizationIssue> detectedIssues = new List<OptimizationIssue>();

        [FoldoutGroup("发现的问题"), ShowInInspector, LabelText("发现的问题"), ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 5)]
        public List<OptimizationIssue> DisplayedIssues = new List<OptimizationIssue>();
        #endregion

        #region 项目资产导入设置变更
        [FoldoutGroup("项目资产导入设置"), ShowInInspector, ReadOnly, LabelText("本次导入设置变更"), ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 6)]
        public List<ImportSettingChangeRecord> importSettingChanges = new List<ImportSettingChangeRecord>();

        [FoldoutGroup("项目资产导入设置"), ShowInInspector, ReadOnly, LabelText("待执行导入设置变更预览"), ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 6)]
        public List<ImportSettingChangeRecord> pendingImportSettingChanges = new List<ImportSettingChangeRecord>();

        [FoldoutGroup("项目资产导入设置"), Button("刷新导入设置预览", ButtonHeight = 32), GUIColor(0.35f, 0.75f, 0.9f)]
        public void RefreshImportSettingPreview()
        {
            pendingImportSettingChanges = BuildPendingImportSettingChanges();
            EditorUtility.DisplayDialog("导入设置预览已刷新", $"预计会修改 {pendingImportSettingChanges.Count} 个项目资产导入设置。", "完成");
        }

        [FoldoutGroup("项目资产导入设置"), Button("导出回滚JSON", ButtonHeight = 32), GUIColor(0.35f, 0.65f, 1f)]
        public void ExportImportSettingRollback()
        {
            if (importSettingChanges.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可导出的变更", "当前还没有记录到项目资产导入设置变更。", "知道了");
                return;
            }

            string path = EditorUtility.SaveFilePanel("导出导入设置回滚文件", Application.dataPath, "SceneOptimization_ImporterRollback.json", "json");
            if (string.IsNullOrEmpty(path))
                return;

            var data = new ImportSettingRollbackData
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sceneName = SceneManager.GetActiveScene().name,
                changes = new List<ImportSettingChangeRecord>(importSettingChanges)
            };

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
                EditorUtility.DisplayDialog("回滚文件已导出", $"已导出 {importSettingChanges.Count} 条导入设置变更。", "完成");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导出失败", $"无法写入回滚 JSON：\n{ex.Message}", "知道了");
            }
        }

        [FoldoutGroup("项目资产导入设置"), Button("从JSON回滚导入设置", ButtonHeight = 32), GUIColor(0.95f, 0.55f, 0.35f)]
        public void RollbackImportSettingsFromJson()
        {
            string path = EditorUtility.OpenFilePanel("选择导入设置回滚文件", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            ImportSettingRollbackData data;
            try
            {
                data = JsonUtility.FromJson<ImportSettingRollbackData>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("回滚文件读取失败", $"无法读取回滚 JSON：\n{ex.Message}", "知道了");
                return;
            }

            if (data == null || data.changes == null || data.changes.Count == 0)
            {
                EditorUtility.DisplayDialog("回滚文件无效", "没有读取到可回滚的导入设置。", "知道了");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认回滚导入设置",
                $"将按 JSON 中的修改前状态回滚 {data.changes.Count} 个资源导入设置。\n\n这会重新导入相关资产，建议先提交或备份项目。",
                "开始回滚", "取消"))
                return;

            int restored = 0;
            int failed = 0;
            var errors = new List<string>();

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var change in data.changes)
                {
                    try
                    {
                        if (RestoreImporterState(change))
                            restored++;
                        else
                        {
                            failed++;
                            errors.Add($"{change.assetPath}: 不支持的导入器或资源不存在");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errors.Add($"{change.assetPath}: {ex.Message}");
                    }
                }
            });

            AssetDatabase.Refresh();
            string detail = errors.Count > 0 ? "\n\n失败项：\n" + SimpleToolsSafetyUtility.JoinPreview(errors, 8) : string.Empty;
            EditorUtility.DisplayDialog("导入设置回滚完成", $"已回滚 {restored} 个，失败 {failed} 个。{detail}", "完成");
        }
        #endregion

        #region 性能指标(扩展)
        private OptimizationReport currentReport = new OptimizationReport();
        private HashSet<string> approvedImporterChangePaths = null;

        // 对象统计
        private int totalObjects = 0;
        private int activeObjects = 0;
        private int staticObjects = 0;
        private int dynamicObjects = 0;

        // 灯光统计
        private int lightCount = 0;
        private int realtimeLightCount = 0;
        private int bakedLightCount = 0;
        private int mixedLightCount = 0;
        private int shadowCastingLights = 0;

        // 对象清理
        private int emptyObjectCount = 0;
        private int missingScriptCount = 0;
        private int disabledObjectCount = 0;

        // 网格统计
        private int meshCount = 0;
        private int highPolyCount = 0;
        private int totalTriangles = 0;
        private int totalVertices = 0;
        private int duplicateMeshes = 0;

        // 材质与纹理
        private int materialCount = 0;
        private int uniqueMaterials = 0;
        private int duplicateMaterials = 0;
        private int textureCount = 0;
        private int oversizedTextureCount = 0;
        private int uncompressedTextureCount = 0;
        private int mipmapDisabledTextures = 0;
        private int readableTextures = 0;

        // 粒子系统
        private int particleSystemCount = 0;
        private int inactiveParticleCount = 0;
        private int highEmissionParticles = 0;

        // 音频
        private int audioSourceCount = 0;
        private int uncompressedAudioCount = 0;
        private int streamingAudioCount = 0;

        // 物理
        private int rigidbodyCount = 0;
        private int kinematicRigidbodyCount = 0;
        private int colliderCount = 0;
        private int triggerColliderCount = 0;
        private int meshColliderCount = 0;
        private int nonConvexMeshColliders = 0;

        // 动画
        private int animatorCount = 0;
        private int animationClipCount = 0;

        // UI
        private int canvasCount = 0;
        private int graphicRaycasterCount = 0;
        private int uiElementCount = 0;

        // 渲染
        private int rendererCount = 0;
        private int skinnedMeshRendererCount = 0;
        private int batchableRenderers = 0;
        private int nonBatchableRenderers = 0;

        // Shader
        private int shaderCount = 0;
        private Dictionary<string, int> shaderUsage = new Dictionary<string, int>();

        // 内存与性能
        private long totalMemoryUsage = 0;
        private long meshMemory = 0;
        private long textureMemory = 0;
        private long audioMemory = 0;
        private int drawCallEstimate = 0;

        // LOD系统
        private int lodGroupCount = 0;
        private int objectsNeedingLOD = 0;

        // 反射探针
        private int reflectionProbeCount = 0;
        private int realtimeReflectionProbes = 0;
        #endregion

        #region 优化设置(精细控制)
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置", "性能检测阈值")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置"), LabelText("实时灯光阈值"), Range(0, 20)]
        public int realtimeLightThreshold = 4;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置"), LabelText("空对象阈值"), Range(0, 500)]
        public int emptyObjectThreshold = 10;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置"), LabelText("高面数阈值"), Range(1000, 100000)]
        public int highPolyThreshold = 10000;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置"), LabelText("纹理大小阈值(MB)"), Range(1, 100)]
        public int textureSizeThreshold = 10;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/阈值设置"), LabelText("粒子发射率阈值"), Range(10, 1000)]
        public int particleEmissionThreshold = 100;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/对象优化", "GameObject相关优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/对象优化"), LabelText("清理空对象")]
        public bool optimizeEmptyObjects = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/对象优化"), LabelText("移除丢失脚本")]
        public bool removeMissingScripts = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/对象优化"), LabelText("禁用非活跃对象")]
        public bool disableInactiveObjects = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/对象优化"), LabelText("标记静态对象")]
        public bool markStaticObjects = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化", "渲染与材质优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("生成LOD系统")]
        public bool generateLODs = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("LOD级别数量"), Range(2, 5), ShowIf("generateLODs")]
        public int lodLevelCount = 3;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("合并材质球")]
        public bool mergeMaterials = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("合并网格(静态)")]
        public bool combineMeshes = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("启用静态批处理")]
        public bool enableStaticBatching = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/渲染优化"), LabelText("优化阴影设置")]
        public bool optimizeShadows = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/纹理优化", "纹理与内存优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/纹理优化"), LabelText("压缩纹理")]
        public bool compressTextures = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/纹理优化"), LabelText("生成Mipmap")]
        public bool generateMipmaps = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/纹理优化"), LabelText("禁用纹理读写")]
        public bool disableTextureReadWrite = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/纹理优化"), LabelText("纹理最大尺寸"), ValueDropdown("GetTextureSizeOptions")]
        public int maxTextureSize = 2048;

        private IEnumerable GetTextureSizeOptions()
        {
            return new ValueDropdownList<int>()
            {
                { "512", 512 },
                { "1024", 1024 },
                { "2048", 2048 },
                { "4096", 4096 },
                { "8192", 8192 }
            };
        }

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/灯光优化", "光照系统优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/灯光优化"), LabelText("转换为烘焙灯光")]
        public bool convertToBakedLights = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/灯光优化"), LabelText("优化光照贴图")]
        public bool optimizeLightmaps = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/灯光优化"), LabelText("优化反射探针")]
        public bool optimizeReflectionProbes = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/灯光优化"), LabelText("启用遮挡剔除")]
        public bool enableOcclusionCulling = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/粒子优化", "粒子系统优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/粒子优化"), LabelText("优化粒子系统")]
        public bool optimizeParticles = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/粒子优化"), LabelText("降低粒子数量")]
        public bool reduceParticleCount = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/粒子优化"), LabelText("禁用非活跃粒子")]
        public bool disableInactiveParticles = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/音频优化", "音频资源优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/音频优化"), LabelText("压缩音频")]
        public bool compressAudio = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/音频优化"), LabelText("启用音频流式加载")]
        public bool enableAudioStreaming = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/物理优化", "物理系统优化")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/物理优化"), LabelText("优化碰撞体")]
        public bool optimizeColliders = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/物理优化"), LabelText("简化Mesh Collider")]
        public bool simplifyMeshColliders = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/物理优化"), LabelText("移除不必要的刚体")]
        public bool removeUnnecessaryRigidbodies = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项", "高级优化选项")]
        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项"), LabelText("自动备份场景")]
        public bool autoBackup = true;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项"), LabelText("仅预览(不应用)")]
        public bool previewOnly = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项"), LabelText("允许修改项目资产导入设置")]
        [InfoBox("关闭时不会修改 Texture/Audio Importer。打开后会改项目资产导入设置，不只影响当前场景，请先提交或备份项目。", InfoMessageType.Warning)]
        public bool allowProjectAssetImportChanges = false;

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项"), LabelText("优化级别"), ValueDropdown("GetOptimizationLevelOptions")]
        public string optimizationLevel = "中度";

        private IEnumerable GetOptimizationLevelOptions()
        {
            return new ValueDropdownList<string>()
            {
                { "轻度 - 安全优化", "轻度" },
                { "中度 - 平衡优化", "中度" },
                { "重度 - 激进优化", "重度" },
                { "自定义 - 手动控制", "自定义" }
            };
        }

        [FoldoutGroup("优化设置"), TitleGroup("优化设置/高级选项"), LabelText("详细日志")]
        public bool verboseLogging = true;
        #endregion

        #region 分析功能(增强版)
        [BoxGroup("分析操作"), Button("🔍 全面场景分析", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void AnalyzeScene()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            detectedIssues.Clear();
            ResetCounters();

            currentReport = new OptimizationReport
            {
                timestamp = DateTime.Now,
                sceneName = SceneManager.GetActiveScene().name
            };

            var allObjects = GetActiveSceneGameObjects();
            totalObjects = allObjects.Count;

            try
            {
                // 阶段1: 对象分析
                EditorUtility.DisplayProgressBar("场景分析 [1/6]", "分析GameObject...", 0.1f);
                for (int i = 0; i < allObjects.Count; i++)
                {
                    AnalyzeGameObject(allObjects[i]);
                    if (i % 50 == 0)
                    {
                        EditorUtility.DisplayProgressBar("场景分析 [1/6]", $"分析GameObject... {i}/{allObjects.Count}", 0.1f + (float)i / Mathf.Max(1, allObjects.Count) * 0.15f);
                    }
                }

                // 阶段2: 材质与纹理分析
                EditorUtility.DisplayProgressBar("场景分析 [2/6]", "分析材质与纹理...", 0.3f);
                AnalyzeMaterials();
                AnalyzeTextures();

                // 阶段3: 网格分析
                EditorUtility.DisplayProgressBar("场景分析 [3/6]", "分析网格资源...", 0.5f);
                AnalyzeMeshes();

                // 阶段4: 音频分析
                EditorUtility.DisplayProgressBar("场景分析 [4/6]", "分析音频资源...", 0.65f);
                AnalyzeAudio();

                // 阶段5: 渲染与性能分析
                EditorUtility.DisplayProgressBar("场景分析 [5/6]", "分析渲染性能...", 0.8f);
                AnalyzeRendering();
                AnalyzeShaders();
                EstimateDrawCalls();

                // 阶段6: 生成报告
                EditorUtility.DisplayProgressBar("场景分析 [6/6]", "生成报告...", 0.95f);
                stopwatch.Stop();
                currentReport.analysisTime = (float)stopwatch.Elapsed.TotalSeconds;

                GenerateComprehensiveReport();
                SortIssuesByPriority();
                UpdateDisplayedIssues();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ShowAnalysisDialog();
        }

        private void BackupScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path))
            {
                string backupPath = scene.path.Replace(".unity", $"_backup_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
                AssetDatabase.CopyAsset(scene.path, backupPath);
                if (verboseLogging)
                    UnityEngine.Debug.Log($"场景已备份到: {backupPath}");
            }
        }

        private void ShowAnalysisDialog()
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine($"✅ 分析完成 (耗时: {currentReport.analysisTime:F2}秒)");
            summary.AppendLine();
            summary.AppendLine($"📊 关键指标:");
            summary.AppendLine($"  • 总对象: {totalObjects} (活跃: {activeObjects}, 静态: {staticObjects})");
            summary.AppendLine($"  • 灯光: {lightCount} (实时: {realtimeLightCount}, 烘焙: {bakedLightCount})");
            summary.AppendLine($"  • 网格: {meshCount} (三角面: {totalTriangles:N0}, 顶点: {totalVertices:N0})");
            summary.AppendLine($"  • 材质: {materialCount} (唯一: {uniqueMaterials}, 重复: {duplicateMaterials})");
            summary.AppendLine($"  • 纹理: {textureCount} (超大: {oversizedTextureCount}, 未压缩: {uncompressedTextureCount})");
            summary.AppendLine();
            summary.AppendLine($"⚠️ 发现问题: {detectedIssues.Count} 个");
            summary.AppendLine($"💾 估算内存: {totalMemoryUsage / 1024 / 1024} MB");
            summary.AppendLine($"🎨 估算Draw Calls: {drawCallEstimate}");
            summary.AppendLine();
            summary.AppendLine("详细结果请查看下方面板");

            EditorUtility.DisplayDialog("场景分析完成", summary.ToString(), "确定");
        }

        private void ResetCounters()
        {
            // 对象统计
            totalObjects = 0;
            activeObjects = 0;
            staticObjects = 0;
            dynamicObjects = 0;

            // 灯光统计
            lightCount = 0;
            realtimeLightCount = 0;
            bakedLightCount = 0;
            mixedLightCount = 0;
            shadowCastingLights = 0;

            // 对象清理
            emptyObjectCount = 0;
            missingScriptCount = 0;
            disabledObjectCount = 0;

            // 网格统计
            meshCount = 0;
            highPolyCount = 0;
            totalTriangles = 0;
            totalVertices = 0;
            duplicateMeshes = 0;

            // 材质与纹理
            materialCount = 0;
            uniqueMaterials = 0;
            duplicateMaterials = 0;
            textureCount = 0;
            oversizedTextureCount = 0;
            uncompressedTextureCount = 0;
            mipmapDisabledTextures = 0;
            readableTextures = 0;

            // 粒子系统
            particleSystemCount = 0;
            inactiveParticleCount = 0;
            highEmissionParticles = 0;

            // 音频
            audioSourceCount = 0;
            uncompressedAudioCount = 0;
            streamingAudioCount = 0;

            // 物理
            rigidbodyCount = 0;
            kinematicRigidbodyCount = 0;
            colliderCount = 0;
            triggerColliderCount = 0;
            meshColliderCount = 0;
            nonConvexMeshColliders = 0;

            // 动画
            animatorCount = 0;
            animationClipCount = 0;

            // UI
            canvasCount = 0;
            graphicRaycasterCount = 0;
            uiElementCount = 0;

            // 渲染
            rendererCount = 0;
            skinnedMeshRendererCount = 0;

            // Shader
            shaderCount = 0;
            shaderUsage.Clear();

            // 内存与性能
            totalMemoryUsage = 0;
            meshMemory = 0;
            textureMemory = 0;
            audioMemory = 0;
            drawCallEstimate = 0;

            // LOD系统
            lodGroupCount = 0;
            objectsNeedingLOD = 0;

            // 反射探针
            reflectionProbeCount = 0;
            realtimeReflectionProbes = 0;
        }

        private void AnalyzeGameObject(GameObject obj)
        {
            // 基础统计
            if (obj.activeInHierarchy)
                activeObjects++;
            else
                disabledObjectCount++;

            if (obj.isStatic)
                staticObjects++;
            else
                dynamicObjects++;

            // 灯光分析(增强)
            var light = obj.GetComponent<Light>();
            if (light != null)
            {
                lightCount++;
                if (light.lightmapBakeType == LightmapBakeType.Realtime)
                {
                    realtimeLightCount++;
                    if (realtimeLightCount > realtimeLightThreshold)
                    {
                        AddIssue(IssueCategory.LightingOptimization, $"实时灯光过多: {obj.name}", SeverityFilter.High, obj,
                            "转换为烘焙灯光或减少灯光数量", 85);
                    }
                }
                else if (light.lightmapBakeType == LightmapBakeType.Baked)
                {
                    bakedLightCount++;
                }
                else
                {
                    mixedLightCount++;
                }

                if (light.shadows != LightShadows.None)
                    shadowCastingLights++;
            }

            // 空对象检测(增强)
            var components = obj.GetComponents<Component>();
            if (components.Length == 1 && obj.transform.childCount == 0)
            {
                emptyObjectCount++;
                if (emptyObjectCount <= 50) // 只记录前50个以避免过多
                {
                    AddIssue(IssueCategory.ObjectCleanup, $"空对象: {obj.name}", SeverityFilter.Low, obj,
                        "删除此空对象", 20);
                }
            }

            // 丢失脚本检测
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    missingScriptCount++;
                    AddIssue(IssueCategory.ObjectCleanup, $"丢失脚本: {obj.name}", SeverityFilter.Medium, obj,
                        "移除丢失的脚本引用", 50);
                    break;
                }
            }

            // 网格渲染器分析
            var meshFilter = obj.GetComponent<MeshFilter>();
            var renderer = obj.GetComponent<Renderer>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var mesh = meshFilter.sharedMesh;
                int triangleCount = mesh.triangles.Length / 3;
                totalTriangles += triangleCount;
                totalVertices += mesh.vertexCount;

                if (triangleCount > highPolyThreshold)
                {
                    highPolyCount++;
                    AddIssue(IssueCategory.MeshOptimization, $"高面数模型: {obj.name} ({triangleCount} 三角面)", SeverityFilter.High, obj,
                        "添加LOD系统或简化模型", 90);
                }

                // 检查是否需要LOD
                if (triangleCount > highPolyThreshold / 2 && obj.GetComponent<LODGroup>() == null)
                {
                    objectsNeedingLOD++;
                }
            }

            if (renderer != null)
            {
                rendererCount++;

                // 静态批处理检查
                if (!obj.isStatic && renderer.GetType() == typeof(MeshRenderer))
                {
                    AddIssue(IssueCategory.RenderingOptimization, $"可标记为静态: {obj.name}", SeverityFilter.Medium, obj,
                        "标记为Static以启用静态批处理", 60);
                }
            }

            // 蒙皮网格渲染器
            var skinnedMesh = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMesh != null)
            {
                skinnedMeshRendererCount++;
            }

            // 粒子系统分析(增强)
            var particleSystem = obj.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystemCount++;
                var emission = particleSystem.emission;
                if (!particleSystem.isPlaying && !particleSystem.isPaused)
                {
                    inactiveParticleCount++;
                }

                if (emission.rateOverTime.constant > particleEmissionThreshold)
                {
                    highEmissionParticles++;
                    AddIssue(IssueCategory.ParticleOptimization, $"高发射率粒子: {obj.name} ({emission.rateOverTime.constant}/s)", SeverityFilter.Medium, obj,
                        "降低粒子发射率", 55);
                }
            }

            // 音频源分析
            var audioSource = obj.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSourceCount++;
                if (audioSource.clip != null)
                {
                    if (!audioSource.clip.loadInBackground)
                        uncompressedAudioCount++;

                    var path = AssetDatabase.GetAssetPath(audioSource.clip);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                        if (importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad)
                        {
                            streamingAudioCount++;
                        }
                    }
                }
            }

            // 物理组件分析(增强)
            var rigidbody = obj.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbodyCount++;
                if (rigidbody.isKinematic)
                    kinematicRigidbodyCount++;
            }

            var colliders = obj.GetComponents<Collider>();
            foreach (var collider in colliders)
            {
                colliderCount++;
                if (collider.isTrigger)
                    triggerColliderCount++;

                var meshCollider = collider as MeshCollider;
                if (meshCollider != null)
                {
                    meshColliderCount++;
                    if (!meshCollider.convex)
                    {
                        nonConvexMeshColliders++;
                        AddIssue(IssueCategory.PhysicsOptimization, $"非凸网格碰撞体: {obj.name}", SeverityFilter.Medium, obj,
                            "使用简单碰撞体或凸网格碰撞体", 65);
                    }
                }
            }

            // 动画分析
            var animator = obj.GetComponent<Animator>();
            if (animator != null)
            {
                animatorCount++;
            }

            // UI分析
            var canvas = obj.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvasCount++;
                var graphicRaycaster = obj.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (graphicRaycaster != null)
                    graphicRaycasterCount++;
            }

            var uiGraphic = obj.GetComponent<UnityEngine.UI.Graphic>();
            if (uiGraphic != null)
            {
                uiElementCount++;
            }

            // LOD组检测
            var lodGroup = obj.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                lodGroupCount++;
            }

            // 反射探针
            var reflectionProbe = obj.GetComponent<ReflectionProbe>();
            if (reflectionProbe != null)
            {
                reflectionProbeCount++;
                if (reflectionProbe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
                {
                    realtimeReflectionProbes++;
                    AddIssue(IssueCategory.LightingOptimization, $"实时反射探针: {obj.name}", SeverityFilter.Medium, obj,
                        "转换为烘焙模式", 70);
                }
            }
        }

        private void AddIssue(IssueCategory category, string description, SeverityFilter severity,
            GameObject target, string fixAction, int estimatedImpact)
        {
            // 只添加当前场景中对象的优化问题
            if (target == null || target.scene != SceneManager.GetActiveScene())
                return;

            detectedIssues.Add(new OptimizationIssue
            {
                category = category,
                description = description,
                severity = severity,
                targetObjectPath = GetGameObjectPath(target),
                fixAction = fixAction,
                estimatedImpact = estimatedImpact
            });
        }
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private void AnalyzeMaterials()
        {
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var materials = LoadActiveSceneDependencyAssets<Material>(sceneDependencyPaths);

            var materialDict = new Dictionary<string, List<Material>>();
            uniqueMaterials = 0;
            materialCount = 0;

            foreach (var mat in materials)
            {
                if (mat == null || mat.shader == null)
                    continue;
                    
                string assetPath = AssetDatabase.GetAssetPath(mat);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
                if (!sceneDependencyPaths.Contains(assetPath))
                    continue;

                materialCount++;

                string matKey = $"{mat.shader.name}_{mat.name}";
                if (!materialDict.ContainsKey(matKey))
                {
                    materialDict[matKey] = new List<Material>();
                    uniqueMaterials++;
                }
                materialDict[matKey].Add(mat);
            }

            // 检测重复材质
            foreach (var kvp in materialDict)
            {
                if (kvp.Value.Count > 1)
                {
                    duplicateMaterials += kvp.Value.Count - 1;
                    GameObject relatedObj = FindGameObjectWithMaterial(kvp.Value[0]);
                    AddIssue(IssueCategory.MaterialOptimization, $"重复材质: {kvp.Key} (x{kvp.Value.Count})", SeverityFilter.Medium, relatedObj,
                        "合并重复的材质", 65);
                }
            }
        }

        private void AnalyzeTextures()
        {
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var textures = LoadActiveSceneDependencyAssets<Texture2D>(sceneDependencyPaths);
            textureCount = 0;

            foreach (var texture in textures)
            {
                if (texture == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path))
                    continue;
                path = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                if (!sceneDependencyPaths.Contains(path))
                    continue;

                textureCount++;

                long textureSize = EstimateTextureMemory(texture);
                textureMemory += textureSize;
                totalMemoryUsage += textureSize;

                // 检测超大纹理
                if (textureSize > textureSizeThreshold * 1024 * 1024)
                {
                    oversizedTextureCount++;
                    GameObject relatedObj = FindGameObjectWithTexture(texture);
                    AddIssue(IssueCategory.TextureOptimization, $"超大纹理: {texture.name} ({textureSize / 1024 / 1024}MB)", SeverityFilter.High, relatedObj,
                        "压缩或缩小纹理", 80);
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    // 检测未压缩纹理
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        uncompressedTextureCount++;
                        GameObject relatedObj = FindGameObjectWithTexture(texture);
                        AddIssue(IssueCategory.TextureOptimization, $"未压缩纹理: {texture.name}", SeverityFilter.Medium, relatedObj,
                            "启用纹理压缩", 70);
                    }

                    // 检测Mipmap
                    if (!importer.mipmapEnabled && texture.width > 512)
                    {
                        mipmapDisabledTextures++;
                    }

                    // 检测可读纹理
                    if (importer.isReadable)
                    {
                        readableTextures++;
                        GameObject relatedObj = FindGameObjectWithTexture(texture);
                        AddIssue(IssueCategory.TextureOptimization, $"可读纹理: {texture.name}", SeverityFilter.Low, relatedObj,
                            "禁用Read/Write以节省内存", 40);
                    }
                }
            }
        }

        private void AnalyzeMeshes()
        {
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var meshes = LoadActiveSceneDependencyAssets<Mesh>(sceneDependencyPaths);
            meshCount = 0;

            var meshDict = new Dictionary<string, List<Mesh>>();

            foreach (var mesh in meshes)
            {
                if (mesh == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(path))
                {
                    path = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                    if (!sceneDependencyPaths.Contains(path))
                        continue;

                    meshCount++;

                    if (!meshDict.ContainsKey(path))
                    {
                        meshDict[path] = new List<Mesh>();
                    }
                    meshDict[path].Add(mesh);
                }

                long meshSize = EstimateMeshMemory(mesh);
                meshMemory += meshSize;
                totalMemoryUsage += meshSize;
            }

            // 检测重复网格
            foreach (var kvp in meshDict)
            {
                if (kvp.Value.Count > 1)
                {
                    duplicateMeshes += kvp.Value.Count - 1;
                }
            }
        }

        private void AnalyzeAudio()
        {
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var audioClips = LoadActiveSceneDependencyAssets<AudioClip>(sceneDependencyPaths);

            foreach (var clip in audioClips)
            {
                if (clip == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path))
                    continue;
                path = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                if (!sceneDependencyPaths.Contains(path))
                    continue;

                long clipSize = EstimateAudioMemory(clip);
                audioMemory += clipSize;
                totalMemoryUsage += clipSize;

                if (!string.IsNullOrEmpty(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer != null)
                    {
                        var settings = importer.defaultSampleSettings;
                        if (settings.compressionFormat == AudioCompressionFormat.PCM)
                        {
                            GameObject relatedObj = FindGameObjectWithAudioClip(clip);
                            AddIssue(IssueCategory.AudioOptimization, $"未压缩音频: {clip.name}", SeverityFilter.Medium, relatedObj,
                                "使用压缩格式", 60);
                        }
                    }
                }
            }
        }

        private void AnalyzeRendering()
        {
            var renderers = GetActiveSceneComponents<Renderer>();
            rendererCount = renderers.Length;

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.gameObject == null)
                    continue;

                // 检查批处理友好性
                if (renderer.gameObject.isStatic)
                {
                    batchableRenderers++;
                }
                else
                {
                    nonBatchableRenderers++;
                }
            }
        }

        private void AnalyzeShaders()
        {
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var materials = LoadActiveSceneDependencyAssets<Material>(sceneDependencyPaths);
            shaderUsage.Clear();

            foreach (var mat in materials)
            {
                if (mat == null || mat.shader == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(mat);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
                if (!sceneDependencyPaths.Contains(assetPath))
                    continue;

                string shaderName = mat.shader.name;
                if (!shaderUsage.ContainsKey(shaderName))
                {
                    shaderUsage[shaderName] = 0;
                }
                shaderUsage[shaderName]++;
            }

            shaderCount = shaderUsage.Count;
        }

        private void EstimateDrawCalls()
        {
            var renderers = GetActiveSceneComponents<Renderer>();
            drawCallEstimate = renderers.Length;

            // 简单估算批处理减少
            var materials = new HashSet<Material>();
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null)
                        {
                            materials.Add(mat);
                        }
                    }
                }
            }
            drawCallEstimate = Mathf.Max(drawCallEstimate - materials.Count, 1);
        }

        private long EstimateMeshMemory(Mesh mesh)
        {
            return mesh.vertexCount * 32 + mesh.triangles.Length * 4; // 粗略估算
        }

        private long EstimateTextureMemory(Texture texture)
        {
            return texture.width * texture.height * 4; // RGBA
        }

        private long EstimateAudioMemory(AudioClip clip)
        {
            return clip.samples * clip.channels * 2; // 16-bit
        }

        private void GenerateComprehensiveReport()
        {
            GenerateSceneOverview();
            GenerateDetailedAnalysisReport();
            GeneratePrioritizedSuggestions();
            GenerateEstimatedBenefits();
        }

        private void GenerateSceneOverview()
        {
            StringBuilder overview = new StringBuilder();
            overview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            overview.AppendLine($"📋 场景: {SceneManager.GetActiveScene().name}");
            overview.AppendLine($"⏱️  分析时间: {currentReport.analysisTime:F2}秒");
            overview.AppendLine($"📅 日期: {currentReport.timestamp:yyyy-MM-dd HH:mm:ss}");
            overview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            overview.AppendLine();
            overview.AppendLine($"🎯 对象总数: {totalObjects} (活跃: {activeObjects}, 静态: {staticObjects})");
            overview.AppendLine($"💡 灯光: {lightCount} 个 (实时: {realtimeLightCount}, 烘焙: {bakedLightCount})");
            overview.AppendLine($"🎨 渲染器: {rendererCount} 个");
            overview.AppendLine($"📐 网格: {meshCount} 个 ({totalTriangles:N0} 三角面)");
            overview.AppendLine($"🖼️  材质/纹理: {materialCount}/{textureCount}");
            overview.AppendLine($"💾 估算内存: {totalMemoryUsage / 1024 / 1024}MB");
            overview.AppendLine($"🎭 Draw Calls: ~{drawCallEstimate}");
            overview.AppendLine($"⚠️  问题总数: {detectedIssues.Count}");

            sceneOverview = overview.ToString();
        }

        private void GenerateDetailedAnalysisReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("═══════════════════════════════════════");
            report.AppendLine("        场景性能分析报告");
            report.AppendLine("═══════════════════════════════════════");
            report.AppendLine();

            // 对象分析
            report.AppendLine("┌─ 对象分析 ────────────────────────┐");
            report.AppendLine($"│ 总对象数: {totalObjects,8} │ 活跃: {activeObjects,6} │");
            report.AppendLine($"│ 静态对象: {staticObjects,8} │ 动态: {dynamicObjects,6} │");
            report.AppendLine($"│ 空对象:   {emptyObjectCount,8} │ 禁用: {disabledObjectCount,6} │");
            report.AppendLine($"│ 丢失脚本: {missingScriptCount,8}               │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 渲染分析
            report.AppendLine("┌─ 渲染性能 ────────────────────────┐");
            report.AppendLine($"│ 渲染器:     {rendererCount,6} │ 蒙皮网格: {skinnedMeshRendererCount,4} │");
            report.AppendLine($"│ 批处理友好: {batchableRenderers,6} │ 非友好:   {nonBatchableRenderers,4} │");
            report.AppendLine($"│ Draw Calls: {drawCallEstimate,6}                │");
            report.AppendLine($"│ LOD组:      {lodGroupCount,6} │ 需要LOD: {objectsNeedingLOD,4} │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 灯光分析
            report.AppendLine("┌─ 光照系统 ────────────────────────┐");
            report.AppendLine($"│ 灯光总数: {lightCount,4} │ 实时: {realtimeLightCount,3} │ 烘焙: {bakedLightCount,3} │");
            report.AppendLine($"│ 混合模式: {mixedLightCount,4} │ 投影: {shadowCastingLights,3}           │");
            report.AppendLine($"│ 反射探针: {reflectionProbeCount,4} │ 实时: {realtimeReflectionProbes,3}           │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 网格分析
            report.AppendLine("┌─ 网格资源 ────────────────────────┐");
            report.AppendLine($"│ 网格总数:   {meshCount,6}                   │");
            report.AppendLine($"│ 高面数模型: {highPolyCount,6}                   │");
            report.AppendLine($"│ 总三角面:   {totalTriangles,12:N0}       │");
            report.AppendLine($"│ 总顶点数:   {totalVertices,12:N0}       │");
            report.AppendLine($"│ 重复网格:   {duplicateMeshes,6}                   │");
            report.AppendLine($"│ 网格内存:   {meshMemory / 1024 / 1024,6} MB              │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 材质纹理
            report.AppendLine("┌─ 材质纹理 ────────────────────────┐");
            report.AppendLine($"│ 材质总数: {materialCount,6} │ 唯一: {uniqueMaterials,6} │");
            report.AppendLine($"│ 重复材质: {duplicateMaterials,6} │ Shader: {shaderCount,4}   │");
            report.AppendLine($"│ 纹理总数: {textureCount,6} │ 超大: {oversizedTextureCount,6} │");
            report.AppendLine($"│ 未压缩:   {uncompressedTextureCount,6} │ 可读: {readableTextures,6} │");
            report.AppendLine($"│ 无Mipmap: {mipmapDisabledTextures,6}                  │");
            report.AppendLine($"│ 纹理内存: {textureMemory / 1024 / 1024,6} MB              │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 粒子系统
            report.AppendLine("┌─ 粒子系统 ────────────────────────┐");
            report.AppendLine($"│ 粒子系统: {particleSystemCount,6}                   │");
            report.AppendLine($"│ 非活跃:   {inactiveParticleCount,6}                   │");
            report.AppendLine($"│ 高发射率: {highEmissionParticles,6}                   │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 音频
            report.AppendLine("┌─ 音频资源 ────────────────────────┐");
            report.AppendLine($"│ 音频源:   {audioSourceCount,6}                   │");
            report.AppendLine($"│ 未压缩:   {uncompressedAudioCount,6}                   │");
            report.AppendLine($"│ 流式加载: {streamingAudioCount,6}                   │");
            report.AppendLine($"│ 音频内存: {audioMemory / 1024 / 1024,6} MB              │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 物理系统
            report.AppendLine("┌─ 物理系统 ────────────────────────┐");
            report.AppendLine($"│ 刚体:     {rigidbodyCount,6} │ 运动学: {kinematicRigidbodyCount,6} │");
            report.AppendLine($"│ 碰撞体:   {colliderCount,6} │ 触发器: {triggerColliderCount,6} │");
            report.AppendLine($"│ 网格碰撞: {meshColliderCount,6} │ 非凸:   {nonConvexMeshColliders,6} │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // UI系统
            report.AppendLine("┌─ UI系统 ──────────────────────────┐");
            report.AppendLine($"│ Canvas:   {canvasCount,6}                   │");
            report.AppendLine($"│ Raycaster:{graphicRaycasterCount,6}                   │");
            report.AppendLine($"│ UI元素:   {uiElementCount,6}                   │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 动画系统
            report.AppendLine("┌─ 动画系统 ────────────────────────┐");
            report.AppendLine($"│ Animator: {animatorCount,6}                   │");
            report.AppendLine("└────────────────────────────────────┘");
            report.AppendLine();

            // 总内存
            report.AppendLine("┌─ 内存统计 ────────────────────────┐");
            report.AppendLine($"│ 网格内存:   {meshMemory / 1024 / 1024,6} MB            │");
            report.AppendLine($"│ 纹理内存:   {textureMemory / 1024 / 1024,6} MB            │");
            report.AppendLine($"│ 音频内存:   {audioMemory / 1024 / 1024,6} MB            │");
            report.AppendLine($"│ ────────────────────────────│");
            report.AppendLine($"│ 总计内存:   {totalMemoryUsage / 1024 / 1024,6} MB            │");
            report.AppendLine("└────────────────────────────────────┘");

            analysisResult = report.ToString();
        }

        private void GeneratePrioritizedSuggestions()
        {
            var sortedIssues = detectedIssues.OrderByDescending(i => i.estimatedImpact).ToList();

            StringBuilder suggestions = new StringBuilder();
            suggestions.AppendLine("═══════════════════════════════════════");
            suggestions.AppendLine("      优化建议 (按优先级排序)");
            suggestions.AppendLine("═══════════════════════════════════════");
            suggestions.AppendLine();

            // 分类统计
            var categoryCounts = sortedIssues.GroupBy(i => i.severity)
                .ToDictionary(g => g.Key, g => g.Count());

            int highCount = categoryCounts.ContainsKey(SeverityFilter.High) ? categoryCounts[SeverityFilter.High] : 0;
            int mediumCount = categoryCounts.ContainsKey(SeverityFilter.Medium) ? categoryCounts[SeverityFilter.Medium] : 0;
            int lowCount = categoryCounts.ContainsKey(SeverityFilter.Low) ? categoryCounts[SeverityFilter.Low] : 0;

            suggestions.AppendLine($"🔴 高优先级: {highCount} 个问题");
            suggestions.AppendLine($"🟡 中优先级: {mediumCount} 个问题");
            suggestions.AppendLine($"🟢 低优先级: {lowCount} 个问题");
            suggestions.AppendLine();
            suggestions.AppendLine("─────────────────────────────────────");
            suggestions.AppendLine();

            // 按类别分组显示前20个问题
            var byCategory = sortedIssues.Take(20).GroupBy(i => i.category);

            foreach (var category in byCategory)
            {
                string categoryName = GetEnumDisplayName(category.Key);
                suggestions.AppendLine($"【{categoryName}】");
                foreach (var issue in category.Take(5))
                {
                    string severityIcon = issue.severity == SeverityFilter.High ? "🔴" :
                                         issue.severity == SeverityFilter.Medium ? "🟡" : "🟢";
                    suggestions.AppendLine($"  {severityIcon} {issue.description}");
                    suggestions.AppendLine($"     → {issue.fixAction} (影响: {issue.estimatedImpact}%)");
                }
                suggestions.AppendLine();
            }

            if (sortedIssues.Count > 20)
            {
                suggestions.AppendLine($"... 还有 {sortedIssues.Count - 20} 个问题未显示");
            }

            optimizationSuggestions = suggestions.ToString();
        }

        private void SortIssuesByPriority()
        {
            detectedIssues = detectedIssues.OrderByDescending(i => i.estimatedImpact).ToList();
            UpdateDisplayedIssues();
        }

        private void GenerateEstimatedBenefits()
        {
            StringBuilder benefits = new StringBuilder();
            benefits.AppendLine("═══════════════════════════════════════");
            benefits.AppendLine("          预计优化收益");
            benefits.AppendLine("═══════════════════════════════════════");
            benefits.AppendLine();

            long memorySaved = 0;
            int drawCallsReduced = 0;
            float performanceGain = 0f;

            // 计算内存节省
            if (emptyObjectCount > emptyObjectThreshold)
            {
                memorySaved += emptyObjectCount * 1024; // 每个空对象约1KB
                float savedMB = emptyObjectCount > 0 ? emptyObjectCount / 1024f : 0;
                benefits.AppendLine($"✓ 清理{emptyObjectCount}个空对象 → ~{savedMB:F2}MB");
            }

            if (oversizedTextureCount > 0)
            {
                long textureSavings = oversizedTextureCount * 5 * 1024 * 1024;
                memorySaved += textureSavings;
                benefits.AppendLine($"✓ 压缩{oversizedTextureCount}个超大纹理 → ~{textureSavings / 1024 / 1024}MB");
                performanceGain += oversizedTextureCount * 2f;
            }

            if (uncompressedTextureCount > 0)
            {
                long textureCompressionSavings = uncompressedTextureCount * 3 * 1024 * 1024;
                memorySaved += textureCompressionSavings;
                benefits.AppendLine($"✓ 压缩{uncompressedTextureCount}个未压缩纹理 → ~{textureCompressionSavings / 1024 / 1024}MB");
            }

            if (highPolyCount > 0)
            {
                drawCallsReduced += highPolyCount / 2;
                benefits.AppendLine($"✓ 为{highPolyCount}个高面数模型添加LOD → -{drawCallsReduced} Draw Calls");
                performanceGain += highPolyCount * 5f;
            }

            if (realtimeLightCount > realtimeLightThreshold)
            {
                int lightsToConvert = realtimeLightCount - realtimeLightThreshold;
                benefits.AppendLine($"✓ 转换{lightsToConvert}个实时灯光为烘焙 → 节省GPU计算");
                performanceGain += lightsToConvert * 8f;
            }

            if (nonBatchableRenderers > 0 && enableStaticBatching)
            {
                int batchReduction = Mathf.Max(nonBatchableRenderers / 4, 1);
                drawCallsReduced += batchReduction;
                benefits.AppendLine($"✓ 启用静态批处理 → -{batchReduction} Draw Calls");
                performanceGain += 10f;
            }

            if (duplicateMaterials > 0 && mergeMaterials)
            {
                int matReduction = Mathf.Max(duplicateMaterials / 3, 1);
                benefits.AppendLine($"✓ 合并{duplicateMaterials}个重复材质 → 减少材质切换");
                drawCallsReduced += matReduction;
            }

            benefits.AppendLine();
            benefits.AppendLine("───────────────────────────────────────");
            benefits.AppendLine($"💾 总内存节省: ~{memorySaved / 1024 / 1024} MB");
            benefits.AppendLine($"🎨 Draw Calls减少: ~{drawCallsReduced}");
            benefits.AppendLine($"⚡ 性能提升: ~{Mathf.Clamp(performanceGain, 5, 50):F0}% (取决于硬件)");
            benefits.AppendLine("───────────────────────────────────────");

            estimatedBenefits = benefits.ToString();
        }
        #endregion

        #region 优化功能(增强版)
        [BoxGroup("优化操作"), Button("⚡ 按当前设置执行优化", ButtonHeight = 34), GUIColor(0.75f, 0.58f, 0.25f)]
        public void AutoOptimizeScene()
        {
            if (detectedIssues.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先执行场景分析！", "确定");
                return;
            }

            bool wantsImporterChanges = !previewOnly && (compressTextures || generateMipmaps || disableTextureReadWrite || compressAudio || enableAudioStreaming);
            if (wantsImporterChanges && !allowProjectAssetImportChanges)
            {
                EditorUtility.DisplayDialog("项目资产修改已拦截",
                    "当前配置包含纹理或音频导入设置修改，这类操作会影响项目资产，不只影响当前场景。\n\n请先关闭这些选项，或明确开启“允许修改项目资产导入设置”。",
                    "知道了");
                return;
            }

            if (wantsImporterChanges)
            {
                pendingImportSettingChanges = BuildPendingImportSettingChanges();
                if (pendingImportSettingChanges.Count == 0)
                {
                    approvedImporterChangePaths = new HashSet<string>();
                }
                else
                {
                    string preview = SimpleToolsSafetyUtility.JoinPreview(
                        pendingImportSettingChanges.Select(c => $"{c.assetPath} | {c.changeSummary}"),
                        10);
                    if (!EditorUtility.DisplayDialog("确认项目资产导入设置变更",
                        $"将修改 {pendingImportSettingChanges.Count} 个项目资产导入设置。\n\n{preview}\n\n这些修改不限于当前场景，执行后会重新导入资产。继续吗？",
                        "确认修改", "取消"))
                        return;

                    approvedImporterChangePaths = new HashSet<string>(pendingImportSettingChanges.Select(c => c.assetPath));
                }
            }

            string confirmMessage = $"准备按当前设置执行优化:\n\n";
            confirmMessage += $"• 发现 {detectedIssues.Count} 个优化项\n";
            confirmMessage += $"• 预计节省 {totalMemoryUsage / 1024 / 1024} MB 内存\n";
            if (wantsImporterChanges) confirmMessage += "• 会修改项目资产导入设置，不限当前场景\n";
            if (previewOnly) confirmMessage += "\n⚠️ 当前为预览模式,不会实际应用\n";
            confirmMessage += "\n是否继续?";

            if (!EditorUtility.DisplayDialog("确认优化", confirmMessage, "开始优化", "取消"))
                return;

            bool shouldBackup = false;
            if (autoBackup && !previewOnly)
            {
                shouldBackup = EditorUtility.DisplayDialog("备份确认", "是否备份当前场景?", "备份", "不备份");
            }

            if (shouldBackup)
            {
                BackupScene();
            }

            int optimizationsApplied = 0;
            List<string> optimizationLog = new List<string>();
            if (wantsImporterChanges)
                importSettingChanges.Clear();

            EditorUtility.DisplayProgressBar("智能优化", "准备优化...", 0f);

            try
            {
                // 对象优化
                if (optimizeEmptyObjects && emptyObjectCount > emptyObjectThreshold)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "清理空对象...", 0.1f);
                    int cleaned = CleanEmptyObjects();
                    if (cleaned > 0)
                    {
                        optimizationsApplied += cleaned;
                        optimizationLog.Add($"✓ 清理了 {cleaned} 个空对象");
                    }
                }

                if (removeMissingScripts && missingScriptCount > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "移除丢失脚本...", 0.2f);
                    int removed = RemoveMissingScripts();
                    if (removed > 0)
                    {
                        optimizationsApplied += removed;
                        optimizationLog.Add($"✓ 移除了 {removed} 个丢失脚本");
                    }
                }

                if (markStaticObjects)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "标记静态对象...", 0.3f);
                    int marked = MarkStaticObjects();
                    if (marked > 0)
                    {
                        optimizationsApplied += marked;
                        optimizationLog.Add($"✓ 标记了 {marked} 个静态对象");
                    }
                }

                // 渲染优化
                if (generateLODs && objectsNeedingLOD > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "生成LOD系统...", 0.4f);
                    int lodCount = GenerateLODSystems();
                    if (lodCount > 0)
                    {
                        optimizationsApplied += lodCount;
                        optimizationLog.Add($"✓ 为 {lodCount} 个模型生成了LOD");
                    }
                }

                if (optimizeShadows)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "优化阴影设置...", 0.45f);
                    int optimized = OptimizeShadowSettings();
                    if (optimized > 0)
                    {
                        optimizationsApplied += optimized;
                        optimizationLog.Add($"✓ 优化了 {optimized} 个阴影设置");
                    }
                }

                // 纹理优化
                if (compressTextures && (oversizedTextureCount > 0 || uncompressedTextureCount > 0))
                {
                    EditorUtility.DisplayProgressBar("智能优化", "压缩纹理...", 0.5f);
                    int compressed = CompressTextures();
                    if (compressed > 0)
                    {
                        optimizationsApplied += compressed;
                        optimizationLog.Add($"✓ 压缩了 {compressed} 个纹理");
                    }
                }

                if (generateMipmaps && mipmapDisabledTextures > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "生成Mipmaps...", 0.6f);
                    int generated = GenerateMipmaps();
                    if (generated > 0)
                    {
                        optimizationsApplied += generated;
                        optimizationLog.Add($"✓ 为 {generated} 个纹理生成了Mipmap");
                    }
                }

                if (disableTextureReadWrite && readableTextures > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "禁用纹理读写...", 0.65f);
                    int disabled = DisableTextureReadWrite();
                    if (disabled > 0)
                    {
                        optimizationsApplied += disabled;
                        optimizationLog.Add($"✓ 禁用了 {disabled} 个纹理的读写");
                    }
                }

                // 粒子优化
                if (optimizeParticles && particleSystemCount > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "优化粒子系统...", 0.7f);
                    int optimizedPS = OptimizeParticleSystems();
                    if (optimizedPS > 0)
                    {
                        optimizationsApplied += optimizedPS;
                        optimizationLog.Add($"✓ 优化了 {optimizedPS} 个粒子系统");
                    }
                }

                // 音频优化
                if (compressAudio && uncompressedAudioCount > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "压缩音频...", 0.8f);
                    int compressedAudio = CompressAudio();
                    if (compressedAudio > 0)
                    {
                        optimizationsApplied += compressedAudio;
                        optimizationLog.Add($"✓ 压缩了 {compressedAudio} 个音频文件");
                    }
                }

                // 物理优化
                if (optimizeColliders && nonConvexMeshColliders > 0)
                {
                    EditorUtility.DisplayProgressBar("智能优化", "优化碰撞体...", 0.9f);
                    int optimizedColliders = OptimizeColliders();
                    if (optimizedColliders > 0)
                    {
                        optimizationsApplied += optimizedColliders;
                        optimizationLog.Add($"✓ 优化了 {optimizedColliders} 个碰撞体");
                    }
                }

                EditorUtility.DisplayProgressBar("智能优化", "完成优化...", 1f);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                approvedImporterChangePaths = null;
            }

            // 显示优化结果
            StringBuilder resultMessage = new StringBuilder();
            resultMessage.AppendLine("🎉 优化完成！");
            resultMessage.AppendLine();
            resultMessage.AppendLine($"应用了 {optimizationsApplied} 项优化:");
            resultMessage.AppendLine();
            foreach (var log in optimizationLog)
            {
                resultMessage.AppendLine(log);
            }

            if (importSettingChanges.Count > 0)
            {
                resultMessage.AppendLine();
                resultMessage.AppendLine($"已记录 {importSettingChanges.Count} 条项目资产导入设置变更。");
                resultMessage.AppendLine("建议立刻导出回滚 JSON，方便验证后恢复。");
            }

            EditorUtility.DisplayDialog("优化完成", resultMessage.ToString(), "确定");

            if (importSettingChanges.Count > 0 &&
                EditorUtility.DisplayDialog("导出回滚文件", "本次修改了项目资产导入设置。现在导出回滚 JSON 吗？", "导出", "稍后"))
            {
                ExportImportSettingRollback();
            }

            // 重新分析以查看效果
            if (!previewOnly)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AnalyzeScene();
            }

        }

        private int CleanEmptyObjects()
        {
            if (previewOnly) return emptyObjectCount;

            var allObjects = GetActiveSceneGameObjects();
            int cleanedCount = 0;

            try
            {
                foreach (var obj in allObjects)
                {
                    if (obj == null)
                        continue;
                        
                    var components = obj.GetComponents<Component>();
                    if (components != null && components.Length == 1 && obj.transform.childCount == 0)
                    {
                        Undo.DestroyObjectImmediate(obj);
                        cleanedCount++;
                    }
                }

                if (verboseLogging && cleanedCount > 0)
                    UnityEngine.Debug.Log($"清理了 {cleanedCount} 个空对象");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"清理空对象时出错: {e.Message}");
            }

            MarkActiveSceneDirtyIfChanged(cleanedCount);
            return cleanedCount;
        }

        private int RemoveMissingScripts()
        {
            if (previewOnly) return missingScriptCount;

            var allObjects = GetActiveSceneGameObjects();
            int cleanedCount = 0;

            try
            {
                foreach (var obj in allObjects)
                {
                    if (obj == null)
                        continue;
                        
                    var components = obj.GetComponents<Component>();
                    if (components == null)
                        continue;
                        
                    var serializedObject = new SerializedObject(obj);
                    var prop = serializedObject.FindProperty("m_Component");

                    int removed = 0;
                    for (int i = components.Length - 1; i >= 0; i--)
                    {
                        if (components[i] == null)
                        {
                        prop.DeleteArrayElementAtIndex(i);
                        removed++;
                        cleanedCount++;
                    }
                }

                    if (removed > 0)
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(obj);
                    }
                }

                if (verboseLogging && cleanedCount > 0)
                    UnityEngine.Debug.Log($"移除了 {cleanedCount} 个丢失脚本");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"移除丢失脚本时出错: {e.Message}");
            }

            MarkActiveSceneDirtyIfChanged(cleanedCount);
            return cleanedCount;
        }

        private int MarkStaticObjects()
        {
            if (previewOnly) return 0;

            var allObjects = GetActiveSceneGameObjects();
            int markedCount = 0;

            try
            {
                foreach (var obj in allObjects)
                {
                    if (obj == null)
                        continue;
                        
                    if (obj.isStatic) 
                        continue;

                    var renderer = obj.GetComponent<Renderer>();
                    var rigidbody = obj.GetComponent<Rigidbody>();
                    var animator = obj.GetComponent<Animator>();
                    var particleSystem = obj.GetComponent<ParticleSystem>();

                    // 没有动态组件的对象可以标记为静态
                    if (renderer != null && rigidbody == null && animator == null && particleSystem == null)
                    {
                        Undo.RecordObject(obj, "Mark Static");
                        obj.isStatic = true;
                        EditorUtility.SetDirty(obj);
                        markedCount++;
                    }
                }

                if (verboseLogging && markedCount > 0)
                    UnityEngine.Debug.Log($"标记了 {markedCount} 个对象为静态");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"标记静态对象时出错: {e.Message}");
            }

            MarkActiveSceneDirtyIfChanged(markedCount);
            return markedCount;
        }

        private int GenerateLODSystems()
        {
            if (previewOnly) return 0;

            var renderers = GetActiveSceneComponents<MeshRenderer>();
            int lodCount = 0;

            try
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.gameObject == null)
                        continue;
                        
                    if (renderer.gameObject.GetComponent<LODGroup>() != null)
                        continue;

                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;

                    int triangleCount = meshFilter.sharedMesh.triangles.Length / 3;
                    if (triangleCount < highPolyThreshold)
                        continue;

                var lodGroup = Undo.AddComponent<LODGroup>(renderer.gameObject);

                float[] lodScreenPercent = { 0.6f, 0.3f, 0.15f, 0.07f, 0.03f };
                int safeLodLevelCount = Mathf.Clamp(lodLevelCount, 1, lodScreenPercent.Length);
                var lods = new LOD[safeLodLevelCount];

                for (int i = 0; i < safeLodLevelCount; i++)
                {
                    lods[i] = new LOD(lodScreenPercent[i], new Renderer[] { renderer });
                }

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                    lodCount++;
                }

                if (verboseLogging && lodCount > 0)
                    UnityEngine.Debug.Log($"为 {lodCount} 个模型生成了LOD系统");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"生成LOD系统时出错: {e.Message}");
            }

            MarkActiveSceneDirtyIfChanged(lodCount);
            return lodCount;
        }

        private int OptimizeShadowSettings()
        {
            if (previewOnly) return 0;

            var lights = GetActiveSceneComponents<Light>();
            int optimizedCount = 0;

            foreach (var light in lights)
            {
                if (light == null || light.gameObject == null)
                    continue;

                if (light.shadows == LightShadows.None)
                    continue;

                // 点光源和聚光灯使用软阴影性能消耗大
                if ((light.type == LightType.Point || light.type == LightType.Spot) &&
                    light.shadows == LightShadows.Soft)
                {
                    Undo.RecordObject(light, "Optimize Shadow");
                    light.shadows = LightShadows.Hard;
                    EditorUtility.SetDirty(light);
                    optimizedCount++;
                }
            }

            MarkActiveSceneDirtyIfChanged(optimizedCount);
            return optimizedCount;
        }

        private int CompressTextures()
        {
            if (previewOnly) return oversizedTextureCount + uncompressedTextureCount;

            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var textures = LoadActiveSceneDependencyAssets<Texture2D>(sceneDependencyPaths);
            int compressedCount = 0;

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var texture in textures)
                {
                    if (texture == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    if (!sceneDependencyPaths.Contains(SimpleToolsSafetyUtility.NormalizeAssetPath(path)))
                        continue;
                    if (!IsApprovedImporterChangePath(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        continue;

                    bool needsReimport = false;
                    string beforeState = CaptureTextureImporterState(importer);
                    var changedFields = new List<string>();

                    if (importer.textureCompression != TextureImporterCompression.Compressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Compressed;
                        needsReimport = true;
                        changedFields.Add("压缩格式");
                    }

                    if (importer.maxTextureSize > maxTextureSize)
                    {
                        importer.maxTextureSize = maxTextureSize;
                        needsReimport = true;
                        changedFields.Add("最大尺寸");
                    }

                    if (needsReimport)
                    {
                        string afterState = CaptureTextureImporterState(importer);
                        AddImportSettingChange(path, "TextureImporter", string.Join("、", changedFields), beforeState, afterState);
                        importer.SaveAndReimport();
                        compressedCount++;
                    }
                }
            });

            if (verboseLogging && compressedCount > 0)
                UnityEngine.Debug.Log($"压缩了 {compressedCount} 个纹理");

            return compressedCount;
        }

        private int GenerateMipmaps()
        {
            if (previewOnly) return mipmapDisabledTextures;

            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var textures = LoadActiveSceneDependencyAssets<Texture2D>(sceneDependencyPaths);
            int generatedCount = 0;

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var texture in textures)
                {
                    if (texture == null || texture.width <= 512)
                        continue;

                    string path = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    if (!sceneDependencyPaths.Contains(SimpleToolsSafetyUtility.NormalizeAssetPath(path)))
                        continue;
                    if (!IsApprovedImporterChangePath(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.mipmapEnabled)
                    {
                        string beforeState = CaptureTextureImporterState(importer);
                        importer.mipmapEnabled = true;
                        string afterState = CaptureTextureImporterState(importer);
                        AddImportSettingChange(path, "TextureImporter", "生成Mipmap", beforeState, afterState);
                        importer.SaveAndReimport();
                        generatedCount++;
                    }
                }
            });

            return generatedCount;
        }

        private int DisableTextureReadWrite()
        {
            if (previewOnly) return readableTextures;

            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var textures = LoadActiveSceneDependencyAssets<Texture2D>(sceneDependencyPaths);
            int disabledCount = 0;

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var texture in textures)
                {
                    if (texture == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    if (!sceneDependencyPaths.Contains(SimpleToolsSafetyUtility.NormalizeAssetPath(path)))
                        continue;
                    if (!IsApprovedImporterChangePath(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.isReadable)
                    {
                        string beforeState = CaptureTextureImporterState(importer);
                        importer.isReadable = false;
                        string afterState = CaptureTextureImporterState(importer);
                        AddImportSettingChange(path, "TextureImporter", "禁用读写", beforeState, afterState);
                        importer.SaveAndReimport();
                        disabledCount++;
                    }
                }
            });

            return disabledCount;
        }

        private int OptimizeParticleSystems()
        {
            if (previewOnly) return inactiveParticleCount;

            var particleSystems = GetActiveSceneComponents<ParticleSystem>();
            int optimizedCount = 0;

            foreach (var ps in particleSystems)
            {
                if (ps == null || ps.gameObject == null)
                    continue;

                if (!ps.isPlaying && !ps.isPaused && disableInactiveParticles)
                {
                    Undo.RecordObject(ps.gameObject, "Disable Particle");
                    ps.gameObject.SetActive(false);
                    EditorUtility.SetDirty(ps.gameObject);
                    optimizedCount++;
                }
                else if (reduceParticleCount)
                {
                    var main = ps.main;
                    var emission = ps.emission;

                    if (emission.rateOverTime.constant > particleEmissionThreshold)
                    {
                        Undo.RecordObject(ps, "Reduce Particles");
                        var rate = emission.rateOverTime;
                        rate.constant = particleEmissionThreshold;
                        emission.rateOverTime = rate;
                        EditorUtility.SetDirty(ps);
                        optimizedCount++;
                    }
                }
            }

            MarkActiveSceneDirtyIfChanged(optimizedCount);
            return optimizedCount;
        }

        private int CompressAudio()
        {
            if (previewOnly) return uncompressedAudioCount;

            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();
            var audioClips = LoadActiveSceneDependencyAssets<AudioClip>(sceneDependencyPaths);
            int compressedCount = 0;

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var clip in audioClips)
                {
                    if (clip == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(clip);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    if (!sceneDependencyPaths.Contains(SimpleToolsSafetyUtility.NormalizeAssetPath(path)))
                        continue;
                    if (!IsApprovedImporterChangePath(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer != null)
                    {
                        var settings = importer.defaultSampleSettings;
                        string beforeState = CaptureAudioImporterState(importer);
                        bool needsReimport = false;
                        var changedFields = new List<string>();

                        if (settings.compressionFormat == AudioCompressionFormat.PCM)
                        {
                            settings.compressionFormat = AudioCompressionFormat.Vorbis;
                            settings.quality = 0.7f;
                            needsReimport = true;
                            changedFields.Add("压缩格式");
                        }

                        if (enableAudioStreaming && settings.loadType != AudioClipLoadType.Streaming)
                        {
                            settings.loadType = AudioClipLoadType.Streaming;
                            needsReimport = true;
                            changedFields.Add("加载方式");
                        }

                        if (needsReimport)
                        {
                            importer.defaultSampleSettings = settings;
                            string afterState = CaptureAudioImporterState(importer);
                            AddImportSettingChange(path, "AudioImporter", string.Join("、", changedFields), beforeState, afterState);
                            importer.SaveAndReimport();
                            compressedCount++;
                        }
                    }
                }
            });

            return compressedCount;
        }

        private int OptimizeColliders()
        {
            if (previewOnly) return nonConvexMeshColliders;

            var meshColliders = GetActiveSceneComponents<MeshCollider>();
            int optimizedCount = 0;

            foreach (var collider in meshColliders)
            {
                if (collider == null || collider.gameObject == null || collider.gameObject.scene != SceneManager.GetActiveScene())
                    continue;

                if (!collider.convex && !collider.GetComponent<Rigidbody>())
                {
                    // 如果没有刚体且不是凸的,尝试转换为简单碰撞体
                    if (simplifyMeshColliders)
                    {
                        Undo.RecordObject(collider.gameObject, "Simplify Collider");
                        Undo.DestroyObjectImmediate(collider);
                        var boxCollider = Undo.AddComponent<BoxCollider>(collider.gameObject);
                        EditorUtility.SetDirty(boxCollider);
                        optimizedCount++;
                    }
                    else if (!collider.convex)
                    {
                        Undo.RecordObject(collider, "Make Convex");
                        collider.convex = true;
                        EditorUtility.SetDirty(collider);
                        optimizedCount++;
                    }
                }
            }

            MarkActiveSceneDirtyIfChanged(optimizedCount);
            return optimizedCount;
        }

        private void MarkActiveSceneDirtyIfChanged(int changedCount)
        {
            if (changedCount <= 0)
                return;

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private List<GameObject> GetActiveSceneGameObjects()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return new List<GameObject>();

            return scene.GetRootGameObjects()
                .Where(root => root != null)
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform != null)
                .Select(transform => transform.gameObject)
                .Where(obj => obj != null && obj.scene == scene)
                .Distinct()
                .ToList();
        }

        private T[] GetActiveSceneComponents<T>() where T : Component
        {
            return GetActiveSceneGameObjects()
                .SelectMany(obj => obj != null ? obj.GetComponents<T>() : Array.Empty<T>())
                .Where(component => component != null)
                .ToArray();
        }

        private HashSet<string> GetActiveSceneDependencyAssetPaths()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                foreach (var path in AssetDatabase.GetDependencies(scene.path, true))
                    AddAssetAndDependencies(path);
            }

            foreach (var renderer in GetActiveSceneComponents<Renderer>())
            {
                if (renderer == null)
                    continue;

                foreach (var material in renderer.sharedMaterials)
                    AddAssetAndDependencies(AssetDatabase.GetAssetPath(material));
            }

            foreach (var meshFilter in GetActiveSceneComponents<MeshFilter>())
                AddAssetAndDependencies(AssetDatabase.GetAssetPath(meshFilter != null ? meshFilter.sharedMesh : null));

            foreach (var skinnedMesh in GetActiveSceneComponents<SkinnedMeshRenderer>())
                AddAssetAndDependencies(AssetDatabase.GetAssetPath(skinnedMesh != null ? skinnedMesh.sharedMesh : null));

            foreach (var source in GetActiveSceneComponents<AudioSource>())
                AddAssetAndDependencies(AssetDatabase.GetAssetPath(source != null ? source.clip : null));

            return result;

            void AddAssetAndDependencies(string assetPath)
            {
                assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
                if (string.IsNullOrEmpty(assetPath) || !SimpleToolsSafetyUtility.IsAssetPath(assetPath))
                    return;

                result.Add(assetPath);
                foreach (var dependency in AssetDatabase.GetDependencies(assetPath, true))
                {
                    var normalized = SimpleToolsSafetyUtility.NormalizeAssetPath(dependency);
                    if (!string.IsNullOrEmpty(normalized) && SimpleToolsSafetyUtility.IsAssetPath(normalized))
                        result.Add(normalized);
                }
            }
        }

        private List<T> LoadActiveSceneDependencyAssets<T>(HashSet<string> sceneDependencyPaths = null) where T : UnityEngine.Object
        {
            sceneDependencyPaths ??= GetActiveSceneDependencyAssetPaths();

            var assets = new List<T>();
            var addedIds = new HashSet<int>();
            foreach (var path in sceneDependencyPaths)
            {
                if (string.IsNullOrEmpty(path) || !SimpleToolsSafetyUtility.IsAssetPath(path))
                    continue;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>())
                {
                    if (asset == null)
                        continue;

                    if (addedIds.Add(asset.GetInstanceID()))
                        assets.Add(asset);
                }
            }

            return assets;
        }

        private void AddImportSettingChange(string assetPath, string importerType, string changeSummary, string beforeState, string afterState)
        {
            importSettingChanges.Add(new ImportSettingChangeRecord
            {
                assetPath = assetPath,
                importerType = importerType,
                changeSummary = changeSummary,
                beforeState = beforeState,
                afterState = afterState,
                changedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private bool IsApprovedImporterChangePath(string assetPath)
        {
            return approvedImporterChangePaths == null || approvedImporterChangePaths.Contains(assetPath);
        }

        private List<ImportSettingChangeRecord> BuildPendingImportSettingChanges()
        {
            var changes = new Dictionary<string, ImportSettingChangeRecord>();
            var sceneDependencyPaths = GetActiveSceneDependencyAssetPaths();

            if (compressTextures || generateMipmaps || disableTextureReadWrite)
            {
                var textures = LoadActiveSceneDependencyAssets<Texture2D>(sceneDependencyPaths);
                foreach (var texture in textures)
                {
                    if (texture == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    path = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                    if (!sceneDependencyPaths.Contains(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        continue;

                    string beforeState = CaptureTextureImporterState(importer);
                    TextureImporterCompression newCompression = importer.textureCompression;
                    int newMaxSize = importer.maxTextureSize;
                    bool newMipmap = importer.mipmapEnabled;
                    bool newReadable = importer.isReadable;
                    var fields = new List<string>();

                    if (compressTextures)
                    {
                        if (newCompression != TextureImporterCompression.Compressed)
                        {
                            newCompression = TextureImporterCompression.Compressed;
                            fields.Add("压缩格式");
                        }
                        if (newMaxSize > maxTextureSize)
                        {
                            newMaxSize = maxTextureSize;
                            fields.Add("最大尺寸");
                        }
                    }

                    if (generateMipmaps && texture.width > 512 && !newMipmap)
                    {
                        newMipmap = true;
                        fields.Add("生成Mipmap");
                    }

                    if (disableTextureReadWrite && newReadable)
                    {
                        newReadable = false;
                        fields.Add("禁用读写");
                    }

                    if (fields.Count == 0)
                        continue;

                    string afterState = BuildTextureImporterState(newCompression, newMaxSize, newMipmap, newReadable);
                    changes[path] = CreateImportSettingRecord(path, "TextureImporter", string.Join("、", fields), beforeState, afterState);
                }
            }

            if (compressAudio || enableAudioStreaming)
            {
                var audioClips = LoadActiveSceneDependencyAssets<AudioClip>(sceneDependencyPaths);
                foreach (var clip in audioClips)
                {
                    if (clip == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(clip);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets"))
                        continue;
                    path = SimpleToolsSafetyUtility.NormalizeAssetPath(path);
                    if (!sceneDependencyPaths.Contains(path))
                        continue;

                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null)
                        continue;

                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    string beforeState = CaptureAudioImporterState(importer);
                    AudioCompressionFormat newFormat = settings.compressionFormat;
                    float newQuality = settings.quality;
                    AudioClipLoadType newLoadType = settings.loadType;
                    var fields = new List<string>();

                    if (compressAudio && newFormat == AudioCompressionFormat.PCM)
                    {
                        newFormat = AudioCompressionFormat.Vorbis;
                        newQuality = 0.7f;
                        fields.Add("压缩格式");
                    }

                    if (enableAudioStreaming && newLoadType != AudioClipLoadType.Streaming)
                    {
                        newLoadType = AudioClipLoadType.Streaming;
                        fields.Add("加载方式");
                    }

                    if (fields.Count == 0)
                        continue;

                    string afterState = BuildAudioImporterState(newFormat, newQuality, newLoadType);
                    changes[path] = CreateImportSettingRecord(path, "AudioImporter", string.Join("、", fields), beforeState, afterState);
                }
            }

            return changes.Values.OrderBy(c => c.assetPath, StringComparer.Ordinal).ToList();
        }

        private ImportSettingChangeRecord CreateImportSettingRecord(string assetPath, string importerType, string summary, string beforeState, string afterState)
        {
            return new ImportSettingChangeRecord
            {
                assetPath = assetPath,
                importerType = importerType,
                changeSummary = summary,
                beforeState = beforeState,
                afterState = afterState,
                changedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private string CaptureTextureImporterState(TextureImporter importer)
        {
            return BuildTextureImporterState(importer.textureCompression, importer.maxTextureSize, importer.mipmapEnabled, importer.isReadable);
        }

        private string BuildTextureImporterState(TextureImporterCompression compression, int maxSize, bool mipmapEnabled, bool isReadable)
        {
            return string.Join("\n", new[]
            {
                $"textureCompression={(int)compression}",
                $"maxTextureSize={maxSize}",
                $"mipmapEnabled={mipmapEnabled}",
                $"isReadable={isReadable}"
            });
        }

        private string CaptureAudioImporterState(AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            return BuildAudioImporterState(settings.compressionFormat, settings.quality, settings.loadType);
        }

        private string BuildAudioImporterState(AudioCompressionFormat compressionFormat, float quality, AudioClipLoadType loadType)
        {
            return string.Join("\n", new[]
            {
                $"compressionFormat={(int)compressionFormat}",
                $"quality={quality.ToString(CultureInfo.InvariantCulture)}",
                $"loadType={(int)loadType}"
            });
        }

        private bool RestoreImporterState(ImportSettingChangeRecord change)
        {
            if (change == null || string.IsNullOrEmpty(change.assetPath))
                return false;

            var importer = AssetImporter.GetAtPath(change.assetPath);
            if (importer == null)
                return false;

            var state = ParseState(change.beforeState);
            if (importer is TextureImporter textureImporter && change.importerType == "TextureImporter")
            {
                if (state.TryGetValue("textureCompression", out string compression))
                    textureImporter.textureCompression = (TextureImporterCompression)int.Parse(compression, CultureInfo.InvariantCulture);
                if (state.TryGetValue("maxTextureSize", out string maxSize))
                    textureImporter.maxTextureSize = int.Parse(maxSize, CultureInfo.InvariantCulture);
                if (state.TryGetValue("mipmapEnabled", out string mipmapEnabled))
                    textureImporter.mipmapEnabled = bool.Parse(mipmapEnabled);
                if (state.TryGetValue("isReadable", out string isReadable))
                    textureImporter.isReadable = bool.Parse(isReadable);

                textureImporter.SaveAndReimport();
                return true;
            }

            if (importer is AudioImporter audioImporter && change.importerType == "AudioImporter")
            {
                AudioImporterSampleSettings settings = audioImporter.defaultSampleSettings;
                if (state.TryGetValue("compressionFormat", out string compressionFormat))
                    settings.compressionFormat = (AudioCompressionFormat)int.Parse(compressionFormat, CultureInfo.InvariantCulture);
                if (state.TryGetValue("quality", out string quality))
                    settings.quality = float.Parse(quality, CultureInfo.InvariantCulture);
                if (state.TryGetValue("loadType", out string loadType))
                    settings.loadType = (AudioClipLoadType)int.Parse(loadType, CultureInfo.InvariantCulture);

                audioImporter.defaultSampleSettings = settings;
                audioImporter.SaveAndReimport();
                return true;
            }

            return false;
        }

        private Dictionary<string, string> ParseState(string state)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(state))
                return result;

            string[] lines = state.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int index = line.IndexOf('=');
                if (index <= 0)
                    continue;

                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1).Trim();
                if (!string.IsNullOrEmpty(key))
                    result[key] = value;
            }

            return result;
        }
        #endregion

        #region 报告导出(增强版)
        [BoxGroup("报告导出"), Button("📄 导出详细报告(TXT)", ButtonHeight = 34), GUIColor(0.5f, 0.8f, 0.5f)]
        public void ExportDetailedReport()
        {
            string defaultFileName = SanitizeFileName($"SceneOptimization_{SceneManager.GetActiveScene().name}_{DateTime.Now:yyyyMMdd_HHmmss}") + ".txt";
            string path = EditorUtility.SaveFilePanel("导出优化报告", "", defaultFileName, "txt");

            if (!string.IsNullOrEmpty(path))
            {
                StringBuilder report = new StringBuilder();

                report.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
                report.AppendLine("║          Unity场景性能优化分析报告                    ║");
                report.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
                report.AppendLine();
                report.AppendLine(sceneOverview);
                report.AppendLine();
                report.AppendLine(analysisResult);
                report.AppendLine();
                report.AppendLine(optimizationSuggestions);
                report.AppendLine();
                report.AppendLine(estimatedBenefits);
                report.AppendLine();

                // 添加Shader使用统计
                if (shaderUsage.Count > 0)
                {
                    report.AppendLine("═══════════════════════════════════════");
                    report.AppendLine("          Shader使用统计");
                    report.AppendLine("═══════════════════════════════════════");
                    var sortedShaders = shaderUsage.OrderByDescending(kvp => kvp.Value);
                    foreach (var shader in sortedShaders.Take(10))
                    {
                        report.AppendLine($"  • {shader.Key}: {shader.Value} 次使用");
                    }
                    report.AppendLine();
                }

                // 添加详细问题列表
                if (detectedIssues.Count > 0)
                {
                    report.AppendLine("═══════════════════════════════════════");
                    report.AppendLine("          详细问题列表");
                    report.AppendLine("═══════════════════════════════════════");
                    report.AppendLine();

                    var groupedIssues = detectedIssues.GroupBy(i => i.category);
                    foreach (var category in groupedIssues)
                    {
                        report.AppendLine($"【{category.Key}】({category.Count()}个问题)");
                        foreach (var issue in category)
                        {
                            string objName = !string.IsNullOrEmpty(issue.targetObjectPath) ? issue.targetObjectPath : "N/A";
                            report.AppendLine($"  [{issue.severity}] {issue.description}");
                            report.AppendLine($"    对象: {objName}");
                            report.AppendLine($"    修复: {issue.fixAction}");
                            report.AppendLine($"    影响: {issue.estimatedImpact}%");
                            report.AppendLine();
                        }
                        report.AppendLine();
                    }
                }

                report.AppendLine("═══════════════════════════════════════");
                report.AppendLine($"报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine("═══════════════════════════════════════");

                try
                {
                    File.WriteAllText(path, report.ToString(), Encoding.UTF8);
                    EditorUtility.DisplayDialog("成功", $"详细报告已导出到:\n{path}", "确定");
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("导出失败", $"无法写入详细报告：\n{ex.Message}", "知道了");
                    return;
                }

                if (EditorUtility.DisplayDialog("打开文件", "是否打开导出的报告?", "打开", "取消"))
                {
                    EditorUtility.OpenWithDefaultApp(path);
                }
            }
        }

        [BoxGroup("报告导出"), Button("📊 导出CSV数据", ButtonHeight = 34), GUIColor(0.5f, 0.7f, 0.9f)]
        public void ExportCSVData()
        {
            string defaultFileName = SanitizeFileName($"SceneOptimization_{SceneManager.GetActiveScene().name}_{DateTime.Now:yyyyMMdd_HHmmss}") + ".csv";
            string path = EditorUtility.SaveFilePanel("导出CSV数据", "", defaultFileName, "csv");

            if (!string.IsNullOrEmpty(path))
            {
                StringBuilder csv = new StringBuilder();

                // CSV表头
                csv.AppendLine("类别,严重程度,描述,对象名称,修复建议,预计影响");

                foreach (var issue in detectedIssues)
                {
                    string objName = !string.IsNullOrEmpty(issue.targetObjectPath) ? issue.targetObjectPath : "N/A";
                    csv.AppendLine(string.Join(",",
                        EscapeCsv(issue.category.ToString()),
                        EscapeCsv(issue.severity.ToString()),
                        EscapeCsv(issue.description),
                        EscapeCsv(objName),
                        EscapeCsv(issue.fixAction),
                        issue.estimatedImpact.ToString(CultureInfo.InvariantCulture)));
                }

                try
                {
                    File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
                    EditorUtility.DisplayDialog("成功", $"CSV数据已导出到:\n{path}", "确定");
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("导出失败", $"无法写入 CSV 数据：\n{ex.Message}", "知道了");
                }
            }
        }
        #endregion

        #region 辅助方法
        private void UpdateDisplayedIssues()
        {
            DisplayedIssues = detectedIssues.Where(issue =>
                ((categoryFilter & issue.category) != 0) &&
                ((severityFilter & issue.severity) != 0) &&
                issue.estimatedImpact >= minImpact
            ).ToList();
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "SceneOptimization";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalidChar, '_');

            fileName = fileName.Trim();
            return string.IsNullOrEmpty(fileName) ? "SceneOptimization" : fileName;
        }

        private string EscapeCsv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private GameObject FindGameObjectWithMaterial(Material mat)
        {
            var renderers = GetActiveSceneComponents<Renderer>();
            foreach (var r in renderers)
            {
                if (r != null && r.gameObject != null && r.sharedMaterials.Contains(mat))
                    return r.gameObject;
            }
            return null;
        }

        private GameObject FindGameObjectWithTexture(Texture texture)
        {
            var renderers = GetActiveSceneComponents<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterials == null)
                    continue;

                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || !mat.HasProperty("_MainTex"))
                        continue;

                    try
                    {
                        var tex = mat.GetTexture("_MainTex");
                        if (tex == texture)
                            return renderer.gameObject;
                    }
                    catch
                    {
                        // Property exists but is not a texture property
                    }
                }
            }
            return null;
        }

        private GameObject FindGameObjectWithAudioClip(AudioClip clip)
        {
            var audioSources = GetActiveSceneComponents<AudioSource>();
            foreach (var source in audioSources)
            {
                if (source != null && source.clip == clip)
                    return source.gameObject;
            }
            return null;
        }

        private string GetEnumDisplayName(System.Enum enumValue)
        {
            var field = enumValue.GetType().GetField(enumValue.ToString());
            if (field != null)
            {
                var attributes = field.GetCustomAttributes(typeof(InspectorNameAttribute), false);
                if (attributes != null && attributes.Length > 0)
                {
                    return ((InspectorNameAttribute)attributes[0]).displayName;
                }
            }
            return enumValue.ToString();
        }
        #endregion
    }
    #endregion

}
