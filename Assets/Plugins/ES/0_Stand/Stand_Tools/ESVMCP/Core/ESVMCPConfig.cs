using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES.VMCP
{
    /// <summary>
    /// 环境数据描述详细等级
    /// </summary>
    public enum EnvironmentDetailLevel
    {
        [LabelText("简短 (约1000字符)")]
        Brief = 1000,
        
        [LabelText("常规 (约4000字符)")]
        Normal = 4000,
        
        [LabelText("完整 (约8000字符)")]
        Complete = 8000,
        
        [LabelText("极限 (约20000字符)")]
        Extreme = 20000,
        
        [LabelText("颗粒级 (无损完整描述)")]
        Granular = int.MaxValue
    }

    /// <summary>
    /// ESVMCP配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "ESVMCPConfig", menuName = "ES/VMCP/配置文件")]
    public class ESVMCPConfig : ESEditorGlobalSo<ESVMCPConfig>
    {
        [Title("文件路径配置")]
        [FolderPath]
        [LabelText("ESVMCP根文件夹")]
        [InfoBox("ESVMCP主文件夹的位置，所有其他路径都相对于此文件夹")]
        public string RootFolder = "Assets/ES/ESVMCP";

        [ReadOnly]
        [LabelText("基础文件夹")]
        [InfoBox("ESVMCP数据存储的基础文件夹，所有子文件夹将自动创建在此文件夹下")]
        public string BaseFolder => System.IO.Path.Combine(RootFolder, "RunningData");

        [ReadOnly]
        [LabelText("输入文件夹")]
        [InfoBox("放置待执行的JSON命令文件")]
        public string InputFolder => System.IO.Path.Combine(BaseFolder, "Input");

        [ReadOnly]
        [LabelText("归档文件夹")]
        [InfoBox("执行完成后的JSON文件归档位置")]
        public string ArchiveFolder => System.IO.Path.Combine(BaseFolder, "Archive");

        [ReadOnly]
        [LabelText("记忆导出文件夹")]
        [InfoBox("导出的记忆文件存储位置")]
        public string MemoryFolder => System.IO.Path.Combine(BaseFolder, "Memory");

        [ReadOnly]
        [LabelText("日志文件夹")]
        [InfoBox("日志文件存储位置")]
        public string LogFolder => System.IO.Path.Combine(BaseFolder, "Logs");

        [ReadOnly]
        [LabelText("资源文件夹")]
        [InfoBox("ESVMCP资源文件存储位置")]
        public string ResourcesFolder => System.IO.Path.Combine(RootFolder, "Resources");

        [Title("资源创建路径配置")]
        [FolderPath]
        [LabelText("资源总路径")]
        [InfoBox("所有ESVMCP创建的资源的根路径")]
        public string ResourcesRootPath = "Assets/ResourceNormal";

        [LabelText("材质文件夹名")]
        [InfoBox("材质资源的子文件夹名称")]
        public string MaterialsFolderName = "Materials";

        [LabelText("预制体文件夹名")]
        [InfoBox("预制体资源的子文件夹名称")]
        public string PrefabsFolderName = "Prefabs";

        [LabelText("纹理文件夹名")]
        [InfoBox("纹理资源的子文件夹名称")]
        public string TexturesFolderName = "Textures";

        [LabelText("场景文件夹名")]
        [InfoBox("场景资源的子文件夹名称")]
        public string ScenesFolderName = "Scenes";

        [LabelText("脚本文件夹名")]
        [InfoBox("脚本资源的子文件夹名称")]
        public string ScriptsFolderName = "Scripts";

        [LabelText("音频文件夹名")]
        [InfoBox("音频资源的子文件夹名称")]
        public string AudioFolderName = "Audio";

        [LabelText("动画文件夹名")]
        [InfoBox("动画资源的子文件夹名称")]
        public string AnimationsFolderName = "Animations";

        [ReadOnly]
        [ShowInInspector]
        [LabelText("材质完整路径")]
        public string MaterialsPath => System.IO.Path.Combine(ResourcesRootPath, MaterialsFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("预制体完整路径")]
        public string PrefabsPath => System.IO.Path.Combine(ResourcesRootPath, PrefabsFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("纹理完整路径")]
        public string TexturesPath => System.IO.Path.Combine(ResourcesRootPath, TexturesFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("场景完整路径")]
        public string ScenesPath => System.IO.Path.Combine(ResourcesRootPath, ScenesFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("脚本完整路径")]
        public string ScriptsPath => System.IO.Path.Combine(ResourcesRootPath, ScriptsFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("音频完整路径")]
        public string AudioPath => System.IO.Path.Combine(ResourcesRootPath, AudioFolderName, "From_ESVMCP");

        [ReadOnly]
        [ShowInInspector]
        [LabelText("动画完整路径")]
        public string AnimationsPath => System.IO.Path.Combine(ResourcesRootPath, AnimationsFolderName, "From_ESVMCP");

        /// <summary>
        /// 获取资源类型的路径
        /// </summary>
        public string GetResourcePath(string resourceType)
        {
            switch (resourceType.ToLower())
            {
                case "material":
                case "materials":
                    return MaterialsPath;
                case "prefab":
                case "prefabs":
                    return PrefabsPath;
                case "texture":
                case "textures":
                    return TexturesPath;
                case "scene":
                case "scenes":
                    return ScenesPath;
                case "script":
                case "scripts":
                    return ScriptsPath;
                case "audio":
                    return AudioPath;
                case "animation":
                case "animations":
                    return AnimationsPath;
                default:
                    return System.IO.Path.Combine(ResourcesRootPath, "Other", "From_ESVMCP");
            }
        }

        [Title("环境数据获取设置")]
        [LabelText("默认描述详细等级")]
        [InfoBox("获取环境数据时的默认详细程度")]
        public EnvironmentDetailLevel DefaultDetailLevel = EnvironmentDetailLevel.Normal;

        [LabelText("包含隐藏对象")]
        [InfoBox("获取环境数据时是否包含隐藏/未激活的对象")]
        public bool IncludeInactiveObjects = false;

        [LabelText("包含Editor Only对象")]
        [InfoBox("获取环境数据时是否包含仅Editor的对象")]
        public bool IncludeEditorOnlyObjects = false;

        [LabelText("最大层级深度")]
        [Range(1, 20)]
        [InfoBox("获取对象层级结构时的最大深度")]
        public int MaxHierarchyDepth = 10;

        [Title("记忆系统配置")]
        [LabelText("持久记忆资产")]
        [InfoBox("跨场景和会话的持久化记忆资产引用")]
        public ESVMCPMemoryAssetEnhanced PersistentMemoryAsset;

        /// <summary>
        /// 获取持久记忆资产（自动加载或创建）
        /// </summary>
        public ESVMCPMemoryAssetEnhanced GetPersistentMemory()
        {
            if (PersistentMemoryAsset == null)
            {
                // 尝试从Resources加载
                PersistentMemoryAsset = UnityEngine.Resources.Load<ESVMCPMemoryAssetEnhanced>("ESVMCPMemory");
                
                #if UNITY_EDITOR
                if (PersistentMemoryAsset == null)
                {
                    // 如果不存在，尝试从Assets路径加载
                    string assetPath = System.IO.Path.Combine(ResourcesFolder, "ESVMCPMemory.asset");
                    PersistentMemoryAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ESVMCPMemoryAssetEnhanced>(assetPath);
                    
                    if (PersistentMemoryAsset == null)
                    {
                        Debug.LogWarning("[ESVMCP] 持久记忆资产未找到，将使用空引用。建议在ESVMCPConfig中设置PersistentMemoryAsset字段。");
                    }
                }
                #endif
            }
            
            return PersistentMemoryAsset;
        }

        [Title("监视器设置")]
        [LabelText("检查间隔(秒)")]
        [Range(0.1f, 10f)]
        [InfoBox("监视输入文件夹的时间间隔")]
        public float CheckInterval = 1.0f;

        [LabelText("自动执行")]
        [InfoBox("检测到新文件时自动执行")]
        public bool AutoExecute = true;

        [LabelText("执行前验证")]
        [InfoBox("执行命令前进行格式和参数验证")]
        public bool ValidateBeforeExecute = true;

        [Title("执行设置")]
        [LabelText("遇错停止")]
        [InfoBox("遇到错误时停止执行后续命令")]
        public bool StopOnError = false;

        [LabelText("单文件最大命令数")]
        [Range(1, 1000)]
        [InfoBox("单个JSON文件允许的最大命令数量")]
        public int MaxCommandsPerFile = 100;

        [LabelText("命令延迟(秒)")]
        [Range(0f, 5f)]
        [InfoBox("命令之间的执行延迟")]
        public float CommandDelay = 0.1f;

        [Title("记忆系统")]
        [LabelText("启用记忆")]
        [InfoBox("启用记忆系统功能")]
        public bool EnableMemory = true;

        [LabelText("最大记忆条目")]
        [Range(100, 10000)]
        [InfoBox("记忆系统保存的最大条目数")]
        public int MaxMemoryEntries = 1000;

        [LabelText("自动导出记忆")]
        [InfoBox("执行命令后自动导出记忆到文本文件")]
        public bool AutoExportMemory = true;

        [LabelText("记忆保留天数")]
        [Range(1, 90)]
        [InfoBox("自动清理多少天前的记忆")]
        public int MemoryRetentionDays = 30;

        [Title("日志设置")]
        [LabelText("启用详细日志")]
        [InfoBox("记录详细的执行日志")]
        public bool EnableVerboseLogging = true;

        [Title("安全设置")]
        [LabelText("启用命令白名单")]
        [InfoBox("只允许白名单中的命令执行")]
        public bool EnableCommandWhitelist = false;

        [LabelText("危险操作警告")]
        [InfoBox("执行危险操作前显示警告")]
        public bool WarnDangerousOperations = true;

        [Title("AI集成")]
        [LabelText("AI指导文档路径")]
        [InfoBox("AI交互指导文档的路径，用于【AI指导】功能")]
        [FilePath]
        public string AIGuidanceDocumentPath => System.IO.Path.Combine(RootFolder, "AI_INTERACTION_GUIDE.md");

        /// <summary>
        /// 获取完整路径
        /// </summary>
        public string GetFullPath(string relativePath)
        {
            return System.IO.Path.Combine(Application.dataPath, "..", relativePath);
        }

        /// <summary>
        /// 确保文件夹存在
        /// </summary>
        public void EnsureFoldersExist()
        {
            EnsureFolderExists(InputFolder);
            EnsureFolderExists(ArchiveFolder);
            EnsureFolderExists(MemoryFolder);
            EnsureFolderExists(LogFolder);
            
            // 确保资源文件夹存在
            EnsureFolderExists(MaterialsPath);
            EnsureFolderExists(PrefabsPath);
            EnsureFolderExists(TexturesPath);
            EnsureFolderExists(ScenesPath);
            EnsureFolderExists(ScriptsPath);
            EnsureFolderExists(AudioPath);
            EnsureFolderExists(AnimationsPath);
        }

        private void EnsureFolderExists(string folder)
        {
            string fullPath = GetFullPath(folder);
            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
                Debug.Log($"[ESVMCP] 创建文件夹: {fullPath}");
            }
        }

        [Button("创建默认文件夹", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void CreateDefaultFolders()
        {
            EnsureFoldersExist();
            Debug.Log("[ESVMCP] 默认文件夹创建完成！");
        }

        [Button("打开输入文件夹", ButtonSizes.Medium)]
        private void OpenInputFolder()
        {
            string fullPath = GetFullPath(InputFolder);
            if (System.IO.Directory.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
            else
            {
                Debug.LogWarning($"[ESVMCP] 文件夹不存在: {fullPath}");
            }
        }

        [Button("打开归档文件夹", ButtonSizes.Medium)]
        private void OpenArchiveFolder()
        {
            string fullPath = GetFullPath(ArchiveFolder);
            if (System.IO.Directory.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(fullPath);
            }
            else
            {
                Debug.LogWarning($"[ESVMCP] 文件夹不存在: {fullPath}");
            }
        }
    }
}
