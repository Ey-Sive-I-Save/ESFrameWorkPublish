using System.Collections.Generic;
using System.Reflection;
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
        public void BeginTransition_CapturesAllFourRevisions()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);

            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out EquipmentTransitionStamp stamp, out string error),
                Is.True,
                error);

            Assert.That(stamp.TransitionId, Is.GreaterThan(0));
            Assert.That(stamp.EntityGeneration, Is.EqualTo(entity.LifecycleGeneration));
            Assert.That(stamp.MappingGeneration, Is.EqualTo(mapping.TransformMappings.Generation));
            Assert.That(stamp.SlotRevision, Is.EqualTo(module.SlotRevision));
            Assert.That(module.IsCurrent(stamp), Is.True);
        }

        [Test]
        public void MappingGenerationChange_RejectsOldTransitionWithoutReparenting()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform equipment = CreateChild(entity.transform, "Equipment");
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out EquipmentTransitionStamp stamp, out _),
                Is.True);

            Assert.That(mapping.Set("UnrelatedSocket", CreateChild(entity.transform, "UnrelatedSocket")), Is.True);

            Assert.That(module.TryCommit(stamp, equipment, true, out _), Is.False);
            Assert.That(equipment.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void EntityGenerationChange_RejectsOldTransition()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform equipment = CreateChild(entity.transform, "Equipment");
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out EquipmentTransitionStamp stamp, out _),
                Is.True);

            entity.OnPoolDespawned();

            Assert.That(module.TryCommit(stamp, equipment, true, out _), Is.False);
            Assert.That(equipment.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void SlotRevisionChange_RejectsOldTransition()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform equipment = CreateChild(entity.transform, "Equipment");
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out EquipmentTransitionStamp stamp, out _),
                Is.True);

            module.NotifySlotsChanged();

            Assert.That(module.TryCommit(stamp, equipment, true, out _), Is.False);
            Assert.That(equipment.parent, Is.SameAs(entity.transform));
        }

        [Test]
        public void Commit_UsesCachedMountAndConsumesStampOnce()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform equipment = CreateChild(entity.transform, "Equipment");
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);
            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out EquipmentTransitionStamp stamp, out _),
                Is.True);

            Assert.That(module.TryCommit(stamp, equipment, true, out string error), Is.True, error);
            Assert.That(equipment.parent, Is.SameAs(mainHand));
            Assert.That(equipment.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(equipment.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(module.TryCommit(stamp, equipment, true, out _), Is.False);
        }

        [Test]
        public void ExplicitWeaponMount_TakesPriorityOverCharacterMapping()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform mainHand = CreateChild(entity.transform, EntityEquipmentSocketKeys.MainHandSocket);
            Transform explicitMount = CreateChild(entity.transform, "WeaponExplicitMount");
            Transform equipment = CreateChild(entity.transform, "Equipment");
            Assert.That(mapping.Set(EntityEquipmentSocketKeys.MainHandSocket, mainHand), Is.True);

            Assert.That(
                module.TryAttach(
                    EntityEquipmentAttachmentTarget.MainHand,
                    equipment,
                    explicitMount,
                    null,
                    true,
                    out _,
                    out string error),
                Is.True,
                error);
            Assert.That(equipment.parent, Is.SameAs(explicitMount));
        }

        [Test]
        public void StrictMode_DoesNotUseLegacyOrEntityRootFallback()
        {
            CreateAttachment(out Entity entity, out _, out EntityEquipmentAttachmentModule module);
            Transform legacyMount = CreateChild(entity.transform, "LegacyMount");

            Assert.That(
                module.TryBeginTransition(
                    EntityEquipmentAttachmentTarget.PrimaryBack,
                    null,
                    legacyMount,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain(EntityEquipmentAttachmentTarget.PrimaryBack.ToString()));
        }

        [Test]
        public void PoolDespawn_ClearsOnlyRuntimeDynamicStringKeys()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform weapon = CreateChild(entity.transform, EntityEquipmentSocketKeys.WeaponSocket);
            Transform dynamicSocket = CreateChild(entity.transform, "RuntimeSocket");
            Assert.That(
                mapping.Set(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, weapon, out _),
                Is.True);
            Assert.That(mapping.SetDynamic("RuntimeSocket", dynamicSocket), Is.True);

            module.OnPoolDespawned();

            Assert.That(mapping.Resolve(DefaultTransformKey.Weapon), Is.SameAs(weapon));
            Assert.That(mapping.Resolve(EntityEquipmentSocketKeys.WeaponSocket), Is.SameAs(weapon));
            Assert.That(mapping.Resolve("RuntimeSocket"), Is.Null);
        }

        [Test]
        public void InvalidMapping_RejectsTransitionWithConflict()
        {
            CreateAttachment(out Entity entity, out EntityTransformMapping mapping, out EntityEquipmentAttachmentModule module);
            Transform first = CreateChild(entity.transform, "First");
            Transform second = CreateChild(entity.transform, "Second");
            var entries = new List<EntityTransformMap.Entry>
            {
                new EntityTransformMap.Entry(DefaultTransformKey.Weapon, "First", first),
                new EntityTransformMap.Entry(DefaultTransformKey.Weapon, "Second", second)
            };
            FieldInfo entriesField = typeof(ESEnumStringMirrorMap<DefaultTransformKey, Transform>).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entriesField, Is.Not.Null);
            entriesField.SetValue(mapping.TransformMappings, entries);
            mapping.TransformMappings.MarkDirty();
            Assert.That(mapping.TransformMappings.IsValid, Is.False);
            EntityTransformMap.Conflict conflict = mapping.TransformMappings.LastConflict;
            Assert.That(conflict.Kind, Is.EqualTo(EntityTransformMap.ConflictKind.DuplicateEnumKey));

            Assert.That(
                module.TryBeginTransition(EntityEquipmentAttachmentTarget.MainHand, null, null, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain(conflict.Message));
        }

        private void CreateAttachment(
            out Entity entity,
            out EntityTransformMapping mapping,
            out EntityEquipmentAttachmentModule module)
        {
            GameObject root = CreateObject("EntityRoot");
            entity = root.AddComponent<Entity>();
            mapping = root.AddComponent<EntityTransformMapping>();
            entity.EnsureEntityStructure();
            EntityBasicDomain domain = entity.basicDomain;
            domain._Editor_RegisterAllButOnlyCreateRelationship(entity);
            entity.RegisterDomain(domain);

            module = new EntityEquipmentAttachmentModule
            {
                allowEntityRootFallback = false
            };
            domain.TryAddModuleRuntime(module);
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
