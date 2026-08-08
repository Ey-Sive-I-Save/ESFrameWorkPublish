using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
                    Assert.That(vertex.uv0, Is.EqualTo(expectedUvs[index]));
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
                Is.EqualTo("【ES】/场景与对象/动态图集 Graphic"));
            Assert.That(ownerMenu.componentMenu,
                Is.EqualTo("【ES】/场景与对象/动态图集 Domain Owner"));
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
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "缺少测试方法：" + methodName);
            method.Invoke(target, arguments);
        }

    }
}
