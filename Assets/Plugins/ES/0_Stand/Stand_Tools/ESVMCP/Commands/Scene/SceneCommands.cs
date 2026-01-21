using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ES.VMCP
{
    /// <summary>
    /// Scene操作类型
    /// </summary>
    public enum CommonSceneOperation
    {
        LoadScene,          // 加载场景
        UnloadScene,        // 卸载场景
        SaveScene,          // 保存场景
        CreateScene,        // 创建新场景
        GetActiveScene,     // 获取当前场景
        SetActiveScene,     // 设置当前场景
        GetAllScenes,       // 获取所有场景
        FindObjects,        // 查找场景中的对象
        GetSceneInfo        // 获取场景信息
    }

    /// <summary>
    /// 统一的Scene操作命令
    /// </summary>
    [ESVMCPCommand("CommonSceneOperation", "统一的Scene操作命令，支持加载、卸载、保存、创建场景等")]
    public class SceneOperationCommand : ESVMCPCommandBase
    {
        [JsonProperty("operation")]
        public CommonSceneOperation Operation { get; set; }

        [JsonProperty("sceneName")]
        public string SceneName { get; set; }

        [JsonProperty("scenePath")]
        public string ScenePath { get; set; }

        [JsonProperty("loadMode")]
        public LoadSceneMode LoadMode { get; set; } = LoadSceneMode.Single;

        [JsonProperty("saveModified")]
        public bool SaveModified { get; set; } = true;

        [JsonProperty("findByName")]
        public string FindByName { get; set; }

        [JsonProperty("findByType")]
        public string FindByType { get; set; }

        public override string Description
        {
            get
            {
                switch (Operation)
                {
                    case CommonSceneOperation.LoadScene:
                        return $"加载场景: {SceneName ?? ScenePath}";
                    case CommonSceneOperation.UnloadScene:
                        return $"卸载场景: {SceneName}";
                    case CommonSceneOperation.SaveScene:
                        return $"保存场景: {SceneName ?? "当前场景"}";
                    case CommonSceneOperation.CreateScene:
                        return $"创建场景: {SceneName}";
                    case CommonSceneOperation.FindObjects:
                        return $"查找对象: {FindByName ?? FindByType}";
                    default:
                        return $"Scene操作: {Operation}";
                }
            }
        }

        public override ESVMCPValidationResult Validate()
        {
            switch (Operation)
            {
                case CommonSceneOperation.LoadScene:
                    if (string.IsNullOrEmpty(SceneName) && string.IsNullOrEmpty(ScenePath))
                        return ESVMCPValidationResult.Failure("加载场景需要指定sceneName或scenePath");
                    break;
                case CommonSceneOperation.UnloadScene:
                case CommonSceneOperation.SetActiveScene:
                    if (string.IsNullOrEmpty(SceneName))
                        return ESVMCPValidationResult.Failure($"{Operation}操作需要指定sceneName");
                    break;
                case CommonSceneOperation.CreateScene:
                    if (string.IsNullOrEmpty(SceneName))
                        return ESVMCPValidationResult.Failure("创建场景需要指定sceneName");
                    break;
            }

            return ESVMCPValidationResult.Success();
        }

        public override ESVMCPCommandResult Execute(ESVMCPExecutionContext context)
        {
            try
            {
                switch (Operation)
                {
                    case CommonSceneOperation.LoadScene:
                        return ExecuteLoadScene(context);
                    case CommonSceneOperation.UnloadScene:
                        return ExecuteUnloadScene(context);
                    case CommonSceneOperation.SaveScene:
                        return ExecuteSaveScene(context);
                    case CommonSceneOperation.CreateScene:
                        return ExecuteCreateScene(context);
                    case CommonSceneOperation.GetActiveScene:
                        return ExecuteGetActiveScene(context);
                    case CommonSceneOperation.SetActiveScene:
                        return ExecuteSetActiveScene(context);
                    case CommonSceneOperation.GetAllScenes:
                        return ExecuteGetAllScenes(context);
                    case CommonSceneOperation.FindObjects:
                        return ExecuteFindObjects(context);
                    case CommonSceneOperation.GetSceneInfo:
                        return ExecuteGetSceneInfo(context);
                    default:
                        return ESVMCPCommandResult.Failed($"不支持的操作类型: {Operation}");
                }
            }
            catch (System.Exception e)
            {
                return ESVMCPCommandResult.Failed($"Scene操作失败: {e.Message}", e);
            }
        }

        private ESVMCPCommandResult ExecuteLoadScene(ESVMCPExecutionContext context)
        {
            if (!string.IsNullOrEmpty(ScenePath))
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, 
                    LoadMode == LoadSceneMode.Single ? OpenSceneMode.Single : OpenSceneMode.Additive);
                return ESVMCPCommandResult.Succeed($"成功加载场景: {scene.name}", new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path
                });
            }
            else
            {
                SceneManager.LoadScene(SceneName, LoadMode);
                return ESVMCPCommandResult.Succeed($"成功加载场景: {SceneName}");
            }
        }

        private ESVMCPCommandResult ExecuteUnloadScene(ESVMCPExecutionContext context)
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.isLoaded)
                return ESVMCPCommandResult.Failed($"场景未加载: {SceneName}");

            bool success = EditorSceneManager.CloseScene(scene, SaveModified);
            if (success)
                return ESVMCPCommandResult.Succeed($"成功卸载场景: {SceneName}");
            else
                return ESVMCPCommandResult.Failed($"卸载场景失败: {SceneName}");
        }

        private ESVMCPCommandResult ExecuteSaveScene(ESVMCPExecutionContext context)
        {
            Scene scene;
            if (!string.IsNullOrEmpty(SceneName))
            {
                scene = SceneManager.GetSceneByName(SceneName);
                if (!scene.isLoaded)
                    return ESVMCPCommandResult.Failed($"场景未加载: {SceneName}");
            }
            else
            {
                scene = SceneManager.GetActiveScene();
            }

            bool success = EditorSceneManager.SaveScene(scene);
            if (success)
                return ESVMCPCommandResult.Succeed($"成功保存场景: {scene.name}");
            else
                return ESVMCPCommandResult.Failed($"保存场景失败: {scene.name}");
        }

        private ESVMCPCommandResult ExecuteCreateScene(ESVMCPExecutionContext context)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            scene.name = SceneName;

            if (!string.IsNullOrEmpty(ScenePath))
            {
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            return ESVMCPCommandResult.Succeed($"成功创建场景: {SceneName}");
        }

        private ESVMCPCommandResult ExecuteGetActiveScene(ESVMCPExecutionContext context)
        {
            Scene scene = SceneManager.GetActiveScene();
            return ESVMCPCommandResult.Succeed($"当前场景: {scene.name}", new Dictionary<string, object>
            {
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["buildIndex"] = scene.buildIndex,
                ["rootCount"] = scene.rootCount
            });
        }

        private ESVMCPCommandResult ExecuteSetActiveScene(ESVMCPExecutionContext context)
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.isLoaded)
                return ESVMCPCommandResult.Failed($"场景未加载: {SceneName}");

            bool success = SceneManager.SetActiveScene(scene);
            if (success)
                return ESVMCPCommandResult.Succeed($"成功设置当前场景: {SceneName}");
            else
                return ESVMCPCommandResult.Failed($"设置当前场景失败: {SceneName}");
        }

        private ESVMCPCommandResult ExecuteGetAllScenes(ESVMCPExecutionContext context)
        {
            var scenes = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                scenes.Add(new
                {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = scene.rootCount
                });
            }

            return ESVMCPCommandResult.Succeed($"找到 {scenes.Count} 个场景", new Dictionary<string, object>
            {
                ["scenes"] = scenes
            });
        }

        private ESVMCPCommandResult ExecuteFindObjects(ESVMCPExecutionContext context)
        {
            var results = new List<object>();

            if (!string.IsNullOrEmpty(FindByName))
            {
                var objects = Object.FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains(FindByName))
                    .Take(50); // 限制结果数量

                foreach (var obj in objects)
                {
                    results.Add(new
                    {
                        name = obj.name,
                        path = GetGameObjectPath(obj),
                        instanceID = obj.GetInstanceID()
                    });
                }
            }
            else if (!string.IsNullOrEmpty(FindByType))
            {
                var type = System.Type.GetType(FindByType);
                if (type == null)
                    return ESVMCPCommandResult.Failed($"未找到类型: {FindByType}");

                var components = Object.FindObjectsOfType(type).Take(50);
                foreach (var comp in components)
                {
                    var go = (comp as Component)?.gameObject ?? (comp as GameObject);
                    if (go != null)
                    {
                        results.Add(new
                        {
                            name = go.name,
                            path = GetGameObjectPath(go),
                            componentType = comp.GetType().Name,
                            instanceID = go.GetInstanceID()
                        });
                    }
                }
            }

            return ESVMCPCommandResult.Succeed($"找到 {results.Count} 个对象", new Dictionary<string, object>
            {
                ["objects"] = results
            });
        }

        private ESVMCPCommandResult ExecuteGetSceneInfo(ESVMCPExecutionContext context)
        {
            Scene scene;
            if (!string.IsNullOrEmpty(SceneName))
            {
                scene = SceneManager.GetSceneByName(SceneName);
                if (!scene.isLoaded)
                    return ESVMCPCommandResult.Failed($"场景未加载: {SceneName}");
            }
            else
            {
                scene = SceneManager.GetActiveScene();
            }

            var rootObjects = scene.GetRootGameObjects();
            var info = new Dictionary<string, object>
            {
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["isLoaded"] = scene.isLoaded,
                ["isDirty"] = scene.isDirty,
                ["buildIndex"] = scene.buildIndex,
                ["rootCount"] = scene.rootCount,
                ["rootObjects"] = rootObjects.Select(go => new Dictionary<string, object>
                {
                    ["name"] = go.name,
                    ["instanceID"] = go.GetInstanceID()
                }).ToList()
            };

            return ESVMCPCommandResult.Succeed($"场景信息: {scene.name}", info);
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
    }
}
