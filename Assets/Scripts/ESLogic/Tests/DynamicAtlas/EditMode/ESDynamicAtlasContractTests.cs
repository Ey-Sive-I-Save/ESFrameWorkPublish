using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ES.Tests.DynamicAtlas
{
    public sealed class ESDynamicAtlasContractTests
    {
        private sealed class FakeLeaseHost : IESDynamicAtlasLeaseHost
        {
            private readonly HashSet<long> live = new HashSet<long> { 7 };
            public int releaseCount;

            public bool TryResolve(long leaseToken, out ESDynamicAtlasResolved resolved)
            {
                resolved = default;
                return live.Contains(leaseToken);
            }

            public bool TryGetLeaseState(long leaseToken, out ESDynamicAtlasLeaseState state)
            {
                state = live.Contains(leaseToken)
                    ? ESDynamicAtlasLeaseState.Ready
                    : ESDynamicAtlasLeaseState.Invalid;
                return live.Contains(leaseToken);
            }

            public void Release(long leaseToken)
            {
                if (live.Remove(leaseToken))
                    releaseCount++;
            }

            public long Subscribe(long leaseToken, Action changed)
            {
                return live.Contains(leaseToken) ? 11 : 0;
            }

            public void Unsubscribe(long observationToken)
            {
            }

            public void Invalidate(long leaseToken)
            {
                live.Remove(leaseToken);
            }
        }

        [Test]
        public void ContentRevision_IsPartOfStableIdentity()
        {
            var first = new ESDynamicAtlasContentKey("avatar:user_1001", "etag-a");
            var second = new ESDynamicAtlasContentKey("avatar:user_1001", "etag-b");

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.ToString(), Is.EqualTo("avatar:user_1001@etag-a"));
        }

        [Test]
        public void CopiedLease_DisposesUnderlyingTokenOnlyOnce()
        {
            var host = new FakeLeaseHost();
            var lease = new ESDynamicAtlasLease(host, 7);
            ESDynamicAtlasLease copied = lease;

            lease.Dispose();
            copied.Dispose();

            Assert.That(host.releaseCount, Is.EqualTo(1));
            Assert.That(copied.TryResolve(out _), Is.False);
        }

        [Test]
        public void Lease_DefaultStateIsInvalidAndLiveStateIsReady()
        {
            var host = new FakeLeaseHost();
            var lease = new ESDynamicAtlasLease(host, 7);

            Assert.That(default(ESDynamicAtlasLease).State,
                Is.EqualTo(ESDynamicAtlasLeaseState.Invalid));
            Assert.That(lease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Ready));
            Assert.That(lease.TryGetState(out ESDynamicAtlasLeaseState state), Is.True);
            Assert.That(state, Is.EqualTo(ESDynamicAtlasLeaseState.Ready));
        }

        [Test]
        public void Graphic_NarrowApiContentKey_UsesGuidAndLocalFileId()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Narrow API Key Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var refer = new ESAssetReferTexture2D();
            refer.InitializeGeneratedReference(
                "guid-abc", 42, ESAssetReferKind.Texture2D, 0, null);
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                string key = InvokePrivateResult<string>(
                    graphic, "ResolveContentKey", refer);
                Assert.That(key, Is.EqualTo("texture:guid-abc:42"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Graphic_AutoAcquireStatusWriteIsGenerationGuarded()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Auto Acquire Guard Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var current = new CancellationTokenSource();
            var replacement = new CancellationTokenSource();
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "autoAcquireCancellation", current);
                Assert.That(InvokePrivateResult<bool>(
                    graphic, "CanWriteAutoAcquireStatus", current), Is.True);

                current.Cancel();
                Assert.That(InvokePrivateResult<bool>(
                    graphic, "CanWriteAutoAcquireStatus", current), Is.False);

                SetPrivateField(graphic, "autoAcquireCancellation", replacement);
                Assert.That(InvokePrivateResult<bool>(
                    graphic, "CanWriteAutoAcquireStatus", current), Is.False);

                SetPrivateField<object>(graphic, "autoAcquireCancellation", null);
                Assert.That(InvokePrivateResult<bool>(
                    graphic, "CanWriteAutoAcquireStatus", current), Is.False);
            }
            finally
            {
                current.Dispose();
                replacement.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DefaultRequest_UsesFourPixelPaddingAndStraightAlpha()
        {
            ESDynamicAtlasRequest request = ESDynamicAtlasRequest.Default;

            Assert.That(request.padding, Is.EqualTo(4));
            Assert.That(request.alphaMode, Is.EqualTo(ESDynamicAtlasAlphaMode.Straight));
            Assert.That(request.colorSpace, Is.EqualTo(ESDynamicAtlasColorSpace.SRGB));
        }

        [Test]
        public void RequestSanitization_RejectsUnknownSerializedEnumValues()
        {
            var request = new ESDynamicAtlasRequest
            {
                padding = 999,
                colorSpace = (ESDynamicAtlasColorSpace)99,
                alphaMode = (ESDynamicAtlasAlphaMode)99,
                filterMode = (FilterMode)99
            };

            ESDynamicAtlasRequest sanitized = request.Sanitized();

            Assert.That(sanitized.padding, Is.EqualTo(16));
            Assert.That(sanitized.colorSpace, Is.EqualTo(ESDynamicAtlasColorSpace.SRGB));
            Assert.That(sanitized.alphaMode, Is.EqualTo(ESDynamicAtlasAlphaMode.Straight));
            Assert.That(sanitized.filterMode, Is.EqualTo(FilterMode.Bilinear));
        }

        [Test]
        public void Graphic_EditorPreview_DoesNotCreateRuntimeLease()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Graphic Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var preview = new Texture2D(8, 4);
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "editorPreviewTexture", preview);
                InvokePrivate(graphic, "OnValidate");

                Assert.That(graphic.HasContent, Is.False);
                Assert.That(graphic.mainTexture, Is.SameAs(preview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        [Test]
        public void Graphic_EditorSpritePreview_UsesSpriteGeometryAndUvs()
        {
            const string TightSpritePath = "Assets/Sprite Shaders Ultimate/Textures/Shapes/Ring.png";
            var gameObject = new GameObject("ES Dynamic Atlas Sprite Preview Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TightSpritePath);
            var mesh = new VertexHelper();
            try
            {
                Assert.That(sprite, Is.Not.Null, "缺少 Tight Sprite 测试夹具：" + TightSpritePath);
                Vector2[] expectedVertices = sprite.vertices;
                Assert.That(expectedVertices.Length, Is.GreaterThan(4),
                    "测试输入必须生成非矩形的 Tight Sprite 几何。");

                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "editorPreviewSprite", sprite);
                InvokePrivate(graphic, "OnValidate");
                MethodInfo populateMesh = graphic.GetType().GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
                Assert.That(populateMesh, Is.Not.Null, "缺少 OnPopulateMesh(VertexHelper)。");
                populateMesh.Invoke(graphic, new object[] { mesh });

                Assert.That(mesh.currentVertCount, Is.EqualTo(expectedVertices.Length),
                    "编辑器 Sprite 预览应使用 Sprite 几何，而不是退化为矩形。 ");
                Assert.That(mesh.currentIndexCount, Is.EqualTo(sprite.triangles.Length),
                    "编辑器 Sprite 预览应使用 Sprite 三角形，而不是退化为矩形索引。 ");

                Vector2[] expectedUvs = sprite.uv;
                for (int index = 0; index < expectedUvs.Length; index++)
                {
                    UIVertex vertex = default;
                    mesh.PopulateUIVertex(ref vertex, index);
                    Assert.That(vertex.uv0.x, Is.EqualTo(expectedUvs[index].x),
                        $"Sprite UV {index} X 应匹配。 ");
                    Assert.That(vertex.uv0.y, Is.EqualTo(expectedUvs[index].y),
                        $"Sprite UV {index} Y 应匹配。 ");
                }
            }
            finally
            {
                mesh.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Graphic_CustomMaterial_SurvivesClearAndDisable()
        {
            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
                Assert.Ignore("当前 Unity 环境没有 UI/Default Shader。");

            var gameObject = new GameObject("ES Dynamic Atlas Graphic Material Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var custom = new Material(shader);
            var host = new FakeLeaseHost();
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "materialMode", ESDynamicAtlasMaterialMode.Custom);
                SetPrivateField(graphic, "customMaterial", custom);
                InvokePrivate(graphic, "RefreshMaterialMode");

                Assert.That(graphic.material, Is.SameAs(custom));
                InvokePrivate(graphic, "Bind", new ESDynamicAtlasLease(host, 7));
                Assert.That(graphic.material, Is.SameAs(custom));
                graphic.Clear();
                Assert.That(graphic.material, Is.SameAs(custom));
                graphic.enabled = false;
                Assert.That(graphic.material, Is.SameAs(custom));
                graphic.enabled = true;
                Assert.That(graphic.material, Is.SameAs(custom));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(custom);
            }
        }

        [Test]
        public void Graphic_InvalidLease_UsesPlaceholderTexture()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Graphic Invalid Lease Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var placeholder = new Texture2D(2, 2);
            var host = new FakeLeaseHost();
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "placeholderTexture", placeholder);
                SetPrivateField(graphic, "lease", new ESDynamicAtlasLease(host, 7));

                host.Invalidate(7);
                InvokePrivate(graphic, "OnAtlasChanged");

                Assert.That(graphic.mainTexture, Is.SameAs(placeholder));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(placeholder);
            }
        }

        [Test]
        public void Graphic_RuntimeRequests_AreRejectedInEditMode()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Graphic Runtime Gate Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var texture = new Texture2D(4, 4);
            try
            {
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                Assert.Throws<InvalidOperationException>(() =>
                    graphic.CopyAsync(
                            ESDynamicAtlas.UIIcons,
                            new ESDynamicAtlasContentKey("test:edit-mode"),
                            texture,
                            ESDynamicAtlasRequest.Default)
                        .GetAwaiter().GetResult());
                Assert.Throws<InvalidOperationException>(() =>
                    graphic.SetAsync(
                            ESDynamicAtlas.UIIcons,
                            new ESDynamicAtlasContentKey("test:edit-mode-resource"),
                            null,
                            ESDynamicAtlasRequest.Default)
                        .GetAwaiter().GetResult());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Module_RuntimeOperations_AreRejectedInEditMode()
        {
            var module = new ESDynamicAtlasModule();
            ESDynamicAtlasDomainPolicy policy = ESDynamicAtlasDomainPolicy.CreatePlatformDefault();

            Assert.Throws<InvalidOperationException>(() =>
                module.ConfigureDomain(ESDynamicAtlas.UIIcons, policy));
            Assert.Throws<InvalidOperationException>(() =>
                module.OpenDomain(ESDynamicAtlas.UIIcons, policy));
            Assert.Throws<InvalidOperationException>(() =>
                module.CloseDomain(ESDynamicAtlas.UIIcons));
            Assert.Throws<InvalidOperationException>(() =>
                ESDynamicAtlas.CloseDomain(ESDynamicAtlas.UIIcons));
        }

        [Test]
        public void Facade_RuntimeOperations_AreRejectedInEditMode()
        {
            var texture = new Texture2D(4, 4);
            var domain = new ESDynamicAtlasDomainKey("test:facade-edit-mode");
            var content = new ESDynamicAtlasContentKey("texture:facade-edit-mode");
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ESDynamicAtlas.CopyAsync(domain, content, texture, ESDynamicAtlasRequest.Default)
                        .GetAwaiter().GetResult());
                Assert.Throws<InvalidOperationException>(() =>
                    ESDynamicAtlas.LoadAsync(domain, content, null, ESDynamicAtlasRequest.Default)
                        .GetAwaiter().GetResult());
                Assert.Throws<InvalidOperationException>(() =>
                    ESDynamicAtlas.ConfigureDomain(domain, ESDynamicAtlasDomainPolicy.CreatePlatformDefault()));
                Assert.Throws<InvalidOperationException>(() =>
                    ESDynamicAtlas.OpenDomain(domain));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void DynamicAtlasComponents_UseTheStandardAddComponentMenus()
        {
            AddComponentMenu graphicMenu = typeof(ESDynamicAtlasGraphic)
                .GetCustomAttribute<AddComponentMenu>();
            AddComponentMenu ownerMenu = typeof(ESDynamicAtlasDomainOwner)
                .GetCustomAttribute<AddComponentMenu>();

            Assert.That(graphicMenu, Is.Not.Null);
            Assert.That(ownerMenu, Is.Not.Null);
            Assert.That(graphicMenu.componentMenu,
                Is.EqualTo("【ES】/UI/动态图集 Graphic"));
            Assert.That(ownerMenu.componentMenu,
                Is.EqualTo("【ES】/UI/动态图集 Domain Owner"));
        }

        [Test]
        public void DomainOwner_DefaultsToTheActualRuntimePlatformPolicy()
        {
            var gameObject = new GameObject("ES Dynamic Atlas Domain Owner Test", typeof(ESDynamicAtlasDomainOwner));
            try
            {
                ESDynamicAtlasDomainOwner owner = gameObject.GetComponent<ESDynamicAtlasDomainOwner>();
                Assert.That(GetPrivateField<bool>(owner, "usePlatformDefaultPolicy"), Is.True,
                    "默认 Domain Owner 不能在 PC 编辑器序列化时固定移动端页面预算。");

                var module = new ESDynamicAtlasModule();
                Assert.That(module.usePlatformDefaultPolicy, Is.True,
                    "默认动态图集模块必须在运行时按平台选择策略。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ShutdownQuarantine_DiagnosticsAreBoundedAndNativeRetentionIsKept()
        {
            Type runtimeType = typeof(ESDynamicAtlasRuntime);
            Type uploadJobType = runtimeType.GetNestedType("UploadJob", BindingFlags.NonPublic);
            Type pageType = runtimeType.GetNestedType("Page", BindingFlags.NonPublic);
            Assert.That(uploadJobType, Is.Not.Null);
            Assert.That(pageType, Is.Not.Null);

            FieldInfo diagnosticsField = runtimeType.GetField(
                "shutdownQuarantineDiagnostics", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo nativeField = runtimeType.GetField(
                "shutdownQuarantines", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo foldedField = runtimeType.GetField(
                "shutdownQuarantineFoldedCount", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(diagnosticsField, Is.Not.Null);
            Assert.That(nativeField, Is.Not.Null);
            Assert.That(foldedField, Is.Not.Null);

            var diagnostics = (System.Collections.IList)diagnosticsField.GetValue(null);
            var nativeRetentions = (System.Collections.IList)nativeField.GetValue(null);
            int foldedBefore = (int)foldedField.GetValue(null);
            var originalDiagnostics = new List<object>();
            var originalNativeRetentions = new List<object>();
            try
            {
                foreach (object diagnostic in diagnostics)
                    originalDiagnostics.Add(diagnostic);
                foreach (object nativeRetention in nativeRetentions)
                    originalNativeRetentions.Add(nativeRetention);
                diagnostics.Clear();
                nativeRetentions.Clear();

                MethodInfo retain = runtimeType.GetMethod(
                    "RetainShutdownQuarantine", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(retain, Is.Not.Null);

                LogAssert.ignoreFailingMessages = true;
                Type uploadJobListType = typeof(List<>).MakeGenericType(uploadJobType);
                Type pageListType = typeof(List<>).MakeGenericType(pageType);
                for (int index = 0; index < 20; index++)
                {
                    var uploads = (System.Collections.IList)Activator.CreateInstance(uploadJobListType);
                    uploads.Add(Activator.CreateInstance(uploadJobType, true));

                    var pages = (System.Collections.IList)Activator.CreateInstance(pageListType);
                    object page = Activator.CreateInstance(pageType, true);
                    FieldInfo pageIdField = pageType.GetField("id",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Assert.That(pageIdField, Is.Not.Null);
                    pageIdField.SetValue(page, index + 1);
                    pages.Add(page);

                    retain.Invoke(null, new object[]
                    {
                        uploads,
                        pages,
                        null,
                        new InvalidOperationException("测试隔离诊断折叠。")
                    });
                }

                Assert.That(diagnostics.Count, Is.LessThanOrEqualTo(16),
                    "停机隔离诊断记录必须受上限约束。 ");
                Assert.That(nativeRetentions.Count, Is.EqualTo(20),
                    "诊断折叠只能淘汰诊断元数据，不能淘汰未知 GPU 使用中的原生保留对象。 ");

                int expectedFolded = foldedBefore + 4;
                Assert.That((int)foldedField.GetValue(null), Is.EqualTo(expectedFolded),
                    "因上限折叠的数量必须可观测。 ");

                Assert.That(ESDynamicAtlasRuntime.TryCreateShutdownQuarantineSnapshot(
                    out ESDynamicAtlasSnapshot snapshot), Is.True);
                Assert.That(snapshot.shutdownQuarantinedCount, Is.GreaterThan(0));
                Assert.That(snapshot.shutdownQuarantineFoldedCount, Is.GreaterThan(0));
                Assert.That(snapshot.quarantinedPageIds, Does.Contain(20),
                    "折叠后仍应能看到最近保留的隔离 Page 诊断。 ");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                if (diagnostics != null)
                {
                    diagnostics.Clear();
                    for (int index = 0; index < originalDiagnostics.Count; index++)
                        diagnostics.Add(originalDiagnostics[index]);
                }
                if (nativeRetentions != null)
                {
                    nativeRetentions.Clear();
                    for (int index = 0; index < originalNativeRetentions.Count; index++)
                        nativeRetentions.Add(originalNativeRetentions[index]);
                }
                if (foldedField != null)
                    foldedField.SetValue(null, foldedBefore);
            }
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic,
                null, GetParameterTypes(arguments), null);
            Assert.That(method, Is.Not.Null, "缺少测试方法：" + methodName);
            method.Invoke(target, arguments);
        }

        private static T InvokePrivateResult<T>(
            object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic,
                null, GetParameterTypes(arguments), null);
            Assert.That(method, Is.Not.Null, "缺少测试方法：" + methodName);
            return (T)method.Invoke(target, arguments);
        }

        private static Type[] GetParameterTypes(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return Type.EmptyTypes;

            var parameterTypes = new Type[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
                parameterTypes[index] = arguments[index]?.GetType() ?? typeof(object);
            return parameterTypes;
        }

    }
}
