using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCharacterIdentityValidationTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Character Identity Validation");
            root.AddComponent<EntityCharacterIdentity>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                EntityCharacterIdentity identity = root.GetComponent<EntityCharacterIdentity>();
                if (identity != null)
                {
                    if (identity.actorDefinition != null)
                        Object.DestroyImmediate(identity.actorDefinition);
                    if (identity.monsterDefinition != null)
                        Object.DestroyImmediate(identity.monsterDefinition);
                    if (identity.npcDefinition != null)
                        Object.DestroyImmediate(identity.npcDefinition);
                }
            }

            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void PlayerFaction_WithPlayerActor_Passes()
        {
            EntityCharacterIdentity identity = ConfigurePlayer(ActorDataKind.Player);

            Assert.That(identity.ValidateFormalCharacter(out string error), Is.True, error);
        }

        [Test]
        public void PlayerFaction_WithNonPlayerActor_Fails()
        {
            EntityCharacterIdentity identity = ConfigurePlayer(ActorDataKind.StoryActor);

            Assert.That(identity.ValidateFormalCharacter(out string error), Is.False);
            Assert.That(error, Does.Contain("ActorDataKind"));
        }

        [Test]
        public void PlayerFaction_WithMonsterDefinition_Fails()
        {
            EntityCharacterIdentity identity = root.GetComponent<EntityCharacterIdentity>();
            identity.prefabRole = EntityCharacterPrefabRole.CharacterVariant;
            identity.faction = EntityCharacterFaction.Player;
            identity.definitionSource = EntityCharacterDefinitionSource.Monster;
            identity.monsterDefinition = ScriptableObject.CreateInstance<MonsterDataInfo>();

            Assert.That(identity.ValidateFormalCharacter(out string error), Is.False);
            Assert.That(error, Does.Contain("ActorDataKind"));

        }

        private EntityCharacterIdentity ConfigurePlayer(ActorDataKind kind)
        {
            EntityCharacterIdentity identity = root.GetComponent<EntityCharacterIdentity>();
            identity.prefabRole = EntityCharacterPrefabRole.CharacterVariant;
            identity.faction = EntityCharacterFaction.Player;
            identity.definitionSource = EntityCharacterDefinitionSource.Actor;
            identity.actorDefinition = ScriptableObject.CreateInstance<ActorDataInfo>();
            identity.actorDefinition.actorKind = kind;
            return identity;
        }
    }
}
