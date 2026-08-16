using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESEquipmentAttachmentModuleTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        [Test]
        public void InitialPose_AlignsGripPivotToAuthorSocket()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);

            Assert.That(
                module.TryApplyInitialPose(
                    new EntityEquipmentAttachmentOperation(
                        weaponRoot,
                        binding,
                        EntityEquipmentAttachmentPose.MainHand,
                        EntityEquipmentVisibilityState.Visible),
                    out string error),
                Is.True,
                error);
            Assert.That(binding.GripPivot.position, Is.EqualTo(mainHand.position));
            Assert.That(weaponRoot.parent, Is.SameAs(mainHand));
        }

        [Test]
        public void Prepare_RecordsEntityAndMappingGenerations()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);

            var request = new EntityEquipmentTransitionRequest(
                new EntityEquipmentAttachmentOperation(
                    weaponRoot,
                    binding,
                    EntityEquipmentAttachmentPose.MainHand,
                    EntityEquipmentVisibilityState.Visible),
                EntityEquipmentTransitionPhase.Equipping,
                1);
            Assert.That(module.TryPrepare(request, out EntityEquipmentTransitionToken token, out string error), Is.True, error);
            Assert.That(token.entityGeneration, Is.EqualTo(entity.LifecycleGeneration));
            Assert.That(token.mappingGeneration, Is.EqualTo(mapping.TransformMappings.Generation));
            Assert.That(module.IsCurrent(token), Is.True);
        }

        [Test]
        public void PreparedTransition_WaitsForAnimationBindingUntilTimeout()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);

            Assert.That(
                module.TryPrepare(
                    CreateRequest(
                        weaponRoot,
                        binding,
                        EntityEquipmentTransitionPhase.Equipping,
                        1),
                    out EntityEquipmentTransitionToken token,
                    out string prepareError),
                Is.True,
                prepareError);
            Assert.That(
                module.TryAbortInvalidOrExpired(out _, out _, out string abortError),
                Is.False,
                abortError);
            Assert.That(module.IsCurrent(token), Is.True);
        }

        [Test]
        public void MappingGenerationChange_RejectsPreparedTransition()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            var request = CreateRequest(weaponRoot, binding, EntityEquipmentTransitionPhase.Equipping, 1);
            Assert.That(module.TryPrepare(request, out EntityEquipmentTransitionToken token, out _), Is.True);

            Assert.That(mapping.Set("UnrelatedSocket", CreateChild(entity.transform, "UnrelatedSocket")), Is.True);
            Assert.That(module.IsCurrent(token), Is.False);
            Assert.That(module.TryCommit(token, out _), Is.False);
            Assert.That(weaponRoot.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void EntityGenerationChange_RejectsPreparedTransition()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(module.TryPrepare(CreateRequest(weaponRoot, binding, EntityEquipmentTransitionPhase.Equipping, 1), out EntityEquipmentTransitionToken token, out _), Is.True);

            entity.OnPoolDespawned();
            Assert.That(module.IsCurrent(token), Is.False);
            Assert.That(module.TryCommit(token, out _), Is.False);
            Assert.That(weaponRoot.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void Switching_PreparesTwoViewsAsOneTransition()
        {
            CreateAttachment(
                out Entity entity,
                out EntityTransformMapping mapping,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding currentBinding,
                out Transform currentRoot);
            EntityWeaponBinding nextBinding;
            Transform nextRoot = CreateWeapon(entity.transform, "NextWeapon", out nextBinding);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform back = CreateChild(entity.transform, EntityEquipmentSocketKeys.PrimaryBackSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.PrimaryBackSocket, back), Is.True);

            var request = new EntityEquipmentTransitionRequest(
                new EntityEquipmentAttachmentOperation(
                    currentRoot,
                    currentBinding,
                    EntityEquipmentAttachmentPose.PrimaryBack,
                    EntityEquipmentVisibilityState.Visible),
                new EntityEquipmentAttachmentOperation(
                    nextRoot,
                    nextBinding,
                    EntityEquipmentAttachmentPose.MainHand,
                    EntityEquipmentVisibilityState.Visible),
                EntityEquipmentTransitionPhase.Switching,
                1);
            Assert.That(module.TryPrepare(request, out _, out string error), Is.True, error);
            Assert.That(module.ActiveOperationCount, Is.EqualTo(2));
            Assert.That(currentRoot.parent, Is.SameAs(entity.transform));
            Assert.That(nextRoot.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void MissingAuthorSocket_RejectsTransitionWithoutRootFallback()
        {
            CreateAttachment(
                out _,
                out _,
                out EntityEquipmentAttachmentModule module,
                out EntityWeaponBinding binding,
                out Transform weaponRoot);
            Assert.That(
                module.TryPrepare(
                    CreateRequest(weaponRoot, binding, EntityEquipmentTransitionPhase.Equipping, 1),
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain(EntityEquipmentAttachmentPose.MainHand.ToString()));
        }

        private EntityEquipmentTransitionRequest CreateRequest(
            Transform weaponRoot,
            EntityWeaponBinding binding,
            EntityEquipmentTransitionPhase phase,
            int revision)
        {
            return new EntityEquipmentTransitionRequest(
                new EntityEquipmentAttachmentOperation(
                    weaponRoot,
                    binding,
                    EntityEquipmentAttachmentPose.MainHand,
                    EntityEquipmentVisibilityState.Visible),
                phase,
                revision);
        }

        private void CreateAttachment(
            out Entity entity,
            out EntityTransformMapping mapping,
            out EntityEquipmentAttachmentModule module,
            out EntityWeaponBinding binding,
            out Transform weaponRoot)
        {
            GameObject root = CreateObject("EntityRoot");
            entity = root.AddComponent<Entity>();
            mapping = root.AddComponent<EntityTransformMapping>();
            entity.EnsureEntityStructure();

            entity.equipmentDomain._Editor_RegisterAllButOnlyCreateRelationship(entity);
            module = new EntityEquipmentAttachmentModule();
            entity.equipmentDomain.TryAddModuleRuntime(module);

            weaponRoot = CreateChild(entity.transform, "Weapon");
            binding = weaponRoot.gameObject.AddComponent<EntityWeaponBinding>();
            Transform grip = CreateChild(weaponRoot, "GripPivot");
            binding.ConfigureReferences(grip, null, null, null, weaponRoot.gameObject);
        }

        private Transform CreateWeapon(
            Transform parent,
            string name,
            out EntityWeaponBinding binding)
        {
            Transform root = CreateChild(parent, name);
            binding = root.gameObject.AddComponent<EntityWeaponBinding>();
            Transform grip = CreateChild(root, "GripPivot");
            binding.ConfigureReferences(grip, null, null, null, root.gameObject);
            return root;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            Transform child = CreateObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }
    }
}
