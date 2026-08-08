using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using ES.EditorInternal;

namespace ES.Tests
{
    public sealed class ESGenericProfileTests
    {
        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Pool_InactiveSourcePrefab_ForwardsPoolLifecycleWithoutSnapshot()
        {
            GameObject prefab = new GameObject("ESGenericProfileTests_PoolPrefab");
            prefab.SetActive(false);
            ESGenericProfilePoolRootReceiver rootReceiver = prefab.AddComponent<ESGenericProfilePoolRootReceiver>();
            ESGenericLife life = prefab.AddComponent<ESGenericLife>();
            Assert.That(life.BindPoolRoot(rootReceiver), Is.True);
            prefab.AddComponent<ESGenericProfile>();

            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                ESGenericProfile profile = instance.GetComponent<ESGenericProfile>();
                Assert.That(profile.RuntimeContext.IsPoolSpawned, Is.True);
                Assert.That(profile.RuntimeContext.PoolGeneration, Is.EqualTo(1));
                Assert.That(profile.RuntimeContext.PoolLifecycleActive, Is.True);

                Assert.That(pool.PushToPool(instance), Is.True);
                Assert.That(profile.RuntimeContext.IsPoolSpawned, Is.False);
                Assert.That(profile.RuntimeContext.PoolGeneration, Is.Zero);
                Assert.That(profile.RuntimeContext.PoolLifecycleActive, Is.False);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Debug_EnableAndDisableWriteOnlyConfiguredEdges()
        {
            GameObject root = new GameObject("ESGenericProfileTests_Debug");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                ESGenericProfileDebugSettings debug = new ESGenericProfileDebugSettings();
                AddExtension(profile.Settings, debug);
                SetField(debug, "enabled", true);
                SetField(
                    debug,
                    "eventMask",
                    ESGenericProfileDebugEventMask.Enabled | ESGenericProfileDebugEventMask.Disabled);
                SetField(debug, "logLevel", ESGenericProfileLogLevel.Log);
                SetField(debug, "message", "generic-profile-lifecycle-edge");
                SetField(debug, "developmentOnly", false);

                LogAssert.Expect(LogType.Log, "generic-profile-lifecycle-edge");
                Assert.That(profile.NotifyEnable(), Is.True);
                LogAssert.Expect(LogType.Log, "generic-profile-lifecycle-edge");
                Assert.That(profile.NotifyDisable(), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerSelfDestroy_IsRejectedForEditorRuntimeAndEnabledForPlayerRuntime()
        {
            MethodInfo method = typeof(ESGenericProfile).GetMethod(
                "ShouldDestroyProfileComponent",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { true, true }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { true, false }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { false, false }), Is.False);
        }

        [Test]
        public void DefinitionKey_IsGeneratedAutomaticallyAndBakeApiDoesNotExist()
        {
            GameObject root = new GameObject("ESGenericProfileTests_DefinitionKey");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);
                string definitionKey = profile.Header.DefinitionKey;

                Assert.That(definitionKey, Is.Not.Null.And.Not.Empty);
                Assert.That(profile.Header.DefinitionKey, Is.EqualTo(definitionKey));
                Assert.That(
                    profile.Header.SchemaVersion,
                    Is.Zero,
                    "EnsureDefinitionKey must not silently upgrade SchemaVersion.");
                Assert.That(
                    typeof(ESGenericProfile).GetMethod("BakeRuntimeSnapshot"),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OutdatedHeader_BlocksLifecycleUntilExplicitMigration()
        {
            GameObject root = new GameObject("ESGenericProfileTests_OutdatedLifecycle");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);

                LogAssert.Expect(
                    LogType.Error,
                    "[ESGenericProfile] Header SchemaVersion=0，当前版本=1；必须先完成显式迁移，已阻止 Awake 生命周期转发。");
                Assert.That(profile.NotifyAwake(), Is.False);
                Assert.That(profile.RuntimeContext.AwakeLifecycleCompleted, Is.False);
                Assert.That(profile.Header.SchemaVersion, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(ESProfileHeader.CurrentSchemaVersion + 1)]
        public void UnsupportedHeaderSchema_NeverDispatchesAnyExtensionLifecycle(int schemaVersion)
        {
            GameObject root = new GameObject("ESGenericProfileTests_UnsupportedSchemaLifecycle");
            try
            {
                LogAssert.ignoreFailingMessages = true;
                var events = new List<string>();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("blocked", 0, events));
                SetHeaderSchemaVersion(profile, schemaVersion);

                Assert.That(profile.NotifyAwake(), Is.False);
                Assert.That(profile.NotifyEnable(), Is.False);
                Assert.That(profile.NotifyPoolSpawned(), Is.False);
                Assert.That(profile.NotifyDisable(), Is.True);
                Assert.That(profile.NotifyPoolDespawned(), Is.True);
                Assert.That(profile.NotifyDestroy(), Is.True);
                Assert.That(events, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Destroy_CleansOnlyExtensionsThatActuallyEnteredBeforeSchemaBecameInvalid()
        {
            GameObject root = new GameObject("ESGenericProfileTests_DestroyEnteredOnly");
            try
            {
                var events = new List<string>();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("entered", 0, events));
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("disabled", 10, events, null, false));

                Assert.That(profile.NotifyAwake(), Is.True);
                SetHeaderSchemaVersion(profile, 0);
                Assert.That(profile.NotifyDestroy(), Is.True);
                CollectionAssert.AreEqual(
                    new[] { "entered.Awake", "entered.Destroy" },
                    events);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Migration_UnversionedProfile_CommitsAndSupportsUndo()
        {
            GameObject root = new GameObject("ESGenericProfileTests_MigrateV0");
            try
            {
                Undo.ClearAll();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);

                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { profile },
                        out ESGenericProfileMigrationReport report),
                    Is.True,
                    report?.Error);
                Assert.That(report.Success, Is.True);
                Assert.That(report.Changed, Is.True);
                Assert.That(report.MigratedProfileCount, Is.EqualTo(1));
                Assert.That(profile.Header.SchemaVersion, Is.EqualTo(ESProfileHeader.CurrentSchemaVersion));

                Undo.PerformUndo();
                Assert.That(profile.Header.SchemaVersion, Is.Zero);

                Undo.PerformRedo();
                Assert.That(
                    profile.Header.SchemaVersion,
                    Is.EqualTo(ESProfileHeader.CurrentSchemaVersion));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Migration_MultiTargetFailure_RollsBackEveryProfile()
        {
            GameObject firstRoot = new GameObject("ESGenericProfileTests_MigrationFirst");
            GameObject secondRoot = new GameObject("ESGenericProfileTests_MigrationSecond");
            try
            {
                Undo.ClearAll();
                ESGenericProfile first = firstRoot.AddComponent<ESGenericProfile>();
                ESGenericProfile second = secondRoot.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(first, 0);
                SetHeaderSchemaVersion(second, 0);
                SetHeaderDisplayName(first, "First Original");
                SetHeaderDisplayName(second, "Second Original");
                IESGenericProfileMigrator[] migrators =
                {
                    new ESGenericProfileFailingMigrationStep(second.name)
                };

                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { first, second },
                        migrators,
                        out ESGenericProfileMigrationReport report),
                    Is.False);
                Assert.That(report.Success, Is.False);
                Assert.That(report.Changed, Is.False);
                Assert.That(report.Error, Does.Contain("forced failure"));
                Assert.That(first.Header.SchemaVersion, Is.Zero);
                Assert.That(second.Header.SchemaVersion, Is.Zero);
                Assert.That(first.Header.DisplayName, Is.EqualTo("First Original"));
                Assert.That(second.Header.DisplayName, Is.EqualTo("Second Original"));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void Migration_StructuralFailure_RestoresManagedReferenceOrderFieldsAndObjectReferences()
        {
            GameObject firstRoot = new GameObject("ESGenericProfileTests_StructuralRollbackFirst");
            GameObject secondRoot = new GameObject("ESGenericProfileTests_StructuralRollbackSecond");
            GameObject originalReference = new GameObject("ESGenericProfileTests_OriginalReference");
            GameObject replacementReference = new GameObject("ESGenericProfileTests_ReplacementReference");
            try
            {
                Undo.ClearAll();
                ESGenericProfile first = firstRoot.AddComponent<ESGenericProfile>();
                ESGenericProfile second = secondRoot.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(first, 0);
                SetHeaderSchemaVersion(second, 0);
                var firstExtension = new ESGenericProfileMigrationPayloadA(
                    "first-original",
                    originalReference);
                var secondExtension = new ESGenericProfileMigrationPayloadB(
                    "second-original",
                    originalReference);
                AddExtension(first.Settings, firstExtension);
                AddExtension(first.Settings, secondExtension);

                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { first, second },
                        new IESGenericProfileMigrator[]
                        {
                            new ESGenericProfileStructuralFailingMigrationStep(
                                second.name,
                                replacementReference)
                        },
                        out ESGenericProfileMigrationReport report),
                    Is.False);
                Assert.That(report.Changed, Is.False, report.Error);
                Assert.That(first.Header.SchemaVersion, Is.Zero);
                Assert.That(
                    first.Settings.Extensions[0],
                    Is.TypeOf<ESGenericProfileMigrationPayloadA>());
                Assert.That(
                    first.Settings.Extensions[1],
                    Is.TypeOf<ESGenericProfileMigrationPayloadB>());
                var restoredFirst =
                    (ESGenericProfileMigrationPayloadA)first.Settings.Extensions[0];
                var restoredSecond =
                    (ESGenericProfileMigrationPayloadB)first.Settings.Extensions[1];
                Assert.That(restoredFirst.Marker, Is.EqualTo("first-original"));
                Assert.That(restoredFirst.Reference, Is.SameAs(originalReference));
                Assert.That(restoredSecond.Marker, Is.EqualTo("second-original"));
                Assert.That(restoredSecond.Reference, Is.SameAs(originalReference));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
                Object.DestroyImmediate(originalReference);
                Object.DestroyImmediate(replacementReference);
            }
        }

        [Test]
        public void Migration_MissingChainAndFutureSchema_DoNotMutateProfile()
        {
            GameObject root = new GameObject("ESGenericProfileTests_MigrationRejected");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);
                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { profile },
                        Array.Empty<IESGenericProfileMigrator>(),
                        out ESGenericProfileMigrationReport missingReport),
                    Is.False);
                Assert.That(missingReport.Error, Does.Contain("缺少迁移链"));
                Assert.That(profile.Header.SchemaVersion, Is.Zero);

                SetHeaderSchemaVersion(profile, ESProfileHeader.CurrentSchemaVersion + 1);
                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { profile },
                        out ESGenericProfileMigrationReport futureReport),
                    Is.False);
                Assert.That(futureReport.Error, Does.Contain("未来 SchemaVersion"));
                Assert.That(
                    profile.Header.SchemaVersion,
                    Is.EqualTo(ESProfileHeader.CurrentSchemaVersion + 1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Migration_PostValidationFailure_RollsBackSchemaAndPayload()
        {
            GameObject root = new GameObject("ESGenericProfileTests_MigrationValidationRollback");
            try
            {
                Undo.ClearAll();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);
                SetHeaderDisplayName(profile, "Validation Original");
                AddExtension(profile.Settings, new ESGenericProfileDebugSettings());
                AddExtension(profile.Settings, new ESGenericProfileDebugSettings());

                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { profile },
                        out ESGenericProfileMigrationReport report),
                    Is.False);
                Assert.That(report.Success, Is.False);
                Assert.That(report.Changed, Is.False);
                Assert.That(report.Error, Does.Contain("迁移后校验失败"));
                Assert.That(report.Error, Does.Contain("不能重复添加"));
                Assert.That(profile.Header.SchemaVersion, Is.Zero);
                Assert.That(profile.Header.DisplayName, Is.EqualTo("Validation Original"));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Migration_PreflightException_IsReportedWithoutMutation()
        {
            GameObject root = new GameObject("ESGenericProfileTests_MigrationPreflightException");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                SetHeaderSchemaVersion(profile, 0);

                Assert.That(
                    ESGenericProfileMigrationService.TryMigrate(
                        new[] { profile },
                        new IESGenericProfileMigrator[]
                        {
                            new ESGenericProfileThrowingMigrationDescriptor()
                        },
                        out ESGenericProfileMigrationReport report),
                    Is.False);
                Assert.That(report.Success, Is.False);
                Assert.That(report.Changed, Is.False);
                Assert.That(report.Error, Does.Contain("迁移预检失败"));
                Assert.That(report.Error, Does.Contain("descriptor failure"));
                Assert.That(profile.Header.SchemaVersion, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Migration_EditorModePolicy_RejectsPlayModeOrTransition()
        {
            MethodInfo method = typeof(ESGenericProfileMigrationService).GetMethod(
                "TryValidateEditorMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] blockedArguments = { true, null };
            Assert.That((bool)method.Invoke(null, blockedArguments), Is.False);
            Assert.That(blockedArguments[1], Does.Contain("PlayMode"));

            object[] editModeArguments = { false, null };
            Assert.That((bool)method.Invoke(null, editModeArguments), Is.True);
            Assert.That(editModeArguments[1], Is.Null);
        }

        [Test]
        public void Extensions_DefaultToFeelListAndAutomaticLifecycleEnabled()
        {
            FieldInfo field = typeof(ESGenericProfileSettings).GetField(
                "extensions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetCustomAttribute<SerializeReference>(), Is.Not.Null);
            ESCollectionDrawStyleAttribute drawStyle =
                field.GetCustomAttribute<ESCollectionDrawStyleAttribute>();
            Assert.That(drawStyle, Is.Not.Null);
            Assert.That(drawStyle.Mode, Is.EqualTo(ESCollectionDrawMode.FeelList));
            Assert.That(drawStyle.EnabledMemberName, Is.EqualTo("enabled"));
            Assert.That(drawStyle.AllowDuplicateItems, Is.False);
            Assert.That(drawStyle.EnforceDefaultOrder, Is.True);
            Assert.That(typeof(ESGenericProfileExtensionSettings).IsAbstract, Is.True);

            ESGenericProfileSettings settings = new ESGenericProfileSettings();
            Assert.That(settings.AutoAwake, Is.True);
            Assert.That(settings.AutoEnable, Is.True);
            Assert.That(settings.AutoPoolLifecycle, Is.True);
            Assert.That(settings.Extensions, Is.Not.Null);
            Assert.That(settings.Extensions.Count, Is.Zero);
        }

        [Test]
        public void Extensions_ExposeUnifiedDefaultOrderContract()
        {
            Assert.That(
                new ESGenericProfilePlayerInitializationSettings(),
                Is.AssignableTo<IESCollectionDefaultOrder>());
            Assert.That(
                new ESGenericProfilePlayerInitializationSettings().DefaultOrder,
                Is.EqualTo(ESGenericProfilePlayerInitializationSettings.DefaultOrderValue));
            Assert.That(
                new ESGenericProfileDebugSettings().DefaultOrder,
                Is.EqualTo(ESGenericProfileDebugSettings.DefaultOrderValue));
            Assert.That(
                new ESGenericProfileChildPrefabSettings().DefaultOrder,
                Is.EqualTo(ESGenericProfileChildPrefabSettings.DefaultOrderValue));
            Assert.That(
                ESGenericProfilePlayerInitializationSettings.DefaultOrderValue,
                Is.LessThan(ESGenericProfileDebugSettings.DefaultOrderValue));
            Assert.That(
                ESGenericProfileDebugSettings.DefaultOrderValue,
                Is.LessThan(ESGenericProfileChildPrefabSettings.DefaultOrderValue));
        }

        [Test]
        public void Extensions_ExposeEditableNameTitleWithTypeDefaultFallback()
        {
            var extension = new ESGenericProfileDebugSettings();
            Assert.That(extension, Is.AssignableTo<IESNameTitle>());
            Assert.That(
                extension.NameTitleDefault,
                Is.EqualTo(ESGenericProfileDebugSettings.DefaultNameTitle));
            Assert.That(extension.NameTitle, Is.EqualTo(extension.NameTitleDefault));

            extension.NameTitle = "  自定义调试名称  ";
            Assert.That(extension.NameTitle, Is.EqualTo("自定义调试名称"));

            extension.NameTitle = "   ";
            Assert.That(extension.NameTitle, Is.EqualTo(extension.NameTitleDefault));
        }

        [Test]
        public void Extensions_KeepOnlyGenericQuickQueriesAndSupportPolymorphism()
        {
            var settings = new ESGenericProfileSettings();
            var debug = new ESGenericProfileDebugSettings();
            var child = new ESGenericProfileChildPrefabSettings();
            AddExtension(settings, debug);
            AddExtension(settings, child);

            Assert.That(settings.ExtensionCount, Is.EqualTo(2));
            Assert.That(settings.HasExtension<ESGenericProfileDebugSettings>(), Is.True);
            Assert.That(settings.GetExtension<ESGenericProfileDebugSettings>(), Is.SameAs(debug));
            Assert.That(
                settings.TryGetExtension<ESGenericProfileExtensionSettings>(out var polymorphicResult),
                Is.True);
            Assert.That(polymorphicResult, Is.SameAs(debug));
            Assert.That(settings.GetExtension<ESGenericProfilePlayerInitializationSettings>(), Is.Null);
            Assert.That(
                typeof(ESGenericProfileSettings).GetMethod(
                    "GetExtension",
                    new[] { typeof(System.Type) }),
                Is.Null);
            Assert.That(
                typeof(ESGenericProfileSettings).GetMethod(
                    "GetExtension",
                    new[] { typeof(string) }),
                Is.Null);
        }

        [Test]
        public void Extensions_DuplicateStableTypeId_IsRejectedBeforeLifecycleDispatch()
        {
            GameObject root = new GameObject("ESGenericProfileTests_DuplicateExtension");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(profile.Settings, new ESGenericProfileDebugSettings());
                AddExtension(profile.Settings, new ESGenericProfileDebugSettings());

                List<string> issues = new List<string>();
                Assert.That(profile.ValidateProfile(issues), Is.False);
                Assert.That(issues.Exists(item => item.Contains("不能重复")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Extensions_IllegalStableOrder_IsRejectedBeforeLifecycleDispatch()
        {
            GameObject root = new GameObject("ESGenericProfileTests_ExtensionOrder");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(profile.Settings, new ESGenericProfileChildPrefabSettings());
                AddExtension(profile.Settings, new ESGenericProfileDebugSettings());

                List<string> issues = new List<string>();
                Assert.That(profile.ValidateProfile(issues), Is.False);
                Assert.That(issues.Exists(item => item.Contains("顺序非法")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifecycleStages_AreIndependentIdempotentAndOrdered()
        {
            GameObject root = new GameObject("ESGenericProfileTests_ApplyRemove");
            try
            {
                var events = new List<string>();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(profile.Settings, new ESGenericProfileLifecycleProbe("a", 0, events));
                AddExtension(profile.Settings, new ESGenericProfileLifecycleProbe("b", 10, events));

                Assert.That(profile.NotifyAwake(), Is.True);
                Assert.That(profile.NotifyAwake(), Is.True);
                Assert.That(profile.NotifyEnable(), Is.True);
                Assert.That(profile.NotifyEnable(), Is.True);
                Assert.That(profile.NotifyPoolSpawned(), Is.True);
                Assert.That(profile.NotifyPoolSpawned(), Is.True);
                Assert.That(profile.NotifyPoolDespawned(), Is.True);
                Assert.That(profile.NotifyPoolDespawned(), Is.True);
                Assert.That(profile.RuntimeContext.IsPoolSpawned, Is.False);
                Assert.That(profile.RuntimeContext.PoolGeneration, Is.Zero);
                Assert.That(profile.NotifyDisable(), Is.True);
                Assert.That(profile.NotifyDisable(), Is.True);
                Assert.That(profile.NotifyDestroy(), Is.True);
                Assert.That(profile.NotifyDestroy(), Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "a.Awake", "b.Awake",
                        "a.Enable", "b.Enable",
                        "a.PoolSpawned", "b.PoolSpawned",
                        "b.PoolDespawned", "a.PoolDespawned",
                        "b.Disable", "a.Disable",
                        "b.Destroy", "a.Destroy"
                    },
                    events);
                Assert.That(profile.RuntimeContext.AwakeLifecycleCompleted, Is.True);
                Assert.That(profile.RuntimeContext.EnableLifecycleActive, Is.False);
                Assert.That(profile.RuntimeContext.PoolLifecycleActive, Is.False);
                Assert.That(profile.RuntimeContext.DestroyLifecycleCompleted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AutomaticLifecycle_CanBeDisabledForExternalOwnership()
        {
            GameObject root = new GameObject("ESGenericProfileTests_ExternalLifecycle");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                var events = new List<string>();
                AddExtension(profile.Settings, new ESGenericProfileLifecycleProbe("external", 0, events));
                SetField(profile.Settings, "autoAwake", false);
                SetField(profile.Settings, "autoEnable", false);
                SetField(profile.Settings, "autoPoolLifecycle", false);

                Assert.That(profile.Settings.AutoAwake, Is.False);
                Assert.That(profile.Settings.AutoEnable, Is.False);
                Assert.That(profile.Settings.AutoPoolLifecycle, Is.False);
                Assert.That(profile.NotifyAwake(), Is.True);
                Assert.That(profile.NotifyEnable(), Is.True);
                Assert.That(profile.NotifyPoolSpawned(), Is.True);
                Assert.That(profile.NotifyPoolDespawned(), Is.True);
                Assert.That(profile.RuntimeContext.IsPoolSpawned, Is.False);
                Assert.That(profile.RuntimeContext.PoolGeneration, Is.Zero);
                Assert.That(profile.NotifyDisable(), Is.True);
                Assert.That(profile.NotifyDestroy(), Is.True);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "external.Awake",
                        "external.Enable",
                        "external.PoolSpawned",
                        "external.PoolDespawned",
                        "external.Disable",
                        "external.Destroy"
                    },
                    events);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnableFailure_RollsBackOnlyEnableStageInReverseOrder()
        {
            GameObject root = new GameObject("ESGenericProfileTests_EnableRollback");
            try
            {
                LogAssert.ignoreFailingMessages = true;
                var events = new List<string>();
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                AddExtension(profile.Settings, new ESGenericProfileLifecycleProbe("a", 0, events));
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("b", 10, events, "Enable"));

                Assert.That(profile.NotifyEnable(), Is.False);
                CollectionAssert.AreEqual(
                    new[] { "a.Enable", "b.Enable", "b.Disable", "a.Disable" },
                    events);
                Assert.That(profile.RuntimeContext.EnableLifecycleActive, Is.False);
                Assert.That(profile.RuntimeContext.AwakeLifecycleCompleted, Is.False);
                Assert.That(profile.RuntimeContext.PoolLifecycleActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PoolSpawn_RepeatedCallback_DoesNotAdvanceGenerationTwice()
        {
            GameObject root = new GameObject("ESGenericProfileTests_IdempotentSpawn");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();

                profile.OnPoolSpawned();
                int generation = profile.RuntimeContext.PoolGeneration;
                profile.OnPoolSpawned();

                Assert.That(profile.RuntimeContext.PoolGeneration, Is.EqualTo(generation));
                Assert.That(profile.RuntimeContext.IsPoolSpawned, Is.True);
                Assert.That(profile.RuntimeContext.PoolLifecycleActive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InspectorSections_UseOnlySharedProfileSectionIdentities()
        {
            AssertSectionField("header", ESProfileEditorSections.HeaderId, "身份与版本");
            AssertSectionField("settings", ESProfileEditorSections.SettingsId, "能力配置");
            AssertSectionProperty("ExtensionRuntimeStatus", ESProfileEditorSections.DiagnosticsId, "运行诊断");
            AssertSectionProperty("PoolRuntimeStatus", ESProfileEditorSections.DiagnosticsId, "运行诊断");

            int sectionMemberCount = 0;
            MemberInfo[] members = typeof(ESGenericProfile).GetMembers(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MemberInfo member in members)
            {
                if (member.GetCustomAttribute<ESEditorSectionAttribute>() != null)
                    sectionMemberCount++;
            }

            Assert.That(sectionMemberCount, Is.EqualTo(4));
        }

        [Test]
        public void ChildPrefab_FollowsEnableAndPoolEdgesWithoutDuplicateCreation()
        {
            GameObject root = new GameObject("ESGenericProfileTests_ChildRoot");
            GameObject parentObject = new GameObject("ChildParent");
            parentObject.transform.SetParent(root.transform, false);
            GameObject childPrefab = new GameObject("ChildPrefab");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                ESGenericProfileChildPrefabSettings child = new ESGenericProfileChildPrefabSettings();
                AddExtension(profile.Settings, child);
                SetField(child, "enabled", true);
                SetField(child, "prefab", childPrefab);
                SetField(child, "parent", parentObject.transform);

                Assert.That(profile.NotifyAwake(), Is.True);
                GameObject first = profile.InstantiatedChild;
                Assert.That(first, Is.Not.Null);
                Assert.That(first.transform.parent, Is.SameAs(parentObject.transform));
                Assert.That(first.activeSelf, Is.True);

                Assert.That(profile.NotifyEnable(), Is.True);
                SetField(child, "enabled", false);
                Assert.That(profile.NotifyDisable(), Is.True);
                Assert.That(first.activeSelf, Is.False);
                SetField(child, "enabled", true);
                Assert.That(profile.NotifyEnable(), Is.True);
                Assert.That(profile.InstantiatedChild, Is.SameAs(first));
                Assert.That(first.activeSelf, Is.True);
                Assert.That(parentObject.transform.childCount, Is.EqualTo(1));

                Assert.That(profile.NotifyPoolSpawned(), Is.True);
                Assert.That(profile.NotifyPoolDespawned(), Is.True);
                Assert.That(first.activeSelf, Is.False);
                Assert.That(profile.NotifyPoolSpawned(), Is.True);
                Assert.That(first.activeSelf, Is.True);
                Assert.That(parentObject.transform.childCount, Is.EqualTo(1));

                Assert.That(profile.NotifyDestroy(), Is.True);
                Assert.That(first == null, Is.True, "NotifyDestroy must clean the instantiated child.");
                Object.DestroyImmediate(profile);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(childPrefab);
            }
        }

        [Test]
        public void Validate_RejectsChildParentOutsideProfileRoot()
        {
            GameObject root = new GameObject("ESGenericProfileTests_ValidateRoot");
            GameObject foreignParent = new GameObject("ForeignParent");
            GameObject childPrefab = new GameObject("ChildPrefab");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                ESGenericProfileChildPrefabSettings child = new ESGenericProfileChildPrefabSettings();
                AddExtension(profile.Settings, child);
                SetField(child, "enabled", true);
                SetField(child, "prefab", childPrefab);
                SetField(child, "parent", foreignParent.transform);

                List<string> issues = new List<string>();
                Assert.That(profile.ValidateProfile(issues), Is.False);
                Assert.That(issues.Exists(item => item.Contains("Parent")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(foreignParent);
                Object.DestroyImmediate(childPrefab);
            }
        }

        [Test]
        public void RuntimeContext_ContainsStateButNoConfigurationList()
        {
            FieldInfo[] fields = typeof(ESGenericProfileRuntimeContext).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(ESGenericProfileSettings)));
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(ESGenericProfileExtensionSettings)));
                Assert.That(
                    field.FieldType.IsGenericType
                    && field.FieldType.GetGenericTypeDefinition() == typeof(List<>),
                    Is.False,
                    "RuntimeContext must not duplicate the Extension List.");
            }
        }

        [Test]
        public void RuntimeContext_ExtensionLifecycleTracking_IsLazyAndOneBytePerSlot()
        {
            GameObject root = new GameObject("ESGenericProfileTests_CompactLifecycleTracking");
            try
            {
                ESGenericProfile profile = root.AddComponent<ESGenericProfile>();
                var events = new List<string>();
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("a", 0, events));
                AddExtension(
                    profile.Settings,
                    new ESGenericProfileLifecycleProbe("b", 10, events));

                FieldInfo field = typeof(ESGenericProfileRuntimeContext).GetField(
                    "extensionLifecycleStates",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                Assert.That(field.FieldType, Is.EqualTo(typeof(byte[])));
                Assert.That(field.GetValue(profile.RuntimeContext), Is.Null);

                Assert.That(profile.NotifyAwake(), Is.True);
                byte[] states = field.GetValue(profile.RuntimeContext) as byte[];
                Assert.That(states, Is.Not.Null);
                Assert.That(states.Length, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifecycleApi_ExposesExplicitStagesAndNoApplyRemovePair()
        {
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyAwake"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyEnable"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyDisable"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyPoolSpawned"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyPoolDespawned"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("NotifyDestroy"), Is.Not.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("ApplyExtensions"), Is.Null);
            Assert.That(typeof(ESGenericProfile).GetMethod("RemoveExtensions"), Is.Null);
            Assert.That(
                typeof(ESGenericProfileExtensionSettings).GetMethod(
                    "Apply",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(ESGenericProfileExtensionSettings).GetMethod(
                    "Remove",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing test field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void SetHeaderSchemaVersion(ESGenericProfile profile, int schemaVersion)
        {
            var serializedProfile = new SerializedObject(profile);
            SerializedProperty header = serializedProfile.FindProperty("header");
            Assert.That(header, Is.Not.Null);
            SerializedProperty property = header.FindPropertyRelative("schemaVersion");
            Assert.That(property, Is.Not.Null);
            property.intValue = schemaVersion;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetHeaderDisplayName(ESGenericProfile profile, string displayName)
        {
            var serializedProfile = new SerializedObject(profile);
            SerializedProperty header = serializedProfile.FindProperty("header");
            Assert.That(header, Is.Not.Null);
            SerializedProperty property = header.FindPropertyRelative("displayName");
            Assert.That(property, Is.Not.Null);
            property.stringValue = displayName;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddExtension(
            ESGenericProfileSettings settings,
            ESGenericProfileExtensionSettings extension)
        {
            GetExtensionList(settings).Add(extension);
        }

        private static List<ESGenericProfileExtensionSettings> GetExtensionList(
            ESGenericProfileSettings settings)
        {
            FieldInfo field = typeof(ESGenericProfileSettings).GetField(
                "extensions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            List<ESGenericProfileExtensionSettings> extensions =
                field.GetValue(settings) as List<ESGenericProfileExtensionSettings>;
            Assert.That(extensions, Is.Not.Null);
            return extensions;
        }

        private static void AssertSectionField(string fieldName, string sectionId, string displayName)
        {
            FieldInfo field = typeof(ESGenericProfile).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            AssertSection(field, sectionId, displayName);
        }

        private static void AssertSectionProperty(string propertyName, string sectionId, string displayName)
        {
            PropertyInfo property = typeof(ESGenericProfile).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            AssertSection(property, sectionId, displayName);
        }

        private static void AssertSection(MemberInfo member, string sectionId, string displayName)
        {
            ESEditorSectionAttribute section = member.GetCustomAttribute<ESEditorSectionAttribute>();
            Assert.That(section, Is.Not.Null);
            Assert.That(section.NavigatorId, Is.EqualTo(ESProfileEditorSections.NavigatorId));
            Assert.That(section.SectionId, Is.EqualTo(sectionId));
            Assert.That(section.DisplayName, Is.EqualTo(displayName));
        }
    }

    [System.Serializable]
    public sealed class ESGenericProfileLifecycleProbe : ESGenericProfileExtensionSettings
    {
        private readonly string id;
        private readonly int order;
        private readonly List<string> events;
        private readonly string throwPhase;
        private readonly bool enabled;

        public ESGenericProfileLifecycleProbe(
            string id,
            int order,
            List<string> events,
            string throwPhase = null,
            bool enabled = true)
        {
            this.id = id;
            this.order = order;
            this.events = events;
            this.throwPhase = throwPhase;
            this.enabled = enabled;
        }

        public override string TypeId => "es.tests.generic-profile." + id;
        public override int SchemaVersion => 1;
        public override int SupportedSchemaVersion => 1;
        public override int DefaultOrder => order;
        public override string NameTitleDefault => id;
        public override bool Enabled => enabled;

        protected internal override void OnProfileAwake(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("Awake");
        }

        protected internal override void OnProfileEnable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("Enable");
        }

        protected internal override void OnProfileDisable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("Disable");
        }

        protected internal override void OnProfilePoolSpawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("PoolSpawned");
        }

        protected internal override void OnProfilePoolDespawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("PoolDespawned");
        }

        protected internal override void OnProfileDestroy(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            Record("Destroy");
        }

        private void Record(string phase)
        {
            events.Add(id + "." + phase);
            if (string.Equals(throwPhase, phase, StringComparison.Ordinal))
                throw new InvalidOperationException(id + " failed during " + phase + ".");
        }
    }

    public sealed class ESGenericProfilePoolRootReceiver : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        public void OnPoolSpawned() { }
        public void OnPoolDespawned() { }
    }

    public sealed class ESGenericProfileFailingMigrationStep : IESGenericProfileMigrator
    {
        private readonly string failProfileName;

        public ESGenericProfileFailingMigrationStep(string failProfileName)
        {
            this.failProfileName = failProfileName;
        }

        public string MigrationId => "es.tests.generic-profile.v0-to-v1-failure";
        public int FromVersion => 0;
        public int ToVersion => 1;

        public bool TryMigrate(
            ESGenericProfile profile,
            SerializedObject serializedProfile,
            out string error)
        {
            SerializedProperty header = serializedProfile.FindProperty("header");
            SerializedProperty displayName = header?.FindPropertyRelative("displayName");
            if (displayName == null)
            {
                error = "missing displayName";
                return false;
            }

            displayName.stringValue += " Migrated";
            if (string.Equals(profile.name, failProfileName, StringComparison.Ordinal))
            {
                error = "forced failure";
                return false;
            }

            error = null;
            return true;
        }
    }

    [System.Serializable]
    public sealed class ESGenericProfileMigrationPayloadA : ESGenericProfileExtensionSettings
    {
        [SerializeField] private string marker;
        [SerializeField] private GameObject reference;

        public ESGenericProfileMigrationPayloadA(string marker, GameObject reference)
        {
            this.marker = marker;
            this.reference = reference;
        }

        public string Marker => marker;
        public GameObject Reference => reference;
        public override string TypeId => "es.tests.generic-profile.migration-payload-a";
        public override int SchemaVersion => 1;
        public override int SupportedSchemaVersion => 1;
        public override int DefaultOrder => 0;
        public override string NameTitleDefault => "Migration Payload A";
        public override bool Enabled => true;
    }

    [System.Serializable]
    public sealed class ESGenericProfileMigrationPayloadB : ESGenericProfileExtensionSettings
    {
        [SerializeField] private string marker;
        [SerializeField] private GameObject reference;

        public ESGenericProfileMigrationPayloadB(string marker, GameObject reference)
        {
            this.marker = marker;
            this.reference = reference;
        }

        public string Marker => marker;
        public GameObject Reference => reference;
        public override string TypeId => "es.tests.generic-profile.migration-payload-b";
        public override int SchemaVersion => 1;
        public override int SupportedSchemaVersion => 1;
        public override int DefaultOrder => 0;
        public override string NameTitleDefault => "Migration Payload B";
        public override bool Enabled => true;
    }

    public sealed class ESGenericProfileStructuralFailingMigrationStep
        : IESGenericProfileMigrator
    {
        private readonly string failProfileName;
        private readonly GameObject replacementReference;

        public ESGenericProfileStructuralFailingMigrationStep(
            string failProfileName,
            GameObject replacementReference)
        {
            this.failProfileName = failProfileName;
            this.replacementReference = replacementReference;
        }

        public string MigrationId => "es.tests.generic-profile.structural-v0-to-v1-failure";
        public int FromVersion => 0;
        public int ToVersion => 1;

        public bool TryMigrate(
            ESGenericProfile profile,
            SerializedObject serializedProfile,
            out string error)
        {
            SerializedProperty header = serializedProfile.FindProperty("header");
            SerializedProperty displayName = header?.FindPropertyRelative("displayName");
            if (displayName == null)
            {
                error = "missing displayName";
                return false;
            }

            displayName.stringValue += " Structural Migration";
            if (string.Equals(profile.name, failProfileName, StringComparison.Ordinal))
            {
                error = "forced structural failure";
                return false;
            }

            SerializedProperty settings = serializedProfile.FindProperty("settings");
            SerializedProperty extensions = settings?.FindPropertyRelative("extensions");
            if (extensions == null || extensions.arraySize < 2)
            {
                error = "missing structural migration payload";
                return false;
            }

            extensions.MoveArrayElement(0, 1);
            for (int index = 0; index < extensions.arraySize; index++)
            {
                SerializedProperty element = extensions.GetArrayElementAtIndex(index);
                SerializedProperty marker = element.FindPropertyRelative("marker");
                SerializedProperty reference = element.FindPropertyRelative("reference");
                if (marker == null || reference == null)
                {
                    error = "invalid structural migration payload";
                    return false;
                }

                marker.stringValue += "-migrated";
                reference.objectReferenceValue = replacementReference;
            }

            error = null;
            return true;
        }
    }

    public sealed class ESGenericProfileThrowingMigrationDescriptor : IESGenericProfileMigrator
    {
        public string MigrationId => throw new InvalidOperationException("descriptor failure");
        public int FromVersion => 0;
        public int ToVersion => 1;

        public bool TryMigrate(
            ESGenericProfile profile,
            SerializedObject serializedProfile,
            out string error)
        {
            error = null;
            return true;
        }
    }
}
