using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    /// <summary>
    /// LocalControl must not retain an Entity across pool return or destruction.
    /// These are EditMode lifecycle checks; they do not claim PlayMode behaviour.
    /// </summary>
    public sealed class ESLocalControlLifecycleTests
    {
        private static readonly MethodInfo OnDisableMethod = typeof(Entity).GetMethod(
            "OnDisable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ESGameManager.LocalControl?.SetControlledEntity(null);
        }

        [TearDown]
        public void TearDown()
        {
            ESGameManager.LocalControl?.SetControlledEntity(null);
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void PoolDespawn_ReleasesControlledEntity()
        {
            Entity entity = CreateEntity("Pooled Local Entity");
            int changes = 0;
            System.Action<Entity, Entity> handler = (_, current) =>
            {
                if (current == null)
                    changes++;
            };
            ESGameManager.LocalControl.OnControlledEntityChanged += handler;

            ESGameManager.LocalControl.SetControlledEntity(entity, new ESRuntimeModeService());
            entity.OnPoolDespawned();

            Assert.That(ESGameManager.LocalControl.ControlledEntity, Is.Null);
            Assert.That(changes, Is.EqualTo(1));
            ESGameManager.LocalControl.OnControlledEntityChanged -= handler;
        }

        [Test]
        public void Destroy_ReleasesControlledEntity()
        {
            Entity entity = CreateEntity("Destroyed Local Entity");
            ESGameManager.LocalControl.SetControlledEntity(entity, new ESRuntimeModeService());

            Object.DestroyImmediate(entity.gameObject);

            Assert.That(ESGameManager.LocalControl.ControlledEntity, Is.Null);
        }

        [Test]
        public void Disable_ReleasesControlledEntity()
        {
            Entity entity = CreateEntity("Disabled Local Entity");
            ESGameManager.LocalControl.SetControlledEntity(entity, new ESRuntimeModeService());

            Assert.That(OnDisableMethod, Is.Not.Null);
            OnDisableMethod.Invoke(entity, null);

            Assert.That(ESGameManager.LocalControl.ControlledEntity, Is.Null);
        }

        [Test]
        public void PoolDespawn_NonOwnerDoesNotRevokeCurrentControl()
        {
            Entity owner = CreateEntity("Current Local Entity");
            Entity other = CreateEntity("Other Entity");
            ESGameManager.LocalControl.SetControlledEntity(owner, new ESRuntimeModeService());

            other.OnPoolDespawned();

            Assert.That(ESGameManager.LocalControl.ControlledEntity, Is.SameAs(owner));
        }

        private Entity CreateEntity(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance.AddComponent<Entity>();
        }
    }
}
