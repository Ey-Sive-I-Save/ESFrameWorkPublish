using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ES.VMCP
{
    /// <summary>
    /// 场景分析器 - 智能分析场景复杂度并生成AI友好的环境信息
    /// </summary>
    public static class ESVMCPSceneAnalyzer
    {
        /// <summary>
        /// 场景复杂度等级
        /// </summary>
        public enum SceneComplexity
        {
            Simple,      // 简单场景 (<30个对象)
            Medium,      // 中等场景 (30-100个对象)
            Complex,     // 复杂场景 (100-500个对象)
            VeryComplex  // 非常复杂 (>500个对象)
        }

        /// <summary>
        /// 生成智能环境识别信息
        /// </summary>
        public static string GenerateEnvironmentInfo(ESVMCPMemoryEnhanced sceneMemory = null)
        {
            var sb = new StringBuilder();
            
            // AI专用提示
            sb.AppendLine("=== ESVMCP 环境识别 - AI专用信息 ===");
            sb.AppendLine();
            sb.AppendLine("这是为AI提供的Unity项目环境识别信息。");
            sb.AppendLine("请基于这些环境数据准备开始编写ESVMCP指令，不要多想，等待我告知你你要做的事情。");
            sb.AppendLine("这些信息包括场景状态、记忆数据、对象详情等，用于指导指令编写。");
            sb.AppendLine();
            sb.AppendLine("--- 环境数据开始 ---");
            sb.AppendLine();

            // 1. 场景基本信息
            AppendSceneBasicInfo(sb);

            // 2. 分析场景复杂度
            var complexity = AnalyzeSceneComplexity(out int totalObjects);
            sb.AppendLine($"[场景复杂度] {complexity} ({totalObjects} 个对象)");
            sb.AppendLine();

            // 3. 场景统计信息
            AppendSceneStatistics(sb);

            // 4. 记忆系统状态
            AppendMemoryInfo(sb, sceneMemory);

            // 5. 场景对象信息（根据复杂度动态调整详细程度）
            AppendSceneObjects(sb, complexity, sceneMemory);

            // 6. 可用命令提示
            AppendCommandHints(sb, complexity);

            sb.AppendLine();
            sb.AppendLine("--- 环境数据结束 ---");
            sb.AppendLine();
            sb.AppendLine("AI提示：请基于以上环境信息，直接准备开始生成ESVMCP JSON指令序列，依据我接下来的要求。不要添加多余解释。");
            sb.AppendLine("注意：你需要生成规范Json放置在"+ESVMCPConfig.Instance.InputFolder+"文件夹中，等待我执行。");
            

            return sb.ToString();
        }

        /// <summary>
        /// 分析场景复杂度
        /// </summary>
        private static SceneComplexity AnalyzeSceneComplexity(out int totalObjects)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var filteredRoots = System.Array.FindAll(rootObjects, obj => obj.tag != "EditorOnly");
            
            totalObjects = CountAllGameObjects(filteredRoots);

            if (totalObjects < 30) return SceneComplexity.Simple;
            if (totalObjects < 100) return SceneComplexity.Medium;
            if (totalObjects < 500) return SceneComplexity.Complex;
            return SceneComplexity.VeryComplex;
        }

        /// <summary>
        /// 添加场景基本信息
        /// </summary>
        private static void AppendSceneBasicInfo(StringBuilder sb)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            sb.AppendLine("[场景基础信息]");
            sb.AppendLine($"场景名称: {scene.name}");
            sb.AppendLine($"场景路径: {scene.path}");
            sb.AppendLine($"已加载: {(scene.isLoaded ? "是" : "否")}");
            sb.AppendLine($"构建索引: {scene.buildIndex}");
            sb.AppendLine($"项目: {UnityEngine.Application.productName}");
            sb.AppendLine($"Unity版本: {UnityEngine.Application.unityVersion}");
            sb.AppendLine();
        }

        /// <summary>
        /// 添加场景统计信息
        /// </summary>
        private static void AppendSceneStatistics(StringBuilder sb)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var allObjects = scene.GetRootGameObjects();
            var filteredObjects = System.Array.FindAll(allObjects, obj => obj.tag != "EditorOnly");

            // 统计活跃/非活跃对象
            int activeCount = 0;
            int inactiveCount = 0;
            
            // 统计组件类型
            var componentCounts = new System.Collections.Generic.Dictionary<string, int>();
            
            // 统计层级深度
            int maxDepth = 0;
            
            // 统计特殊对象
            int camerasCount = 0;
            int lightsCount = 0;
            int renderersCount = 0;
            int physicsCount = 0;
            int canvasCount = 0;

            foreach (var root in filteredObjects)
            {
                CollectStatistics(root, 0, ref activeCount, ref inactiveCount, 
                                ref maxDepth, ref camerasCount, ref lightsCount, 
                                ref renderersCount, ref physicsCount, ref canvasCount,
                                componentCounts);
            }

            sb.AppendLine("[场景统计]");
            sb.AppendLine($"总对象数: {activeCount + inactiveCount}");
            sb.AppendLine($"  - 活跃: {activeCount}");
            sb.AppendLine($"  - 未激活: {inactiveCount}");
            sb.AppendLine($"最大层级深度: {maxDepth}");
            sb.AppendLine();
            
            sb.AppendLine("[关键组件统计]");
            if (camerasCount > 0) sb.AppendLine($"相机: {camerasCount}");
            if (lightsCount > 0) sb.AppendLine($"光源: {lightsCount}");
            if (renderersCount > 0) sb.AppendLine($"渲染器: {renderersCount}");
            if (physicsCount > 0) sb.AppendLine($"物理组件: {physicsCount}");
            if (canvasCount > 0) sb.AppendLine($"UI Canvas: {canvasCount}");
            
            if (componentCounts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("[自定义脚本分布] (前10个)");
                var topScripts = componentCounts.OrderByDescending(kvp => kvp.Value).Take(10);
                foreach (var kvp in topScripts)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            sb.AppendLine();
        }

        /// <summary>
        /// 收集统计信息
        /// </summary>
        private static void CollectStatistics(GameObject go, int depth, 
                                             ref int activeCount, ref int inactiveCount,
                                             ref int maxDepth, ref int camerasCount, 
                                             ref int lightsCount, ref int renderersCount,
                                             ref int physicsCount, ref int canvasCount,
                                             System.Collections.Generic.Dictionary<string, int> componentCounts)
        {
            if (go.activeSelf) activeCount++;
            else inactiveCount++;

            if (depth > maxDepth) maxDepth = depth;

            // 统计组件
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                var type = comp.GetType();
                
                // 统计特殊组件
                if (type.Name.Contains("Camera")) camerasCount++;
                else if (type.Name.Contains("Light")) lightsCount++;
                else if (type.Name.Contains("Renderer")) renderersCount++;
                else if (type.Name.Contains("Rigidbody") || type.Name.Contains("Collider")) physicsCount++;
                else if (type.Name == "Canvas") canvasCount++;
                
                // 统计自定义脚本
                if (type.Namespace != null && 
                    !type.Namespace.StartsWith("UnityEngine") && 
                    !type.Namespace.StartsWith("UnityEditor"))
                {
                    if (componentCounts.ContainsKey(type.Name))
                        componentCounts[type.Name]++;
                    else
                        componentCounts[type.Name] = 1;
                }
            }

            // 递归子对象
            foreach (Transform child in go.transform)
            {
                CollectStatistics(child.gameObject, depth + 1, 
                                ref activeCount, ref inactiveCount,
                                ref maxDepth, ref camerasCount, 
                                ref lightsCount, ref renderersCount,
                                ref physicsCount, ref canvasCount,
                                componentCounts);
            }
        }

        /// <summary>
        /// 添加记忆信息
        /// </summary>
        private static void AppendMemoryInfo(StringBuilder sb, ESVMCPMemoryEnhanced sceneMemory)
        {
            sb.AppendLine("[记忆系统]");
            
            // 场景记忆（短期）
            if (sceneMemory != null)
            {
                sb.AppendLine($"短期记忆: {sceneMemory.TotalMemoryItems} 项");
                if (sceneMemory.TotalMemoryItems > 0)
                {
                var allKeys = sceneMemory.GetAllKeys();
                foreach (var key in allKeys)
                {
                    var item = sceneMemory.GetMemoryItem(key);
                    if (item != null)
                    {
                        sb.AppendLine($"  {key}: {item.Resolve()}");
                    }
                }
            }
            
            // GameObject引用
            if (sceneMemory.GameObjectMemory > 0)
            {
                sb.AppendLine($"对象引用: {sceneMemory.GameObjectMemory} 个");
                var goKeys = sceneMemory.GetKeysByType(ESVMCPMemoryItemType.GameObject);
                foreach (var goKey in goKeys)
                {
                    var go = sceneMemory.GetGameObject(goKey);
                    if (go != null)
                    {
                        sb.AppendLine($"  {goKey}: {go.name}");
                    }
                }
            }
        }
        else
        {
            sb.AppendLine("短期记忆: 未初始化");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// 添加场景对象信息
    /// </summary>
    private static void AppendSceneObjects(StringBuilder sb, SceneComplexity complexity, ESVMCPMemoryEnhanced sceneMemory)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootObjects = scene.GetRootGameObjects();
        var filteredRoots = System.Array.FindAll(rootObjects, obj => obj.tag != "EditorOnly");

        sb.AppendLine($"[场景对象] {filteredRoots.Length} 个根对象");

            // 根据复杂度决定输出详细程度
            switch (complexity)
            {
                case SceneComplexity.Simple:
                    // 简单场景：输出所有详细信息
                    AppendDetailedObjects(sb, filteredRoots, sceneMemory, true);
                    break;

                case SceneComplexity.Medium:
                    // 中等场景：输出主要对象和在记忆中的对象
                    AppendDetailedObjects(sb, filteredRoots, sceneMemory, false);
                    break;

                case SceneComplexity.Complex:
                case SceneComplexity.VeryComplex:
                    // 复杂场景：只输出概要和关键对象
                    AppendSummaryObjects(sb, filteredRoots, sceneMemory);
                    break;
            }
        }

        /// <summary>
        /// 输出详细对象信息
        /// </summary>
        private static void AppendDetailedObjects(StringBuilder sb, GameObject[] roots, ESVMCPMemoryEnhanced sceneMemory, bool includeAll)
        {
            var memoryKeys = sceneMemory != null ? new HashSet<string>(sceneMemory.GetKeysByType(ESVMCPMemoryItemType.GameObject)) : new HashSet<string>();

            foreach (var root in roots)
            {
                AppendGameObjectInfo(sb, root, 0, memoryKeys, includeAll);
            }
        }

        /// <summary>
        /// 输出对象概要信息
        /// </summary>
        private static void AppendSummaryObjects(StringBuilder sb, GameObject[] roots, ESVMCPMemoryEnhanced sceneMemory)
        {
            var memoryKeys = sceneMemory != null ? new HashSet<string>(sceneMemory.GetKeysByType(ESVMCPMemoryItemType.GameObject)) : new HashSet<string>();

            // 只输出根对象和在记忆中的对象
            foreach (var root in roots)
            {
                sb.AppendLine($"  {root.name} ({CountChildren(root)} 子对象)");
                
                // 输出记忆中的子对象
                AppendMemorizedChildObjects(sb, root, new HashSet<string>(memoryKeys), 2);
            }
        }

        /// <summary>
        /// 输出GameObject详细信息
        /// </summary>
        private static void AppendGameObjectInfo(StringBuilder sb, GameObject go, int indent, HashSet<string> memoryKeys, bool includeAll)
        {
            string indentStr = new string(' ', indent * 2);
            bool isInMemory = IsInMemory(go, memoryKeys);

            // 基本信息
            sb.Append($"{indentStr}[{go.name}]");
            
            // Tag和Layer
            if (go.tag != "Untagged")
            {
                sb.Append($" tag:{go.tag}");
            }
            sb.Append($" layer:{LayerMask.LayerToName(go.layer)}");
            
            // 激活状态
            if (!go.activeSelf)
            {
                sb.Append(" [未激活]");
            }
            
            // 记忆标记
            if (isInMemory)
            {
                sb.Append(" [已记忆]");
            }

            sb.AppendLine();

            // Transform信息（中等场景显示）
            if (includeAll || isInMemory)
            {
                var t = go.transform;
                sb.AppendLine($"{indentStr}  位置: ({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})");
                sb.AppendLine($"{indentStr}  旋转: ({t.eulerAngles.x:F2}, {t.eulerAngles.y:F2}, {t.eulerAngles.z:F2})");
                sb.AppendLine($"{indentStr}  缩放: ({t.localScale.x:F2}, {t.localScale.y:F2}, {t.localScale.z:F2})");
            }

            // 组件信息
            var components = go.GetComponents<Component>();
            
            // Unity内置组件
            var unityComponents = components.Where(c => c != null && 
                                                       c.GetType().Namespace != null &&
                                                       c.GetType().Namespace.StartsWith("UnityEngine") &&
                                                       c.GetType().Name != "Transform").ToList();
            if (unityComponents.Count > 0)
            {
                sb.AppendLine($"{indentStr}  组件: {string.Join(", ", unityComponents.Select(c => c.GetType().Name))}");
            }
            
            // 自定义脚本组件
            var scriptComponents = components.Where(c => c != null && 
                                                       c.GetType().Namespace != null &&
                                                       !c.GetType().Namespace.StartsWith("UnityEngine") &&
                                                       !c.GetType().Namespace.StartsWith("UnityEditor")).ToList();
            if (scriptComponents.Count > 0)
            {
                sb.AppendLine($"{indentStr}  脚本: {string.Join(", ", scriptComponents.Select(c => c.GetType().Name))}");
            }

            // 递归子对象（只在includeAll或对象在记忆中时递归）
            if (includeAll || isInMemory)
            {
                foreach (Transform child in go.transform)
                {
                    AppendGameObjectInfo(sb, child.gameObject, indent + 1, memoryKeys, includeAll);
                }
            }
        }

        /// <summary>
        /// 输出记忆中的子对象
        /// </summary>
        private static void AppendMemorizedChildObjects(StringBuilder sb, GameObject parent, HashSet<string> memoryKeys, int indent)
        {
            string indentStr = new string(' ', indent * 2);
            
            foreach (Transform child in parent.transform)
            {
                if (IsInMemory(child.gameObject, memoryKeys))
                {
                    sb.AppendLine($"{indentStr}{child.name} [已记忆]");
                }
                
                // 递归检查
                AppendMemorizedChildObjects(sb, child.gameObject, memoryKeys, indent);
            }
        }

        /// <summary>
        /// 检查对象是否在记忆中
        /// </summary>
        private static bool IsInMemory(GameObject go, HashSet<string> memoryKeys)
        {
            return memoryKeys.Contains(go.name) || 
                   memoryKeys.Contains(go.GetInstanceID().ToString());
        }

        /// <summary>
        /// 统计所有GameObject数量
        /// </summary>
        private static int CountAllGameObjects(GameObject[] roots)
        {
            int count = 0;
            foreach (var root in roots)
            {
                count += 1 + CountChildren(root);
            }
            return count;
        }

        /// <summary>
        /// 统计子对象数量
        /// </summary>
        private static int CountChildren(GameObject obj)
        {
            int count = 0;
            foreach (Transform child in obj.transform)
            {
                count += 1 + CountChildren(child.gameObject);
            }
            return count;
        }

        /// <summary>
        /// 添加命令提示信息
        /// </summary>
        private static void AppendCommandHints(StringBuilder sb, SceneComplexity complexity)
        {
            sb.AppendLine();
            sb.AppendLine("[可用命令提示]");
            sb.AppendLine("根据当前场景状态，以下是一些常用命令：");
            sb.AppendLine();
            
            // 基础命令
            sb.AppendLine("=== 基础操作 ===");
            sb.AppendLine("• CreateGameObject - 创建新对象");
            sb.AppendLine("• DestroyGameObject - 删除对象");
            sb.AppendLine("• FindGameObject - 查找对象");
            sb.AppendLine("• SetActiveState - 设置对象激活状态");
            sb.AppendLine();
            
            // 记忆系统
            sb.AppendLine("=== 记忆系统 ===");
            sb.AppendLine("• SaveMemory - 保存数据到记忆");
            sb.AppendLine("• LoadMemory - 从记忆加载数据");
            sb.AppendLine("• ClearMemory - 清除记忆数据");
            sb.AppendLine("• RememberGameObject - 记住某个对象");
            sb.AppendLine();
            
            // 高级命令（根据复杂度显示）
            if (complexity == SceneComplexity.Simple || complexity == SceneComplexity.Medium)
            {
                sb.AppendLine("=== 高级操作 ===");
                sb.AppendLine("• SetProperty - 动态设置对象属性");
                sb.AppendLine("• BatchOperation - 批量操作多个对象");
                sb.AppendLine("• ConditionalExecute - 条件执行命令");
                sb.AppendLine();
            }
            
            // 场景管理
            sb.AppendLine("=== 场景管理 ===");
            sb.AppendLine("• SaveScene - 保存当前场景");
            sb.AppendLine("• LoadScene - 加载场景");
            sb.AppendLine();
            
            sb.AppendLine("提示: 使用 'help <命令名>' 获取详细帮助信息");
        }
    }
}
