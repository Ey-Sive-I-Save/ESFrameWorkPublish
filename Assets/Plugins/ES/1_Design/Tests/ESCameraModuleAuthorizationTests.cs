using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    /// <summary>
    /// 模块门面负责本地观测授权；Director 的纯仲裁测试不能替代这里的拒绝与撤销验证。
    /// 此程序集只通过 InternalsVisibleTo 注册测试 View，不接触业务可调用的授权后门。
    /// </summary>
    public sealed class ESCameraModuleAuthorizationTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private ESCameraModule module;
        private RecordingAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            ESGameManager.LocalControl?.SetControlledEntity(null);
            module = new ESCameraModule();
            module.Signal_IsActiveAndEnable = true;
            adapter = new RecordingAdapter();
            Assert.That(module.RegisterView(ESCameraViewId.Main, 41, adapter), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            ESGameManager.LocalControl?.SetControlledEntity(null);
            if (module != null)
                module.Signal_IsActiveAndEnable = false;

            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
            module = null;
            adapter = null;
        }

        [Test]
        public void Push_RejectsUnregisteredObjectAndNonLocalEntity()
        {
            GameObject nonEntityOwner = CreateObject("Unregistered Camera Owner");
            Assert.That(module.Push(CreateRequest(nonEntityOwner, "unregistered")), Is.EqualTo(ESCameraLease.Invalid));

            Entity nonLocalEntity = CreateObject("AI Entity").AddComponent<Entity>();
            Assert.That(module.Push(CreateRequest(nonLocalEntity, "ai")), Is.EqualTo(ESCameraLease.Invalid));
        }

        [Test]
        public void Push_AcceptsOnlyTheCurrentLocalEntity()
        {
            Entity localEntity = CreateObject("Local Entity").AddComponent<Entity>();
            ESGameManager.LocalControl.SetControlledEntity(localEntity, new ESRuntimeModeService());

            ESCameraLease lease = module.Push(CreateRequest(localEntity, "local"));

            Assert.That(lease.IsValid, Is.True);
            Assert.That(module.TrySetLook(lease, new Vector2(2f, -1f)), Is.True);
            module.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.hasWinner, Is.True);
            Assert.That(adapter.last.lookInput, Is.EqualTo(new Vector2(2f, -1f)));
        }

        [Test]
        public void ControlSwitch_ReleasesAllLeasesOwnedByPreviousEntity()
        {
            Entity previous = CreateObject("Previous Local Entity").AddComponent<Entity>();
            Entity current = CreateObject("Next Local Entity").AddComponent<Entity>();
            ESGameManager.LocalControl.SetControlledEntity(previous, new ESRuntimeModeService());

            ESCameraLease defaultLease = module.Push(CreateRequest(previous, "default"));
            ESCameraRequest vehicleRequest = CreateRequest(previous, "vehicle");
            vehicleRequest.kind = ESCameraRequestKind.Shot;
            ESCameraLease vehicleLease = module.Push(vehicleRequest);
            Assert.That(defaultLease.IsValid, Is.True);
            Assert.That(vehicleLease.IsValid, Is.True);

            module.OnControlledEntityChanged(previous, current);

            Assert.That(module.TrySetLook(defaultLease, Vector2.one), Is.False);
            Assert.That(module.TrySetLook(vehicleLease, Vector2.one), Is.False);
            Assert.That(module.Release(defaultLease), Is.False);
            Assert.That(module.Release(vehicleLease), Is.False);
        }

        [Test]
        public void ControlRevocation_RejectsWritesButStillAllowsLeaseCleanup()
        {
            Entity localEntity = CreateObject("Local Entity").AddComponent<Entity>();
            ESGameManager.LocalControl.SetControlledEntity(localEntity, new ESRuntimeModeService());
            ESCameraLease lease = module.Push(CreateRequest(localEntity, "local"));
            Assert.That(lease.IsValid, Is.True);

            ESGameManager.LocalControl.SetControlledEntity(null);

            Assert.That(module.TrySetLook(lease, Vector2.one), Is.False);
            Assert.That(module.TrySetTarget(lease, CreateObject("Replacement Follow").transform), Is.False);
            Assert.That(module.Release(lease), Is.True);
            module.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.clearCount, Is.EqualTo(1));
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }

        private ESCameraRequest CreateRequest(UnityEngine.Object owner, string definitionKey)
        {
            GameObject follow = CreateObject("Follow " + definitionKey);
            return ESCameraRequest.CreateBase(
                ESCameraViewId.Main,
                new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, definitionKey),
                0,
                owner,
                follow.transform);
        }

        private sealed class RecordingAdapter : IESCameraViewAdapter
        {
            public bool IsReady => true;
            public Transform OutputTransform => null;

            public bool TryResolveDefinition(ESCameraDefinitionReference reference, out ESCameraDefinitionRuntimeHandle handle)
            {
                handle = reference.IsConfigured
                    ? new ESCameraDefinitionRuntimeHandle(2, 1, 1, "TEST")
                    : default;
                return handle.IsValid;
            }
            public ESCameraResolvedView last;
            public int clearCount;

            public bool Apply(in ESCameraResolvedView resolved)
            {
                last = resolved;
                return true;
            }

            public void Clear()
            {
                clearCount++;
                last = default;
            }
        }
    }
}
