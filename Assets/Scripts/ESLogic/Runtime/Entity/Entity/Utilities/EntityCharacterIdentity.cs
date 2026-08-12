using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// 角色 Prefab 的静态身份声明。
    /// 只表达该 Prefab 所处的生产阶段、阵营和正式 Variant 的唯一 DataInfo；
    /// 骨骼/Socket 映射和碰撞体分别由 EntityTransformMapping 和标准 Collider 节点负责。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Entity))]
    [RequireComponent(typeof(EntityTransformMapping))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "ES", sourceAssembly: "ES_Logic", sourceClassName: "EntityCharacterProfile")]
    [AddComponentMenu("【ES】/角色与交互/角色身份")]
    public sealed class EntityCharacterIdentity : MonoBehaviour
    {
        [Title("角色身份")]
        [LabelText("角色类型")]
        public EntityCharacterPrefabRole prefabRole = EntityCharacterPrefabRole.BuildInput;

        [LabelText("阵营")]
        [Tooltip("角色业务身份；不得借用 Unity Layer 或 GameTag 表达。")]
        public EntityCharacterFaction faction = EntityCharacterFaction.Unspecified;

        [Title("正式角色定义")]
        [LabelText("角色数据类型")]
        [ShowIf(nameof(IsFormalCharacter))]
        public EntityCharacterDefinitionSource definitionSource = EntityCharacterDefinitionSource.None;

        [ShowIf(nameof(ShowsActorDefinition)), LabelText("角色数据")]
        public ActorDataInfo actorDefinition;
        [ShowIf(nameof(ShowsMonsterDefinition)), LabelText("怪物数据")]
        public MonsterDataInfo monsterDefinition;
        [ShowIf(nameof(ShowsNpcDefinition)), LabelText("NPC 数据")]
        public NpcDataInfo npcDefinition;

        [Title("默认相机意图")]
        [ShowIf(nameof(IsFormalCharacter)), LabelText("相机 Definition")]
        [Tooltip("未配置表示该角色不主动占用本地相机。玩家 Variant 只能填写稳定 Definition 引用，而不是 VCam 引用。")]
        public ESCameraDefinitionReference defaultCameraDefinition;

        [SerializeField, HideInInspector, FormerlySerializedAs("defaultCameraProfileKey"), FormerlySerializedAs("defaultCameraDefinitionKey")]
        private string legacyDefaultCameraDefinitionKey;

        [ShowIf(nameof(IsFormalCharacter)), LabelText("相机 ViewKey")]
        public string defaultCameraViewKey = "MainView";

        [ShowIf(nameof(IsFormalCharacter)), LabelText("相机优先级")]
        public int defaultCameraPriority;

        private bool IsFormalCharacter => prefabRole == EntityCharacterPrefabRole.CharacterVariant;
        private bool ShowsActorDefinition => IsFormalCharacter && definitionSource == EntityCharacterDefinitionSource.Actor;
        private bool ShowsMonsterDefinition => IsFormalCharacter && definitionSource == EntityCharacterDefinitionSource.Monster;
        private bool ShowsNpcDefinition => IsFormalCharacter && definitionSource == EntityCharacterDefinitionSource.Npc;

        /// <summary>
        /// 由同根 Entity 在自身生命周期中调用的唯一 Prefab 定义入口。
        /// BuildInput 保证无定义；RuntimePoolTemplate 保留给租户调用 Entity.BindDefinition；
        /// CharacterVariant 则使用本身份声明的唯一 DataInfo。
        /// </summary>
        public bool ApplyPrefabDefinition(Entity entity, out string error)
        {
            if (entity == null)
            {
                error = "缺少根 Entity。";
                return false;
            }

            switch (prefabRole)
            {
                case EntityCharacterPrefabRole.BuildInput:
                    entity.ClearDefinition();
                    error = string.Empty;
                    return true;
                case EntityCharacterPrefabRole.RuntimePoolTemplate:
                    // 通用池模板没有固有定义。这里绝不能清除租户刚注入的 DataInfo。
                    error = string.Empty;
                    return true;
                case EntityCharacterPrefabRole.CharacterVariant:
                    return TryBindVariantDefinition(entity, out error);
                default:
                    error = "未识别的 Prefab 角色类型。";
                    return false;
            }
        }

        /// <summary>
        /// 正式 Variant 的唯一 DataInfo 绑定入口。通用池模板不使用本入口，租出方按需直接调用 Entity.BindDefinition。
        /// </summary>
        public bool TryBindVariantDefinition(Entity entity, out string error)
        {
            if (!IsFormalCharacter)
            {
                error = "只有“正式角色”可以从角色身份声明自动绑定角色数据。";
                return false;
            }
            if (entity == null)
            {
                error = "缺少根 Entity。";
                return false;
            }

            switch (definitionSource)
            {
                case EntityCharacterDefinitionSource.Actor when actorDefinition != null:
                    entity.BindDefinition(actorDefinition);
                    error = string.Empty;
                    return true;
                case EntityCharacterDefinitionSource.Monster when monsterDefinition != null:
                    entity.BindDefinition(monsterDefinition);
                    error = string.Empty;
                    return true;
                case EntityCharacterDefinitionSource.Npc when npcDefinition != null:
                    entity.BindDefinition(npcDefinition);
                    error = string.Empty;
                    return true;
                default:
                    error = "正式角色必须选择且只能选择一个匹配的角色、怪物或 NPC 数据。";
                    return false;
            }
        }

        /// <summary>
        /// 角色只有通过身份声明提供默认镜头意图。它只生成纯 CameraRequest，绝不引用
        /// Cinemachine 或场景 Rig；不存在相机配置的正式 NPC/怪物会自然返回 false。
        /// </summary>
        public bool TryCreateDefaultCameraRequest(
            Entity entity,
            EntityTransformMapping mapping,
            out ESCameraRequest request)
        {
            request = default;
            if (!IsFormalCharacter
                || entity == null
                || mapping == null
                || !defaultCameraDefinition.IsConfigured)
            {
                return false;
            }

            Transform follow = mapping.Resolve("CameraTarget");
            if (follow == null)
                follow = mapping.Resolve(DefaultTransformKey.Camera);
            if (follow == null)
                return false;

            request = ESCameraRequest.CreateBase(
                new ESCameraViewId(defaultCameraViewKey),
                defaultCameraDefinition,
                defaultCameraPriority,
                entity,
                follow,
                mapping.Resolve("CameraAimTarget"));
            return request.IsStructurallyValid;
        }

#if UNITY_EDITOR
        public void ConfigureBuildInput()
        {
            prefabRole = EntityCharacterPrefabRole.BuildInput;
            faction = EntityCharacterFaction.Unspecified;
            ClearDefinition();
        }

        public void ConfigureRuntimePoolTemplate()
        {
            prefabRole = EntityCharacterPrefabRole.RuntimePoolTemplate;
            faction = EntityCharacterFaction.Unspecified;
            ClearDefinition();
        }

        public bool ValidateTemplateRole(EntityCharacterPrefabRole expectedRole, out string error)
        {
            if (prefabRole != expectedRole)
            {
                error = "角色类型不符合当前模板阶段：期望=" + expectedRole + "，实际=" + prefabRole;
                return false;
            }

            if (faction != EntityCharacterFaction.Unspecified)
            {
                error = "角色制作模板和通用角色池模板不得声明阵营。";
                return false;
            }

            if (definitionSource != EntityCharacterDefinitionSource.None
                || actorDefinition != null || monsterDefinition != null || npcDefinition != null)
            {
                error = "角色制作模板和通用角色池模板不得配置角色数据；通用角色池模板应由租出方直接调用 Entity.BindDefinition 指定本次角色数据。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool ValidateFormalCharacter(out string error)
        {
            if (prefabRole != EntityCharacterPrefabRole.CharacterVariant)
            {
                error = "正式角色必须使用“正式角色”类型的角色身份声明。";
                return false;
            }

            if (faction == EntityCharacterFaction.Unspecified)
            {
                error = "正式角色必须声明阵营。";
                return false;
            }

            if (faction == EntityCharacterFaction.Player
                && (definitionSource != EntityCharacterDefinitionSource.Actor
                    || actorDefinition == null
                    || actorDefinition.actorKind != ActorDataKind.Player))
            {
                error = "Player 正式角色必须使用 ActorDataInfo，且 ActorDataKind 必须为 Player。";
                return false;
            }

            if (!HasExactlyOneDefinition())
            {
                error = "正式角色必须选择且只能选择一个匹配的角色、怪物或 NPC 数据。";
                return false;
            }

            error = string.Empty;
            return true;
        }
#endif

        private void ClearDefinition()
        {
            definitionSource = EntityCharacterDefinitionSource.None;
            actorDefinition = null;
            monsterDefinition = null;
            npcDefinition = null;
        }

        private bool HasExactlyOneDefinition()
        {
            switch (definitionSource)
            {
                case EntityCharacterDefinitionSource.Actor:
                    return actorDefinition != null && monsterDefinition == null && npcDefinition == null;
                case EntityCharacterDefinitionSource.Monster:
                    return actorDefinition == null && monsterDefinition != null && npcDefinition == null;
                case EntityCharacterDefinitionSource.Npc:
                    return actorDefinition == null && monsterDefinition == null && npcDefinition != null;
                default:
                    return false;
            }
        }
    }

    public enum EntityCharacterPrefabRole
    {
        [InspectorName("角色制作模板")] BuildInput = 0,
        [InspectorName("通用角色池模板")] RuntimePoolTemplate = 1,
        [InspectorName("正式角色")] CharacterVariant = 2,
    }

    /// <summary>阵营是角色业务身份，不复用 Unity Layer 或 GameTag 表达。</summary>
    public enum EntityCharacterFaction
    {
        [InspectorName("未声明")] Unspecified = 0,
        [InspectorName("中立")] Neutral = 1,
        [InspectorName("玩家")] Player = 2,
        [InspectorName("友方")] Ally = 3,
        [InspectorName("敌对")] Enemy = 4,
        [InspectorName("怪物")] Monster = 5,
        [InspectorName("NPC")] Npc = 6,
    }

    public enum EntityCharacterDefinitionSource
    {
        [InspectorName("无")] None = 0,
        [InspectorName("角色数据")] Actor = 1,
        [InspectorName("怪物数据")] Monster = 2,
        [InspectorName("NPC 数据")] Npc = 3,
    }
}
