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
    internal static class ESAutomationUnityEditorControl
    {
        private const string SuppressedKey = "ES.Automation.UnityCompilation.Suppressed";
        private const string AiSuppressionOwnerKey = "ES.Automation.UnityCompilation.AiSuppressionOwner";
        private const int MaxSceneModificationOperations = 64;
        private const int MaxHierarchyPathLength = 1024;
        private const int MaxSceneObjectNameLength = 256;
        private static bool initialized;
        private static bool reloadLockHeld;

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            EditorApplication.delayCall += RestoreCompilationPolicy;
        }

        internal static bool IsAutoCompilationSuppressed => SessionState.GetBool(SuppressedKey, false);
        internal static bool IsAutoCompilationEnabled => !IsAutoCompilationSuppressed;

        internal static JObject GetCompilationState()
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
                ["aiSuppressionOwner"] = SessionState.GetString(AiSuppressionOwnerKey, string.Empty),
            };
        }

        internal static JObject SetAutoCompilationFromUser(bool enabled)
        {
            InitializeForEditor();
            ClearAiSuppressionOwnership();
            return SetAutoCompilationCore(enabled);
        }

        internal static JObject SetAutoCompilationFromAi(bool enabled, string actorId)
        {
            InitializeForEditor();
            bool wasSuppressed = IsAutoCompilationSuppressed;
            string existingAiOwner = SessionState.GetString(AiSuppressionOwnerKey, string.Empty);
            JObject state = SetAutoCompilationCore(enabled);
            if (enabled)
            {
                ClearAiSuppressionOwnership();
            }
            else if (!wasSuppressed)
            {
                SessionState.SetString(AiSuppressionOwnerKey,
                    string.IsNullOrWhiteSpace(actorId) ? "ai.unknown" : actorId);
            }
            // 已经由人工关闭时不把所有权伪装成 AI；已有 AI 所有者也保持原始责任主体。
            else if (!string.IsNullOrWhiteSpace(existingAiOwner))
            {
                SessionState.SetString(AiSuppressionOwnerKey, existingAiOwner);
            }
            state["aiSuppressionOwner"] = SessionState.GetString(AiSuppressionOwnerKey, string.Empty);
            return state;
        }

        /// <summary>
        /// 只恢复由 AI 控制面设置的编译抑制。人工菜单设置的 SessionState 不会被 Bridge 关闭意外改写。
        /// </summary>
        internal static bool TryRestoreAiOwnedAutoCompilation(out string owner)
        {
            InitializeForEditor();
            owner = SessionState.GetString(AiSuppressionOwnerKey, string.Empty);
            if (string.IsNullOrWhiteSpace(owner) || !IsAutoCompilationSuppressed)
            {
                if (!string.IsNullOrWhiteSpace(owner)) ClearAiSuppressionOwnership();
                owner = string.Empty;
                return false;
            }
            SetAutoCompilationCore(true);
            ClearAiSuppressionOwnership();
            return true;
        }

        private static JObject SetAutoCompilationCore(bool enabled)
        {
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

        internal static JObject TriggerCompilation(bool forceRefresh)
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

        /// <summary>
        /// 只构造不可变的场景操作计划。真正写入必须由 AI Bridge 在一次性人工批准后调用 Apply。
        /// </summary>
        internal static ESAutomationSceneModificationPlan PrepareActiveSceneModification(
            string scenePath, JArray operations, bool save)
        {
            InitializeForEditor();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PlayMode 中禁止通过 AI Bridge 修改或保存场景。");
            if (operations == null || operations.Count == 0)
                throw new InvalidOperationException("scene.modify 至少需要一个白名单操作。");
            if (operations.Count > MaxSceneModificationOperations)
                throw new InvalidOperationException("scene.modify 单次最多允许 " + MaxSceneModificationOperations
                    + " 个操作；请拆分为多次独立 dry-run 和人工批准。");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("当前没有可修改的已加载 Active Scene。");
            if (string.IsNullOrWhiteSpace(scene.path) || !scene.path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("当前 Active Scene 必须是已保存的 Assets 场景，不能修改临时或包内场景。");
            if (string.IsNullOrWhiteSpace(scenePath)
                || !string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("scenePath 必须非空且精确匹配当前 Active Scene，不能切换或隐式选择场景。");

            var planned = new List<ESAutomationSceneModificationOperation>(operations.Count);
            foreach (JToken token in operations)
            {
                if (token.Type != JTokenType.Object) throw new InvalidOperationException("scene.modify.operations 必须全部是对象。");
                JObject operation = (JObject)token;
                RequireExactProperties(operation, "operation", "targetPath", "value");
                string operationName = ReadString(operation, "operation");
                string targetPath = ReadString(operation, "targetPath");
                GameObject target = FindHierarchyObject(scene, targetPath);
                ValidateOperation(operationName, operation["value"], target);
                planned.Add(new ESAutomationSceneModificationOperation(
                    operationName,
                    targetPath,
                    GetStableGlobalObjectId(target),
                    operation["value"].DeepClone()));
            }

            return new ESAutomationSceneModificationPlan(scene.path, save, planned);
        }

        internal static JObject ApplyPreparedSceneModification(ESAutomationSceneModificationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            InitializeForEditor();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PlayMode 中禁止通过 AI Bridge 修改或保存场景。");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded
                || !string.Equals(scene.path, plan.ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Active Scene 已变化，批准的场景计划已失效。");

            var resolvedOperations = new List<Tuple<ESAutomationSceneModificationOperation, GameObject>>(plan.Operations.Count);
            var uniqueTargets = new List<UnityEngine.Object>();
            var targetSet = new HashSet<GameObject>();
            foreach (ESAutomationSceneModificationOperation operation in plan.Operations)
            {
                GameObject target = ResolveStableTarget(operation, scene);
                ValidateOperation(operation.Operation, operation.Value, target);
                resolvedOperations.Add(Tuple.Create(operation, target));
                if (targetSet.Add(target)) uniqueTargets.Add(target);
            }

            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("ES AI 场景计划应用");
                Undo.RegisterCompleteObjectUndo(uniqueTargets.ToArray(), "ES AI 场景计划应用");

                foreach (Tuple<ESAutomationSceneModificationOperation, GameObject> item in resolvedOperations)
                    ApplyOperation(item.Item1.Operation, item.Item1.Value, item.Item2);

                foreach (UnityEngine.Object target in uniqueTargets)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(target))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    EditorUtility.SetDirty(target);
                }
                EditorSceneManager.MarkSceneDirty(scene);

                // 先完成 Undo 分组，再尝试落盘。这样保存失败或后续异常时，回滚不会留下
                // “磁盘已写入、内存已撤销”的半提交状态。
                Undo.CollapseUndoOperations(undoGroup);

                // 响应数据也在保存前构造，保存成功后不再执行可能抛异常的结构化分配。
                JObject responseData = plan.CreateResponseData(false, plan.SaveRequested);
                if (plan.SaveRequested)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Unity 未能保存已批准的 Active Scene。");
                }

                return responseData;
            }
            catch
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "开启自动 Unity 编译")]
        private static void EnableAutoCompilationFromMenu() => SetAutoCompilationFromUser(true);

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "关闭自动 Unity 编译")]
        private static void DisableAutoCompilationFromMenu() => SetAutoCompilationFromUser(false);

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "触发 Unity 编译")]
        private static void TriggerCompilationFromMenu() => TriggerCompilation(true);

        private static void RestoreCompilationPolicy()
        {
            // delayCall 是一次性恢复任务；先解除注册，避免 ReloadDomain/重复初始化
            // 后残留旧委托或重复执行编译锁恢复。
            EditorApplication.delayCall -= RestoreCompilationPolicy;
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
            if (string.IsNullOrWhiteSpace(targetPath)
                || targetPath.Length > MaxHierarchyPathLength
                || !string.Equals(targetPath, targetPath.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("targetPath 必须是非空的精确层级路径。");
            string[] parts = targetPath.Split(new[] { '/' }, StringSplitOptions.None);
            if (parts.Length == 0) throw new InvalidOperationException("targetPath 不能为空。");
            foreach (string part in parts)
                ValidateHierarchySegment(part, "targetPath");

            GameObject matchedRoot = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!string.Equals(root.name, parts[0], StringComparison.Ordinal)) continue;
                if (matchedRoot != null)
                    throw new InvalidOperationException("targetPath 匹配多个根对象，拒绝歧义目标：" + targetPath);
                matchedRoot = root;
            }
            if (matchedRoot == null) throw new InvalidOperationException("找不到场景对象：" + targetPath);

            Transform current = matchedRoot.transform;
            for (int index = 1; index < parts.Length; index++)
            {
                Transform matchedChild = null;
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (!string.Equals(child.name, parts[index], StringComparison.Ordinal)) continue;
                    if (matchedChild != null)
                        throw new InvalidOperationException("targetPath 匹配多个同名子对象，拒绝歧义目标：" + targetPath);
                    matchedChild = child;
                }
                if (matchedChild == null) throw new InvalidOperationException("找不到场景对象：" + targetPath);
                current = matchedChild;
            }
            return current.gameObject;
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
                    if (((string)value).Length > MaxSceneObjectNameLength)
                        throw new InvalidOperationException("setName.value 超过 " + MaxSceneObjectNameLength + " 字符限制。");
                    ValidateHierarchySegment((string)value, "setName.value");
                    break;
                case "setTag":
                    if (value == null || value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value)) throw new InvalidOperationException("setTag.value 必须是非空字符串。");
                    if (!IsDefinedTag((string)value)) throw new InvalidOperationException("setTag.value 不是项目已定义 Tag：" + (string)value);
                    break;
                case "setLayer":
                    if (value == null || value.Type != JTokenType.Integer || (int)value < 0 || (int)value > 31) throw new InvalidOperationException("setLayer.value 必须是 0–31 的整数。");
                    if (!IsDefinedLayer((int)value)) throw new InvalidOperationException("setLayer.value 不是项目已定义 Layer：" + (int)value);
                    break;
                default:
                    throw new InvalidOperationException("未注册的场景操作：" + operationName);
            }
        }

        private static void ApplyOperation(string operationName, JToken value, GameObject target)
        {
            switch (operationName)
            {
                case "setActive": target.SetActive((bool)value); break;
                case "setName": target.name = (string)value; break;
                case "setTag": target.tag = (string)value; break;
                case "setLayer": target.layer = (int)value; break;
            }
        }

        private static string GetStableGlobalObjectId(GameObject target)
        {
            try
            {
                GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(target);
                if (id.identifierType == 0 || id.targetObjectId == 0
                    || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) != target)
                    throw new InvalidOperationException("目标对象没有可复核的 GlobalObjectId：" + target.name);
                return id.ToString();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法为场景目标建立稳定身份：" + target.name, exception);
            }
        }

        private static GameObject ResolveStableTarget(ESAutomationSceneModificationOperation operation, Scene scene)
        {
            if (!GlobalObjectId.TryParse(operation.TargetGlobalObjectId, out GlobalObjectId id))
                throw new InvalidOperationException("批准计划的目标 GlobalObjectId 无效：" + operation.TargetPath);
            GameObject target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
            if (target == null || target.scene.handle != scene.handle)
                throw new InvalidOperationException("批准计划的目标已变更或不再属于 Active Scene：" + operation.TargetPath);
            string resolvedPath = GetHierarchyPath(target);
            if (!string.Equals(resolvedPath, operation.TargetPath, StringComparison.Ordinal))
                throw new InvalidOperationException("批准计划的目标层级路径已变化：" + operation.TargetPath);
            return target;
        }

        private static string GetHierarchyPath(GameObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var segments = new List<string>();
            Transform current = target.transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static bool IsDefinedTag(string tag)
        {
            foreach (string candidate in UnityEditorInternal.InternalEditorUtility.tags)
                if (string.Equals(candidate, tag, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsDefinedLayer(int layer) => !string.IsNullOrWhiteSpace(LayerMask.LayerToName(layer));

        private static void ValidateHierarchySegment(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || value == "."
                || value == ".."
                || value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0)
                throw new InvalidOperationException(context + " 不能包含空段、首尾空白、.、.. 或路径分隔符。");
            foreach (char character in value)
            {
                if (char.IsControl(character))
                    throw new InvalidOperationException(context + " 不能包含控制字符。");
            }
        }

        private static void ClearAiSuppressionOwnership() => SessionState.SetString(AiSuppressionOwnerKey, string.Empty);

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

    internal sealed class ESAutomationSceneModificationOperation
    {
        private readonly JToken value;

        internal ESAutomationSceneModificationOperation(string operation, string targetPath, string targetGlobalObjectId, JToken value)
        {
            Operation = operation ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            TargetGlobalObjectId = targetGlobalObjectId ?? string.Empty;
            this.value = value?.DeepClone();
        }

        internal string Operation { get; }
        internal string TargetPath { get; }
        internal string TargetGlobalObjectId { get; }
        internal JToken Value => value?.DeepClone();
    }

    internal sealed class ESAutomationSceneModificationPlan
    {
        private readonly IReadOnlyList<ESAutomationSceneModificationOperation> operations;

        internal ESAutomationSceneModificationPlan(
            string scenePath,
            bool saveRequested,
            List<ESAutomationSceneModificationOperation> operations)
        {
            ScenePath = scenePath ?? string.Empty;
            SaveRequested = saveRequested;
            this.operations = new List<ESAutomationSceneModificationOperation>(
                operations ?? new List<ESAutomationSceneModificationOperation>()).AsReadOnly();
        }

        internal string ScenePath { get; }
        internal bool SaveRequested { get; }
        internal IReadOnlyList<ESAutomationSceneModificationOperation> Operations => operations;

        internal JObject CreateFingerprintPayload()
        {
            var serializedOperations = new JArray();
            foreach (ESAutomationSceneModificationOperation operation in operations)
            {
                serializedOperations.Add(new JObject
                {
                    ["operation"] = operation.Operation,
                    ["targetPath"] = operation.TargetPath,
                    ["targetGlobalObjectId"] = operation.TargetGlobalObjectId,
                    ["value"] = operation.Value?.DeepClone(),
                });
            }
            return new JObject
            {
                ["scenePath"] = ScenePath,
                ["save"] = SaveRequested,
                ["operations"] = serializedOperations,
            };
        }

        internal JObject CreateResponseData(bool dryRun, bool saved)
        {
            var serializedOperations = new JArray();
            foreach (ESAutomationSceneModificationOperation operation in operations)
            {
                serializedOperations.Add(new JObject
                {
                    ["operation"] = operation.Operation,
                    ["targetPath"] = operation.TargetPath,
                    ["targetGlobalObjectId"] = operation.TargetGlobalObjectId,
                    ["value"] = operation.Value?.DeepClone(),
                });
            }
            return new JObject
            {
                ["scenePath"] = ScenePath,
                ["dryRun"] = dryRun,
                ["saveRequested"] = SaveRequested,
                ["saved"] = saved,
                ["operationCount"] = operations.Count,
                ["operations"] = serializedOperations,
                ["undoSupported"] = !dryRun,
            };
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
