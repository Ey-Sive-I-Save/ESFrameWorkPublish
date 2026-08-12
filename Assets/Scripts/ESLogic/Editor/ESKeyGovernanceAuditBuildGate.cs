using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// Makes authoritative stable-Key violations a Player build failure while keeping review warnings visible.
    /// </summary>
    internal sealed class ESKeyGovernanceAuditBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;

        public void OnPreprocessBuild(BuildReport report)
        {
            ESKeyGovernanceAudit.RunAndThrowIfErrors("Player build");
            ESMonoScriptIdentityAudit.RunAndThrowIfErrors("Player build");
            ESCharacterTemplateReleaseGate.RunAndThrowIfErrors("Player build");
            ESAudioContentAudit.RunAndThrowIfErrors("Player build", true);
        }
    }

    /// <summary>
    /// Keeps the resource pipeline extensible: ES_Editor exposes the hook and project governance owns the policy.
    /// </summary>
    /// <summary>资源烘焙前的 Key 审计订阅由 AssemblyStream 显式安装。</summary>
    internal sealed class ESKeyGovernanceAuditResourceBuildGate : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAssetBundleBuilder.BeforeBuildValidation -= AuditBeforeResourceBake;
            ESAssetBundleBuilder.BeforeBuildValidation += AuditBeforeResourceBake;
        }

        private static void AuditBeforeResourceBake()
        {
            ESKeyGovernanceAudit.RunAndThrowIfErrors("Resource bake");
            ESMonoScriptIdentityAudit.RunAndThrowIfErrors("Resource bake");
            ESCharacterTemplateReleaseGate.RunAndThrowIfErrors("Resource bake");
            ESAudioContentAudit.RunAndThrowIfErrors("Resource bake", false);
        }
    }

    /// <summary>
    /// 迁移脚本的硬门禁：Unity 的资产引用依赖 .meta GUID，而可挂载脚本还要求
    /// 文件名、Mono 类型名和编译后的 MonoScript 三者一致。该审计只覆盖已登记的
    /// 高风险类型迁移，不对历史实验代码做未经授权的全盘重命名。
    /// </summary>
    internal static class ESMonoScriptIdentityAudit
    {
        private readonly struct MigrationContract
        {
            public readonly string assetPath;
            public readonly string expectedGuid;
            public readonly Type expectedType;
            public readonly string oldTypeName;

            public MigrationContract(string assetPath, string expectedGuid, Type expectedType, string oldTypeName)
            {
                this.assetPath = assetPath;
                this.expectedGuid = expectedGuid;
                this.expectedType = expectedType;
                this.oldTypeName = oldTypeName;
            }
        }

        private readonly struct AssetContract
        {
            public readonly string assetPath;
            public readonly string expectedGuid;
            public readonly string expectedScriptGuid;

            public AssetContract(string assetPath, string expectedGuid, string expectedScriptGuid)
            {
                this.assetPath = assetPath;
                this.expectedGuid = expectedGuid;
                this.expectedScriptGuid = expectedScriptGuid;
            }
        }

        private readonly struct GuidContract
        {
            public readonly string assetPath;
            public readonly string expectedGuid;

            public GuidContract(string assetPath, string expectedGuid)
            {
                this.assetPath = assetPath;
                this.expectedGuid = expectedGuid;
            }
        }

        private static readonly MigrationContract[] Contracts =
        {
            new MigrationContract(
                "Assets/Scripts/ESLogic/Runtime/Camera/Content/ESCameraViewDefinition.cs",
                "2ec636e27c6c4859a44b7fa352b4b91d",
                typeof(ESCameraViewDefinition),
                "ESCameraProfile"),
            new MigrationContract(
                "Assets/Scripts/ESLogic/Runtime/Camera/Content/ESCameraViewDefinitionCatalog.cs",
                "9ee80928fa684cddb3b4933b80c7b974",
                typeof(ESCameraViewDefinitionCatalog),
                "ESCameraProfileCatalog"),
            new MigrationContract(
                "Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs",
                "8c00f6a7e06e4b7ca48b68f6b52ba0cb",
                typeof(EntityCharacterIdentity),
                "EntityCharacterProfile"),
            new MigrationContract(
                "Assets/Scripts/ESLogic/Runtime/State/Parameter/System/StateDefaultNumericParameterConfig.cs",
                "b733ce200c4cfae449d38052fbd29000",
                typeof(StateDefaultNumericParameterConfig),
                "StateDefaultNumericParameterProfile")
        };

        private static readonly AssetContract[] AssetContracts =
        {
            new AssetContract(
                "Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset",
                "38bda9db880d3ea4a83af046c5168196",
                "9ee80928fa684cddb3b4933b80c7b974"),
            new AssetContract(
                "Assets/ESNormalAssets/Camera/ViewDefinitions/PlayerThirdPerson.asset",
                "3d32a95d6d34fd44db1788e400d88c5d",
                "2ec636e27c6c4859a44b7fa352b4b91d"),
            new AssetContract(
                "Assets/ESNormalAssets/Camera/ViewDefinitions/VehicleChase.asset",
                "abf9fc5bfa606cd48a28755e5498c7a4",
                "2ec636e27c6c4859a44b7fa352b4b91d")
        };

        private static readonly GuidContract[] GuidContracts =
        {
            new GuidContract(
                "Assets/ESNormalAssets/Camera/ViewDefinitions",
                "992a331a462668449953985468822d17")
        };

        [MenuItem("【ES】/验证与诊断/静态审计/Mono脚本名称与GUID")]
        private static void RunAndLog()
        {
            List<string> errors = new List<string>(Contracts.Length * 2);
            Validate(errors);
            if (errors.Count == 0)
            {
                Debug.Log("[ES Mono脚本审计] 通过：迁移脚本的文件名、Mono 类型名、GUID 均一致。");
                return;
            }

            Debug.LogError(BuildReport("手动审计", errors));
        }

        internal static void RunAndThrowIfErrors(string stage)
        {
            List<string> errors = new List<string>(Contracts.Length * 2);
            Validate(errors);
            if (errors.Count > 0)
                throw new BuildFailedException(BuildReport(stage, errors));
        }

        private static void Validate(List<string> errors)
        {
            for (int i = 0; i < Contracts.Length; i++)
            {
                MigrationContract contract = Contracts[i];
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(contract.assetPath);
                if (script == null)
                {
                    errors.Add(contract.assetPath + "：找不到 MonoScript；旧类型 " + contract.oldTypeName + " 的迁移入口已断裂。");
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(contract.assetPath);
                if (!string.Equals(fileName, contract.expectedType.Name, StringComparison.Ordinal))
                    errors.Add(contract.assetPath + "：脚本文件名 " + fileName + " 与主类型 " + contract.expectedType.Name + " 不一致。");

                Type actualType = script.GetClass();
                if (actualType != contract.expectedType)
                    errors.Add(contract.assetPath + "：MonoScript.GetClass() 为 "
                               + (actualType == null ? "<null>" : actualType.FullName)
                               + "，期望 " + contract.expectedType.FullName + "。");

                string guid = AssetDatabase.AssetPathToGUID(contract.assetPath);
                if (!string.Equals(guid, contract.expectedGuid, StringComparison.Ordinal))
                    errors.Add(contract.assetPath + "：GUID=" + guid + "，期望保留旧 GUID=" + contract.expectedGuid + "。");
            }

            for (int i = 0; i < AssetContracts.Length; i++)
            {
                AssetContract contract = AssetContracts[i];
                string absoluteAssetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", contract.assetPath));
                if (!File.Exists(absoluteAssetPath))
                {
                    errors.Add(contract.assetPath + "：迁移后的资产不存在。");
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(contract.assetPath);
                if (!string.Equals(guid, contract.expectedGuid, StringComparison.Ordinal))
                    errors.Add(contract.assetPath + "：资产 GUID=" + guid + "，期望保留旧 GUID=" + contract.expectedGuid + "。");

                string yaml = File.ReadAllText(absoluteAssetPath, Encoding.UTF8);
                string expectedScript = "m_Script: {fileID: 11500000, guid: " + contract.expectedScriptGuid + ", type: 3}";
                if (yaml.IndexOf(expectedScript, StringComparison.Ordinal) < 0)
                    errors.Add(contract.assetPath + "：m_Script 未指向期望脚本 GUID=" + contract.expectedScriptGuid + "。");
            }

            for (int i = 0; i < GuidContracts.Length; i++)
            {
                GuidContract contract = GuidContracts[i];
                string guid = AssetDatabase.AssetPathToGUID(contract.assetPath);
                if (!string.Equals(guid, contract.expectedGuid, StringComparison.Ordinal))
                    errors.Add(contract.assetPath + "：目录 GUID=" + guid + "，期望保留旧 GUID=" + contract.expectedGuid + "。");
            }
        }

        private static string BuildReport(string stage, List<string> errors)
        {
            return "[ES Mono脚本审计] " + stage + " 已阻止：发现 " + errors.Count + " 个错误。\n- "
                   + string.Join("\n- ", errors);
        }
    }

    /// <summary>
    /// Cold editor/release validation for authored audio integrations.
    /// Runtime intentionally performs no hierarchy discovery: this audit makes the one valid
    /// Pool-root contract explicit before a Prefab can reach formal content.
    /// </summary>
    internal static class ESAudioContentAudit
    {
        private static readonly string[] AssetSearchRoots = { "Assets" };

        [MenuItem("【ES】/验证与诊断/静态审计/音频内容与对象池")]
        private static void RunAndLog()
        {
            var errors = new List<string>(32);
            RunAudit(errors, true);
            if (errors.Count == 0)
            {
                Debug.Log("[ESAudio 内容审计] 通过：未发现 Pool Root/Extension 或 Legacy 裸 AudioSource 命令问题。");
                return;
            }

            Debug.LogError(BuildErrorReport("手动审计", errors));
        }

        /// <summary>
        /// Blocks formal content when a VFX emitter cannot participate in the documented Pool
        /// lifecycle, or when serialized command data still carries a rejected raw-Source command.
        /// Player builds include configured scenes; resource bakes validate authored assets only so
        /// they never open/close user scenes as a side effect.
        /// </summary>
        internal static void RunAndThrowIfErrors(string stage, bool includeBuildScenes)
        {
            var errors = new List<string>(32);
            RunAudit(errors, includeBuildScenes);
            if (errors.Count > 0)
                throw new BuildFailedException(BuildErrorReport(stage, errors));
        }

        private static void RunAudit(List<string> errors, bool includeBuildScenes)
        {
            ValidatePrefabs(errors);
            ValidateScriptableObjectCommands(errors);
            if (includeBuildScenes)
                ValidateBuildSceneCommands(errors);
        }

        private static void ValidatePrefabs(List<string> errors)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", AssetSearchRoots);
            Array.Sort(prefabGuids, StringComparer.Ordinal);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add(path + "：无法加载 Prefab，无法验证音频 Pool 生命周期。");
                    continue;
                }

                ESVfxAudioEmitterSet[] sets = prefab.GetComponentsInChildren<ESVfxAudioEmitterSet>(true);
                for (int j = 0; j < sets.Length; j++)
                    ValidateEmitterSetPoolContract(sets[j], path, errors);

                ScanLegacyAudioCommands(prefab.GetComponentsInChildren<MonoBehaviour>(true), path, errors);
            }
        }

        private static void ValidateEmitterSetPoolContract(
            ESVfxAudioEmitterSet set,
            string assetPath,
            List<string> errors)
        {
            if (set == null)
                return;

            // ESVfxAudioEmitterSet registers only against ESGenericLife on its own GameObject.
            // A child Set can neither be the pooled root nor become an Entity-root extension, so
            // accepting it would make its pool callbacks depend on incidental hierarchy behavior.
            if (set.transform.parent != null)
            {
                errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                           + "：ESVfxAudioEmitterSet 必须位于可池化 Prefab 根节点。"
                           + "Entity 音频请把 Set 与 Entity 同挂在根节点；独立 VFX 请让 Set 成为根节点唯一生命周期接收者。");
                return;
            }

            Entity entity = set.GetComponent<Entity>();
            ESGenericLife life = set.GetComponent<ESGenericLife>();
            if (entity != null)
            {
                if (life == null)
                {
                    errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                               + "：Entity 与 ESVfxAudioEmitterSet 同根时必须添加 ESGenericLife，"
                               + "并明确把 Entity 设为 Pool Root；Set 只能作为 Extension。");
                }
                else if (!ReferenceEquals(life.PoolRootLifecycleComponent, entity))
                {
                    errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                               + "：Entity 根的 ESGenericLife.Pool Root 必须指向 Entity。"
                               + "不要把 ESVfxAudioEmitterSet 设为 Root；它会在 Awake 注册为 Extension。");
                }

                return;
            }

            int lifecycleReceiverCount = CountPoolLifecycleReceivers(set.gameObject, out bool setIsOnlyReceiver);
            if (life == null)
            {
                if (lifecycleReceiverCount != 1 || !setIsOnlyReceiver)
                {
                    errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                               + "：独立 VFX 没有 Entity 时，ESVfxAudioEmitterSet 必须是根节点唯一的 Pool 生命周期接收者。"
                               + "存在其他接收者时请添加 ESGenericLife，并显式绑定唯一 Root/Extension。");
                }

                return;
            }

            if (!ReferenceEquals(life.PoolRootLifecycleComponent, set))
            {
                errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                           + "：独立 VFX 的 ESGenericLife.Pool Root 必须指向 ESVfxAudioEmitterSet。");
            }

            if (lifecycleReceiverCount != 1 || !setIsOnlyReceiver)
            {
                errors.Add(assetPath + " | " + GetHierarchyPath(set.transform)
                           + "：独立 VFX 的 Set 只能是唯一 Pool Root；其他生命周期接收者必须拆为独立 Prefab，"
                           + "或改为 Entity 根下的显式 Extension。");
            }
        }

        private static int CountPoolLifecycleReceivers(GameObject gameObject, out bool setIsOnlyReceiver)
        {
            setIsOnlyReceiver = false;
            if (gameObject == null)
                return 0;

            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            int count = 0;
            ESVfxAudioEmitterSet onlySet = null;
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IESGameObjectPoolLifecycle))
                    continue;

                count++;
                onlySet = components[i] as ESVfxAudioEmitterSet;
            }

            setIsOnlyReceiver = count == 1 && onlySet != null;
            return count;
        }

        private static void ValidateScriptableObjectCommands(List<string> errors)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", AssetSearchRoots);
            Array.Sort(guids, StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int j = 0; j < assets.Length; j++)
                {
                    if (assets[j] is ScriptableObject scriptableObject)
                        ScanLegacyAudioCommand(scriptableObject, path, errors);
                }
            }
        }

        private static void ValidateBuildSceneCommands(List<string> errors)
        {
            var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene sceneEntry = scenes[i];
                if (sceneEntry == null || !sceneEntry.enabled
                    || string.IsNullOrWhiteSpace(sceneEntry.path)
                    || !visitedPaths.Add(sceneEntry.path))
                    continue;

                Scene scene = SceneManager.GetSceneByPath(sceneEntry.path);
                bool openedByAudit = false;
                try
                {
                    if (!scene.isLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(sceneEntry.path, OpenSceneMode.Additive);
                        openedByAudit = true;
                    }

                    if (!scene.isLoaded)
                    {
                        errors.Add(sceneEntry.path + "：无法加载 Build 场景，无法验证 Legacy 音频命令。");
                        continue;
                    }

                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int j = 0; j < roots.Length; j++)
                        ScanLegacyAudioCommands(roots[j].GetComponentsInChildren<MonoBehaviour>(true), sceneEntry.path, errors);
                }
                catch (Exception exception)
                {
                    errors.Add(sceneEntry.path + "：读取 Build 场景中的 Legacy 音频命令失败：" + exception.Message);
                }
                finally
                {
                    if (openedByAudit && scene.IsValid())
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ScanLegacyAudioCommands(MonoBehaviour[] components, string path, List<string> errors)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    ScanLegacyAudioCommand(components[i], path, errors);
            }
        }

        private static void ScanLegacyAudioCommand(UnityEngine.Object owner, string path, List<string> errors)
        {
            if (owner == null)
                return;

            try
            {
                SerializedObject serializedObject = new SerializedObject(owner);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ManagedReference)
                        continue;

                    string typeName = property.managedReferenceFullTypename;
                    if (!IsLegacyAudioCommandTypeName(typeName))
                        continue;

                    errors.Add(path + " | " + owner.name + " | " + property.propertyPath
                               + "：发现已禁用的裸 AudioSource 命令 " + GetLegacyCommandDisplayName(typeName)
                               + "。请替换为“播放/停止受管音频发射器”。");
                }
            }
            catch (Exception exception)
            {
                errors.Add(path + " | " + owner.name + "：读取序列化命令失败，不能安全放行：" + exception.Message);
            }
        }

        private static bool IsLegacyAudioCommandTypeName(string typeName)
        {
            return !string.IsNullOrEmpty(typeName)
                   && (typeName.IndexOf("ESCommand_AudioSource_Play", StringComparison.Ordinal) >= 0
                       || typeName.IndexOf("ESCommand_AudioSource_Stop", StringComparison.Ordinal) >= 0);
        }

        private static string GetLegacyCommandDisplayName(string typeName)
        {
            return typeName.IndexOf("ESCommand_AudioSource_Play", StringComparison.Ordinal) >= 0
                ? "ESCommand_AudioSource_Play"
                : "ESCommand_AudioSource_Stop";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var segments = new List<string>(4);
            for (Transform current = transform; current != null; current = current.parent)
                segments.Add(current.name);
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string BuildErrorReport(string stage, List<string> errors)
        {
            return "[ESAudio 内容审计] " + stage + " 已阻止：发现 " + errors.Count + " 个错误。\n- "
                   + string.Join("\n- ", errors);
        }
    }
}
