using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests.UI
{
    public sealed class ESUIWindowCatalogTests
    {
        [Test]
        public void Catalog_ResolvesBuiltInAndStringAliasesToOneDefinition()
        {
            ESUIWindowDefinition definition = CreateDefinition(ESUIWindowId.Inventory, "ui:inventory");
            ESUIWindowCatalog catalog = CreateCatalog(definition);
            try
            {
                Assert.That(catalog.TryBuild(out string error), Is.True, error);
                Assert.That(catalog.TryGet(ESUIWindowIdentity.FromBuiltIn(ESUIWindowId.Inventory), out ESUIWindowDefinition byId), Is.True);
                Assert.That(catalog.TryGet(ESUIWindowIdentity.FromString("ui:inventory"), out ESUIWindowDefinition byKey), Is.True);
                Assert.That(byId, Is.SameAs(definition));
                Assert.That(byKey, Is.SameAs(definition));
                Assert.That(catalog.TryGet(new ESUIWindowIdentity(ESUIWindowId.Inventory, "ui:other"), out _), Is.False);
            }
            finally
            {
                DestroyCatalog(catalog, definition);
            }
        }

        [Test]
        public void Catalog_RejectsDuplicateStringKeys()
        {
            ESUIWindowDefinition first = CreateDefinition(ESUIWindowId.Inventory, "ui:inventory");
            ESUIWindowDefinition second = CreateDefinition(ESUIWindowId.Map, "ui:inventory");
            ESUIWindowCatalog catalog = CreateCatalog(first, second);
            try
            {
                Assert.That(catalog.TryBuild(out string error), Is.False);
                StringAssert.Contains("重复 StringKey", error);
            }
            finally
            {
                DestroyCatalog(catalog, first, second);
            }
        }

        [Test]
        public void Identity_RejectsNoneAndBlankStringKeys()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ESUIWindowIdentity.FromBuiltIn(ESUIWindowId.None));
            Assert.Throws<ArgumentException>(() => ESUIWindowIdentity.FromString("  "));
        }

        [Test]
        public void Definition_RejectsKeepInactiveForMultipleInstances()
        {
            ESUIWindowDefinition definition = CreateDefinition(ESUIWindowId.Inventory, "ui:inventory");
            try
            {
                SetField(definition, "allowMultipleInstances", true);
                SetField(definition, "closePolicy", ESUIWindowClosePolicy.KeepInactive);

                Assert.That(definition.TryValidate(out string error), Is.False);
                StringAssert.Contains("多实例", error);
            }
            finally
            {
                DestroyCatalog(null, definition);
            }
        }

        [Test]
        public void RootShutdown_DestroysRetainedInactiveInstance()
        {
            GameObject rootObject = new GameObject("UI Root Test");
            GameObject windowObject = new GameObject("Retained Window Test");
            ESUIRootCoordinator root = rootObject.AddComponent<ESUIRootCoordinator>();
            CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
            Type instanceType = typeof(ESUIRootCoordinator).Assembly.GetType("ES.ESUIWindowInstance");
            Assert.That(instanceType, Is.Not.Null);

            object instance = Activator.CreateInstance(instanceType, true);
            SetField(instance, "root", root);
            SetField(instance, "gameObject", windowObject);
            SetField(instance, "state", ESUIWindowState.Closed);
            SetField(instance, "isRetainedInactive", true);

            try
            {
                SetField(root, "lifetimeCancellation", lifetimeCancellation);
                AddToPrivateSet(root, "allInstances", instance);
                InvokePrivate(root, "ShutdownLocalState");

                Assert.That(GetField<object>(instance, "gameObject"), Is.Null);
                Assert.That(GetPrivateCollectionCount(root, "allInstances"), Is.Zero);
                Assert.That(lifetimeCancellation.IsCancellationRequested, Is.True);
            }
            finally
            {
                lifetimeCancellation.Dispose();
                if (rootObject != null)
                    UnityEngine.Object.DestroyImmediate(rootObject);
                if (windowObject != null)
                    UnityEngine.Object.DestroyImmediate(windowObject);
            }
        }

        [Test]
        public void Root_DiscardsKeepInactiveWhenCacheBudgetIsZero()
        {
            GameObject rootObject = new GameObject("UI Root Cache Test");
            GameObject windowObject = new GameObject("Retained Window Cache Test");
            ESUIRootCoordinator root = rootObject.AddComponent<ESUIRootCoordinator>();
            ESUIWindowDefinition definition = ScriptableObject.CreateInstance<ESUIWindowDefinition>();
            Type instanceType = typeof(ESUIRootCoordinator).Assembly.GetType("ES.ESUIWindowInstance");
            Assert.That(instanceType, Is.Not.Null);

            object instance = Activator.CreateInstance(instanceType, true);
            SetField(instance, "root", root);
            SetField(instance, "definition", definition);
            SetField(instance, "gameObject", windowObject);
            SetField(instance, "state", ESUIWindowState.Visible);

            try
            {
                SetField(root, "maxRetainedInactiveWindows", 0);
                AddToPrivateSet(root, "allInstances", instance);
                InvokePrivate(root, "CloseInstanceImmediately", instance, ESUIWindowCloseEffect.KeepInactive);

                Assert.That(GetField<object>(instance, "gameObject"), Is.Null);
                Assert.That(GetPrivateCollectionCount(root, "allInstances"), Is.Zero);
                Assert.That(GetPrivateCollectionCount(root, "inactiveSingletons"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(windowObject);
            }
        }

        [Test]
        public void Root_RecreatesLifetimeCancellationAfterShutdownWhenRegistrationRemainsValid()
        {
            GameObject rootObject = new GameObject("UI Root Lifetime Test");
            ESUIRootCoordinator root = rootObject.AddComponent<ESUIRootCoordinator>();
            ESUIWindowModule module = new ESUIWindowModule();
            Dictionary<string, ESUIRootCoordinator> roots = new Dictionary<string, ESUIRootCoordinator>
            {
                { ESUI.MainRootKey, root }
            };
            ESUIRootLease registration = (ESUIRootLease)Activator.CreateInstance(
                typeof(ESUIRootLease),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { module, root, ESUI.MainRootKey, 1 },
                null);

            try
            {
                SetField(module, "roots", roots);
                SetField(root, "rootRegistration", registration);
                InvokePrivate(root, "GetLifetimeCancellationToken");
                CancellationTokenSource first = GetField<CancellationTokenSource>(root, "lifetimeCancellation");

                InvokePrivate(root, "ShutdownLocalState");
                Assert.That(first.IsCancellationRequested, Is.True);
                Assert.That(GetField<CancellationTokenSource>(root, "lifetimeCancellation"), Is.Null);

                InvokePrivate(root, "GetLifetimeCancellationToken");
                CancellationTokenSource second = GetField<CancellationTokenSource>(root, "lifetimeCancellation");
                Assert.That(second, Is.Not.Null);
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.IsCancellationRequested, Is.False);
            }
            finally
            {
                registration.Dispose();
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static ESUIWindowDefinition CreateDefinition(ESUIWindowId id, string key)
        {
            ESUIWindowDefinition definition = ScriptableObject.CreateInstance<ESUIWindowDefinition>();
            ESAssetReferPrefab prefab = new ESAssetReferPrefab();
            prefab.InitializeGeneratedReference("test-prefab-" + key, 0, ESAssetReferKind.Prefab, 0, string.Empty);

            SetField(definition, "builtInId", id);
            SetField(definition, "stringKey", key);
            SetField(definition, "prefab", prefab);
            return definition;
        }

        private static ESUIWindowCatalog CreateCatalog(params ESUIWindowDefinition[] definitions)
        {
            ESUIWindowCatalog catalog = ScriptableObject.CreateInstance<ESUIWindowCatalog>();
            SetField(catalog, "definitions", new List<ESUIWindowDefinition>(definitions));
            return catalog;
        }

        private static void DestroyCatalog(ESUIWindowCatalog catalog, params ESUIWindowDefinition[] definitions)
        {
            if (catalog != null)
                UnityEngine.Object.DestroyImmediate(catalog);

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                    UnityEngine.Object.DestroyImmediate(definitions[i]);
            }
        }

        private static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "找不到测试目标字段：" + name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "找不到测试目标字段：" + name);
            return (T)field.GetValue(target);
        }

        private static void AddToPrivateSet(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "找不到测试目标字段：" + fieldName);
            MethodInfo add = field.FieldType.GetMethod("Add");
            Assert.That(add, Is.Not.Null, "找不到测试集合 Add 方法：" + fieldName);
            Assert.That((bool)add.Invoke(field.GetValue(target), new[] { value }), Is.True);
        }

        private static int GetPrivateCollectionCount(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "找不到测试目标字段：" + fieldName);
            PropertyInfo count = field.FieldType.GetProperty("Count");
            Assert.That(count, Is.Not.Null, "找不到测试集合 Count 属性：" + fieldName);
            return (int)count.GetValue(field.GetValue(target));
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "找不到测试目标方法：" + methodName);
            return method.Invoke(target, arguments);
        }
    }
}
