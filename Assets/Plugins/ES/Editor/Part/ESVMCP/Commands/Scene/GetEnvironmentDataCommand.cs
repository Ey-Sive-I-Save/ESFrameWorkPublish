using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ES.VMCP
{
    /// <summary>
    /// 环境数据获取命令 - 获取场景中的完整环境信息
    /// 支持5个详细等级，从简短到颗粒级
    /// </summary>
    [ESVMCPCommand("GetEnvironmentData", "获取环境数据，支持多个详细等级（简短/常规/完整/极限/颗粒级）")]
    public class GetEnvironmentDataCommand : ESVMCPCommandBase
    {
        [JsonProperty("detailLevel")]
        public EnvironmentDetailLevel? DetailLevel { get; set; }

        [JsonProperty("sceneName")]
        public string SceneName { get; set; }

        [JsonProperty("includeInactive")]
        public bool? IncludeInactive { get; set; }

        [JsonProperty("maxDepth")]
        public int? MaxDepth { get; set; }

        [JsonProperty("filterByTag")]
        public string FilterByTag { get; set; }

        [JsonProperty("filterByLayer")]
        public int? FilterByLayer { get; set; }

        public override string Description => $"获取环境数据 (等级: {DetailLevel ?? ESVMCPConfig.Instance.DefaultDetailLevel})";

        public override ESVMCPValidationResult Validate()
        {
            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                var config = ESVMCPConfig.Instance;
                var level = DetailLevel ?? config.DefaultDetailLevel;
                var includeInactive = IncludeInactive ?? config.IncludeInactiveObjects;
                var maxDepth = MaxDepth ?? config.MaxHierarchyDepth;

                // 获取目标场景
                Scene scene = string.IsNullOrEmpty(SceneName) 
                    ? SceneManager.GetActiveScene() 
                    : SceneManager.GetSceneByName(SceneName);

                if (!scene.isLoaded)
                {
                    return ESVMCPCommandResult.Failed($"场景未加载: {SceneName ?? "Active Scene"}");
                }

                // 生成环境数据
                var envData = GenerateEnvironmentData(scene, level, includeInactive, maxDepth);

                return ESVMCPCommandResult.Succeed("成功获取环境数据", envData);
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"获取环境数据失败: {e.Message}", e);
            }
        }

        private Dictionary<string, object> GenerateEnvironmentData(
            Scene scene, 
            EnvironmentDetailLevel level, 
            bool includeInactive, 
            int maxDepth)
        {
            var data = new Dictionary<string, object>();
            int targetChars = (int)level;

            // 基础场景信息（所有等级都包含）
            data["sceneName"] = scene.name;
            data["scenePath"] = scene.path;
            data["isLoaded"] = scene.isLoaded;
            data["buildIndex"] = scene.buildIndex;
            data["detailLevel"] = level.ToString();
            data["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 获取根对象
            var rootObjects = scene.GetRootGameObjects();
            var allObjects = GetAllGameObjects(rootObjects, includeInactive, maxDepth);

            // 应用过滤
            if (!string.IsNullOrEmpty(FilterByTag))
            {
                allObjects = allObjects.Where(go => go.CompareTag(FilterByTag)).ToList();
            }
            if (FilterByLayer.HasValue)
            {
                allObjects = allObjects.Where(go => go.layer == FilterByLayer.Value).ToList();
            }

            data["totalObjectCount"] = allObjects.Count;
            data["rootObjectCount"] = rootObjects.Length;
            data["activeObjectCount"] = allObjects.Count(go => go.activeInHierarchy);
            data["inactiveObjectCount"] = allObjects.Count(go => !go.activeInHierarchy);

            // 根据等级生成不同详细程度的数据
            switch (level)
            {
                case EnvironmentDetailLevel.Brief:
                    AddBriefData(data, scene, allObjects, targetChars);
                    break;
                case EnvironmentDetailLevel.Normal:
                    AddNormalData(data, scene, allObjects, targetChars);
                    break;
                case EnvironmentDetailLevel.Complete:
                    AddCompleteData(data, scene, allObjects, targetChars);
                    break;
#if UNITY_EDITOR
                case EnvironmentDetailLevel.Extreme:
                    AddExtremeData(data, scene, allObjects, targetChars);
                    break;
                case EnvironmentDetailLevel.Granular:
                    AddGranularData(data, scene, allObjects, maxDepth);
                    break;
#endif
            }

            return data;
        }

        #region 数据生成方法

        /// <summary>
        /// 简短模式 - 约1000字符，只包含关键摘要
        /// </summary>
        private void AddBriefData(Dictionary<string, object> data, Scene scene, List<GameObject> allObjects, int targetChars)
        {
            // 对象类型统计
            var componentStats = new Dictionary<string, int>();
            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    if (!componentStats.ContainsKey(typeName))
                        componentStats[typeName] = 0;
                    componentStats[typeName]++;
                }
            }

            data["componentStatistics"] = componentStats.OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Tag统计
            var tagStats = allObjects.GroupBy(go => go.tag)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count());
            data["tagStatistics"] = tagStats;

            // Layer统计
            var layerStats = allObjects.GroupBy(go => LayerMask.LayerToName(go.layer))
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count());
            data["layerStatistics"] = layerStats;

            // 关键对象列表（仅名称）
            data["keyObjects"] = allObjects
                .Where(go => go.GetComponent<Camera>() != null || 
                             go.GetComponent<Light>() != null ||
                             go.GetComponent<Rigidbody>() != null)
                .Take(20)
                .Select(go => new Dictionary<string, object>
                {
                    ["name"] = go.name,
                    ["type"] = GetObjectType(go)
                })
                .ToList();

            // 简短描述
            data["summary"] = GenerateBriefSummary(allObjects, componentStats);
        }

        /// <summary>
        /// 常规模式 - 约4000字符，包含主要对象和组件信息
        /// </summary>
        private void AddNormalData(Dictionary<string, object> data, Scene scene, List<GameObject> allObjects, int targetChars)
        {
            AddBriefData(data, scene, allObjects, targetChars);

            // 详细对象列表
            var objectList = new List<Dictionary<string, object>>();
            int charCount = 0;
            
            foreach (var go in allObjects.Take(100))
            {
                var objInfo = new Dictionary<string, object>
                {
                    ["name"] = go.name,
                    ["path"] = GetGameObjectPath(go),
                    ["active"] = go.activeInHierarchy,
                    ["tag"] = go.tag,
                    ["layer"] = LayerMask.LayerToName(go.layer),
                    ["position"] = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    ["components"] = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToList()
                };

                objectList.Add(objInfo);
                charCount += JsonConvert.SerializeObject(objInfo).Length;
                
                if (charCount > targetChars * 0.7f) break;
            }

            data["objects"] = objectList;

            // 材质信息
            var materials = new HashSet<Material>();
            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null) materials.Add(mat);
                    }
                }
            }
            data["materialCount"] = materials.Count;
            data["materials"] = materials.Take(20).Select(m => new Dictionary<string, object>
            {
                ["name"] = m.name,
                ["shader"] = m.shader != null ? m.shader.name : "None"
            }).ToList();

            // 光照信息
            var lights = allObjects
                .Select(go => go.GetComponent<Light>())
                .Where(l => l != null)
                .ToList();
            data["lightCount"] = lights.Count;
            data["lights"] = lights.Take(10).Select(l => new Dictionary<string, object>
            {
                ["name"] = l.gameObject.name,
                ["type"] = l.type.ToString(),
                ["intensity"] = l.intensity,
                ["color"] = new { r = l.color.r, g = l.color.g, b = l.color.b }
            }).ToList();

            // 相机信息
            var cameras = allObjects
                .Select(go => go.GetComponent<Camera>())
                .Where(c => c != null)
                .ToList();
            data["cameraCount"] = cameras.Count;
            data["cameras"] = cameras.Select(c => new Dictionary<string, object>
            {
                ["name"] = c.gameObject.name,
                ["clearFlags"] = c.clearFlags.ToString(),
                ["cullingMask"] = c.cullingMask,
                ["fieldOfView"] = c.fieldOfView
            }).ToList();
        }

        /// <summary>
        /// 完整模式 - 约8000字符，包含大部分细节
        /// </summary>
        private void AddCompleteData(Dictionary<string, object> data, Scene scene, List<GameObject> allObjects, int targetChars)
        {
            AddNormalData(data, scene, allObjects, targetChars);

            // 物理信息
            var rigidbodies = allObjects
                .Select(go => go.GetComponent<Rigidbody>())
                .Where(rb => rb != null)
                .ToList();
            data["rigidbodyCount"] = rigidbodies.Count;
            data["rigidbodies"] = rigidbodies.Take(50).Select(rb => new Dictionary<string, object>
            {
                ["name"] = rb.gameObject.name,
                ["mass"] = rb.mass,
                ["useGravity"] = rb.useGravity,
                ["isKinematic"] = rb.isKinematic,
                ["drag"] = rb.drag
            }).ToList();

            // 碰撞器信息
            var colliders = allObjects
                .SelectMany(go => go.GetComponents<Collider>())
                .Where(c => c != null)
                .ToList();
            data["colliderCount"] = colliders.Count;
            data["colliders"] = colliders.Take(50).Select(c => new Dictionary<string, object>
            {
                ["name"] = c.gameObject.name,
                ["type"] = c.GetType().Name,
                ["isTrigger"] = c.isTrigger,
                ["enabled"] = c.enabled
            }).ToList();

            // 音频源信息
            var audioSources = allObjects
                .Select(go => go.GetComponent<AudioSource>())
                .Where(a => a != null)
                .ToList();
            data["audioSourceCount"] = audioSources.Count;
            data["audioSources"] = audioSources.Take(20).Select(a => new Dictionary<string, object>
            {
                ["name"] = a.gameObject.name,
                ["clip"] = a.clip != null ? a.clip.name : "None",
                ["volume"] = a.volume,
                ["loop"] = a.loop,
                ["playOnAwake"] = a.playOnAwake
            }).ToList();

            // 动画器信息
            var animators = allObjects
                .Select(go => go.GetComponent<Animator>())
                .Where(a => a != null)
                .ToList();
            data["animatorCount"] = animators.Count;
            data["animators"] = animators.Take(20).Select(a => new Dictionary<string, object>
            {
                ["name"] = a.gameObject.name,
                ["enabled"] = a.enabled,
                ["controller"] = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "None"
            }).ToList();

            // 粒子系统信息
            var particleSystems = allObjects
                .Select(go => go.GetComponent<ParticleSystem>())
                .Where(ps => ps != null)
                .ToList();
            data["particleSystemCount"] = particleSystems.Count;
            data["particleSystems"] = particleSystems.Take(20).Select(ps => new Dictionary<string, object>
            {
                ["name"] = ps.gameObject.name,
                ["isPlaying"] = ps.isPlaying,
                ["maxParticles"] = ps.main.maxParticles,
                ["duration"] = ps.main.duration
            }).ToList();

            // 层级结构（前50个根对象）
            data["hierarchyStructure"] = scene.GetRootGameObjects()
                .Take(50)
                .Select(root => BuildHierarchyTree(root, 0, 4))
                .ToList();
        }

        /// <summary>
        /// 极限模式 - 约20000字符，包含几乎所有信息
        /// </summary>
#if UNITY_EDITOR
        private void AddExtremeData(Dictionary<string, object> data, Scene scene, List<GameObject> allObjects, int targetChars)
        {
            AddCompleteData(data, scene, allObjects, targetChars);

            // 详细的组件属性信息
            var detailedComponents = new List<Dictionary<string, object>>();
            int charCount = 0;

            foreach (var go in allObjects.Take(200))
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    var compInfo = new Dictionary<string, object>
                    {
                        ["object"] = go.name,
                        ["componentType"] = comp.GetType().Name,
                        ["enabled"] = comp is Behaviour behaviour ? behaviour.enabled : true,
                        ["properties"] = GetComponentProperties(comp)
                    };

                    detailedComponents.Add(compInfo);
                    charCount += JsonConvert.SerializeObject(compInfo).Length;

                    if (charCount > targetChars * 0.5f) break;
                }
                if (charCount > targetChars * 0.5f) break;
            }

            data["detailedComponents"] = detailedComponents;

            // 纹理信息
            var textures = new HashSet<Texture>();
            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) continue;
                        var shader = mat.shader;
                        if (shader == null) continue;

                        for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                        {
                            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                            {
                                string propName = ShaderUtil.GetPropertyName(shader, i);
                                Texture tex = mat.GetTexture(propName);
                                if (tex != null) textures.Add(tex);
                            }
                        }
                    }
                }
            }

            data["textureCount"] = textures.Count;
            data["textures"] = textures.Take(50).Select(t => new Dictionary<string, object>
            {
                ["name"] = t.name,
                ["width"] = t.width,
                ["height"] = t.height,
                ["filterMode"] = t.filterMode.ToString(),
                ["wrapMode"] = t.wrapMode.ToString()
            }).ToList();

            // Mesh信息
            var meshes = new HashSet<Mesh>();
            foreach (var go in allObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    meshes.Add(meshFilter.sharedMesh);
                }
            }

            data["meshCount"] = meshes.Count;
            data["meshes"] = meshes.Take(50).Select(m => new Dictionary<string, object>
            {
                ["name"] = m.name,
                ["vertexCount"] = m.vertexCount,
                ["triangleCount"] = m.triangles.Length / 3,
                ["subMeshCount"] = m.subMeshCount
            }).ToList();

            // 脚本组件统计
            var scriptComponents = new Dictionary<string, List<string>>();
            foreach (var go in allObjects.Take(200))
            {
                var monoBehaviours = go.GetComponents<MonoBehaviour>();
                foreach (var mb in monoBehaviours)
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (!scriptComponents.ContainsKey(typeName))
                    {
                        scriptComponents[typeName] = new List<string>();
                    }
                    scriptComponents[typeName].Add(go.name);
                }
            }

            data["scriptComponents"] = scriptComponents
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(30)
                .ToDictionary(kvp => kvp.Key, kvp => new Dictionary<string, object>
                {
                    ["count"] = kvp.Value.Count,
                    ["instances"] = kvp.Value.Take(10).ToList()
                });
        }
#endif

        /// <summary>
        /// 颗粒级模式 - 无字符限制，完整描述所有内容
        /// </summary>
#if UNITY_EDITOR
        private void AddGranularData(Dictionary<string, object> data, Scene scene, List<GameObject> allObjects, int maxDepth)
        {
            // 完整对象列表（无限制）
            var fullObjectList = new List<Dictionary<string, object>>();
            
            foreach (var go in allObjects)
            {
                fullObjectList.Add(BuildCompleteObjectInfo(go));
            }

            data["completeObjectList"] = fullObjectList;

            // 完整层级结构
            data["completeHierarchy"] = scene.GetRootGameObjects()
                .Select(root => BuildHierarchyTree(root, 0, maxDepth))
                .ToList();

            // 所有材质的完整信息
            var allMaterials = new List<Dictionary<string, object>>();
            var processedMaterials = new HashSet<Material>();

            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && !processedMaterials.Contains(mat))
                        {
                            processedMaterials.Add(mat);
                            allMaterials.Add(BuildCompleteMaterialInfo(mat));
                        }
                    }
                }
            }

            data["completeMaterials"] = allMaterials;

            // 所有组件的完整信息
            var allComponents = new Dictionary<string, List<Dictionary<string, object>>>();

            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().FullName;
                    
                    if (!allComponents.ContainsKey(typeName))
                    {
                        allComponents[typeName] = new List<Dictionary<string, object>>();
                    }

                    allComponents[typeName].Add(new Dictionary<string, object>
                    {
                        ["object"] = go.name,
                        ["objectPath"] = GetGameObjectPath(go),
                        ["instanceID"] = comp.GetInstanceID(),
                        ["enabled"] = comp is Behaviour behaviour ? behaviour.enabled : true,
                        ["properties"] = GetComponentProperties(comp)
                    });
                }
            }

            data["completeComponents"] = allComponents;

            // 场景设置
            data["sceneSettings"] = new Dictionary<string, object>
            {
                ["ambientMode"] = RenderSettings.ambientMode.ToString(),
                ["ambientLight"] = new { r = RenderSettings.ambientLight.r, g = RenderSettings.ambientLight.g, b = RenderSettings.ambientLight.b },
                ["ambientIntensity"] = RenderSettings.ambientIntensity,
                ["skybox"] = RenderSettings.skybox != null ? RenderSettings.skybox.name : "None",
                ["fog"] = RenderSettings.fog,
                ["fogColor"] = new { r = RenderSettings.fogColor.r, g = RenderSettings.fogColor.g, b = RenderSettings.fogColor.b },
                ["fogMode"] = RenderSettings.fogMode.ToString(),
                ["fogDensity"] = RenderSettings.fogDensity
            };

            // 物理设置
            data["physicsSettings"] = new Dictionary<string, object>
            {
                ["gravity"] = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                ["defaultSolverIterations"] = Physics.defaultSolverIterations,
                ["defaultSolverVelocityIterations"] = Physics.defaultSolverVelocityIterations,
                ["bounceThreshold"] = Physics.bounceThreshold,
                ["sleepThreshold"] = Physics.sleepThreshold
            };

            // 质量中心分析
            var centerOfMass = CalculateCenterOfMass(allObjects);
            data["sceneCenterOfMass"] = new { x = centerOfMass.x, y = centerOfMass.y, z = centerOfMass.z };

            // 边界分析
            var bounds = CalculateSceneBounds(allObjects);
            data["sceneBounds"] = new Dictionary<string, object>
            {
                ["center"] = new { x = bounds.center.x, y = bounds.center.y, z = bounds.center.z },
                ["size"] = new { x = bounds.size.x, y = bounds.size.y, z = bounds.size.z },
                ["min"] = new { x = bounds.min.x, y = bounds.min.y, z = bounds.min.z },
                ["max"] = new { x = bounds.max.x, y = bounds.max.y, z = bounds.max.z }
            };

            // 性能统计
            data["performanceMetrics"] = new Dictionary<string, object>
            {
                ["totalVertexCount"] = CalculateTotalVertices(allObjects),
                ["totalTriangleCount"] = CalculateTotalTriangles(allObjects),
                ["drawCallEstimate"] = EstimateDrawCalls(allObjects),
                ["memoryEstimateMB"] = EstimateMemoryUsage(allObjects)
            };
        }
#endif

        #endregion

        #region 辅助方法

        private List<GameObject> GetAllGameObjects(GameObject[] roots, bool includeInactive, int maxDepth)
        {
            var result = new List<GameObject>();
            foreach (var root in roots)
            {
                if (!includeInactive && !root.activeInHierarchy) continue;
                CollectGameObjects(root, result, 0, maxDepth, includeInactive);
            }
            return result;
        }

        private void CollectGameObjects(GameObject go, List<GameObject> result, int currentDepth, int maxDepth, bool includeInactive)
        {
            if (!includeInactive && !go.activeInHierarchy) return;
            
            result.Add(go);

            if (currentDepth >= maxDepth) return;

            foreach (Transform child in go.transform)
            {
                CollectGameObjects(child.gameObject, result, currentDepth + 1, maxDepth, includeInactive);
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private string GetObjectType(GameObject go)
        {
            if (go.GetComponent<Camera>()) return "Camera";
            if (go.GetComponent<Light>()) return "Light";
            if (go.GetComponent<Rigidbody>()) return "Physics";
            if (go.GetComponent<Renderer>()) return "Renderer";
            if (go.GetComponent<AudioSource>()) return "Audio";
            if (go.GetComponent<Animator>()) return "Animated";
            if (go.GetComponent<ParticleSystem>()) return "Particles";
            return "GameObject";
        }

        private string GenerateBriefSummary(List<GameObject> allObjects, Dictionary<string, int> componentStats)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"场景包含 {allObjects.Count} 个对象。");
            
            var topComponents = componentStats.OrderByDescending(kvp => kvp.Value).Take(3);
            sb.Append("主要组件: ");
            sb.AppendLine(string.Join(", ", topComponents.Select(kvp => $"{kvp.Key}({kvp.Value})")));

            int cameraCount = allObjects.Count(go => go.GetComponent<Camera>() != null);
            int lightCount = allObjects.Count(go => go.GetComponent<Light>() != null);
            
            if (cameraCount > 0) sb.AppendLine($"相机: {cameraCount}个");
            if (lightCount > 0) sb.AppendLine($"光源: {lightCount}个");

            return sb.ToString();
        }

        private Dictionary<string, object> BuildHierarchyTree(GameObject go, int currentDepth, int maxDepth)
        {
            var node = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["active"] = go.activeInHierarchy,
                ["childCount"] = go.transform.childCount
            };

            if (currentDepth < maxDepth && go.transform.childCount > 0)
            {
                var children = new List<Dictionary<string, object>>();
                foreach (Transform child in go.transform)
                {
                    children.Add(BuildHierarchyTree(child.gameObject, currentDepth + 1, maxDepth));
                }
                node["children"] = children;
            }

            return node;
        }

        private Dictionary<string, object> BuildCompleteObjectInfo(GameObject go)
        {
            var info = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["instanceID"] = go.GetInstanceID(),
                ["active"] = go.activeInHierarchy,
                ["activeSelf"] = go.activeSelf,
                ["isStatic"] = go.isStatic,
                ["tag"] = go.tag,
                ["layer"] = go.layer,
                ["layerName"] = LayerMask.LayerToName(go.layer)
            };

            // Transform信息
            var transform = go.transform;
            info["transform"] = new Dictionary<string, object>
            {
                ["localPosition"] = new { x = transform.localPosition.x, y = transform.localPosition.y, z = transform.localPosition.z },
                ["position"] = new { x = transform.position.x, y = transform.position.y, z = transform.position.z },
                ["localRotation"] = new { x = transform.localRotation.x, y = transform.localRotation.y, z = transform.localRotation.z, w = transform.localRotation.w },
                ["rotation"] = new { x = transform.rotation.x, y = transform.rotation.y, z = transform.rotation.z, w = transform.rotation.w },
                ["localScale"] = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z },
                ["childCount"] = transform.childCount,
                ["parent"] = transform.parent != null ? transform.parent.name : null
            };

            // 所有组件
            var components = go.GetComponents<Component>();
            info["components"] = components
                .Where(c => c != null)
                .Select(c => new Dictionary<string, object>
                {
                    ["type"] = c.GetType().Name,
                    ["fullType"] = c.GetType().FullName,
                    ["enabled"] = c is Behaviour behaviour ? behaviour.enabled : true
                })
                .ToList();

            return info;
        }

        private Dictionary<string, object> BuildCompleteMaterialInfo(Material mat)
        {
#if UNITY_EDITOR
            var info = new Dictionary<string, object>
            {
                ["name"] = mat.name,
                ["shader"] = mat.shader != null ? mat.shader.name : "None",
                ["renderQueue"] = mat.renderQueue
            };

            if (mat.shader != null)
            {
                var properties = new Dictionary<string, object>();
                int propCount = ShaderUtil.GetPropertyCount(mat.shader);

                for (int i = 0; i < propCount; i++)
                {
                    string propName = ShaderUtil.GetPropertyName(mat.shader, i);
                    var propType = ShaderUtil.GetPropertyType(mat.shader, i);

                    try
                    {
                        switch (propType)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                var color = mat.GetColor(propName);
                                properties[propName] = new { r = color.r, g = color.g, b = color.b, a = color.a };
                                break;
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                properties[propName] = mat.GetFloat(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.Vector:
                                var vec = mat.GetVector(propName);
                                properties[propName] = new { x = vec.x, y = vec.y, z = vec.z, w = vec.w };
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                var tex = mat.GetTexture(propName);
                                properties[propName] = tex != null ? tex.name : "None";
                                break;
                        }
                    }
                    catch { }
                }

                info["properties"] = properties;
            }

            return info;
#else
            return new Dictionary<string, object> { ["name"] = mat.name };
#endif
        }

        private Dictionary<string, object> GetComponentProperties(Component comp)
        {
#if UNITY_EDITOR
            var properties = new Dictionary<string, object>();
            
            try
            {
                var serializedObject = new SerializedObject(comp);
                var iterator = serializedObject.GetIterator();
                
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.Generic) continue;
                    
                    try
                    {
                        properties[iterator.name] = GetSerializedPropertyValue(iterator);
                    }
                    catch { }
                }
            }
            catch { }

            return properties;
#else
            return new Dictionary<string, object>();
#endif
        }

#if UNITY_EDITOR
        private object GetSerializedPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    return new { r = prop.colorValue.r, g = prop.colorValue.g, b = prop.colorValue.b, a = prop.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None";
                case SerializedPropertyType.Enum:
                    return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return new { x = prop.vector2Value.x, y = prop.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = prop.vector3Value.x, y = prop.vector3Value.y, z = prop.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = prop.vector4Value.x, y = prop.vector4Value.y, z = prop.vector4Value.z, w = prop.vector4Value.w };
                case SerializedPropertyType.Quaternion:
                    return new { x = prop.quaternionValue.x, y = prop.quaternionValue.y, z = prop.quaternionValue.z, w = prop.quaternionValue.w };
                case SerializedPropertyType.Rect:
                    return new { x = prop.rectValue.x, y = prop.rectValue.y, width = prop.rectValue.width, height = prop.rectValue.height };
                case SerializedPropertyType.Bounds:
                    return new { 
                        center = new { x = prop.boundsValue.center.x, y = prop.boundsValue.center.y, z = prop.boundsValue.center.z },
                        size = new { x = prop.boundsValue.size.x, y = prop.boundsValue.size.y, z = prop.boundsValue.size.z }
                    };
                default:
                    return prop.propertyType.ToString();
            }
        }
#endif

        private Vector3 CalculateCenterOfMass(List<GameObject> allObjects)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var go in allObjects)
            {
                sum += go.transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private Bounds CalculateSceneBounds(List<GameObject> allObjects)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool initialized = false;

            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (!initialized)
                    {
                        bounds = renderer.bounds;
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return bounds;
        }

        private int CalculateTotalVertices(List<GameObject> allObjects)
        {
            int total = 0;
            foreach (var go in allObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    total += meshFilter.sharedMesh.vertexCount;
                }
            }
            return total;
        }

        private int CalculateTotalTriangles(List<GameObject> allObjects)
        {
            int total = 0;
            foreach (var go in allObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    total += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }
            return total;
        }

        private int EstimateDrawCalls(List<GameObject> allObjects)
        {
            var uniqueMaterials = new HashSet<Material>();
            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null) uniqueMaterials.Add(mat);
                    }
                }
            }
            return uniqueMaterials.Count;
        }

        private float EstimateMemoryUsage(List<GameObject> allObjects)
        {
            float totalMB = 0;

            // Mesh内存估算
            var meshes = new HashSet<Mesh>();
            foreach (var go in allObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    meshes.Add(meshFilter.sharedMesh);
                }
            }
            foreach (var mesh in meshes)
            {
                // 粗略估算：每个顶点约48字节（位置12+法线12+UV12+切线12）
                totalMB += mesh.vertexCount * 48f / (1024f * 1024f);
            }

            // 纹理内存估算
            var textures = new HashSet<Texture>();
            foreach (var go in allObjects)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null) continue;
#if UNITY_EDITOR
                        for (int i = 0; i < ShaderUtil.GetPropertyCount(mat.shader); i++)
                        {
                            if (ShaderUtil.GetPropertyType(mat.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                            {
                                var tex = mat.GetTexture(ShaderUtil.GetPropertyName(mat.shader, i));
                                if (tex != null) textures.Add(tex);
                            }
                        }
#endif
                    }
                }
            }
            foreach (var tex in textures)
            {
                // 粗略估算：RGBA32格式每像素4字节
                totalMB += (tex.width * tex.height * 4f) / (1024f * 1024f);
            }

            return totalMB;
        }

        #endregion
    }
}
