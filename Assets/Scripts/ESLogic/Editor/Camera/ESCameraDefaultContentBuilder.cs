using System;
using System.Collections.Generic;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 首批可直接运行的玩家第三人称与载具追逐相机内容。它是明确的内容生产工具：
    /// 生成相机视图定义、Rig Prefab 与两份索引；运行时绝不再创建这些资产。
    /// </summary>
    public static class ESCameraDefaultContentBuilder
    {
        public const string ContentFolder = "Assets/ESNormalAssets/Camera";
        public const string DefinitionFolder = ContentFolder + "/ViewDefinitions";
        public const string RigFolder = ContentFolder + "/Rigs";
        public const string PlayerThirdPersonDefinitionPath = DefinitionFolder + "/PlayerThirdPerson.asset";
        public const string PlayerThirdPersonRigPath = RigFolder + "/PlayerThirdPersonRig.prefab";
        public const string VehicleChaseDefinitionPath = DefinitionFolder + "/VehicleChase.asset";
        public const string VehicleChaseRigPath = RigFolder + "/VehicleChaseRig.prefab";
        public const string DefinitionCatalogPath = ContentFolder + "/ESCameraViewDefinitionCatalog.asset";
        public const string RigCatalogPath = ContentFolder + "/ESCameraRigCatalog.asset";
        public const string BlenderSettingsPath = ContentFolder + "/ESCameraBlenderSettings.asset";
        public const string GlobalPolicyPath = ContentFolder + "/ESCameraGlobalPolicy.asset";

        public const string PlayerThirdPersonDefinitionKey = "player.third_person";
        public const string PlayerThirdPersonRigKey = "player.third_person";
        public const string VehicleChaseDefinitionKey = "vehicle.chase";
        public const string VehicleChaseRigKey = "vehicle.chase";
        public static readonly ESCameraDefinitionReference PlayerThirdPersonDefinition = new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.PlayerThirdPerson, PlayerThirdPersonDefinitionKey);
        public static readonly ESCameraDefinitionReference VehicleChaseDefinition = new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.VehicleChase, VehicleChaseDefinitionKey);

        [MenuItem("【ES】/内容制作/相机/创建或刷新默认玩家与载具相机内容", false, 140)]
        public static void EnsureDefaultPlayerCameraContentMenu()
        {
            if (!EditorUtility.DisplayDialog(
                "创建或刷新默认相机内容",
                "此操作会更新默认 Player/Vehicle ViewDefinition、Rig Prefab、Catalog 和全局策略资产。已有生成内容可能被覆盖，是否继续？",
                "继续",
                "取消"))
                return;

            EnsureDefaultPlayerCameraContent();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[ESCamera] 已创建默认玩家第三人称与载具追逐相机定义、Rig 与索引。", Selection.activeObject);
        }

        [MenuItem("【ES】/内容制作/相机/一键创建并验证默认内容", false, 139)]
        public static void EnsureAndValidateDefaultPlayerCameraContentMenu()
        {
            if (!EditorUtility.DisplayDialog(
                "一键创建并验证默认相机内容",
                "此操作会更新默认 Player/Vehicle 相机资产并立即执行静态验证。已有生成内容可能被覆盖，是否继续？",
                "继续",
                "取消"))
                return;

            EnsureDefaultPlayerCameraContent();
            ValidateDefaultPlayerCameraContentMenu();
        }

        [MenuItem("【ES】/内容制作/相机/验证默认相机内容", false, 141)]
        public static void ValidateDefaultPlayerCameraContentMenu()
        {
            ESCameraGlobalPolicy policy = AssetDatabase.LoadAssetAtPath<ESCameraGlobalPolicy>(GlobalPolicyPath);
            ESCameraViewDefinitionCatalog definitions = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            ESCameraRigCatalog rigs = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(RigCatalogPath);
            if (policy == null || definitions == null || rigs == null)
            {
                Debug.LogError("[ESCamera] 默认相机内容不完整：请先执行“创建或刷新默认玩家与载具相机内容”。");
                return;
            }

            if (!policy.TryValidate(out string policyError))
            {
                Debug.LogError("[ESCamera] 全局策略无效：" + policyError, policy);
                return;
            }

            if (!definitions.IsValid || !rigs.IsValid)
            {
                Debug.LogError("[ESCamera] 默认相机内容验证失败：请检查 ViewDefinition Catalog、Rig Catalog 和 Prefab 组件合同。");
                return;
            }

            Debug.Log("[ESCamera] 默认相机内容验证通过：全局策略、ViewDefinition Catalog 与 Rig Catalog 静态合同有效。", policy);
        }

        [MenuItem("【ES】/内容制作/相机/迁移视图到全局策略", false, 142)]
        public static void MigrateDefinitionsToGlobalPolicyMenu()
        {
            ESCameraGlobalPolicy policy = AssetDatabase.LoadAssetAtPath<ESCameraGlobalPolicy>(GlobalPolicyPath);
            ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            if (policy == null || catalog == null)
            {
                Debug.LogError("[ESCamera] 找不到全局策略或 ViewDefinition Catalog；请先创建默认相机内容。");
                return;
            }

            if (!policy.TryValidate(out string policyError))
            {
                Debug.LogError("[ESCamera] 迁移中止：全局策略无效：" + policyError, policy);
                return;
            }

            List<ESCameraViewDefinition> definitions = new List<ESCameraViewDefinition>();
            if (!catalog.TryCopyDefinitionsForAuthoring(definitions, out string catalogError))
            {
                Debug.LogError("[ESCamera] 迁移中止：" + catalogError, catalog);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "迁移相机全局字段",
                "将把全局输入与避障策略同步到 " + definitions.Count + " 个旧 ViewDefinition 的兼容字段。此操作支持 Undo。",
                "继续迁移",
                "取消"))
                return;

            Undo.RecordObject(policy, "迁移相机全局策略");
            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                Undo.RecordObject(definition, "迁移相机全局策略");
                SyncLegacyDefinitionDefaults(definition, policy);
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ESCamera] 已迁移 " + definitions.Count + " 个 ViewDefinition；旧字段仍保留为兼容缓存。", policy);
        }

        [MenuItem("【ES】/内容制作/相机/报告全局策略一致性", false, 143)]
        public static void ReportGlobalPolicyConsistencyMenu()
        {
            ESCameraGlobalPolicy policy = AssetDatabase.LoadAssetAtPath<ESCameraGlobalPolicy>(GlobalPolicyPath);
            ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            if (policy == null || catalog == null)
            {
                Debug.LogError("[ESCamera] 找不到全局策略或 ViewDefinition Catalog；无法生成一致性报告。");
                return;
            }

            List<ESCameraViewDefinition> definitions = new List<ESCameraViewDefinition>();
            if (!catalog.TryCopyDefinitionsForAuthoring(definitions, out string catalogError))
            {
                Debug.LogError("[ESCamera] 一致性报告中止：" + catalogError, catalog);
                return;
            }

            int mismatchCount = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (definition == null || IsLegacyDefinitionSynchronized(definition, policy))
                    continue;

                mismatchCount++;
                Debug.LogWarning("[ESCamera] ViewDefinition '" + definition.Definition
                    + "' 的隐藏兼容字段与 GlobalPolicy 不一致；建议执行迁移菜单。", definition);
            }

            if (mismatchCount == 0)
                Debug.Log("[ESCamera] 全局策略一致性报告通过：" + definitions.Count + " 个 ViewDefinition 均已同步。", policy);
            else
                Debug.LogWarning("[ESCamera] 全局策略一致性报告发现 " + mismatchCount + " 个不同步的 ViewDefinition（共 " + definitions.Count + " 个）。", policy);
        }

        [MenuItem("【ES】/内容制作/相机/生成相机配置报告", false, 144)]
        public static void GenerateCameraConfigurationReportMenu()
        {
            ESCameraGlobalPolicy policy = AssetDatabase.LoadAssetAtPath<ESCameraGlobalPolicy>(GlobalPolicyPath);
            ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            ESCameraRigCatalog rigs = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(RigCatalogPath);
            StringBuilder report = new StringBuilder(512);
            report.AppendLine("[ESCamera] 配置报告");
            report.AppendLine("全局策略：" + (policy != null && policy.IsValid ? "有效" : "缺失或无效"));
            report.AppendLine("ViewDefinition Catalog：" + (catalog != null && catalog.IsValid ? "有效" : "缺失或无效"));
            report.AppendLine("Rig Catalog：" + (rigs != null && rigs.IsValid ? "有效" : "缺失或无效"));

            int definitionCount = 0;
            int mismatchCount = 0;
            if (catalog != null)
            {
                List<ESCameraViewDefinition> definitions = new List<ESCameraViewDefinition>();
                if (catalog.TryCopyDefinitionsForAuthoring(definitions, out _))
                {
                    definitionCount = definitions.Count;
                    if (policy != null)
                    {
                        for (int i = 0; i < definitions.Count; i++)
                        {
                            if (definitions[i] != null && !IsLegacyDefinitionSynchronized(definitions[i], policy))
                                mismatchCount++;
                        }
                    }
                }
            }

            report.AppendLine("View 数量：" + definitionCount);
            report.AppendLine("Rig 数量：" + (rigs != null ? rigs.EntryCount : 0));
            report.AppendLine("隐藏兼容字段漂移：" + mismatchCount);
            report.AppendLine("下一步：" + (mismatchCount > 0 ? "执行“迁移视图到全局策略”" : "进入 Unity/PlayMode 验收"));
            Debug.Log(report.ToString(), policy != null ? policy : catalog);
        }

        private static bool IsLegacyDefinitionSynchronized(ESCameraViewDefinition definition, ESCameraGlobalPolicy policy)
        {
            return definition.povLookSensitivity == policy.povLookSensitivity
                && definition.freeLookSensitivity == policy.freeLookSensitivity
                && Mathf.Approximately(definition.pointerLookScale, policy.pointerLookScale)
                && definition.maxPovLookRate == policy.maxPovLookRate
                && definition.maxFreeLookRate == policy.maxFreeLookRate
                && definition.invertVerticalLook == policy.invertVerticalLook
                && definition.enableObstruction == policy.enableObstruction
                && definition.obstructionMask.value == policy.obstructionMask.value
                && Mathf.Approximately(definition.obstructionCameraRadius, policy.obstructionCameraRadius)
                && Mathf.Approximately(definition.obstructionMinimumDistance, policy.obstructionMinimumDistance)
                && definition.obstructionMaximumEffort == policy.obstructionMaximumEffort
                && Mathf.Approximately(definition.obstructionDamping, policy.obstructionDamping)
                && Mathf.Approximately(definition.obstructionDampingWhenOccluded, policy.obstructionDampingWhenOccluded);
        }

        public static void EnsureDefaultPlayerCameraContent()
        {
            EnsureFolder(ContentFolder);
            EnsureFolder(DefinitionFolder);
            EnsureFolder(RigFolder);

            ESCameraViewDefinition playerDefinition = LoadOrCreate<ESCameraViewDefinition>(PlayerThirdPersonDefinitionPath);
            ConfigureDefinition(
                playerDefinition,
                "PlayerThirdPerson",
                PlayerThirdPersonDefinition,
                PlayerThirdPersonRigKey,
                60f,
                new Vector2(220f, 0.5f));

            ESCameraViewDefinition vehicleDefinition = LoadOrCreate<ESCameraViewDefinition>(VehicleChaseDefinitionPath);
            ConfigureDefinition(
                vehicleDefinition,
                "VehicleChase",
                VehicleChaseDefinition,
                VehicleChaseRigKey,
                65f,
                new Vector2(200f, 0.45f));

            ESCameraGlobalPolicy globalPolicy = LoadOrCreate<ESCameraGlobalPolicy>(GlobalPolicyPath);
            SyncLegacyDefinitionDefaults(playerDefinition, globalPolicy);
            SyncLegacyDefinitionDefaults(vehicleDefinition, globalPolicy);
            EditorUtility.SetDirty(playerDefinition);
            EditorUtility.SetDirty(vehicleDefinition);
            EditorUtility.SetDirty(globalPolicy);

            GameObject playerRigPrefab = RebuildPlayerThirdPersonRig();
            GameObject vehicleRigPrefab = RebuildVehicleChaseRig();
            ESCameraViewDefinitionCatalog definitionCatalog = LoadOrCreate<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            definitionCatalog.SetDefinitionsForAuthoring(new[] { playerDefinition, vehicleDefinition });
            EditorUtility.SetDirty(definitionCatalog);

            ESCameraRigCatalog rigCatalog = LoadOrCreate<ESCameraRigCatalog>(RigCatalogPath);
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry
                {
                    rigKey = PlayerThirdPersonRigKey,
                    rigPrefab = playerRigPrefab,
                },
                new ESCameraRigCatalog.Entry
                {
                    rigKey = VehicleChaseRigKey,
                    rigPrefab = vehicleRigPrefab,
                },
            });
            EditorUtility.SetDirty(rigCatalog);

            CinemachineBlenderSettings blenderSettings = LoadOrCreate<CinemachineBlenderSettings>(BlenderSettingsPath);
            blenderSettings.m_CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend
                {
                    m_From = CinemachineBlenderSettings.kBlendFromAnyCameraLabel,
                    m_To = PlayerThirdPersonRigKey,
                    m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.2f),
                },
                new CinemachineBlenderSettings.CustomBlend
                {
                    m_From = CinemachineBlenderSettings.kBlendFromAnyCameraLabel,
                    m_To = VehicleChaseRigKey,
                    m_Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.35f),
                },
            };
            EditorUtility.SetDirty(blenderSettings);
            AssetDatabase.SaveAssetIfDirty(playerDefinition);
            AssetDatabase.SaveAssetIfDirty(vehicleDefinition);
            AssetDatabase.SaveAssetIfDirty(globalPolicy);
            AssetDatabase.SaveAssetIfDirty(definitionCatalog);
            AssetDatabase.SaveAssetIfDirty(rigCatalog);
            AssetDatabase.SaveAssetIfDirty(blenderSettings);
            AssetDatabase.Refresh();
        }

        private static void SyncLegacyDefinitionDefaults(ESCameraViewDefinition definition, ESCameraGlobalPolicy policy)
        {
            if (definition == null || policy == null)
                return;

            // 保留旧字段的序列化兼容值；正式运行时由 GlobalPolicy 优先覆盖。
            definition.povLookSensitivity = policy.povLookSensitivity;
            definition.freeLookSensitivity = policy.freeLookSensitivity;
            definition.pointerLookScale = policy.pointerLookScale;
            definition.maxPovLookRate = policy.maxPovLookRate;
            definition.maxFreeLookRate = policy.maxFreeLookRate;
            definition.invertVerticalLook = policy.invertVerticalLook;
            definition.enableObstruction = policy.enableObstruction;
            definition.obstructionMask = policy.obstructionMask;
            definition.obstructionCameraRadius = policy.obstructionCameraRadius;
            definition.obstructionMinimumDistance = policy.obstructionMinimumDistance;
            definition.obstructionMaximumEffort = policy.obstructionMaximumEffort;
            definition.obstructionDamping = policy.obstructionDamping;
            definition.obstructionDampingWhenOccluded = policy.obstructionDampingWhenOccluded;
        }

        public static bool TryLoadDefaultPlayerCameraContent(
            out ESCameraViewDefinitionCatalog definitionCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            return TryLoadDefaultCameraContent(PlayerThirdPersonDefinition, "默认玩家", out definitionCatalog, out rigCatalog, out error);
        }

        public static bool TryLoadDefaultVehicleCameraContent(
            out ESCameraViewDefinitionCatalog definitionCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            return TryLoadDefaultCameraContent(VehicleChaseDefinition, "默认载具", out definitionCatalog, out rigCatalog, out error);
        }

        /// <summary>
        /// 为制作场景建立 MainView。CinemachineBrain 与 SceneBinding 的实际装配只允许留在
        /// Camera 模块内；角色场景工具只接收返回的 Unity Camera 用于移动投影。
        /// </summary>
        public static Camera CreateDefaultMainViewForAuthoring(Transform sceneRoot)
        {
            if (sceneRoot == null)
                throw new ArgumentNullException(nameof(sceneRoot));

            if (!TryLoadDefaultPlayerCameraContent(
                    out ESCameraViewDefinitionCatalog definitionCatalog,
                    out ESCameraRigCatalog rigCatalog,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            // SceneBinding must receive the same project-wide policy asset used by the
            // generated catalogs.  Do not pass an editor-only/uninitialized local variable:
            // a null policy silently reactivates per-definition legacy fields at runtime.
            ESCameraGlobalPolicy globalPolicy = AssetDatabase.LoadAssetAtPath<ESCameraGlobalPolicy>(GlobalPolicyPath);
            string policyError = string.Empty;
            bool policyValid = globalPolicy != null && globalPolicy.TryValidate(out policyError);
            if (!policyValid)
                throw new InvalidOperationException("默认相机全局策略缺失或无效：" + (policyError ?? GlobalPolicyPath));

            GameObject cameraObject = new GameObject("ES Camera System (MainView)");
            Transform rigRoot = null;
            try
            {
                cameraObject.transform.SetParent(sceneRoot, false);
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -5f), Quaternion.identity);
                cameraObject.tag = "MainCamera";

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.05f;
                camera.fieldOfView = 60f;
                camera.clearFlags = CameraClearFlags.Skybox;

                CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
                brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.SmartUpdate;
                brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
                rigRoot = new GameObject("Runtime Rigs (Director Owned)").transform;
                rigRoot.SetParent(sceneRoot, false);
                ESCameraSceneBinding binding = cameraObject.AddComponent<ESCameraSceneBinding>();
                binding.ConfigureForAuthoring(
                    ESCameraViewId.Main.Key,
                    camera,
                    brain,
                    definitionCatalog,
                    rigCatalog,
                    AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(BlenderSettingsPath),
                    rigRoot,
                    globalPolicy);
                return camera;
            }
            catch
            {
                if (rigRoot != null)
                    UnityEngine.Object.DestroyImmediate(rigRoot.gameObject);
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                throw;
            }
        }

        private static GameObject RebuildPlayerThirdPersonRig()
        {
            return RebuildFreeLookRig(
                PlayerThirdPersonRigPath,
                "PlayerThirdPersonRig",
                CinemachineOrbitalTransposer.BindingMode.LockToTargetOnAssign,
                new[]
                {
                    new CinemachineFreeLook.Orbit(3.0f, 2.0f),
                    new CinemachineFreeLook.Orbit(1.75f, 3.5f),
                    new CinemachineFreeLook.Orbit(0.45f, 2.1f),
                });
        }

        private static GameObject RebuildVehicleChaseRig()
        {
            return RebuildFreeLookRig(
                VehicleChaseRigPath,
                "VehicleChaseRig",
                CinemachineOrbitalTransposer.BindingMode.LockToTargetWithWorldUp,
                new[]
                {
                    new CinemachineFreeLook.Orbit(3.8f, 5.0f),
                    new CinemachineFreeLook.Orbit(2.2f, 7.0f),
                    new CinemachineFreeLook.Orbit(0.8f, 5.2f),
                });
        }

        private static GameObject RebuildFreeLookRig(
            string prefabPath,
            string rigName,
            CinemachineOrbitalTransposer.BindingMode bindingMode,
            CinemachineFreeLook.Orbit[] orbits)
        {
            GameObject root = new GameObject(rigName);
            try
            {
                CinemachineFreeLook freeLook = root.AddComponent<CinemachineFreeLook>();
                root.AddComponent<CinemachineCameraOffset>();
                CinemachineCollider obstruction = root.AddComponent<CinemachineCollider>();
                freeLook.Priority = 0;
                freeLook.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
                freeLook.m_XAxis.m_InputAxisName = string.Empty;
                freeLook.m_YAxis.m_InputAxisName = string.Empty;
                freeLook.m_XAxis.m_MaxSpeed = 0f;
                freeLook.m_YAxis.m_MaxSpeed = 0f;
                freeLook.m_BindingMode = bindingMode;
                freeLook.m_Orbits = orbits;
                obstruction.m_CollideAgainst = ESPhysicsLayers.CameraObstacleMask;
                obstruction.m_CameraRadius = 0.2f;
                obstruction.m_MinimumDistanceFromTarget = 0.25f;
                obstruction.m_MaximumEffort = 4;
                obstruction.m_Damping = 0.12f;
                obstruction.m_DampingWhenOccluded = 0.05f;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("保存默认相机 Rig 失败：" + prefabPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureDefinition(
            ESCameraViewDefinition definition,
            string name,
            ESCameraDefinitionReference definitionReference,
            string rigKey,
            float fieldOfView,
            Vector2 freeLookSensitivity)
        {
            bool preserveAuthoredValues = definition != null
                && definition.Definition.IsConfigured
                && !string.IsNullOrWhiteSpace(definition.rigKey);
            definition.name = name;
            definition.SetDefinitionForAuthoring(definitionReference);
            definition.rigKey = rigKey;
            if (!preserveAuthoredValues)
            {
                definition.freeLookSensitivity = freeLookSensitivity;
                definition.povLookSensitivity = new Vector2(220f, 90f);
                definition.pointerLookScale = 0.001f;
                definition.invertVerticalLook = false;
                definition.baseFieldOfView = fieldOfView;
                definition.baseDistanceScale = 1f;
                definition.baseShoulderOffset = Vector3.zero;
                definition.baseShakeAmplitude = 0f;
                definition.enableObstruction = true;
                definition.obstructionMask = ESPhysicsLayers.CameraObstacleMask;
                definition.obstructionCameraRadius = 0.2f;
                definition.obstructionMinimumDistance = 0.25f;
                definition.obstructionMaximumEffort = 4;
                definition.obstructionDamping = 0.12f;
                definition.obstructionDampingWhenOccluded = 0.05f;
            }
            EditorUtility.SetDirty(definition);
        }

        private static bool TryLoadDefaultCameraContent(
            ESCameraDefinitionReference definitionReference,
            string displayName,
            out ESCameraViewDefinitionCatalog definitionCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            definitionCatalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(DefinitionCatalogPath);
            rigCatalog = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(RigCatalogPath);
            CinemachineBlenderSettings blenderSettings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(BlenderSettingsPath);
            if (definitionCatalog == null || rigCatalog == null || blenderSettings == null)
            {
                error = "缺少默认相机 Catalog 或 BlenderSettings。请显式执行“创建或刷新默认玩家与载具相机内容”。";
                return false;
            }

            if (!definitionCatalog.TryResolve(definitionReference, out ESCameraDefinitionRuntimeHandle handle)
                || !definitionCatalog.TryGet(handle, out ESCameraViewDefinition definition)
                || definition == null
                || !rigCatalog.TryGetPrefab(definition.rigKey, out _))
            {
                error = displayName + " Definition/Rig Catalog 不完整。请显式刷新默认玩家与载具相机内容。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            try
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            catch
            {
                if (asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
                    UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("相机内容路径必须位于 Assets 下：" + folder);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
