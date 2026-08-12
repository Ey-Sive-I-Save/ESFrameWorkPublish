using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// 受管 Unity Editor 控制面：只接受已注册的编译控制和白名单场景操作。
    /// 不接受脚本、任意命令、任意资产路径或 PlayMode 写入。
    /// </summary>
    public static class ESAutomationUnityEditorControl
    {
        private const string SuppressedKey = "ES.Automation.UnityCompilation.Suppressed";
        private static bool initialized;
        private static bool reloadLockHeld;

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            EditorApplication.delayCall += RestoreCompilationPolicy;
        }

        public static bool IsAutoCompilationSuppressed => SessionState.GetBool(SuppressedKey, false);
        public static bool IsAutoCompilationEnabled => !IsAutoCompilationSuppressed;

        public static JObject GetCompilationState()
        {
            InitializeForEditor();
            return new JObject
            {
                ["autoCompilationEnabled"] = IsAutoCompilationEnabled,
                ["autoCompilationSuppressed"] = IsAutoCompilationSuppressed,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isUpdating"] = EditorApplication.isUpdating,
                ["isPlaying"] = EditorApplication.isPlayingOrWillChangePlaymode,
                ["reloadLockHeldByEs"] = reloadLockHeld,
            };
        }

        public static JObject SetAutoCompilation(bool enabled)
        {
            InitializeForEditor();
            bool wasSuppressed = IsAutoCompilationSuppressed;
            if (enabled)
            {
                SessionState.SetBool(SuppressedKey, false);
                if (wasSuppressed)
                    AssetDatabase.AllowAutoRefresh();
                if (reloadLockHeld)
                {
                    reloadLockHeld = false;
                    EditorApplication.UnlockReloadAssemblies();
                }
            }
            else
            {
                SessionState.SetBool(SuppressedKey, true);
                if (!wasSuppressed)
                    AssetDatabase.DisallowAutoRefresh();
                if (!reloadLockHeld)
                {
                    reloadLockHeld = true;
                    EditorApplication.LockReloadAssemblies();
                }
            }

            return GetCompilationState();
        }

        public static JObject TriggerCompilation(bool forceRefresh)
        {
            InitializeForEditor();
            if (!IsAutoCompilationEnabled)
                throw new InvalidOperationException("自动 Unity 编译当前已关闭；请先执行 setUnityAutoCompilation(enabled=true)。");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PlayMode 或即将进入 PlayMode 时禁止触发 Editor 编译。");

            if (forceRefresh)
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            JObject data = GetCompilationState();
            data["triggered"] = true;
            data["forceRefresh"] = forceRefresh;
            return data;
        }

        public static JObject ModifyActiveScene(string scenePath, JArray operations, bool save, bool dryRun)
        {
            InitializeForEditor();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PlayMode 中禁止通过 AI Bridge 修改或保存场景。");
            if (operations == null || operations.Count == 0)
                throw new InvalidOperationException("scene.modify 至少需要一个白名单操作。");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("当前没有可修改的已加载 Active Scene。");
            if (!string.IsNullOrWhiteSpace(scenePath) && !string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("scenePath 必须精确匹配当前 Active Scene，不能切换或隐式打开其他场景。");

            var planned = new List<JObject>(operations.Count);
            var validatedOperations = new List<Tuple<string, JToken, GameObject>>(operations.Count);
            foreach (JToken token in operations)
            {
                if (token.Type != JTokenType.Object) throw new InvalidOperationException("scene.modify.operations 必须全部是对象。");
                JObject operation = (JObject)token;
                RequireExactProperties(operation, "operation", "targetPath", "value");
                string operationName = ReadString(operation, "operation");
                string targetPath = ReadString(operation, "targetPath");
                GameObject target = FindHierarchyObject(scene, targetPath);
                if (target == null) throw new InvalidOperationException("找不到场景对象：" + targetPath);
                ValidateOperation(operationName, operation["value"], target);
                planned.Add(new JObject
                {
                    ["operation"] = operationName,
                    ["targetPath"] = targetPath,
                    ["targetInstanceId"] = target.GetInstanceID(),
                });
                validatedOperations.Add(Tuple.Create(operationName, operation["value"], target));
            }

            if (!dryRun)
            {
                foreach (Tuple<string, JToken, GameObject> operation in validatedOperations)
                    ApplyOperation(operation.Item1, operation.Item2, operation.Item3);
            }

            bool saved = false;
            if (!dryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (save) saved = EditorSceneManager.SaveScene(scene);
            }

            return new JObject
            {
                ["scenePath"] = scene.path,
                ["sceneName"] = scene.name,
                ["dryRun"] = dryRun,
                ["saveRequested"] = save,
                ["saved"] = saved,
                ["operationCount"] = planned.Count,
                ["operations"] = JArray.FromObject(planned),
                ["undoSupported"] = !dryRun,
            };
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "开启自动 Unity 编译")]
        private static void EnableAutoCompilationFromMenu() => SetAutoCompilation(true);

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "关闭自动 Unity 编译")]
        private static void DisableAutoCompilationFromMenu() => SetAutoCompilation(false);

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "触发 Unity 编译")]
        private static void TriggerCompilationFromMenu() => TriggerCompilation(true);

        private static void RestoreCompilationPolicy()
        {
            if (!IsAutoCompilationSuppressed) return;
            AssetDatabase.DisallowAutoRefresh();
            if (!reloadLockHeld)
            {
                reloadLockHeld = true;
                EditorApplication.LockReloadAssemblies();
            }
        }

        private static GameObject FindHierarchyObject(Scene scene, string targetPath)
        {
            string[] parts = (targetPath ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!string.Equals(root.name, parts[0], StringComparison.Ordinal)) continue;
                Transform current = root.transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    current = current.Find(parts[i]);
                    if (current == null) return null;
                }
                return current.gameObject;
            }
            return null;
        }

        private static void ValidateOperation(string operationName, JToken value, GameObject target)
        {
            switch (operationName)
            {
                case "setActive":
                    if (value == null || value.Type != JTokenType.Boolean) throw new InvalidOperationException("setActive.value 必须是布尔值。");
                    break;
                case "setName":
                    if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value)) throw new InvalidOperationException("setName.value 必须是非空字符串。");
                    break;
                case "setTag":
                    if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value)) throw new InvalidOperationException("setTag.value 必须是非空字符串。");
                    break;
                case "setLayer":
                    if (value == null || value.Type != JTokenType.Integer || (int)value < 0 || (int)value > 31) throw new InvalidOperationException("setLayer.value 必须是 0–31 的整数。");
                    break;
                default:
                    throw new InvalidOperationException("未注册的场景操作：" + operationName);
            }
        }

        private static void ApplyOperation(string operationName, JToken value, GameObject target)
        {
            Undo.RecordObject(target, "ES AI 场景修改：" + operationName);
            switch (operationName)
            {
                case "setActive": target.SetActive((bool)value); break;
                case "setName": target.name = (string)value; break;
                case "setTag": target.tag = (string)value; break;
                case "setLayer": target.layer = (int)value; break;
            }
            EditorUtility.SetDirty(target);
        }

        private static string ReadString(JObject root, string property)
        {
            if (root[property] == null || root[property].Type != JTokenType.String) throw new InvalidOperationException(property + " 必须是字符串。");
            return (string)root[property];
        }

        private static void RequireExactProperties(JObject root, params string[] expectedProperties)
        {
            var expected = new HashSet<string>(expectedProperties, StringComparer.Ordinal);
            foreach (JProperty property in root.Properties())
                if (!expected.Contains(property.Name)) throw new InvalidOperationException("场景操作包含未注册字段：" + property.Name);
            foreach (string property in expectedProperties)
                if (root[property] == null) throw new InvalidOperationException("场景操作缺少字段：" + property);
        }
    }

    internal sealed class ESAutomationUnityEditorControlInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAutomationUnityEditorControl.InitializeForEditor();
        }
    }
}
