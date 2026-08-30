using NUnit.Framework;
using Cinemachine;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCameraDefinitionCatalogTests
    {
        private ESCameraViewDefinitionCatalog catalog;
        private ESCameraRigCatalog rigCatalog;
        private GameObject rigPrefab;
        private ESCameraViewDefinition first;
        private ESCameraViewDefinition second;
        private ESCameraGlobalPolicy globalPolicy;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<ESCameraViewDefinitionCatalog>();
            rigCatalog = ScriptableObject.CreateInstance<ESCameraRigCatalog>();
            rigPrefab = new GameObject("Camera Rig Prefab");
            rigPrefab.AddComponent<CinemachineVirtualCamera>();
            first = CreateDefinition("First", ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person", "player.rig");
            second = CreateDefinition("Second", ESCameraDefinitionEnumKey.VehicleChase, "vehicle.chase", "vehicle.rig");
            globalPolicy = ScriptableObject.CreateInstance<ESCameraGlobalPolicy>();
            globalPolicy.obstructionMask = 1;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(rigPrefab);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(rigCatalog);
            Object.DestroyImmediate(globalPolicy);
        }

        [Test]
        public void GlobalPolicy_DefaultValuesAreValid()
        {
            Assert.That(globalPolicy.IsValid, Is.True);
        }

        [Test]
        public void GlobalPolicy_InvalidInputRateIsRejected()
        {
            globalPolicy.maxPovLookRate = new Vector2(0f, 180f);

            Assert.That(globalPolicy.IsValid, Is.False);
        }

        [Test]
        public void GlobalPolicy_InvalidObstructionBudgetIsRejected()
        {
            globalPolicy.obstructionMaximumEffort = 0;

            Assert.That(globalPolicy.IsValid, Is.False);
        }

        [Test]
        public void GlobalPolicy_DisabledObstructionAllowsEmptyMask()
        {
            globalPolicy.enableObstruction = false;
            globalPolicy.obstructionMask = 0;

            Assert.That(globalPolicy.IsValid, Is.True);
        }

        [Test]
        public void DuplicateDefinition_IsHardFailure()
        {
            ESCameraViewDefinition duplicate = CreateDefinition("Duplicate", ESCameraDefinitionEnumKey.PlayerThirdPerson, "player.third_person", "other.rig");
            try
            {
                catalog.SetDefinitionsForAuthoring(new[] { first, duplicate });

                Assert.That(catalog.IsValid, Is.False);
                Assert.That(catalog.BuildError, Does.Contain("构建失败"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void AliasConflict_IsHardFailure()
        {
            ESCameraViewDefinition conflict = CreateDefinition("Alias Conflict", ESCameraDefinitionEnumKey.PlayerThirdPerson, "different.player", "other.rig");
            try
            {
                catalog.SetDefinitionsForAuthoring(new[] { first, conflict });

                Assert.That(catalog.IsValid, Is.False);
                Assert.That(catalog.BuildError, Does.Contain("构建失败"));
            }
            finally
            {
                Object.DestroyImmediate(conflict);
            }
        }

        [Test]
        public void Rebuild_RejectsPreviousRuntimeHandle()
        {
            catalog.SetDefinitionsForAuthoring(new[] { first, second });
            Assert.That(catalog.TryResolve(first.Definition, out ESCameraDefinitionRuntimeHandle oldHandle), Is.True);

            catalog.SetDefinitionsForAuthoring(new[] { second, first });

            Assert.That(catalog.TryGet(oldHandle, out _), Is.False);
            Assert.That(catalog.TryResolve(first.Definition, out ESCameraDefinitionRuntimeHandle currentHandle), Is.True);
            Assert.That(currentHandle.runtimeKey, Is.EqualTo(oldHandle.runtimeKey));
            Assert.That(currentHandle.catalogGeneration, Is.Not.EqualTo(oldHandle.catalogGeneration));
        }

        [Test]
        public void DuplicateRig_IsHardFailure()
        {
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(rigCatalog.IsValid, Is.False);
            Assert.That(rigCatalog.BuildError, Does.Contain("重复 RigKey"));
        }

        [Test]
        public void RigWithoutRootVirtualCamera_IsHardFailure()
        {
            GameObject invalidRig = new GameObject("Invalid Camera Rig");
            try
            {
                rigCatalog.SetEntriesForAuthoring(new[]
                {
                    new ESCameraRigCatalog.Entry { rigKey = "invalid.rig", rigPrefab = invalidRig },
                });

                Assert.That(rigCatalog.IsValid, Is.False);
                Assert.That(rigCatalog.BuildError, Does.Contain("必须且只能挂载一个"));
            }
            finally
            {
                Object.DestroyImmediate(invalidRig);
            }
        }

        [Test]
        public void RigWithMultipleRootVirtualCameras_IsHardFailure()
        {
            GameObject invalidRig = new GameObject("Ambiguous Camera Rig");
            try
            {
                invalidRig.AddComponent<CinemachineVirtualCamera>();
                invalidRig.AddComponent<CinemachineVirtualCamera>();
                rigCatalog.SetEntriesForAuthoring(new[]
                {
                    new ESCameraRigCatalog.Entry { rigKey = "ambiguous.rig", rigPrefab = invalidRig },
                });

                Assert.That(rigCatalog.IsValid, Is.False);
                Assert.That(rigCatalog.BuildError, Does.Contain("必须且只能挂载一个"));
            }
            finally
            {
                Object.DestroyImmediate(invalidRig);
            }
        }

        [Test]
        public void DestroyedRigPrefab_IsNotReturnedByCatalog()
        {
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });
            Assert.That(rigCatalog.IsValid, Is.True);

            Object.DestroyImmediate(rigPrefab);

            Assert.That(rigCatalog.TryGetPrefab("player.rig", out GameObject resolved), Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void InvalidRebuild_ClearsPreviousRigIndex()
        {
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });
            Assert.That(rigCatalog.IsValid, Is.True);
            Assert.That(rigCatalog.TryGetPrefab("player.rig", out _), Is.True);

            GameObject invalidRig = new GameObject("Invalid Rebuild Rig");
            try
            {
                rigCatalog.SetEntriesForAuthoring(new[]
                {
                    new ESCameraRigCatalog.Entry { rigKey = "invalid.rig", rigPrefab = invalidRig },
                });

                Assert.That(rigCatalog.IsValid, Is.False);
                Assert.That(rigCatalog.TryGetPrefab("player.rig", out GameObject previous), Is.False);
                Assert.That(previous, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(invalidRig);
            }
        }

        [Test]
        public void MissingDefinitionRig_IsHardFailure()
        {
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "other.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, out string error), Is.False);
            Assert.That(error, Does.Contain("不存在的 RigKey"));
        }

        [Test]
        public void InvalidLensTuning_IsHardFailure()
        {
            first.baseFieldOfView = float.NaN;
            catalog.SetDefinitionsForAuthoring(new[] { first });

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.BuildError, Does.Contain("非法镜头参数"));
        }

        [Test]
        public void InvalidObstructionBudget_IsHardFailure()
        {
            first.obstructionMaximumEffort = 0;
            catalog.SetDefinitionsForAuthoring(new[] { first });

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.BuildError, Does.Contain("非法镜头参数"));
        }

        [Test]
        public void InvalidLookRate_IsHardFailure()
        {
            first.maxPovLookRate = new Vector2(0f, 180f);
            catalog.SetDefinitionsForAuthoring(new[] { first });

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.BuildError, Does.Contain("非法镜头参数"));
        }

        [Test]
        public void EnabledObstructionWithoutMask_IsHardFailure()
        {
            first.obstructionMask = 0;
            catalog.SetDefinitionsForAuthoring(new[] { first });

            Assert.That(catalog.IsValid, Is.False);
            Assert.That(catalog.BuildError, Does.Contain("非法镜头参数"));
        }

        [Test]
        public void EnabledObstructionWithoutCollider_IsHardFailure()
        {
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, out string error), Is.False);
            Assert.That(error, Does.Contain("缺少 CinemachineCollider"));
        }

        [Test]
        public void GlobalPolicyObstruction_DoesNotTrustHiddenViewFallback()
        {
            first.enableObstruction = false;
            globalPolicy.enableObstruction = true;
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, globalPolicy, out string error), Is.False);
            Assert.That(error, Does.Contain("缺少 CinemachineCollider"));
        }

        [Test]
        public void EnabledObstructionWithMultipleColliders_IsHardFailure()
        {
            rigPrefab.AddComponent<CinemachineCollider>();
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, out string error), Is.False);
            Assert.That(error, Does.Contain("必须且只能包含一个 CinemachineCollider"));
        }

        [Test]
        public void DisabledObstruction_DoesNotRequireColliderOrMask()
        {
            first.enableObstruction = false;
            first.obstructionMask = 0;
            catalog.SetDefinitionsForAuthoring(new[] { first });
            rigCatalog.SetEntriesForAuthoring(new[]
            {
                new ESCameraRigCatalog.Entry { rigKey = "player.rig", rigPrefab = rigPrefab },
            });

            Assert.That(catalog.IsValid, Is.True);
            Assert.That(catalog.TryValidateRigDependencies(rigCatalog, out string error), Is.True, error);
        }

        [Test]
        public void DefaultPlayerAndVehiclePrefabs_UseConfiguredDefinitionReferences()
        {
            EntityCharacterIdentity player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ESNormalAssets/CharacterVariants/大黑塔.prefab")
                .GetComponent<EntityCharacterIdentity>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.defaultCameraDefinition.IsConfigured, Is.True);
            ESCameraViewDefinition playerDefinition = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinition>(
                "Assets/ESNormalAssets/Camera/ViewDefinitions/PlayerThirdPerson.asset");
            Assert.That(playerDefinition, Is.Not.Null);
            Assert.That(playerDefinition.IsContentValid, Is.True);

            string[] vehiclePaths =
            {
                "Assets/ESNormalAssets/VehiclePrototypes/BlockCar.prefab",
                "Assets/ESNormalAssets/VehiclePrototypes/BlockBicycle.prefab",
                "Assets/ESNormalAssets/VehiclePrototypes/BlockHelicopter.prefab",
            };
            for (int i = 0; i < vehiclePaths.Length; i++)
            {
                VehicleController vehicle = AssetDatabase.LoadAssetAtPath<GameObject>(vehiclePaths[i]).GetComponent<VehicleController>();
                Assert.That(vehicle, Is.Not.Null, vehiclePaths[i]);
                Assert.That(vehicle.driverCameraDefinition.IsConfigured, Is.True, vehiclePaths[i]);
            }
            ESCameraViewDefinition vehicleDefinition = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinition>(
                "Assets/ESNormalAssets/Camera/ViewDefinitions/VehicleChase.asset");
            Assert.That(vehicleDefinition, Is.Not.Null);
            Assert.That(vehicleDefinition.IsContentValid, Is.True);
        }

        [Test]
        public void FormalRigs_HaveExactlyOneColliderForEnabledObstruction()
        {
            string[] rigPaths =
            {
                "Assets/ESNormalAssets/Camera/Rigs/PlayerThirdPersonRig.prefab",
                "Assets/ESNormalAssets/Camera/Rigs/VehicleChaseRig.prefab",
            };
            for (int i = 0; i < rigPaths.Length; i++)
            {
                GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPaths[i]);
                Assert.That(rig, Is.Not.Null, rigPaths[i]);
                CinemachineCollider[] colliders = rig.GetComponentsInChildren<CinemachineCollider>(true);
                Assert.That(colliders, Has.Length.EqualTo(1), rigPaths[i]);
                Assert.That(colliders[0].m_CollideAgainst.value, Is.Not.EqualTo(0), rigPaths[i]);
                Assert.That(colliders[0].m_CameraRadius, Is.GreaterThan(0f), rigPaths[i]);
                Assert.That(colliders[0].m_MinimumDistanceFromTarget, Is.GreaterThan(0f), rigPaths[i]);
            }
        }

        [Test]
        public void FormalCameraScene_ContainsBlendAndIndependentRigRootBindings()
        {
            const string scenePath = "Assets/Scenes/Tests/ESPlayerControllerTest.unity";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenePath);
            Assert.That(File.Exists(absolutePath), Is.True, scenePath);
            string yaml = File.ReadAllText(absolutePath);

            Assert.That(yaml, Does.Contain("blenderSettings:"), scenePath);
            Assert.That(yaml, Does.Contain("defaultBlend:"), scenePath);
            Assert.That(yaml, Does.Contain("rigRoot:"), scenePath);
        }

        [Test]
        public void FormalBlendSettings_CoversPlayerAndVehicleDefinitions()
        {
            const string settingsPath = "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, settingsPath);
            Assert.That(File.Exists(absolutePath), Is.True, settingsPath);
            string yaml = File.ReadAllText(absolutePath);

            Assert.That(yaml, Does.Contain("m_To: player.third_person"), settingsPath);
            Assert.That(yaml, Does.Contain("m_To: vehicle.chase"), settingsPath);
            Assert.That(yaml, Does.Contain("m_Time: 0.2"), settingsPath);
            Assert.That(yaml, Does.Contain("m_Time: 0.35"), settingsPath);
        }

        [Test]
        public void FormalBlendSettings_MapsEveryDefinitionCatalogEntry()
        {
            const string catalogPath = "Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset";
            ESCameraViewDefinitionCatalog formalCatalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(catalogPath);
            Assert.That(formalCatalog, Is.Not.Null, catalogPath);
            var definitions = new System.Collections.Generic.List<ESCameraViewDefinition>();
            Assert.That(formalCatalog.TryCopyDefinitionsForAuthoring(definitions, out string catalogError), Is.True, catalogError);

            const string settingsPath = "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, settingsPath);
            string yaml = File.ReadAllText(absolutePath);
            Assert.That(yaml, Does.Contain("m_CustomBlends:"), settingsPath);
            MatchCollection targetMatches = Regex.Matches(yaml, @"m_To:\s*([^\r\n]+)");
            var targets = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < targetMatches.Count; i++)
                targets.Add(targetMatches[i].Groups[1].Value.Trim());
            Assert.That(targets.Count, Is.EqualTo(definitions.Count), settingsPath);
            for (int i = 0; i < definitions.Count; i++)
            {
                Assert.That(definitions[i], Is.Not.Null);
                Assert.That(targets.Contains(definitions[i].Definition.stringKey), Is.True, definitions[i].Definition.stringKey);
            }
        }

        [Test]
        public void FormalBlendSettings_HasValidStyleAndPositiveDuration()
        {
            const string settingsPath = "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, settingsPath);
            string yaml = File.ReadAllText(absolutePath);
            MatchCollection styles = Regex.Matches(yaml, @"m_Style:\s*(-?\d+)");
            MatchCollection durations = Regex.Matches(yaml, @"m_Time:\s*([0-9]+(?:\.[0-9]+)?)");

            Assert.That(styles.Count, Is.GreaterThan(0), settingsPath);
            Assert.That(styles.Count, Is.EqualTo(durations.Count), settingsPath);
            for (int i = 0; i < styles.Count; i++)
            {
                int style = int.Parse(styles[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                float duration = float.Parse(durations[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(style, Is.InRange(0, 6), settingsPath);
                Assert.That(duration, Is.GreaterThan(0f), settingsPath);
            }
        }

        [Test]
        public void FormalBlendSettings_HasUniqueFromToPairs()
        {
            const string settingsPath = "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, settingsPath);
            string yaml = File.ReadAllText(absolutePath);
            MatchCollection fromMatches = Regex.Matches(yaml, @"m_From:\s*'([^']+)'");
            MatchCollection toMatches = Regex.Matches(yaml, @"m_To:\s*([^\r\n]+)");
            Assert.That(fromMatches.Count, Is.GreaterThan(0), settingsPath);
            Assert.That(fromMatches.Count, Is.EqualTo(toMatches.Count), settingsPath);

            var pairs = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < fromMatches.Count; i++)
            {
                string from = fromMatches[i].Groups[1].Value.Trim();
                string to = toMatches[i].Groups[1].Value.Trim();
                Assert.That(from, Is.Not.Empty, settingsPath);
                Assert.That(to, Is.Not.Empty, settingsPath);
                Assert.That(pairs.Add(from + "\u001f" + to), Is.True, "重复 Blend 来源/目标：" + from + " -> " + to);
            }
        }

        [Test]
        public void FormalCameraScene_ReferencesCurrentCameraAssets()
        {
            const string scenePath = "Assets/Scenes/Tests/ESPlayerControllerTest.unity";
            string sceneAbsolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenePath);
            string sceneYaml = File.ReadAllText(sceneAbsolutePath);
            string[] assetPaths =
            {
                "Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset",
                "Assets/ESNormalAssets/Camera/ESCameraRigCatalog.asset",
                "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset",
            };
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string metaPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPaths[i] + ".meta");
                string meta = File.ReadAllText(metaPath);
                Match guidMatch = Regex.Match(meta, @"guid:\s*([0-9a-fA-F]{32})");
                Assert.That(guidMatch.Success, Is.True, assetPaths[i]);
                Assert.That(sceneYaml, Does.Contain("guid: " + guidMatch.Groups[1].Value), assetPaths[i]);
            }
        }

        [Test]
        public void FormalCameraAssets_HaveExpectedTypesAndPrefabPaths()
        {
            ESCameraViewDefinitionCatalog definitionCatalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(
                "Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset");
            ESCameraRigCatalog rigCatalog = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(
                "Assets/ESNormalAssets/Camera/ESCameraRigCatalog.asset");
            CinemachineBlenderSettings blenderSettings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(
                "Assets/ESNormalAssets/Camera/ESCameraBlenderSettings.asset");
            Assert.That(definitionCatalog, Is.Not.Null);
            Assert.That(rigCatalog, Is.Not.Null);
            Assert.That(blenderSettings, Is.Not.Null);

            for (int i = 0; i < rigCatalog.EntryCount; i++)
            {
                Assert.That(rigCatalog.TryGetEntry(i, out string rigKey, out GameObject prefab), Is.True);
                Assert.That(rigKey, Is.Not.Empty);
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                Assert.That(prefabPath, Does.EndWith(".prefab"), rigKey);
                Assert.That(prefab.GetComponents<CinemachineVirtualCameraBase>(), Has.Length.EqualTo(1), rigKey);
            }
        }

        [Test]
        public void FormalDefinitions_ResolveEveryRigKeyThroughCatalog()
        {
            ESCameraViewDefinitionCatalog definitionCatalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(
                "Assets/ESNormalAssets/Camera/ESCameraViewDefinitionCatalog.asset");
            ESCameraRigCatalog rigCatalog = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(
                "Assets/ESNormalAssets/Camera/ESCameraRigCatalog.asset");
            var definitions = new System.Collections.Generic.List<ESCameraViewDefinition>();
            Assert.That(definitionCatalog.TryCopyDefinitionsForAuthoring(definitions, out string catalogError), Is.True, catalogError);
            Assert.That(rigCatalog.IsValid, Is.True, rigCatalog.BuildError);

            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                Assert.That(definition, Is.Not.Null);
                Assert.That(rigCatalog.TryGetPrefab(definition.rigKey, out GameObject prefab), Is.True, definition.Definition.stringKey);
                Assert.That(prefab, Is.Not.Null, definition.Definition.stringKey);
            }
        }

        private static ESCameraViewDefinition CreateDefinition(
            string name,
            ESCameraDefinitionEnumKey enumKey,
            string stringKey,
            string rigKey)
        {
            ESCameraViewDefinition definition = ScriptableObject.CreateInstance<ESCameraViewDefinition>();
            definition.name = name;
            definition.SetDefinitionForAuthoring(new ESCameraDefinitionReference(enumKey, stringKey));
            definition.rigKey = rigKey;
            return definition;
        }
    }
}
