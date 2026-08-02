using System;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 首批可直接运行的玩家第三人称与载具追逐相机内容。它是明确的内容生产工具：
    /// 生成 Profile、Rig Prefab 与两份 Catalog；运行时绝不再创建这些资产。
    /// </summary>
    public static class ESCameraDefaultContentBuilder
    {
        public const string ContentFolder = "Assets/ESNormalAssets/Camera";
        public const string ProfileFolder = ContentFolder + "/Profiles";
        public const string RigFolder = ContentFolder + "/Rigs";
        public const string PlayerThirdPersonProfilePath = ProfileFolder + "/PlayerThirdPerson.asset";
        public const string PlayerThirdPersonRigPath = RigFolder + "/PlayerThirdPersonRig.prefab";
        public const string VehicleChaseProfilePath = ProfileFolder + "/VehicleChase.asset";
        public const string VehicleChaseRigPath = RigFolder + "/VehicleChaseRig.prefab";
        public const string ProfileCatalogPath = ContentFolder + "/ESCameraProfileCatalog.asset";
        public const string RigCatalogPath = ContentFolder + "/ESCameraRigCatalog.asset";
        public const string BlenderSettingsPath = ContentFolder + "/ESCameraBlenderSettings.asset";

        public const string PlayerThirdPersonProfileKey = "player.third_person";
        public const string PlayerThirdPersonRigKey = "player.third_person";
        public const string VehicleChaseProfileKey = "vehicle.chase";
        public const string VehicleChaseRigKey = "vehicle.chase";

        [MenuItem("【ES】/内容制作/相机/创建或刷新默认玩家与载具相机内容", false, 140)]
        public static void EnsureDefaultPlayerCameraContentMenu()
        {
            EnsureDefaultPlayerCameraContent();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ESCameraProfileCatalog>(ProfileCatalogPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[ESCamera] 已创建默认玩家第三人称与载具追逐 Profile、Rig 与 Catalog。", Selection.activeObject);
        }

        public static void EnsureDefaultPlayerCameraContent()
        {
            EnsureFolder(ContentFolder);
            EnsureFolder(ProfileFolder);
            EnsureFolder(RigFolder);

            ESCameraProfile playerProfile = LoadOrCreate<ESCameraProfile>(PlayerThirdPersonProfilePath);
            ConfigureProfile(
                playerProfile,
                "PlayerThirdPerson",
                PlayerThirdPersonProfileKey,
                PlayerThirdPersonRigKey,
                60f,
                new Vector2(220f, 0.5f));

            ESCameraProfile vehicleProfile = LoadOrCreate<ESCameraProfile>(VehicleChaseProfilePath);
            ConfigureProfile(
                vehicleProfile,
                "VehicleChase",
                VehicleChaseProfileKey,
                VehicleChaseRigKey,
                65f,
                new Vector2(200f, 0.45f));

            GameObject playerRigPrefab = RebuildPlayerThirdPersonRig();
            GameObject vehicleRigPrefab = RebuildVehicleChaseRig();
            ESCameraProfileCatalog profileCatalog = LoadOrCreate<ESCameraProfileCatalog>(ProfileCatalogPath);
            profileCatalog.SetProfilesForAuthoring(new[] { playerProfile, vehicleProfile });
            EditorUtility.SetDirty(profileCatalog);

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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static bool TryLoadDefaultPlayerCameraContent(
            out ESCameraProfileCatalog profileCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            return TryLoadDefaultCameraContent(PlayerThirdPersonProfileKey, "默认玩家", out profileCatalog, out rigCatalog, out error);
        }

        public static bool TryLoadDefaultVehicleCameraContent(
            out ESCameraProfileCatalog profileCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            return TryLoadDefaultCameraContent(VehicleChaseProfileKey, "默认载具", out profileCatalog, out rigCatalog, out error);
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
                    out ESCameraProfileCatalog profileCatalog,
                    out ESCameraRigCatalog rigCatalog,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            GameObject cameraObject = new GameObject("ES Camera System (MainView)");
            cameraObject.transform.SetParent(sceneRoot, false);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -5f), Quaternion.identity);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.Skybox;

            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            Transform rigRoot = new GameObject("Runtime Rigs (Director Owned)").transform;
            rigRoot.SetParent(cameraObject.transform, false);
            ESCameraSceneBinding binding = cameraObject.AddComponent<ESCameraSceneBinding>();
            binding.ConfigureForAuthoring(
                ESCameraViewId.Main.Key,
                camera,
                brain,
                profileCatalog,
                rigCatalog,
                AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(BlenderSettingsPath),
                rigRoot);
            return camera;
        }

        private static GameObject RebuildPlayerThirdPersonRig()
        {
            return RebuildFreeLookRig(
                PlayerThirdPersonRigPath,
                "PlayerThirdPersonRig",
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
                new[]
                {
                    new CinemachineFreeLook.Orbit(3.8f, 5.0f),
                    new CinemachineFreeLook.Orbit(2.2f, 7.0f),
                    new CinemachineFreeLook.Orbit(0.8f, 5.2f),
                });
        }

        private static GameObject RebuildFreeLookRig(string prefabPath, string rigName, CinemachineFreeLook.Orbit[] orbits)
        {
            GameObject root = new GameObject(rigName);
            try
            {
                CinemachineFreeLook freeLook = root.AddComponent<CinemachineFreeLook>();
                root.AddComponent<CinemachineCameraOffset>();
                CinemachineCollider obstruction = root.AddComponent<CinemachineCollider>();
                freeLook.Priority = 0;
                freeLook.m_XAxis.m_InputAxisName = string.Empty;
                freeLook.m_YAxis.m_InputAxisName = string.Empty;
                freeLook.m_XAxis.m_MaxSpeed = 0f;
                freeLook.m_YAxis.m_MaxSpeed = 0f;
                freeLook.m_BindingMode = CinemachineOrbitalTransposer.BindingMode.LockToTargetWithWorldUp;
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

        private static void ConfigureProfile(
            ESCameraProfile profile,
            string name,
            string profileKey,
            string rigKey,
            float fieldOfView,
            Vector2 freeLookSensitivity)
        {
            profile.name = name;
            profile.profileKey = profileKey;
            profile.rigKey = rigKey;
            profile.freeLookSensitivity = freeLookSensitivity;
            profile.povLookSensitivity = new Vector2(220f, 90f);
            profile.invertVerticalLook = false;
            profile.baseFieldOfView = fieldOfView;
            profile.baseDistanceScale = 1f;
            profile.baseShoulderOffset = Vector3.zero;
            profile.baseShakeAmplitude = 0f;
            profile.enableObstruction = true;
            profile.obstructionMask = ESPhysicsLayers.CameraObstacleMask;
            profile.obstructionCameraRadius = 0.2f;
            profile.obstructionMinimumDistance = 0.25f;
            profile.obstructionMaximumEffort = 4;
            profile.obstructionDamping = 0.12f;
            profile.obstructionDampingWhenOccluded = 0.05f;
            EditorUtility.SetDirty(profile);
        }

        private static bool TryLoadDefaultCameraContent(
            string profileKey,
            string displayName,
            out ESCameraProfileCatalog profileCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            profileCatalog = AssetDatabase.LoadAssetAtPath<ESCameraProfileCatalog>(ProfileCatalogPath);
            rigCatalog = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(RigCatalogPath);
            CinemachineBlenderSettings blenderSettings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(BlenderSettingsPath);
            if (profileCatalog == null || rigCatalog == null || blenderSettings == null)
            {
                error = "缺少默认相机 Catalog 或 BlenderSettings。请显式执行“创建或刷新默认玩家与载具相机内容”。";
                return false;
            }

            if (!profileCatalog.TryGet(profileKey, out ESCameraProfile profile)
                || profile == null
                || !rigCatalog.TryGetPrefab(profile.rigKey, out _))
            {
                error = displayName + " Profile/Rig Catalog 不完整。请显式刷新默认玩家与载具相机内容。";
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
            AssetDatabase.CreateAsset(asset, path);
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
