using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCharacterDefinitionKindTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                    Object.DestroyImmediate(definitions[i]);
            }

            definitions.Clear();

            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void PlayerFormal_RejectsNonPlayerActorData()
        {
            EntityCharacterIdentity profile = CreatePlayerProfile(ActorDataKind.Rider);

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterDefinitionKind(
                    profile,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("ActorDataKind.Player"));
        }

        [Test]
        public void PlayerFormal_AcceptsPlayerActorData()
        {
            EntityCharacterIdentity profile = CreatePlayerProfile(ActorDataKind.Player);

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterDefinitionKind(
                    profile,
                    out string error),
                Is.True,
                error);
        }

        [Test]
        public void NonPlayerFormal_RejectsPlayerActorData()
        {
            ActorDataInfo definition = CreateDefinition(ActorDataKind.Player);
            GameObject root = CreateObject("NPC With Player Actor Data");
            EntityCharacterIdentity profile = root.AddComponent<EntityCharacterIdentity>();
            profile.prefabRole = EntityCharacterPrefabRole.CharacterVariant;
            profile.faction = EntityCharacterFaction.Npc;
            profile.definitionSource = EntityCharacterDefinitionSource.Actor;
            profile.actorDefinition = definition;

            Assert.That(
                ESCharacterTemplateReleaseGate.ValidateFormalCharacterDefinitionKind(
                    profile,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("非 Player"));
        }

        private EntityCharacterIdentity CreatePlayerProfile(ActorDataKind actorKind)
        {
            ActorDataInfo definition = CreateDefinition(actorKind);
            GameObject root = CreateObject("Player With " + actorKind);
            EntityCharacterIdentity profile = root.AddComponent<EntityCharacterIdentity>();
            profile.prefabRole = EntityCharacterPrefabRole.CharacterVariant;
            profile.faction = EntityCharacterFaction.Player;
            profile.definitionSource = EntityCharacterDefinitionSource.Actor;
            profile.actorDefinition = definition;
            return profile;
        }

        private ActorDataInfo CreateDefinition(ActorDataKind actorKind)
        {
            ActorDataInfo definition = ScriptableObject.CreateInstance<ActorDataInfo>();
            definition.actorKind = actorKind;
            definitions.Add(definition);
            return definition;
        }

        private GameObject CreateObject(string name)
        {
            GameObject instance = new GameObject(name);
            created.Add(instance);
            return instance;
        }
    }
}
