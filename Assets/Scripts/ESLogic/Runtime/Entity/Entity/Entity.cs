using System;
using System.Collections.Generic;
using KinematicCharacterController;
using Sirenix.OdinInspector;
using UnityEngine;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace ES
{
    // Entity：直接接入 KCC 的角色核心（不走模块，超高频）
    [Serializable, TypeRegistryItem("实体核心")]
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public partial class Entity : Core, ICharacterController, IESEffectLeaseOwner, IESGameObjectPoolLifecycle
    {
        [ESEditorSection("core", "核心配置", -100f)]
        [LabelText("主 Animator")]
        public Animator animator;

        [NonSerialized] private EntityTransformMapping _transformMapping;

        /// <summary>
        /// 角色稳定挂点的运行时入口。首次绑定后只读取缓存，不允许业务代码在热路径重新 Find 层级。
        /// </summary>
        public EntityTransformMapping TransformMapping => EnsureTransformMapping();

        internal void BindTransformMapping(EntityTransformMapping mapping)
        {
            if (mapping == null)
                return;

            _transformMapping = mapping;
            _transformMapping.RebuildRuntimeCache();
        }

        public EntityTransformMapping EnsureTransformMapping()
        {
            if (_transformMapping == null)
            {
                _transformMapping = GetComponent<EntityTransformMapping>();
                _transformMapping?.RebuildRuntimeCache();
            }

            return _transformMapping;
        }

        /// <summary>
        /// 角色定义只从同根 Profile 读取：Variant 使用其唯一 DataInfo，
        /// 通用池模板保留给租户显式 BindDefinition，基础模板保持无定义。
        /// </summary>
        private void ApplyPrefabProfileDefinition()
        {
            EntityCharacterIdentity profile = GetComponent<EntityCharacterIdentity>();
            if (profile == null)
                return;

            if (!profile.ApplyPrefabDefinition(this, out string error))
                Debug.LogError("[Entity] Profile 定义绑定失败：" + error, profile);
        }

        public bool TryResolveTransform(DefaultTransformKey key, out Transform transform)
        {
            EntityTransformMapping mapping = EnsureTransformMapping();
            transform = mapping != null ? mapping.Resolve(key) : null;
            return transform != null;
        }

        public bool TryResolveTransform(string key, out Transform transform)
        {
            EntityTransformMapping mapping = EnsureTransformMapping();
            transform = mapping != null ? mapping.Resolve(key) : null;
            return transform != null;
        }

        [NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Entity长期OpSupport")]
        [ESEditorSection("diagnostics", "诊断", 200f)]
        public ESOpSupport opSupport;

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureEntityOpSupport();
                return opSupport;
            }
        }

        [NonSerialized] private ESTagCollection tags;
        [NonSerialized] private readonly ESTagLeaseSet intrinsicTagLeases = new ESTagLeaseSet();
        [NonSerialized] private IReadOnlyList<ESTagStableReference> intrinsicTags;
        [NonSerialized] private UnityEngine.Object intrinsicTagDefinition;
        [NonSerialized] private string intrinsicTagError;
        [NonSerialized] private bool waitsForTagCatalog;
        [NonSerialized] private ESTagDefinitionState intrinsicTagState;
        [NonSerialized] private bool waitsForAttributeCatalog;
        [NonSerialized] private bool authoringMotionBaselineCaptured;
        [NonSerialized] private float authoringMaxStableMoveSpeed;
        [NonSerialized] private float authoringStableMovementSharpness;
        [NonSerialized] private float authoringMaxAirMoveSpeed;
        [NonSerialized] private float authoringAirAccelerationSpeed;
        [NonSerialized] private float authoringJumpSpeed;
        [NonSerialized] private float authoringOrientationSharpness;
        [NonSerialized] private float authoringSpeedMultiplier;
        [NonSerialized] private float authoringSpeedLimit;

        /// <summary>Entity is one Tag host. The container itself has no Entity-specific behavior.</summary>
        public ESTagCollection Tags => tags ??= CreateTagCollection();
        public ESTagDefinitionState IntrinsicTagState => intrinsicTagState;
        public UnityEngine.Object IntrinsicTagDefinition => intrinsicTagDefinition;
        public string IntrinsicTagError => intrinsicTagError ?? string.Empty;

        #region Domains

        [ESEditorSection("body", "身体基础", 10f)]
        [HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBasicDomain basicDomain = new EntityBasicDomain();

        [ESEditorSection("ai", "意识 AI", 20f)]
        [HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityAIDomain aiDomain = new EntityAIDomain();

        [ESEditorSection("buff", "Buff", 30f)]
        [HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityBuffDomain buffDomain = new EntityBuffDomain();

        [ESEditorSection("state", "状态表现", 50f)]
        [HideLabel, HideReferenceObjectPicker, SerializeReference]
        public EntityStateDomain stateDomain = new EntityStateDomain();

        #endregion

        #region KCC

        [ESEditorSection("body", "身体基础", 10f)]
        [Title("身体运动核心（KCC，高频）")]
        [HideLabel]
        public EntityKCCData kcc = new EntityKCCData();

        #endregion

        #region Lifecycle

        protected override void OnBeforeAwakeRegister()
        {
            EnsureEntityStructure();
            CaptureAuthoringMotionBaseline();
            EnsureEntityOpSupport();
            Tags.Warmup();
            EnsureTransformMapping();
            ApplyPrefabProfileDefinition();
            RefreshDefaultCameraRequest();
            InitializeKCC();
        }

        private void Reset()
        {
            EnsureEntityStructure();
        }

        private void OnValidate()
        {
            EnsureEntityStructure();
        }

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            // 统一注册：只注册需要参与当前实体运行的域
            RegisterDomain(basicDomain);
            RegisterDomain(aiDomain);
            RegisterDomain(buffDomain);
            RegisterDomain(stateDomain);
        }

        #endregion

        #region 运行逻辑

        protected override void Update()
        {
            base.Update();
        }

        /// <summary>
        /// Pool return ends this Entity lifetime before deactivation. Old Tag and value-change
        /// handles become stale, so a delayed release cannot affect the next renter.
        /// </summary>
        public void OnPoolDespawned()
        {
            ESActionPoolLifecycleDiagnostics.RecordDespawn();
            basicDomain?.NotifyPoolDespawned();
            ESActionPoolLifecycleDiagnostics.Record("Entity.CameraRelease");
            ESGameManager.Camera?.ReleaseOwnedBy(this);
            ESActionPoolLifecycleDiagnostics.Record("Entity.DefaultCameraRelease");
            ReleaseDefaultCameraRequest();
            aiDomain?.ResetControlArbitrationForLifecycle();
            ESActionPoolLifecycleDiagnostics.Record("Entity.TagCatalogUnsubscribe");
            UnsubscribeFromTagCatalog();
            ESActionPoolLifecycleDiagnostics.Record("Entity.AttributeCatalogUnsubscribe");
            UnsubscribeFromAttributeCatalog();
            ESActionPoolLifecycleDiagnostics.Record("Entity.BuffClear");
            buffDomain?.ClearAllBuffs();
            ESActionPoolLifecycleDiagnostics.Record("Entity.ClearDefinition");
            ClearDefinition();
            ESActionPoolLifecycleDiagnostics.Record("Entity.ValueChangeReset");
            ResetValueChangesForLifecycleEnd();
            ESActionPoolLifecycleDiagnostics.Record("Entity.TagReset");
            tags?.ResetForReuse();
        }

        /// <summary>Called by the pool while inactive, before the next activation.</summary>
        public void OnPoolSpawned()
        {
            ESActionPoolLifecycleDiagnostics.RecordSpawn();
            EnsureEntityStructure();
            basicDomain?.NotifyPoolSpawned();
            CaptureAuthoringMotionBaseline();
            EnsureEntityOpSupport();
            Tags.Warmup();
            EnsureTransformMapping();
            ApplyPrefabProfileDefinition();
            ApplyIntrinsicTags();
            RefreshDefaultCameraRequest();
        }

        protected override void OnDestroy()
        {
            ESGameManager.Camera?.ReleaseOwnedBy(this);
            ReleaseDefaultCameraRequest();
            UnsubscribeFromTagCatalog();
            UnsubscribeFromAttributeCatalog();
            intrinsicTagLeases.ReleaseAll();
            intrinsicTags = null;
            intrinsicTagDefinition = null;
            intrinsicTagState = ESTagDefinitionState.Empty;
            if (tags != null)
            {
                tags.Dispose();
                tags = null;
            }
            base.OnDestroy();
            ResetValueChangesForLifecycleEnd();

            opSupport?.Dispose();

            opSupport = null;
        }

        #endregion

        #region KCC API

        public void InitializeKCC()
        {
            kcc.Initialize(this);
        }

        public void EnsureEntityStructure()
        {
            basicDomain ??= new EntityBasicDomain();
            aiDomain ??= new EntityAIDomain();
            buffDomain ??= new EntityBuffDomain();
            BindGameCoreAttributeCatalog();
            stateDomain ??= new EntityStateDomain();
            stateDomain.stateMachine ??= new StateMachine();
            kcc ??= new EntityKCCData();
        }

        public void EnsureEntityOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.CreateStandalone();

            if (opSupport.Kind != ESOpSupportKind.Entity || opSupport.OwnerEntity != this)
                opSupport.InitializeEntityOwner(this, GetInstanceID());
        }

        private ESTagCollection CreateTagCollection()
        {
            return new ESTagCollection();
        }

        #endregion

        #region 游戏标签 API

        /// <summary>Binds the sole Actor definition that owns this Entity's birth Tags.</summary>
        public bool BindDefinition(ActorDataInfo definition)
        {
            bool applied = BindIntrinsicTags(definition, definition != null ? definition.tags : null);
            ApplyDefinitionMotion(
                definition != null ? definition.motionShared : null,
                definition != null ? definition.motionVariable : default);
            return applied;
        }

        /// <summary>Binds the sole Monster definition that owns this Entity's birth Tags.</summary>
        public bool BindDefinition(MonsterDataInfo definition)
        {
            bool applied = BindIntrinsicTags(definition, definition != null ? definition.tags : null);
            ApplyDefinitionMotion(
                definition != null ? definition.motionShared : null,
                definition != null ? definition.motionVariable : default);
            return applied;
        }

        /// <summary>Binds the sole NPC definition that owns this Entity's birth Tags.</summary>
        public bool BindDefinition(NpcDataInfo definition)
        {
            bool applied = BindIntrinsicTags(definition, definition != null ? definition.tags : null);
            ApplyDefinitionMotion(
                definition != null ? definition.motionShared : null,
                definition != null ? definition.motionVariable : default);
            return applied;
        }

        /// <summary>RuntimeData keeps only a reference to its originating Monster definition's direct Tag list.</summary>
        public bool BindDefinition(ESMonsterRuntimeData definition)
        {
            bool applied = BindIntrinsicTags(definition != null ? definition.soSource : null, definition != null ? definition.tags : null);
            ApplyDefinitionMotion(
                definition != null ? definition.sharedData : null,
                definition != null ? definition.defaultVariableData : default);
            return applied;
        }

        /// <summary>RuntimeData keeps only a reference to its originating NPC definition's direct Tag list.</summary>
        public bool BindDefinition(ESNpcRuntimeData definition)
        {
            bool applied = BindIntrinsicTags(definition != null ? definition.soSource : null, definition != null ? definition.tags : null);
            ApplyDefinitionMotion(
                definition != null ? definition.sharedData : null,
                definition != null ? definition.defaultVariableData : default);
            return applied;
        }

        /// <summary>
        /// Applies the currently bound definition without count jitter. Invalid replacement data
        /// leaves the prior active Leases intact; a missing Catalog enters Pending and retries on bind.
        /// </summary>
        public bool ApplyIntrinsicTags()
        {
            if (intrinsicTags == null || intrinsicTags.Count == 0)
            {
                intrinsicTagLeases.ReleaseAll();
                intrinsicTagState = ESTagDefinitionState.Empty;
                intrinsicTagError = null;
                UnsubscribeFromTagCatalog();
                return true;
            }

            if (!ESTagRuntimeCatalog.IsBound)
            {
                intrinsicTagState = ESTagDefinitionState.Pending;
                intrinsicTagError = "Tag Catalog is not bound.";
                SubscribeToTagCatalog();
                return false;
            }

            if (intrinsicTagLeases.MatchesTags(intrinsicTags))
            {
                intrinsicTagState = ESTagDefinitionState.Applied;
                intrinsicTagError = null;
                UnsubscribeFromTagCatalog();
                return true;
            }

            if (!intrinsicTagLeases.TryApply(Tags, intrinsicTags, this, out string error))
            {
                intrinsicTagState = ESTagDefinitionState.Failed;
                intrinsicTagError = error;
                UnsubscribeFromTagCatalog();
                return false;
            }

            intrinsicTagState = ESTagDefinitionState.Applied;
            intrinsicTagError = null;
            UnsubscribeFromTagCatalog();
            return true;
        }

        /// <summary>Returns only a Tag currently held by this Entity's definition LeaseSet.</summary>
        public bool HasIntrinsicTag(ESTagStableReference tag)
        {
            return intrinsicTagState == ESTagDefinitionState.Applied
                   && ESTagRuntimeCatalog.TryGetRuntimeKey(tag, out int runtimeKey)
                   && intrinsicTagLeases.Contains(ESTagId.FromInt32(runtimeKey));
        }

        /// <summary>Returns only a currently active definition-owned Tag.</summary>
        public bool HasIntrinsicTag(ESTagId tag)
        {
            return intrinsicTagState == ESTagDefinitionState.Applied && intrinsicTagLeases.Contains(tag);
        }

        /// <summary>Releases definition-owned Tags but preserves the binding for an explicit later reapply.</summary>
        public void ReleaseIntrinsicTags()
        {
            intrinsicTagLeases.ReleaseAll();
            if (intrinsicTags == null || intrinsicTags.Count == 0)
                intrinsicTagState = ESTagDefinitionState.Empty;
            else
                intrinsicTagState = ESTagRuntimeCatalog.IsBound ? ESTagDefinitionState.Failed : ESTagDefinitionState.Pending;
        }

        /// <summary>Ends the definition binding and releases only its own Tag Leases.</summary>
        public void ClearDefinition()
        {
            UnsubscribeFromTagCatalog();
            intrinsicTagLeases.ReleaseAll();
            intrinsicTags = null;
            intrinsicTagDefinition = null;
            intrinsicTagError = null;
            intrinsicTagState = ESTagDefinitionState.Empty;
            RestoreAuthoringMotionBaseline();
        }

        private void CaptureAuthoringMotionBaseline()
        {
            if (authoringMotionBaselineCaptured || kcc == null)
                return;

            authoringMaxStableMoveSpeed = kcc.maxStableMoveSpeed;
            authoringStableMovementSharpness = kcc.stableMovementSharpness;
            authoringMaxAirMoveSpeed = kcc.maxAirMoveSpeed;
            authoringAirAccelerationSpeed = kcc.airAccelerationSpeed;
            authoringJumpSpeed = kcc.jumpSpeed;
            authoringOrientationSharpness = kcc.orientationSharpness;
            authoringSpeedMultiplier = kcc.speedMultiplier;
            authoringSpeedLimit = kcc.speedLimit;
            authoringMotionBaselineCaptured = true;
        }

        private void ApplyDefinitionMotion(
            EntityMotionSharedData sharedData,
            EntityMotionVariableData variableData)
        {
            CaptureAuthoringMotionBaseline();
            if (kcc == null || sharedData == null)
            {
                RestoreAuthoringMotionBaseline();
                return;
            }

            kcc.maxStableMoveSpeed = sharedData.maxStableMoveSpeed;
            kcc.stableMovementSharpness = sharedData.stableMovementSharpness;
            kcc.maxAirMoveSpeed = sharedData.maxAirMoveSpeed;
            kcc.airAccelerationSpeed = sharedData.airAccelerationSpeed;
            kcc.jumpSpeed = sharedData.jumpSpeed;
            kcc.orientationSharpness = sharedData.orientationSharpness > 0f
                ? sharedData.orientationSharpness
                : authoringOrientationSharpness;
            // Zero is the CLR default for old serialized variable data, not a useful spawn speed.
            // A deliberate stop must use the Entity Move Permit, so it cannot silently brick input.
            kcc.speedMultiplier = variableData.speedMultiplier > 0f
                ? variableData.speedMultiplier
                : 1f;
            kcc.speedLimit = variableData.speedLimit;
        }

        private void RestoreAuthoringMotionBaseline()
        {
            if (!authoringMotionBaselineCaptured || kcc == null)
                return;

            kcc.maxStableMoveSpeed = authoringMaxStableMoveSpeed;
            kcc.stableMovementSharpness = authoringStableMovementSharpness;
            kcc.maxAirMoveSpeed = authoringMaxAirMoveSpeed;
            kcc.airAccelerationSpeed = authoringAirAccelerationSpeed;
            kcc.jumpSpeed = authoringJumpSpeed;
            kcc.orientationSharpness = authoringOrientationSharpness;
            kcc.speedMultiplier = authoringSpeedMultiplier;
            kcc.speedLimit = authoringSpeedLimit;
        }

        private bool BindIntrinsicTags(UnityEngine.Object definition, IReadOnlyList<ESTagStableReference> definitionTags)
        {
            bool changed = !ReferenceEquals(intrinsicTagDefinition, definition)
                           || !ReferenceEquals(intrinsicTags, definitionTags);
            intrinsicTagDefinition = definition;
            intrinsicTags = definitionTags;
            if (changed && !ESTagRuntimeCatalog.IsBound)
                intrinsicTagLeases.ReleaseAll();

            return ApplyIntrinsicTags();
        }

        private void SubscribeToTagCatalog()
        {
            if (waitsForTagCatalog)
                return;

            ESTagRuntimeCatalog.CatalogBound += HandleTagCatalogBound;
            waitsForTagCatalog = true;
        }

        private void UnsubscribeFromTagCatalog()
        {
            if (!waitsForTagCatalog)
                return;

            ESTagRuntimeCatalog.CatalogBound -= HandleTagCatalogBound;
            waitsForTagCatalog = false;
        }

        private void HandleTagCatalogBound()
        {
            ApplyIntrinsicTags();
        }

        public bool HasGameTag(ESGameTag tag)
        {
            return Tags.Has(ESTagId.FromInt32((ushort)tag));
        }

        public bool HasGameTag(ESTagId tag)
        {
            return Tags.Has(tag);
        }

        public byte GetGameTagCount(ESGameTag tag)
        {
            return (byte)Math.Min(byte.MaxValue, Tags.GetCount(ESTagId.FromInt32((ushort)tag)));
        }

        public byte GetGameTagCount(ESTagId tag)
        {
            return (byte)Math.Min(byte.MaxValue, Tags.GetCount(tag));
        }

        public ESTagMask64 GetGameTagMask()
        {
            return Tags.HotMask;
        }

        public bool HasAnyGameTag(ESTagMask64 mask)
        {
            return Tags.HasAny(mask);
        }

        public bool HasAllGameTags(ESTagMask64 mask)
        {
            return Tags.HasAll(mask);
        }

        /// <summary>
        /// Evaluates a compiled Core plus Extension Tag condition. The common Core-only path is
        /// two mask tests and does not touch the sparse extension dictionary.
        /// </summary>
        public bool MatchesTagCondition(ESTagConditionRuntime condition)
        {
            return Tags.Matches(condition);
        }

        /// <summary>
        /// Business-facing condition query. The configuration owns stable Core and StringKey
        /// identities; its current-process RuntimeKey representation stays internal.
        /// </summary>
        public bool MatchesTagCondition(ESTagConditionConfig config)
        {
            return Tags.Matches(config);
        }

        /// <summary>
        /// The explicit diagnostic form of <see cref="MatchesTagCondition"/>. A false return
        /// means the condition itself cannot be evaluated under the active Catalog; a true
        /// return with <paramref name="matches"/> false means it was evaluated and did not match.
        /// </summary>
        public bool TryMatchesTagCondition(ESTagConditionRuntime condition, out bool matches, out string error)
        {
            return Tags.TryMatches(condition, out matches, out error);
        }

        /// <summary>
        /// Diagnostic form of the stable configuration query. A false return means the
        /// configuration cannot be compiled or evaluated under the active Tag Catalog.
        /// </summary>
        public bool TryMatchesTagCondition(ESTagConditionConfig config, out bool matches, out string error)
        {
            return Tags.TryMatches(config, out matches, out error);
        }

        public void ClearGameTags()
        {
            Tags.Clear();
        }

        public int GetTagCount(ESTagId tag)
        {
            return Tags.GetCount(tag);
        }

        public ESTagDebugSnapshot GetTagDebugSnapshot()
        {
            return Tags.GetDebugSnapshot();
        }

        /// <summary>
        /// Creates a stable presence view excluding Tags currently supplied by the bound definition.
        /// It is not a replacement for Buff/equipment/task persistence: those domains must restore
        /// their own Lease ownership, especially when they share a Tag with the definition.
        /// </summary>
        public bool TryCreateNonIntrinsicTagSnapshot(
            ESTagStableTransferScope scope,
            out ESTagStableSnapshot snapshot,
            out string error)
        {
            return intrinsicTagLeases.TryCreateSnapshotWithoutOwnedTags(Tags, scope, out snapshot, out error);
        }

        #endregion

        #region ValueChange / Attribute Runtime

        private struct ValueChangeEffectSlot
        {
            public int generation;
            public bool isActive;
        }

        // Fixed character slots are compact reference arrays. A resolver is materialized only when a
        // Buff/code modifier actually targets that slot; KCC can read an unmodified base value directly.
        [ShowInInspector, ReadOnly, LabelText("角色 Float ValueChange")]
        private readonly ESFloatValueChangeSet[] characterFloatStats = new ESFloatValueChangeSet[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterFloatStatIsActive = new byte[(int)ESCharacterFloatAttributeId.Count];

        [ShowInInspector, ReadOnly, LabelText("角色 Permit ValueChange")]
        private readonly ESPermitSet[] characterPermitStats = new ESPermitSet[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitStatIsActive = new byte[(int)ESCharacterPermitAttributeId.Count];

        // GameCore may declare additional Character HotSlot attributes without adding a compiled
        // KCC enum. They use these Catalog slot arrays; the fixed arrays above remain the faster
        // typed path for built-in movement and control reads.
        private ESFloatValueChangeSet[] catalogHotFloatStats;
        private byte[] catalogHotFloatStatIsActive;
        private ESPermitSet[] catalogHotPermitStats;
        private byte[] catalogHotPermitStatIsActive;

        // Compiled with the definition table, then read directly by KCC. These arrays deliberately
        // replace per-frame Catalog/Dictionary lookups for fixed character slots.
        private readonly float[] characterFloatDefinitionBases = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterFloatHasDefinitionBase = new byte[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatDefinitionMinimums = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatDefinitionMaximums = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly float[] characterFloatExplicitBases = new float[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterFloatHasExplicitBase = new byte[(int)ESCharacterFloatAttributeId.Count];
        private readonly byte[] characterPermitDefinitionFallbacks = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitHasDefinitionFallback = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitExplicitFallbacks = new byte[(int)ESCharacterPermitAttributeId.Count];
        private readonly byte[] characterPermitHasExplicitFallback = new byte[(int)ESCharacterPermitAttributeId.Count];

        // Optional attributes remain sparse, but are always indexed by an already-resolved
        // process-local RuntimeKey. Their StringKey never enters a per-instance dictionary.
        [ShowInInspector, ReadOnly, LabelText("稀疏 Float ValueChange")]
        private Dictionary<int, ESFloatValueChangeSet> sparseFloatStats;

        [ShowInInspector, ReadOnly, LabelText("稀疏 Permit ValueChange")]
        private Dictionary<int, ESPermitSet> sparsePermitStats;

        // Explicit bases belong to business runtime state, not to a modifier Set. Sparse RuntimeKey
        // entries are discarded on catalog rebind; fixed character ids remain stable for the entity.
        private Dictionary<int, float> sparseFloatExplicitBases;
        private Dictionary<int, bool> sparsePermitExplicitFallbacks;
        private List<ESFloatValueChangeSet> recycledSparseFloatStats;
        private List<ESPermitSet> recycledSparsePermitStats;

        [NonSerialized] private ESSuperAttributeTable superAttributeTable;
        [NonSerialized] private ESSuperAttributeCatalog superAttributeCatalog;
        [NonSerialized] private string superAttributeCatalogError;
        private List<ValueChangeEffectSlot> valueChangeEffectSlots;
        private List<int> freeValueChangeEffectSlots;
        private int activeValueChangeEffectCount;
        private bool isValueChangeResetting;

        public int ActiveValueChangeEffectCount => activeValueChangeEffectCount;

        /// <summary>
        /// Production Entities only consume the already-bound GameCore Character Catalog. They do
        /// not serialize a per-Prefab schema, so every character shares the same stable identity,
        /// bounds and Hot/Sparse layout for the current consumer.
        /// </summary>
        private void BindGameCoreAttributeCatalog()
        {
            if (ESAttributeRuntimeCatalog.TryGet(ESAttributeBakeTable.CharacterScope, out ESSuperAttributeCatalog catalog))
            {
                BindSuperAttributeCatalog(catalog);
                UnsubscribeFromAttributeCatalog();
                return;
            }

            if (superAttributeCatalog != null || HasMaterializedValueChangeSets())
                ClearValueChanges();

            superAttributeTable = null;
            superAttributeCatalog = null;
            superAttributeCatalogError = "角色属性 Catalog 尚未绑定。GameCore 必须在 Entity 启动前完成加载。";
            RebuildFixedSlotDefinitionCache();
            SubscribeToAttributeCatalog();
        }

        private void BindSuperAttributeCatalog(ESSuperAttributeCatalog catalog)
        {
            if (ReferenceEquals(superAttributeCatalog, catalog))
                return;
            if (activeValueChangeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot bind a different Attribute Catalog while ValueChange effects are active. Release the owning EffectLease first.");
            }

            if (superAttributeCatalog != null || HasMaterializedValueChangeSets())
                ClearValueChanges();

            superAttributeTable = null;
            superAttributeCatalog = catalog;
            superAttributeCatalogError = catalog == null ? "角色属性 Catalog 缺失。" : null;
            RebuildFixedSlotDefinitionCache();
        }

        private void SubscribeToAttributeCatalog()
        {
            if (waitsForAttributeCatalog)
                return;

            ESAttributeRuntimeCatalog.CatalogBound += HandleAttributeCatalogBound;
            waitsForAttributeCatalog = true;
        }

        private void UnsubscribeFromAttributeCatalog()
        {
            if (!waitsForAttributeCatalog)
                return;

            ESAttributeRuntimeCatalog.CatalogBound -= HandleAttributeCatalogBound;
            waitsForAttributeCatalog = false;
        }

        private void HandleAttributeCatalogBound()
        {
            BindGameCoreAttributeCatalog();
        }

        /// <summary>
        /// Legacy test/editor injection only. Production Entity configuration is the single
        /// Character schema in GameCore and reaches this object through ESAttributeRuntimeCatalog.
        /// </summary>
        public void BindSuperAttributeTable(ESSuperAttributeTable table)
        {
            // A disabled table contributes no schema/defaults. Fixed built-in slots still work from
            // their caller-supplied base values, while unregistered sparse keys remain unavailable.
            ESSuperAttributeTable effectiveTable = table != null && table.enabled ? table : null;
            if (ReferenceEquals(superAttributeTable, effectiveTable) && superAttributeCatalog != null)
                return;

            if (activeValueChangeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot bind a different AttributeTable while ValueChange effects are active. Release the owning EffectLease first.");
            }

            if (superAttributeCatalog != null || HasMaterializedValueChangeSets())
                ClearValueChanges();

            if (!ReferenceEquals(superAttributeTable, effectiveTable))
            {
                sparseFloatExplicitBases?.Clear();
                sparsePermitExplicitFallbacks?.Clear();
            }

            superAttributeTable = effectiveTable;
            superAttributeCatalog = null;
            superAttributeCatalogError = null;
            if (effectiveTable != null && !effectiveTable.TryBuildCatalog(out superAttributeCatalog, out superAttributeCatalogError))
                superAttributeCatalog = null;

            RebuildFixedSlotDefinitionCache();
        }

        public ESSuperAttributeCatalog SuperAttributeCatalog => superAttributeCatalog;
        public string SuperAttributeCatalogError => superAttributeCatalogError;

        /// <summary>
        /// Creates one runtime-only ownership boundary for modifiers. The returned lease is the
        /// only supported way to write or release that boundary. Its slot id remains private so a
        /// delayed writer cannot attach modifiers to a newer lease that reused the same slot.
        /// </summary>
        public ESEffectLease CreateValueChangeEffectLease()
        {
            if (isValueChangeResetting)
                throw new InvalidOperationException("Cannot create a ValueChange EffectLease while the Entity is resetting or rebinding.");

            EnsureValueChangeEffectSlots();
            int slotIndex;
            ValueChangeEffectSlot slot;
            int freeLast = freeValueChangeEffectSlots.Count - 1;
            if (freeLast >= 0)
            {
                slotIndex = freeValueChangeEffectSlots[freeLast];
                freeValueChangeEffectSlots.RemoveAt(freeLast);
                slot = valueChangeEffectSlots[slotIndex];
            }
            else
            {
                slotIndex = valueChangeEffectSlots.Count;
                slot = default;
                valueChangeEffectSlots.Add(slot);
            }

            if (slot.generation == int.MaxValue)
                throw new InvalidOperationException("Entity ValueChange effect generation exhausted.");

            slot.generation++;
            slot.isActive = true;
            valueChangeEffectSlots[slotIndex] = slot;
            activeValueChangeEffectCount++;
            return new ESEffectLease(this, slotIndex, slot.generation);
        }

        bool IESEffectLeaseOwner.IsEffectActive(int effectSlot, int generation)
        {
            return IsEffectSlotActive(effectSlot, generation);
        }

        private bool IsEffectSlotActive(int effectSlot, int generation)
        {
            return !isValueChangeResetting
                   && valueChangeEffectSlots != null
                   && (uint)effectSlot < (uint)valueChangeEffectSlots.Count
                   && valueChangeEffectSlots[effectSlot].isActive
                   && valueChangeEffectSlots[effectSlot].generation == generation;
        }

        bool IESEffectLeaseOwner.TryAddEffectFloat(
            int effectSlot,
            int generation,
            ESFloatValueChangeSet set,
            ESFloatValueChangeOp op,
            float value,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            token = ESValueChangeToken.Invalid;
            if (!IsEffectSlotActive(effectSlot, generation)
                || set == null
                || !set.IsEffectLeaseHost(this))
                return false;

            token = set.Add(op, value, effectSlot + 1, sourceId, priority, enabled);
            return token.IsValid;
        }

        bool IESEffectLeaseOwner.TryAddEffectPermit(
            int effectSlot,
            int generation,
            ESPermitSet set,
            ESPermitLaw law,
            int sourceId,
            int priority,
            bool enabled,
            out ESValueChangeToken token)
        {
            token = ESValueChangeToken.Invalid;
            if (!IsEffectSlotActive(effectSlot, generation)
                || set == null
                || !set.IsEffectLeaseHost(this))
                return false;

            token = set.Add(law, effectSlot + 1, sourceId, priority, enabled);
            return token.IsValid;
        }

        /// <summary>
        /// Lease callback. A stale or copied lease cannot release a newer effect slot because the
        /// generation must match. All Tokens owned by this effect are released across every Set.
        /// </summary>
        public bool ReleaseEffect(int effectSlot, int generation)
        {
            if (valueChangeEffectSlots == null || (uint)effectSlot >= (uint)valueChangeEffectSlots.Count)
                return false;

            ValueChangeEffectSlot slot = valueChangeEffectSlots[effectSlot];
            if (!slot.isActive || slot.generation != generation)
                return false;

            slot.isActive = false;
            valueChangeEffectSlots[effectSlot] = slot;
            try
            {
                // Keep this slot unavailable while every Set finishes its cleanup. A listener may
                // create a new effect, but it must receive a different OwnerId until this release completes.
                ReleaseAllValueChangesByOwner(effectSlot + 1);
            }
            finally
            {
                activeValueChangeEffectCount--;
                freeValueChangeEffectSlots.Add(effectSlot);
            }
            return true;
        }

        public ESFloatValueChangeSet GetFloatStat(string key, float baseValue = 0f)
        {
            return GetFloatStat(0, key, baseValue);
        }

        /// <summary>Stable-key boundary. Both aliases must resolve to the same attribute definition.</summary>
        public ESFloatValueChangeSet GetFloatStat(ushort enumKey, string key, float baseValue = 0f)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
                return GetCharacterFloatStat(characterId, baseValue);

            return TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetFloatStat(runtimeKey, baseValue)
                : null;
        }

        /// <summary>Runtime path for an already resolved catalog key.</summary>
        public ESFloatValueChangeSet GetFloatStat(int runtimeKey, float baseValue = 0f)
        {
            ThrowIfValueChangeResetting();

            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
                return null;

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                return ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId)
                    ? GetCharacterFloatStat(characterId, baseValue)
                    : GetCatalogHotFloatStat(runtimeKey, definition, baseValue);
            }

            float resolvedBaseValue = ResolveSparseFloatBase(runtimeKey, baseValue);
            if (sparseFloatStats == null || !sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet set))
            {
                set = RentSparseFloatStat(resolvedBaseValue, definition.minValue, definition.maxValue);
                EnsureSparseFloatStats().Add(runtimeKey, set);
            }
            else
            {
                ConfigureFloatStat(set, resolvedBaseValue, definition.minValue, definition.maxValue);
            }

            set.BindEffectLeaseHost(this);
            return set;
        }

        /// <summary>Gets or creates the modifier resolver for a fixed character float slot.</summary>
        public ESFloatValueChangeSet GetCharacterFloatStat(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f)
        {
            ThrowIfValueChangeResetting();

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return null;

            int index = (int)id;
            float resolvedBaseValue = ResolveCharacterFloatBase(id, fallbackBaseValue);
            ESFloatValueChangeSet set = characterFloatStats[index];
            bool activate = characterFloatStatIsActive[index] == 0;
            if (set == null)
            {
                set = new ESFloatValueChangeSet(resolvedBaseValue);
                characterFloatStats[index] = set;
            }
            else if (activate)
            {
                // A raw Set reference is runtime-only. Reset once more at activation so a stale
                // caller cannot leave modifiers in an inactive pooled slot between renters.
                set.ResetForReuse();
            }

            ConfigureFloatStat(
                set,
                resolvedBaseValue,
                characterFloatDefinitionMinimums[index],
                characterFloatDefinitionMaximums[index]);
            characterFloatStatIsActive[index] = 1;

            set.BindEffectLeaseHost(this);
            return set;
        }

        /// <summary>Returns an existing fixed-slot resolver without catalog lookup or allocation.</summary>
        public bool TryGetCharacterFloatStat(ESCharacterFloatAttributeId id, out ESFloatValueChangeSet set)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
            {
                set = null;
                return false;
            }

            int index = (int)id;
            set = characterFloatStats[index];
            return set != null && characterFloatStatIsActive[index] != 0;
        }

        /// <summary>Returns an existing float stat without creating an empty ValueChange set.</summary>
        public bool TryGetFloatStat(string key, out ESFloatValueChangeSet set)
        {
            return TryGetFloatStat(0, key, out set);
        }

        public bool TryGetFloatStat(ushort enumKey, string key, out ESFloatValueChangeSet set)
        {
            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
            {
                int characterIndex = (int)characterId;
                set = characterFloatStats[characterIndex];
                return set != null && characterFloatStatIsActive[characterIndex] != 0;
            }

            if (TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return TryGetFloatStat(runtimeKey, out set);

            set = null;
            return false;
        }

        public bool TryGetFloatStat(int runtimeKey, out ESFloatValueChangeSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                && definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                if (ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId))
                {
                    int characterIndex = (int)characterId;
                    set = characterFloatStats[characterIndex];
                    return set != null && characterFloatStatIsActive[characterIndex] != 0;
                }

                return TryGetCatalogHotFloatStat(runtimeKey, out set);
            }

            if (sparseFloatStats != null)
                return sparseFloatStats.TryGetValue(runtimeKey, out set);

            set = null;
            return false;
        }

        /// <summary>Gets the resolved float value, creating the stat with <paramref name="baseValue"/> when needed.</summary>
        public float GetFloatStatValue(string key, float baseValue = 0f)
        {
            return GetFloatStatValue(0, key, baseValue);
        }

        public float GetFloatStatValue(ushort enumKey, string key, float baseValue = 0f)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
                return GetCharacterFloatStatValue(characterId, baseValue);

            if (!TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return baseValue;

            ESFloatValueChangeSet set = GetFloatStat(runtimeKey, baseValue);
            return set != null ? set.Value : baseValue;
        }

        /// <summary>
        /// Generic Catalog HotSlot read. Callers resolve the stable identity once during setup and
        /// retain this process-local key for their hot loop; untouched Hot slots stay allocation-free.
        /// </summary>
        public float GetFloatStatValue(int runtimeKey, float fallbackBaseValue = 0f)
        {
            ThrowIfValueChangeResetting();
            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
            {
                return fallbackBaseValue;
            }

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId))
            {
                return GetCharacterFloatStatValue(characterId, fallbackBaseValue);
            }

            float resolvedBaseValue = ResolveSparseFloatBase(runtimeKey, fallbackBaseValue);
            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                if (TryGetCatalogHotFloatStat(runtimeKey, out ESFloatValueChangeSet hotSet))
                {
                    if (hotSet.BaseValue != resolvedBaseValue)
                        hotSet.BaseValue = resolvedBaseValue;
                    return hotSet.Value;
                }

                return resolvedBaseValue < definition.minValue
                    ? definition.minValue
                    : (resolvedBaseValue > definition.maxValue ? definition.maxValue : resolvedBaseValue);
            }

            if (sparseFloatStats != null && sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet sparseSet))
            {
                if (sparseSet.BaseValue != resolvedBaseValue)
                    sparseSet.BaseValue = resolvedBaseValue;
                return sparseSet.Value;
            }

            return resolvedBaseValue < definition.minValue
                ? definition.minValue
                : (resolvedBaseValue > definition.maxValue ? definition.maxValue : resolvedBaseValue);
        }

        /// <summary>
        /// Fixed-slot read for KCC and combat hot paths. It performs only array access and scalar work;
        /// no string lookup, Dictionary lookup, or resolver allocation occurs for an untouched slot.
        /// </summary>
        public float GetCharacterFloatStatValue(ESCharacterFloatAttributeId id, float fallbackBaseValue = 0f)
        {
            ThrowIfValueChangeResetting();

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return fallbackBaseValue;

            float resolvedBaseValue = ResolveCharacterFloatBase(id, fallbackBaseValue);
            int index = (int)id;
            ESFloatValueChangeSet set = characterFloatStats[index];
            if (set == null || characterFloatStatIsActive[index] == 0)
                return ClampCharacterFloatValue(id, resolvedBaseValue);

            if (set.BaseValue != resolvedBaseValue)
                set.BaseValue = resolvedBaseValue;
            return set.Value;
        }

        /// <summary>
        /// Returns a structured stat view without creating a ValueChange set for an untouched
        /// character slot. This is intended for inspectors and runtime diagnostics, never KCC.
        /// </summary>
        public ESFloatStatSnapshot GetCharacterFloatStatDebugSnapshot(
            ESCharacterFloatAttributeId id,
            float fallbackBaseValue = 0f)
        {
            if (!ESCharacterAttributeCatalog.IsValid(id))
                return ESFloatStatSnapshot.FromBaseValue(fallbackBaseValue, float.NegativeInfinity, float.PositiveInfinity);

            int index = (int)id;
            ESFloatValueChangeSet set = characterFloatStats[index];
            if (set != null && characterFloatStatIsActive[index] != 0)
                return set.GetDebugSnapshot();

            return ESFloatStatSnapshot.FromBaseValue(
                ResolveCharacterFloatBase(id, fallbackBaseValue),
                characterFloatDefinitionMinimums[index],
                characterFloatDefinitionMaximums[index]);
        }

        /// <summary>
        /// Stable-key diagnostic boundary. It validates both aliases but does not materialize a
        /// resolver, so opening a debug panel cannot change Entity memory or gameplay state.
        /// </summary>
        public bool TryGetFloatStatDebugSnapshot(
            ushort enumKey,
            string key,
            float fallbackBaseValue,
            out ESFloatStatSnapshot snapshot)
        {
            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
            {
                snapshot = GetCharacterFloatStatDebugSnapshot(characterId, fallbackBaseValue);
                return true;
            }

            if (!TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
            {
                snapshot = default;
                return false;
            }

            return TryGetFloatStatDebugSnapshot(runtimeKey, fallbackBaseValue, out snapshot);
        }

        public bool TryGetFloatStatDebugSnapshot(string key, float fallbackBaseValue, out ESFloatStatSnapshot snapshot)
        {
            return TryGetFloatStatDebugSnapshot(0, key, fallbackBaseValue, out snapshot);
        }

        /// <summary>Runtime-key diagnostic path. RuntimeKey is process-local and must not be saved or sent over the network.</summary>
        public bool TryGetFloatStatDebugSnapshot(int runtimeKey, float fallbackBaseValue, out ESFloatStatSnapshot snapshot)
        {
            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition))
            {
                snapshot = default;
                return false;
            }

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId))
            {
                snapshot = GetCharacterFloatStatDebugSnapshot(characterId, fallbackBaseValue);
                return true;
            }

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                if (TryGetCatalogHotFloatStat(runtimeKey, out ESFloatValueChangeSet hotSet))
                {
                    snapshot = hotSet.GetDebugSnapshot();
                    return true;
                }

                snapshot = ESFloatStatSnapshot.FromBaseValue(
                    ResolveSparseFloatBase(runtimeKey, fallbackBaseValue),
                    definition.minValue,
                    definition.maxValue);
                return true;
            }

            if (definition.storagePolicy != ESKeyStoragePolicy.Sparse)
            {
                snapshot = default;
                return false;
            }

            if (sparseFloatStats != null && sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet set))
            {
                snapshot = set.GetDebugSnapshot();
                return true;
            }

            snapshot = ESFloatStatSnapshot.FromBaseValue(
                ResolveSparseFloatBase(runtimeKey, fallbackBaseValue),
                definition.minValue,
                definition.maxValue);
            return true;
        }

        /// <summary>
        /// Copies the current float-stat surface into caller-owned storage for an inspector or
        /// remote diagnostic panel. It contains stable identity; RuntimeKey is diagnostic-only.
        /// </summary>
        public void CopyFloatStatDebugEntriesTo(List<ESFloatStatDebugEntry> destination, float fallbackBaseValue = 0f)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (superAttributeCatalog != null)
                CopyConfiguredFloatStatDebugEntries(destination, fallbackBaseValue);

            for (int i = 0; i < (int)ESCharacterFloatAttributeId.Count; i++)
            {
                ESCharacterFloatAttributeId id = (ESCharacterFloatAttributeId)i;
                ushort enumKey = ESCharacterAttributeCatalog.GetEnumKey(id);
                string stringKey = ESCharacterAttributeCatalog.GetKey(id);
                if (ContainsFloatStatDebugEntry(destination, enumKey, stringKey))
                    continue;

                destination.Add(new ESFloatStatDebugEntry
                {
                    enumKey = enumKey,
                    stringKey = stringKey,
                    storagePolicy = ESKeyStoragePolicy.HotSlot,
                    isMaterialized = characterFloatStatIsActive[i] != 0,
                    stat = GetCharacterFloatStatDebugSnapshot(id, fallbackBaseValue)
                });
            }
        }

        /// <summary>Sets a runtime business base without affecting active modifiers.</summary>
        public void SetFloatStatBaseValue(string key, float baseValue)
        {
            SetFloatStatBaseValue(0, key, baseValue);
        }

        public void SetFloatStatBaseValue(ushort enumKey, string key, float baseValue)
        {
            ThrowIfValueChangeResetting();
            ValidateFiniteFloatBase(baseValue);

            if (TryResolveCharacterFloatSlot(enumKey, key, out ESCharacterFloatAttributeId characterId))
            {
                SetCharacterFloatStatBaseValue(characterId, baseValue);
                return;
            }

            if (!TryResolveFloatRuntimeKey(enumKey, key, out int runtimeKey))
                return;

            EnsureSparseFloatExplicitBases()[runtimeKey] = baseValue;
            if (sparseFloatStats != null && sparseFloatStats.TryGetValue(runtimeKey, out ESFloatValueChangeSet set))
                set.BaseValue = baseValue;
        }

        /// <summary>Sets a fixed runtime base without materializing a modifier resolver.</summary>
        public void SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId id, float baseValue)
        {
            ThrowIfValueChangeResetting();
            ValidateFiniteFloatBase(baseValue);

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return;

            int index = (int)id;
            characterFloatExplicitBases[index] = baseValue;
            characterFloatHasExplicitBase[index] = 1;
            ESFloatValueChangeSet set = characterFloatStats[index];
            if (set != null && characterFloatStatIsActive[index] != 0)
                set.BaseValue = baseValue;
        }

        /// <summary>Sets a permit's fallback value without changing any active permit modifiers.</summary>
        public void SetPermitFallbackValue(string key, bool fallbackValue)
        {
            SetPermitFallbackValue(0, key, fallbackValue);
        }

        public void SetPermitFallbackValue(ushort enumKey, string key, bool fallbackValue)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                SetCharacterPermitFallbackValue(characterId, fallbackValue);
                return;
            }

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return;

            EnsureSparsePermitExplicitFallbacks()[runtimeKey] = fallbackValue;
            if (sparsePermitStats != null && sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet set))
                set.FallbackValue = fallbackValue;
        }

        /// <summary>Sets a fixed permit fallback without materializing a resolver.</summary>
        public void SetCharacterPermitFallbackValue(ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            ThrowIfValueChangeResetting();

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return;

            int index = (int)id;
            characterPermitExplicitFallbacks[index] = fallbackValue ? (byte)1 : (byte)0;
            characterPermitHasExplicitFallback[index] = 1;
            ESPermitSet set = characterPermitStats[index];
            if (set != null && characterPermitStatIsActive[index] != 0)
                set.FallbackValue = fallbackValue;
        }

        public ESPermitSet GetPermit(string key, bool fallbackValue = true)
        {
            return GetPermit(0, key, fallbackValue);
        }

        /// <summary>Stable-key boundary. Both aliases must resolve to the same permit definition.</summary>
        public ESPermitSet GetPermit(ushort enumKey, string key, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
                return GetCharacterPermit(characterId, fallbackValue);

            return TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey)
                ? GetPermit(runtimeKey, fallbackValue)
                : null;
        }

        /// <summary>Runtime path for an already resolved catalog key.</summary>
        public ESPermitSet GetPermit(int runtimeKey, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (superAttributeCatalog == null
                || !superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition))
                return null;

            if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                return ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId characterId)
                    ? GetCharacterPermit(characterId, fallbackValue)
                    : GetCatalogHotPermit(runtimeKey, fallbackValue);
            }

            bool resolvedFallbackValue = ResolveSparsePermitFallback(runtimeKey, fallbackValue);
            if (sparsePermitStats == null || !sparsePermitStats.TryGetValue(runtimeKey, out ESPermitSet set))
            {
                set = RentSparsePermitStat(resolvedFallbackValue);
                EnsureSparsePermitStats().Add(runtimeKey, set);
            }
            else if (set.FallbackValue != resolvedFallbackValue)
            {
                set.FallbackValue = resolvedFallbackValue;
            }

            set.BindEffectLeaseHost(this);
            return set;
        }

        /// <summary>Gets or creates the modifier resolver for a fixed character permit slot.</summary>
        public ESPermitSet GetCharacterPermit(ESCharacterPermitAttributeId id, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return null;

            int index = (int)id;
            bool resolvedFallbackValue = ResolveCharacterPermitFallback(id, fallbackValue);
            ESPermitSet set = characterPermitStats[index];
            bool activate = characterPermitStatIsActive[index] == 0;
            if (set == null)
            {
                set = new ESPermitSet(resolvedFallbackValue);
                characterPermitStats[index] = set;
            }
            else if (activate)
            {
                set.ResetForReuse();
            }
            if (set.FallbackValue != resolvedFallbackValue)
            {
                set.FallbackValue = resolvedFallbackValue;
            }
            characterPermitStatIsActive[index] = 1;

            set.BindEffectLeaseHost(this);
            return set;
        }

        /// <summary>Gets the resolved permission value, creating the set with <paramref name="fallbackValue"/> when needed.</summary>
        public bool GetPermitValue(string key, bool fallbackValue = true)
        {
            return GetPermitValue(0, key, fallbackValue);
        }

        public bool GetPermitValue(ushort enumKey, string key, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
                return GetCharacterPermitValue(characterId, fallbackValue);

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return fallbackValue;

            ESPermitSet set = GetPermit(runtimeKey, fallbackValue);
            return set == null ? fallbackValue : set.Value;
        }

        /// <summary>Fixed-slot permit read for hot character paths; no resolver is created for the common no-modifier case.</summary>
        public bool GetCharacterPermitValue(ESCharacterPermitAttributeId id, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (!ESCharacterAttributeCatalog.IsValid(id))
                return fallbackValue;

            bool resolvedFallbackValue = ResolveCharacterPermitFallback(id, fallbackValue);
            int index = (int)id;
            ESPermitSet set = characterPermitStats[index];
            if (set == null || characterPermitStatIsActive[index] == 0)
                return resolvedFallbackValue;

            if (set.FallbackValue != resolvedFallbackValue)
                set.FallbackValue = resolvedFallbackValue;
            return set.Value;
        }

        /// <summary>Gets the resolved permission and the winning rule's metadata.</summary>
        public ESPermitLawResult GetPermitResult(string key, bool fallbackValue = true)
        {
            return GetPermitResult(0, key, fallbackValue);
        }

        public ESPermitLawResult GetPermitResult(ushort enumKey, string key, bool fallbackValue = true)
        {
            ThrowIfValueChangeResetting();

            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                int characterIndex = (int)characterId;
                ESPermitSet fixedSet = characterPermitStats[characterIndex];
                bool resolvedFallbackValue = ResolveCharacterPermitFallback(characterId, fallbackValue);
                if (fixedSet == null || characterPermitStatIsActive[characterIndex] == 0)
                    return ESPermitLawResult.Fallback(resolvedFallbackValue);

                if (fixedSet.FallbackValue != resolvedFallbackValue)
                    fixedSet.FallbackValue = resolvedFallbackValue;
                return fixedSet.Result;
            }

            if (!TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return ESPermitLawResult.Fallback(fallbackValue);

            ESPermitSet set = GetPermit(runtimeKey, fallbackValue);
            return set == null ? ESPermitLawResult.Fallback(fallbackValue) : set.Result;
        }

        /// <summary>
        /// Clears inactive Entity-level ValueChange sets and invalidates their existing tokens.
        /// Active effects must first release their leases so a live Buff cannot be left holding
        /// stale Tokens after a reset or catalog transition.
        /// </summary>
        public void ClearValueChanges()
        {
            if (activeValueChangeEffectCount != 0)
            {
                throw new InvalidOperationException(
                    "Cannot clear ValueChanges while effects are active. Release their EffectLease or remove the owning Buff first.");
            }

            if (isValueChangeResetting)
                throw new InvalidOperationException("ValueChanges are already being reset or rebound.");

            isValueChangeResetting = true;
            try
            {
                for (int i = 0; i < characterFloatStats.Length; i++)
                {
                    ESFloatValueChangeSet set = characterFloatStats[i];
                    if (set != null)
                        set.ResetForReuse();
                    characterFloatStats[i] = null;
                    characterFloatStatIsActive[i] = 0;
                }
                if (catalogHotFloatStats != null)
                {
                    for (int i = 0; i < catalogHotFloatStats.Length; i++)
                    {
                        catalogHotFloatStats[i]?.ResetForReuse();
                        catalogHotFloatStats[i] = null;
                        catalogHotFloatStatIsActive[i] = 0;
                    }
                }

                if (sparseFloatStats != null)
                {
                    foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                        set.ResetForReuse();
                    sparseFloatStats.Clear();
                }

                for (int i = 0; i < characterPermitStats.Length; i++)
                {
                    ESPermitSet set = characterPermitStats[i];
                    if (set != null)
                        set.ResetForReuse();
                    characterPermitStats[i] = null;
                    characterPermitStatIsActive[i] = 0;
                }
                if (catalogHotPermitStats != null)
                {
                    for (int i = 0; i < catalogHotPermitStats.Length; i++)
                    {
                        catalogHotPermitStats[i]?.ResetForReuse();
                        catalogHotPermitStats[i] = null;
                        catalogHotPermitStatIsActive[i] = 0;
                    }
                }

                if (sparsePermitStats != null)
                {
                    foreach (ESPermitSet set in sparsePermitStats.Values)
                        set.ResetForReuse();
                    sparsePermitStats.Clear();
                }
            }
            finally
            {
                isValueChangeResetting = false;
            }
        }

        private void ReleaseAllValueChangesByOwner(int ownerId)
        {
            for (int i = 0; i < characterFloatStats.Length; i++)
                ReleaseAllValueChangesByOwner(characterFloatStats[i], ownerId);
            if (catalogHotFloatStats != null)
            {
                for (int i = 0; i < catalogHotFloatStats.Length; i++)
                    ReleaseAllValueChangesByOwner(catalogHotFloatStats[i], ownerId);
            }
            if (sparseFloatStats != null)
            {
                foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                    ReleaseAllValueChangesByOwner(set, ownerId);
            }

            for (int i = 0; i < characterPermitStats.Length; i++)
                ReleaseAllValueChangesByOwner(characterPermitStats[i], ownerId);
            if (catalogHotPermitStats != null)
            {
                for (int i = 0; i < catalogHotPermitStats.Length; i++)
                    ReleaseAllValueChangesByOwner(catalogHotPermitStats[i], ownerId);
            }
            if (sparsePermitStats != null)
            {
                foreach (ESPermitSet set in sparsePermitStats.Values)
                    ReleaseAllValueChangesByOwner(set, ownerId);
            }
        }

        private static void ReleaseAllValueChangesByOwner(ESFloatValueChangeSet set, int ownerId)
        {
            if (set == null)
                return;

            try
            {
                set.ReleaseAllByOwner(ownerId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ReleaseAllValueChangesByOwner(ESPermitSet set, int ownerId)
        {
            if (set == null)
                return;

            try
            {
                set.ReleaseAllByOwner(ownerId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private bool HasMaterializedValueChangeSets()
        {
            for (int i = 0; i < characterFloatStats.Length; i++)
            {
                if (characterFloatStatIsActive[i] != 0)
                    return true;
            }
            if (catalogHotFloatStatIsActive != null)
            {
                for (int i = 0; i < catalogHotFloatStatIsActive.Length; i++)
                {
                    if (catalogHotFloatStatIsActive[i] != 0)
                        return true;
                }
            }
            if (sparseFloatStats != null && sparseFloatStats.Count != 0)
                return true;

            for (int i = 0; i < characterPermitStats.Length; i++)
            {
                if (characterPermitStatIsActive[i] != 0)
                    return true;
            }
            if (catalogHotPermitStatIsActive != null)
            {
                for (int i = 0; i < catalogHotPermitStatIsActive.Length; i++)
                {
                    if (catalogHotPermitStatIsActive[i] != 0)
                        return true;
                }
            }
            return sparsePermitStats != null && sparsePermitStats.Count != 0;
        }

        /// <summary>
        /// 只读查询现有许可，不创建字典项。适合交互、移动等高频运行时检查。
        /// </summary>
        public bool TryGetPermit(string key, out ESPermitSet set)
        {
            return TryGetPermit(0, key, out set);
        }

        public bool TryGetPermit(ushort enumKey, string key, out ESPermitSet set)
        {
            if (TryResolveCharacterPermitSlot(enumKey, key, out ESCharacterPermitAttributeId characterId))
            {
                int characterIndex = (int)characterId;
                set = characterPermitStats[characterIndex];
                return set != null && characterPermitStatIsActive[characterIndex] != 0;
            }

            if (TryResolvePermitRuntimeKey(enumKey, key, out int runtimeKey))
                return TryGetPermit(runtimeKey, out set);

            set = null;
            return false;
        }

        public bool TryGetPermit(int runtimeKey, out ESPermitSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                && definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
            {
                if (ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId characterId))
                {
                    int characterIndex = (int)characterId;
                    set = characterPermitStats[characterIndex];
                    return set != null && characterPermitStatIsActive[characterIndex] != 0;
                }

                return TryGetCatalogHotPermit(runtimeKey, out set);
            }

            if (sparsePermitStats != null)
                return sparsePermitStats.TryGetValue(runtimeKey, out set);

            set = null;
            return false;
        }

        private void ThrowIfValueChangeResetting()
        {
            if (isValueChangeResetting)
            {
                throw new InvalidOperationException(
                    "Cannot create or modify Entity ValueChanges while the Entity is resetting or rebinding.");
            }
        }

        private void EnsureValueChangeEffectSlots()
        {
            if (valueChangeEffectSlots == null)
                valueChangeEffectSlots = new List<ValueChangeEffectSlot>(4);
            if (freeValueChangeEffectSlots == null)
                freeValueChangeEffectSlots = new List<int>(4);
        }

        private Dictionary<int, ESFloatValueChangeSet> EnsureSparseFloatStats()
        {
            return sparseFloatStats ??= new Dictionary<int, ESFloatValueChangeSet>(4);
        }

        private ESFloatValueChangeSet GetCatalogHotFloatStat(
            int runtimeKey,
            ESSuperFloatAttributeDefinition definition,
            float fallbackBaseValue)
        {
            if (superAttributeCatalog == null || !superAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int slot))
                return null;

            EnsureCatalogHotFloatStorage();
            float resolvedBaseValue = ResolveSparseFloatBase(runtimeKey, fallbackBaseValue);
            ESFloatValueChangeSet set = catalogHotFloatStats[slot];
            bool activate = catalogHotFloatStatIsActive[slot] == 0;
            if (set == null)
            {
                set = new ESFloatValueChangeSet(resolvedBaseValue);
                catalogHotFloatStats[slot] = set;
            }
            else if (activate)
            {
                set.ResetForReuse();
            }

            ConfigureFloatStat(set, resolvedBaseValue, definition.minValue, definition.maxValue);
            catalogHotFloatStatIsActive[slot] = 1;
            set.BindEffectLeaseHost(this);
            return set;
        }

        private bool TryGetCatalogHotFloatStat(int runtimeKey, out ESFloatValueChangeSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetFloatHotSlot(runtimeKey, out int slot)
                && catalogHotFloatStats != null
                && (uint)slot < (uint)catalogHotFloatStats.Length)
            {
                set = catalogHotFloatStats[slot];
                return set != null && catalogHotFloatStatIsActive[slot] != 0;
            }

            set = null;
            return false;
        }

        private ESPermitSet GetCatalogHotPermit(int runtimeKey, bool fallbackValue)
        {
            if (superAttributeCatalog == null || !superAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int slot))
                return null;

            EnsureCatalogHotPermitStorage();
            bool resolvedFallback = ResolveSparsePermitFallback(runtimeKey, fallbackValue);
            ESPermitSet set = catalogHotPermitStats[slot];
            bool activate = catalogHotPermitStatIsActive[slot] == 0;
            if (set == null)
            {
                set = new ESPermitSet(resolvedFallback);
                catalogHotPermitStats[slot] = set;
            }
            else if (activate)
            {
                set.ResetForReuse();
            }

            if (set.FallbackValue != resolvedFallback)
                set.FallbackValue = resolvedFallback;
            catalogHotPermitStatIsActive[slot] = 1;
            set.BindEffectLeaseHost(this);
            return set;
        }

        private bool TryGetCatalogHotPermit(int runtimeKey, out ESPermitSet set)
        {
            if (superAttributeCatalog != null
                && superAttributeCatalog.TryGetPermitHotSlot(runtimeKey, out int slot)
                && catalogHotPermitStats != null
                && (uint)slot < (uint)catalogHotPermitStats.Length)
            {
                set = catalogHotPermitStats[slot];
                return set != null && catalogHotPermitStatIsActive[slot] != 0;
            }

            set = null;
            return false;
        }

        private void EnsureCatalogHotFloatStorage()
        {
            int count = superAttributeCatalog != null ? superAttributeCatalog.FloatHotSlotCount : 0;
            if (catalogHotFloatStats != null && catalogHotFloatStats.Length == count)
                return;

            catalogHotFloatStats = new ESFloatValueChangeSet[count];
            catalogHotFloatStatIsActive = new byte[count];
        }

        private void EnsureCatalogHotPermitStorage()
        {
            int count = superAttributeCatalog != null ? superAttributeCatalog.PermitHotSlotCount : 0;
            if (catalogHotPermitStats != null && catalogHotPermitStats.Length == count)
                return;

            catalogHotPermitStats = new ESPermitSet[count];
            catalogHotPermitStatIsActive = new byte[count];
        }

        private Dictionary<int, ESPermitSet> EnsureSparsePermitStats()
        {
            return sparsePermitStats ??= new Dictionary<int, ESPermitSet>(4);
        }

        private Dictionary<int, float> EnsureSparseFloatExplicitBases()
        {
            return sparseFloatExplicitBases ??= new Dictionary<int, float>(4);
        }

        private Dictionary<int, bool> EnsureSparsePermitExplicitFallbacks()
        {
            return sparsePermitExplicitFallbacks ??= new Dictionary<int, bool>(4);
        }

        private static void ConfigureFloatStat(
            ESFloatValueChangeSet set,
            float baseValue,
            float minimumValue,
            float maximumValue)
        {
            if (set.BaseValue != baseValue)
                set.BaseValue = baseValue;
            if (set.MinimumValue != minimumValue || set.MaximumValue != maximumValue)
                set.SetBounds(minimumValue, maximumValue);
        }

        private ESFloatValueChangeSet RentSparseFloatStat(float baseValue, float minimumValue, float maximumValue)
        {
            ESFloatValueChangeSet set = null;
            if (recycledSparseFloatStats != null)
            {
                int last = recycledSparseFloatStats.Count - 1;
                if (last >= 0)
                {
                    set = recycledSparseFloatStats[last];
                    recycledSparseFloatStats.RemoveAt(last);
                }
            }

            if (set == null)
                set = new ESFloatValueChangeSet(baseValue);
            else
                set.ResetForReuse();

            ConfigureFloatStat(set, baseValue, minimumValue, maximumValue);
            return set;
        }

        private ESPermitSet RentSparsePermitStat(bool fallbackValue)
        {
            ESPermitSet set = null;
            if (recycledSparsePermitStats != null)
            {
                int last = recycledSparsePermitStats.Count - 1;
                if (last >= 0)
                {
                    set = recycledSparsePermitStats[last];
                    recycledSparsePermitStats.RemoveAt(last);
                }
            }

            if (set == null)
                return new ESPermitSet(fallbackValue);

            set.ResetForReuse();
            if (set.FallbackValue != fallbackValue)
                set.FallbackValue = fallbackValue;
            return set;
        }

        private void RecycleSparseFloatStat(ESFloatValueChangeSet set)
        {
            if (set == null)
                return;

            recycledSparseFloatStats ??= new List<ESFloatValueChangeSet>(4);
            recycledSparseFloatStats.Add(set);
        }

        private void RecycleSparsePermitStat(ESPermitSet set)
        {
            if (set == null)
                return;

            recycledSparsePermitStats ??= new List<ESPermitSet>(4);
            recycledSparsePermitStats.Add(set);
        }

        private float ResolveCharacterFloatBase(ESCharacterFloatAttributeId id, float fallbackBaseValue)
        {
            int index = (int)id;
            if (characterFloatHasExplicitBase[index] != 0)
                return characterFloatExplicitBases[index];

            return characterFloatHasDefinitionBase[index] != 0
                ? characterFloatDefinitionBases[index]
                : fallbackBaseValue;
        }

        private static void ValidateFiniteFloatBase(float baseValue)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
                throw new System.ArgumentOutOfRangeException(nameof(baseValue), "Entity attribute base value must be finite.");
        }

        private bool ResolveCharacterPermitFallback(ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            int index = (int)id;
            if (characterPermitHasExplicitFallback[index] != 0)
                return characterPermitExplicitFallbacks[index] != 0;

            return characterPermitHasDefinitionFallback[index] != 0
                ? characterPermitDefinitionFallbacks[index] != 0
                : fallbackValue;
        }

        private float ResolveSparseFloatBase(int runtimeKey, float fallbackBaseValue)
        {
            if (sparseFloatExplicitBases != null && sparseFloatExplicitBases.TryGetValue(runtimeKey, out float explicitBase))
                return explicitBase;

            superAttributeCatalog.TryResolveFloatBase(runtimeKey, fallbackBaseValue, out float resolvedBaseValue);
            return resolvedBaseValue;
        }

        private bool ResolveSparsePermitFallback(int runtimeKey, bool fallbackValue)
        {
            if (sparsePermitExplicitFallbacks != null && sparsePermitExplicitFallbacks.TryGetValue(runtimeKey, out bool explicitFallback))
                return explicitFallback;

            superAttributeCatalog.TryResolvePermitFallback(runtimeKey, fallbackValue, out bool resolvedFallbackValue);
            return resolvedFallbackValue;
        }

        private float ClampCharacterFloatValue(ESCharacterFloatAttributeId id, float value)
        {
            int index = (int)id;
            float minimum = characterFloatDefinitionMinimums[index];
            float maximum = characterFloatDefinitionMaximums[index];
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        private void CopyConfiguredFloatStatDebugEntries(List<ESFloatStatDebugEntry> destination, float fallbackBaseValue)
        {
            if (superAttributeCatalog == null)
                return;

            int definitionCount = superAttributeCatalog.FloatDefinitionCount;
            if (destination.Capacity < definitionCount)
                destination.Capacity = definitionCount;

            for (int i = 0; i < definitionCount; i++)
            {
                if (!superAttributeCatalog.TryGetFloatDefinitionAt(i, out ESSuperFloatAttributeDefinition definition))
                    continue;

                int runtimeKey = 0;
                bool hasRuntimeKey = superAttributeCatalog != null
                                     && superAttributeCatalog.TryGetRuntimeKey(definition.enumKey, definition.StringKey, out runtimeKey);
                bool isMaterialized = false;
                ESFloatStatSnapshot snapshot;
                if (hasRuntimeKey && TryGetFloatStatDebugSnapshot(runtimeKey, fallbackBaseValue, out snapshot))
                {
                    if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                        && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId characterId))
                    {
                        isMaterialized = characterFloatStatIsActive[(int)characterId] != 0;
                    }
                    else if (definition.storagePolicy == ESKeyStoragePolicy.HotSlot)
                    {
                        isMaterialized = TryGetCatalogHotFloatStat(runtimeKey, out _);
                    }
                    else if (definition.storagePolicy == ESKeyStoragePolicy.Sparse)
                    {
                        isMaterialized = sparseFloatStats != null && sparseFloatStats.ContainsKey(runtimeKey);
                    }
                }
                else
                {
                    float baseValue = definition.overrideBaseValue ? definition.baseValue : fallbackBaseValue;
                    snapshot = ESFloatStatSnapshot.FromBaseValue(baseValue, definition.minValue, definition.maxValue);
                }

                destination.Add(new ESFloatStatDebugEntry
                {
                    enumKey = definition.enumKey,
                    stringKey = definition.StringKey,
                    displayName = definition.displayName,
                    runtimeKey = runtimeKey,
                    storagePolicy = definition.storagePolicy,
                    isMaterialized = isMaterialized,
                    stat = snapshot
                });
            }
        }

        private static bool ContainsFloatStatDebugEntry(
            List<ESFloatStatDebugEntry> entries,
            ushort enumKey,
            string stringKey)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ESFloatStatDebugEntry entry = entries[i];
                if (entry.enumKey == enumKey && string.Equals(entry.stringKey, stringKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves authored fixed-slot defaults exactly once per table bind. KCC later reads only
        /// these compact arrays and its existing resolver slots; custom sparse definitions stay in
        /// the Catalog path because they are never part of the motion hot loop.
        /// </summary>
        private void RebuildFixedSlotDefinitionCache()
        {
            Array.Clear(characterFloatDefinitionBases, 0, characterFloatDefinitionBases.Length);
            Array.Clear(characterFloatHasDefinitionBase, 0, characterFloatHasDefinitionBase.Length);
            Array.Clear(characterPermitDefinitionFallbacks, 0, characterPermitDefinitionFallbacks.Length);
            Array.Clear(characterPermitHasDefinitionFallback, 0, characterPermitHasDefinitionFallback.Length);

            for (int i = 0; i < characterFloatDefinitionMinimums.Length; i++)
            {
                characterFloatDefinitionMinimums[i] = float.NegativeInfinity;
                characterFloatDefinitionMaximums[i] = float.PositiveInfinity;
            }

            if (superAttributeCatalog == null)
                return;

            for (int i = 0; i < characterFloatStats.Length; i++)
            {
                ESCharacterFloatAttributeId id = (ESCharacterFloatAttributeId)i;
                ushort enumKey = ESCharacterAttributeCatalog.GetEnumKey(id);
                if (!superAttributeCatalog.TryGetRuntimeKey(enumKey, ESCharacterAttributeCatalog.GetKey(id), out int runtimeKey)
                    || !superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                    || definition.storagePolicy != ESKeyStoragePolicy.HotSlot
                    || definition.enumKey != enumKey)
                {
                    continue;
                }

                characterFloatDefinitionMinimums[i] = definition.minValue;
                characterFloatDefinitionMaximums[i] = definition.maxValue;
                characterFloatDefinitionBases[i] = definition.baseValue;
                characterFloatHasDefinitionBase[i] = definition.overrideBaseValue ? (byte)1 : (byte)0;
            }

            for (int i = 0; i < characterPermitStats.Length; i++)
            {
                ESCharacterPermitAttributeId id = (ESCharacterPermitAttributeId)i;
                ushort enumKey = ESCharacterAttributeCatalog.GetEnumKey(id);
                if (!superAttributeCatalog.TryGetRuntimeKey(enumKey, ESCharacterAttributeCatalog.GetKey(id), out int runtimeKey)
                    || !superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                    || definition.storagePolicy != ESKeyStoragePolicy.HotSlot
                    || definition.enumKey != enumKey
                    || !definition.overrideFallbackValue)
                {
                    continue;
                }

                characterPermitDefinitionFallbacks[i] = definition.fallbackValue ? (byte)1 : (byte)0;
                characterPermitHasDefinitionFallback[i] = 1;
            }
        }

        private bool TryResolveFloatRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return superAttributeCatalog != null
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out runtimeKey)
                   && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out _);
        }

        private bool TryResolvePermitRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return superAttributeCatalog != null
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out runtimeKey)
                   && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out _);
        }

        private bool TryResolveCharacterFloatSlot(ushort enumKey, string key, out ESCharacterFloatAttributeId id)
        {
            bool enumConfigured = enumKey != 0;
            bool stringConfigured = !string.IsNullOrEmpty(key);
            ESCharacterFloatAttributeId enumId = default;
            ESCharacterFloatAttributeId stringId = default;
            bool hasEnum = enumConfigured && ESCharacterAttributeCatalog.TryGetFloatId(enumKey, out enumId);
            bool hasString = stringConfigured && ESCharacterAttributeCatalog.TryGetFloatId(key, out stringId);

            if ((enumConfigured && !hasEnum)
                || (stringConfigured && !hasString)
                || (hasEnum && hasString && enumId != stringId))
            {
                id = default;
                return false;
            }

            id = hasEnum ? enumId : stringId;
            if (!hasEnum && !hasString)
                return false;

            if (superAttributeTable != null && superAttributeCatalog == null)
                return false;

            if (superAttributeCatalog == null)
                return true;

            return superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey)
                   && superAttributeCatalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition)
                   && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                   && ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out ESCharacterFloatAttributeId catalogId)
                   && catalogId == id;
        }

        private bool TryResolveCharacterPermitSlot(ushort enumKey, string key, out ESCharacterPermitAttributeId id)
        {
            bool enumConfigured = enumKey != 0;
            bool stringConfigured = !string.IsNullOrEmpty(key);
            ESCharacterPermitAttributeId enumId = default;
            ESCharacterPermitAttributeId stringId = default;
            bool hasEnum = enumConfigured && ESCharacterAttributeCatalog.TryGetPermitId(enumKey, out enumId);
            bool hasString = stringConfigured && ESCharacterAttributeCatalog.TryGetPermitId(key, out stringId);

            if ((enumConfigured && !hasEnum)
                || (stringConfigured && !hasString)
                || (hasEnum && hasString && enumId != stringId))
            {
                id = default;
                return false;
            }

            id = hasEnum ? enumId : stringId;
            if (!hasEnum && !hasString)
                return false;

            if (superAttributeTable != null && superAttributeCatalog == null)
                return false;

            if (superAttributeCatalog == null)
                return true;

            return superAttributeCatalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey)
                   && superAttributeCatalog.TryGetPermitDefinition(runtimeKey, out ESSuperPermitAttributeDefinition definition)
                   && definition.storagePolicy == ESKeyStoragePolicy.HotSlot
                   && ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out ESCharacterPermitAttributeId catalogId)
                   && catalogId == id;
        }


        /// <summary>
        /// Pool return and destruction both invalidate active EffectLeases. Slots retain their
        /// generation across lifecycles, so a copied old lease cannot observe a reused owner id.
        /// </summary>
        private void ResetValueChangesForLifecycleEnd()
        {
            if (isValueChangeResetting)
                return;

            isValueChangeResetting = true;
            try
            {
                InvalidateAllValueChangeEffectSlots();
                ClearValueChangeRuntimeBasesForLifecycleEnd();
                ClearValueChangeSetsForLifecycleEnd();
            }
            finally
            {
                activeValueChangeEffectCount = 0;
                isValueChangeResetting = false;
            }
        }

        private void InvalidateAllValueChangeEffectSlots()
        {
            if (valueChangeEffectSlots == null)
                return;

            EnsureValueChangeEffectSlots();
            freeValueChangeEffectSlots.Clear();
            for (int i = 0; i < valueChangeEffectSlots.Count; i++)
            {
                ValueChangeEffectSlot slot = valueChangeEffectSlots[i];
                slot.isActive = false;
                valueChangeEffectSlots[i] = slot;
                freeValueChangeEffectSlots.Add(i);
            }

            activeValueChangeEffectCount = 0;
        }

        private void ClearValueChangeRuntimeBasesForLifecycleEnd()
        {
            Array.Clear(characterFloatExplicitBases, 0, characterFloatExplicitBases.Length);
            Array.Clear(characterFloatHasExplicitBase, 0, characterFloatHasExplicitBase.Length);
            Array.Clear(characterPermitExplicitFallbacks, 0, characterPermitExplicitFallbacks.Length);
            Array.Clear(characterPermitHasExplicitFallback, 0, characterPermitHasExplicitFallback.Length);
            sparseFloatExplicitBases?.Clear();
            sparsePermitExplicitFallbacks?.Clear();
        }

        private void ClearValueChangeSetsForLifecycleEnd()
        {
            for (int i = 0; i < characterFloatStats.Length; i++)
            {
                try
                {
                    characterFloatStats[i]?.ResetForReuse();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    characterFloatStatIsActive[i] = 0;
                }
            }

            if (catalogHotFloatStats != null)
            {
                for (int i = 0; i < catalogHotFloatStats.Length; i++)
                {
                    try
                    {
                        catalogHotFloatStats[i]?.ResetForReuse();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        catalogHotFloatStatIsActive[i] = 0;
                    }
                }
            }

            if (sparseFloatStats != null)
            {
                foreach (ESFloatValueChangeSet set in sparseFloatStats.Values)
                {
                    try
                    {
                        set.ResetForReuse();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        RecycleSparseFloatStat(set);
                    }
                }
                sparseFloatStats.Clear();
            }

            for (int i = 0; i < characterPermitStats.Length; i++)
            {
                try
                {
                    characterPermitStats[i]?.ResetForReuse();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    characterPermitStatIsActive[i] = 0;
                }
            }

            if (catalogHotPermitStats != null)
            {
                for (int i = 0; i < catalogHotPermitStats.Length; i++)
                {
                    try
                    {
                        catalogHotPermitStats[i]?.ResetForReuse();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        catalogHotPermitStatIsActive[i] = 0;
                    }
                }
            }

            if (sparsePermitStats != null)
            {
                foreach (ESPermitSet set in sparsePermitStats.Values)
                {
                    try
                    {
                        set.ResetForReuse();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                    finally
                    {
                        RecycleSparsePermitStat(set);
                    }
                }
                sparsePermitStats.Clear();
            }
        }

        #endregion

        #region KCC API

        public void SetMoveInput(Vector3 moveInput)
        {
            kcc.SetMoveInput(moveInput);
        }

        public void SetLookInput(Vector3 lookInput)
        {
            kcc.SetLookInput(lookInput);
        }

        public void ResetKCCInputs()
        {
            kcc.ResetInputs();
        }

        public void RequestJump()
        {
            kcc.RequestJump();
        }

        public void SetCrouch(bool enable)
        {
            kcc.SetCrouch(enable);
        }

        public void SetRootMotionVelocity(Vector3 velocity)
        {
            kcc.SetRootMotionVelocity(velocity);
        }

        public void ClearRootMotionVelocity()
        {
            kcc.ClearRootMotionVelocity();
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            kcc.SetSpeedMultiplier(multiplier);
        }

        public void SetSpeedLimit(float limit)
        {
            kcc.SetSpeedLimit(limit);
        }

        public void ResetSpeedModifiers()
        {
            kcc.ResetSpeedModifiers();
        }

        public void SetLocomotionSupportFlags(StateSupportFlags flags)
        {
            stateDomain.stateMachine.SetSupportFlags(flags);
        }

        public void SetVerticalInput(float input)
        {
            kcc.SetVerticalInput(input);
        }

        #endregion

        #region ICharacterController

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            kcc.UpdateRotation(this, ref currentRotation, deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            kcc.UpdateVelocity(this, ref currentVelocity, deltaTime);
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            kcc.BeforeCharacterUpdate(this, deltaTime);
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            kcc.PostGroundingUpdate(this, deltaTime);
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            kcc.AfterCharacterUpdate(this, deltaTime);
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return kcc.IsColliderValidForCollisions(this, coll);
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            kcc.OnGroundHit(this, hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            kcc.OnMovementHit(this, hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            kcc.ProcessHitStabilityReport(this, hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, ref hitStabilityReport);
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            kcc.OnDiscreteCollisionDetected(this, hitCollider);
        }

        #endregion
    }

    /// <summary>
    /// 将“一个被控制实体”的部分 GameTag 单向投影为全局 RuntimeModeTag。
    /// 该投影只服务输入/UI 策略，不能反向修改实体事实；多人或多实体场景必须由控制权系统
    /// 显式 Bind 当前本地控制实体，避免把其他 NPC 的死亡、眩晕投影到玩家输入。
    /// </summary>
    public sealed class ESGameTagRuntimeModeProjector : IDisposable, IReceiveLink<ESTagPresenceChangedLink>
    {
        private Entity entity;
        private ESRuntimeModeService modeService;
        private ESRuntimeModeTagHandle combatHandle;
        private ESRuntimeModeTagHandle aimingHandle;
        private ESRuntimeModeTagHandle mountedHandle;
        private ESRuntimeModeTagHandle climbingHandle;
        private ESRuntimeModeTagHandle deadHandle;
        private ESRuntimeModeTagHandle stunnedHandle;

        public bool IsBound => entity != null && modeService != null;

        public void Bind(Entity controlledEntity, ESRuntimeModeService runtimeModeService)
        {
            Dispose();
            if (controlledEntity == null || runtimeModeService == null)
                return;

            entity = controlledEntity;
            modeService = runtimeModeService;
            entity.Tags.AddPresenceChangedReceiver(this);

            Sync(ESGameTag.战斗类_战斗中);
            Sync(ESGameTag.战斗类_瞄准中);
            Sync(ESGameTag.移动类_骑乘中);
            Sync(ESGameTag.移动类_攀爬中);
            Sync(ESGameTag.生命类_死亡);
            Sync(ESGameTag.控制类_眩晕);
        }

        public void Dispose()
        {
            if (entity != null)
                entity.Tags.RemovePresenceChangedReceiver(this);

            Release(ref combatHandle);
            Release(ref aimingHandle);
            Release(ref mountedHandle);
            Release(ref climbingHandle);
            Release(ref deadHandle);
            Release(ref stunnedHandle);
            entity = null;
            modeService = null;
        }

        private void Sync(ESGameTag tag)
        {
            if (entity != null)
                HandleGameTagPresenceChanged(tag, entity.HasGameTag(tag));
        }

        void IReceiveLink<ESTagPresenceChangedLink>.OnLink(ESTagPresenceChangedLink link)
        {
            if (ESGameTagCatalog.TryFromCoreId(link.Tag, out ESGameTag coreTag))
                HandleGameTagPresenceChanged(coreTag, link.IsPresent);
        }

        private void HandleGameTagPresenceChanged(ESGameTag tag, bool present)
        {
            if (modeService == null)
                return;

            switch (tag)
            {
                case ESGameTag.战斗类_战斗中:
                    SynchronizeTag(ESRuntimeModeTag.Combat, ref combatHandle, present);
                    break;
                case ESGameTag.战斗类_瞄准中:
                    SynchronizeTag(ESRuntimeModeTag.Aiming, ref aimingHandle, present);
                    break;
                case ESGameTag.移动类_骑乘中:
                    SynchronizeTag(ESRuntimeModeTag.Mounted, ref mountedHandle, present);
                    break;
                case ESGameTag.移动类_攀爬中:
                    SynchronizeTag(ESRuntimeModeTag.Climbing, ref climbingHandle, present);
                    break;
                case ESGameTag.生命类_死亡:
                    SynchronizeTag(ESRuntimeModeTag.Dead, ref deadHandle, present);
                    break;
                case ESGameTag.控制类_眩晕:
                    SynchronizeTag(ESRuntimeModeTag.Stunned, ref stunnedHandle, present);
                    break;
            }
        }

        private void SynchronizeTag(ESRuntimeModeTag tag, ref ESRuntimeModeTagHandle handle, bool present)
        {
            if (present)
            {
                if (!handle.IsValid)
                    handle = modeService.AddTag(tag, entity);
                return;
            }

            Release(ref handle);
        }

        private void Release(ref ESRuntimeModeTagHandle handle)
        {
            if (modeService != null && handle.IsValid)
                modeService.RemoveTag(handle);
            handle = ESRuntimeModeTagHandle.Invalid;
        }
    }

    #region KCC Data

    [Serializable]
    public class EntityKCCData
    {
        [Title("KCC 组件")]
        [LabelText("角色运动器")]
        public KinematicCharacterMotor motor;

        [Title("稳定地面移动")]
        [LabelText("地面最大速度")]
        public float maxStableMoveSpeed = 8f;
        [LabelText("地面速度响应")]
        public float stableMovementSharpness = 15f;

        [Title("空中移动")]
        [LabelText("空中最大速度")]
        public float maxAirMoveSpeed = 8f;
        [LabelText("空中加速度")]
        public float airAccelerationSpeed = 5f;
        [LabelText("空中阻力")]
        public float drag = 0.1f;

        [Title("速度倍率/限速")]
        [LabelText("速度倍率")]
        public float speedMultiplier = 1f;
        [LabelText("平面速度上限")]
        [Tooltip("<=0 表示不限制")]
        public float speedLimit = 0f;

        [Title("跳跃")]
        [LabelText("基础跳跃速度")]
        public float jumpSpeed = 8f;
        [LabelText("跳跃速度倍率")]
        [Tooltip("跳跃速度倍率（降低跳跃高度）")]
        public float jumpSpeedMultiplier = 0.8f;
        [LabelText("上升重力倍率")]
        [Tooltip("上升阶段重力倍率(>1 更短更硬)")]
        public float jumpApexGravityMultiplier = 2f;
        [LabelText("下落重力倍率")]
        [Tooltip("下落阶段重力倍率(>1 更快落地)")]
        public float jumpFallGravityMultiplier = 1.3f;

        [Title("下蹲")]
        [LabelText("站立胶囊高度")]
        public float standingCapsuleHeight = 2f;
        [LabelText("下蹲胶囊高度")]
        public float crouchedCapsuleHeight = 1f;
        [LabelText("下蹲速度倍率")]
        [Tooltip("下蹲移动速度倍率")]
        public float crouchSpeedMultiplier = 0.5f;

        [Title("旋转")]
        [LabelText("朝向响应")]
        public float orientationSharpness = 10f;

        [Title("重力")]
        [LabelText("重力向量")]
        public Vector3 gravity_ = new Vector3(0f, -9.81f, 0f);

        [Title("跳跃请求")]
        [LabelText("跳跃请求缓冲时长(秒)")]
        [Tooltip("跳跃请求超过该时长仍未在地面被消费，则自动过期，避免落地后二次起跳。")]
        public float jumpRequestBufferTime = 0.12f;

        [Title("根运动")]
        [LabelText("启用根运动速度")]
        public bool useRootMotion = true;
        [LabelText("根运动倍率")]
        public float rootMotionScale = 1f;
        [LabelText("仅稳定地面应用")]
        public bool rootMotionGroundOnly = true;

        [Title("输入（世界空间）")]
        [LabelText("移动输入")]
        public Vector3 moveInput;
        [LabelText("朝向输入")]
        public Vector3 lookInput;

        [LabelText("垂直输入")]
        public float verticalInput;

        [Title("Monitor（运行监视）")]
        [HideLabel]
        public EntityKCCMonitor monitor = new EntityKCCMonitor();

        [LabelText("Monitor调试")]
        public bool debugMonitor = false;

        [LabelText("防止静止上漂")]
        public bool preventUpwardDriftWhenIdle = true;

        [LabelText("上漂阈值(米/帧)")]
        public float upwardDriftThreshold = 0.005f;

        private Vector3 _lastVelocity;
        private Vector3 _rootMotionVelocity;
        private int _rootMotionWriteFrame = -1;
        private bool _jumpRequested;
        private float _jumpRequestTime = -999f;
        private bool _crouchRequested;
        private bool _isCrouched;
        private Vector3 _lastTransientPosition;

        [NonSerialized] private bool _matchTargetPoseActive;
        [NonSerialized] private bool _matchTargetReleaseAfterApply;
        [NonSerialized] private Vector3 _matchTargetPendingPosition;
        [NonSerialized] private Quaternion _matchTargetPendingRotation = Quaternion.identity;
        [NonSerialized] private int _matchTargetPoseSequence;
        [NonSerialized] private int _matchTargetConsumedSequence;
        [NonSerialized] private bool _matchTargetAppliedThisTick;

        [ShowInInspector, ReadOnly, LabelText("跳跃请求中")]
        public bool JumpRequested => _jumpRequested;

        [ShowInInspector, ReadOnly, LabelText("最近KCC跳跃请求帧")]
        public int lastKccJumpRequestFrame;

        [ShowInInspector, ReadOnly, LabelText("最近KCC起跳帧")]
        public int lastKccJumpApplyFrame;

        [ShowInInspector, ReadOnly, LabelText("最近KCC跳跃过期帧")]
        public int lastKccJumpExpiredFrame;

        [NonSerialized]
        public EntityBasicFlyModule flyModule;

        [NonSerialized]
        public EntityBasicSwimModule swimModule;

        [NonSerialized]
        public EntityBasicClimbModule climbModule;

        [NonSerialized]
        public EntityBasicMountModule mountModule;

        [NonSerialized] private ESWorkScheduler<IEntityKCCBeforeMotion> _beforeScheduler;
        [NonSerialized] private ESWorkScheduler<IEntityKCCRotationMotion> _rotationScheduler;
        [NonSerialized] private ESWorkScheduler<IEntityKCCVelocityMotion> _velocityScheduler;
        [NonSerialized] private StateMachine _stateMachine;
        [NonSerialized] private StateSupportFlags _currentSupportFlags;
        [NonSerialized] private bool _motionSchedulersReady;

        [NonSerialized] public int workSelf;
        [NonSerialized] public int workWorld;
        [NonSerialized] public int workOther;

        public StateSupportFlags CurrentSupportFlags => _currentSupportFlags;

        [ShowInInspector, ReadOnly, LabelText("注册的运动前置任务")]
        public int RegisteredBeforeMotionCount => _beforeScheduler != null ? _beforeScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("注册的旋转任务")]
        public int RegisteredRotationMotionCount => _rotationScheduler != null ? _rotationScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("注册的速度任务")]
        public int RegisteredVelocityMotionCount => _velocityScheduler != null ? _velocityScheduler.Count : 0;

        [ShowInInspector, ReadOnly, LabelText("扩展运动已接管速度")]
        public bool lastVelocityHandledByFeature;

        [ShowInInspector, ReadOnly, LabelText("MatchTarget 位姿待应用")]
        public bool HasPendingMatchTargetPose => _matchTargetPoseActive && _matchTargetPoseSequence != _matchTargetConsumedSequence;

        /// <summary>
        /// MatchTarget 活跃期间由 KCC 维护根位姿；普通朝向、重力、RootMotion 和其它速度能力不得覆盖它。
        /// 该属性只读，供 KCC 自身阶段判断，不参与业务状态机。
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("MatchTarget 运动锁定")]
        public bool IsMatchTargetMotionLocked => _matchTargetPoseActive || _matchTargetAppliedThisTick;

        public bool HasWork => workSelf > 0 || workWorld > 0 || workOther > 0;

        private static float ResolveSuperFloat(Entity owner, ESCharacterFloatAttributeId id, float fallbackValue)
        {
            return owner != null ? owner.GetCharacterFloatStatValue(id, fallbackValue) : fallbackValue;
        }

        private static bool ResolveSuperPermit(Entity owner, ESCharacterPermitAttributeId id, bool fallbackValue)
        {
            return owner != null ? owner.GetCharacterPermitValue(id, fallbackValue) : fallbackValue;
        }

        private void ResetWork()
        {
            workSelf = 100;
            workWorld = 100;
            workOther = 100;
        }

        public void StopWork()
        {
            workSelf = 0;
            workWorld = 0;
            workOther = 0;
        }

        public void Initialize(Entity owner)
        {
            if (owner == null)
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：owner 为空。");
                return;
            }
            if (motor == null)
            {
                motor = owner.GetComponent<KinematicCharacterMotor>();
                if (motor == null)
                {
                    motor = owner.gameObject.AddComponent<KinematicCharacterMotor>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[EntityKCCData] {owner.name} 缺少 KinematicCharacterMotor，已自动补齐。建议在预制体上固定配置 KCC 参数。", owner);
#endif
                }
            }
            _stateMachine = owner.stateDomain != null ? owner.stateDomain.stateMachine : null;
            if (motor != null)
            {
                motor.CharacterController = owner;
                if (motor.Capsule != null && standingCapsuleHeight <= 0f)
                {
                    standingCapsuleHeight = motor.Capsule.height;
                }
                if (crouchedCapsuleHeight <= 0f)
                {
                    crouchedCapsuleHeight = Mathf.Max(0.5f, standingCapsuleHeight * 0.5f);
                }
            }
            if (motor != null)
            {
                _lastTransientPosition = motor.TransientPosition;
            }
            else
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：缺少 KinematicCharacterMotor。");
                return;
            }

            if (_stateMachine == null)
            {
                Debug.Assert(false, "EntityKCCData.Initialize 失败：缺少 StateMachine。");
                return;
            }

            EnsureMotionSchedulers();
        }

        public void SetMoveInput(Vector3 input)
        {
            moveInput = Vector3.ClampMagnitude(input, 1f);
        }

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SetLookInput(Vector3 input)
        {
            lookInput = input.sqrMagnitude > 0f ? input.normalized : Vector3.zero;
        }

        public void ResetInputs()
        {
            moveInput = Vector3.zero;
            lookInput = Vector3.zero;
            verticalInput = 0f;
            _jumpRequested = false;
            _jumpRequestTime = -999f;
        }

        public void RequestJump()
        {
            _jumpRequested = true;
            _jumpRequestTime = Time.time;
            lastKccJumpRequestFrame = Time.frameCount;
        }

        public void SetCrouch(bool enable)
        {
            _crouchRequested = enable;
        }

        public void SetRootMotionVelocity(Vector3 velocity)
        {
            _rootMotionVelocity = velocity;
            _rootMotionWriteFrame = Time.frameCount;
        }

        public void ClearRootMotionVelocity()
        {
            _rootMotionVelocity = Vector3.zero;
            _rootMotionWriteFrame = -1;
        }

        /// <summary>
        /// 提交由 State/Animator 计算出的 MatchTarget 根位姿。
        /// 位姿在下一个 KCC BeforeCharacterUpdate 边界应用，避免普通 Update 直接争写 Motor。
        /// </summary>
        public void QueueMatchTargetPose(Vector3 position, Quaternion rotation, bool releaseAfterApply)
        {
            _matchTargetPendingPosition = position;
            _matchTargetPendingRotation = rotation;
            _matchTargetReleaseAfterApply = releaseAfterApply;
            _matchTargetPoseActive = true;

            if (_matchTargetPoseSequence == int.MaxValue)
            {
                _matchTargetPoseSequence = 1;
                _matchTargetConsumedSequence = 0;
            }
            else
            {
                _matchTargetPoseSequence++;
            }
        }

        /// <summary>
        /// 取消尚未进入物理边界的 MatchTarget 位姿。
        /// </summary>
        public void ClearMatchTargetPose()
        {
            _matchTargetPoseActive = false;
            _matchTargetReleaseAfterApply = false;
            _matchTargetConsumedSequence = _matchTargetPoseSequence;
        }

        /// <summary>
        /// 当渲染帧快于物理帧时，MatchTarget 继续以上一次尚未应用的计划位姿为计算起点，
        /// 避免多个 Update 都从同一个 Motor 物理位置重复计算而丢失推进量。
        /// </summary>
        public bool TryGetPendingMatchTargetPose(out Vector3 position, out Quaternion rotation)
        {
            if (_matchTargetPoseActive && _matchTargetPoseSequence != _matchTargetConsumedSequence)
            {
                position = _matchTargetPendingPosition;
                rotation = _matchTargetPendingRotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetSpeedLimit(float limit)
        {
            speedLimit = limit;
        }

        public void ResetSpeedModifiers()
        {
            speedMultiplier = 1f;
            speedLimit = 0f;
        }

        public void BeforeCharacterUpdate(Entity owner, float deltaTime)
        {
            _matchTargetAppliedThisTick = false;
            ApplyPendingMatchTargetPose();
            _lastTransientPosition = motor.TransientPosition;
            ApplyCrouch();

            EnsureMotionSchedulers();
            _currentSupportFlags = _stateMachine.currentSupportFlags;
            _beforeScheduler.Reset();
            ResetWork();
            if (!HasWork)
                return;

            Vector3 initialPosition = motor.TransientPosition;
            int count = _beforeScheduler.Count;
            for (int i = 0; i < count && HasWork; i++)
            {
                if (!_beforeScheduler.TryGetAlive(i, out IEntityKCCBeforeMotion task) || task == null)
                    continue;

                try
                {
                    if (task.BeforeCharacterUpdate(owner, this, initialPosition, deltaTime))
                        StopWork();
                }
                catch (Exception exception)
                {
                    LogMotionFeatureException("Before", exception);
                }
            }
        }

        private void ApplyPendingMatchTargetPose()
        {
            if (!_matchTargetPoseActive || _matchTargetPoseSequence == _matchTargetConsumedSequence)
                return;

            motor.SetPositionAndRotation(
                _matchTargetPendingPosition,
                _matchTargetPendingRotation,
                true);
            _matchTargetConsumedSequence = _matchTargetPoseSequence;
            _matchTargetAppliedThisTick = true;

            if (_matchTargetReleaseAfterApply)
            {
                _matchTargetPoseActive = false;
                _matchTargetReleaseAfterApply = false;
            }
        }

        public void UpdateRotation(Entity owner, ref Quaternion currentRotation, float deltaTime)
        {
            if (IsMatchTargetMotionLocked)
            {
                currentRotation = motor.TransientRotation;
                return;
            }

            EnsureMotionSchedulers();
            _currentSupportFlags = _stateMachine.currentSupportFlags;
            _rotationScheduler.Reset();
            ResetWork();
            if (HasWork)
            {
                Quaternion initialRotation = currentRotation;
                int count = _rotationScheduler.Count;
                for (int i = 0; i < count && HasWork; i++)
                {
                    if (!_rotationScheduler.TryGetAlive(i, out IEntityKCCRotationMotion task) || task == null)
                        continue;

                    Quaternion beforeTask = currentRotation;
                    try
                    {
                        if (task.UpdateRotation(owner, this, initialRotation, ref currentRotation, deltaTime))
                        {
                            StopWork();
                            return;
                        }
                    }
                    catch (Exception exception)
                    {
                        currentRotation = beforeTask;
                        LogMotionFeatureException("Rotation", exception);
                    }
                }
            }

            if (!ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Rotate, true))
                return;

            float finalOrientationSharpness = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.OrientationSharpness, orientationSharpness);
            if (lookInput.sqrMagnitude <= 0f || finalOrientationSharpness <= 0f)
                return;

            Vector3 smoothedLookInputDirection = Vector3.Slerp(motor.CharacterForward, lookInput, 1f - Mathf.Exp(-finalOrientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, motor.CharacterUp);
        }

        public void UpdateVelocity(Entity owner, ref Vector3 currentVelocity, float deltaTime)
        {
            bool canMove = ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Move, true);
            bool canJump = ResolveSuperPermit(owner, ESCharacterPermitAttributeId.Jump, true);
            Vector3 effectiveMoveInput = canMove ? moveInput : Vector3.zero;

            float multiplier = Mathf.Max(0f, speedMultiplier);
            float stableMaxSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.GroundMaxMoveSpeed, maxStableMoveSpeed) * multiplier;
            float airMaxSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.AirMaxMoveSpeed, maxAirMoveSpeed) * multiplier;
            float finalCrouchSpeedMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.CrouchSpeedMultiplier, crouchSpeedMultiplier);
            float finalGroundMovementSharpness = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.GroundMovementSharpness, stableMovementSharpness);
            float finalJumpSpeed = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpSpeed, jumpSpeed);
            float finalJumpSpeedMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpSpeedMultiplier, jumpSpeedMultiplier);
            float finalAirAcceleration = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.AirAcceleration, airAccelerationSpeed);
            float finalApexGravityMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpApexGravityMultiplier, jumpApexGravityMultiplier);
            float finalFallGravityMultiplier = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.JumpFallGravityMultiplier, jumpFallGravityMultiplier);
            float finalDrag = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.Drag, drag);
            float finalRootMotionScale = ResolveSuperFloat(owner, ESCharacterFloatAttributeId.RootMotionScale, rootMotionScale);
            if (_isCrouched)
                stableMaxSpeed *= Mathf.Clamp01(finalCrouchSpeedMultiplier);
            if (speedLimit > 0f)
            {
                stableMaxSpeed = Mathf.Min(stableMaxSpeed, speedLimit);
                airMaxSpeed = Mathf.Min(airMaxSpeed, speedLimit);
            }

            Vector3 targetMovementVelocity = Vector3.zero;
            bool handled = false;
            lastVelocityHandledByFeature = false;
            if (IsMatchTargetMotionLocked)
            {
                currentVelocity = Vector3.zero;
                _lastVelocity = currentVelocity;
                return;
            }

            _currentSupportFlags = _stateMachine.currentSupportFlags;
            EnsureMotionSchedulers();
            _velocityScheduler.Reset();
            ResetWork();
            if (HasWork)
            {
                Vector3 initialVelocity = currentVelocity;
                int count = _velocityScheduler.Count;
                for (int i = 0; i < count && HasWork; i++)
                {
                    if (!_velocityScheduler.TryGetAlive(i, out IEntityKCCVelocityMotion task) || task == null)
                        continue;

                    Vector3 beforeTask = currentVelocity;
                    try
                    {
                        if (task.UpdateVelocity(owner, this, initialVelocity, ref currentVelocity, deltaTime))
                        {
                            handled = true;
                            lastVelocityHandledByFeature = true;
                            StopWork();
                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        currentVelocity = beforeTask;
                        LogMotionFeatureException("Velocity", exception);
                    }
                }
            }

            if (!handled && motor.GroundingStatus.IsStableOnGround)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                    lastKccJumpExpiredFrame = Time.frameCount;
                }

                currentVelocity = motor.GetDirectionTangentToSurface(currentVelocity, motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                Vector3 inputRight = Vector3.Cross(effectiveMoveInput, motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(motor.GroundingStatus.GroundNormal, inputRight).normalized * effectiveMoveInput.magnitude;
                targetMovementVelocity = reorientedInput * stableMaxSpeed;

                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-finalGroundMovementSharpness * deltaTime));

                if (_jumpRequested && canJump)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    lastKccJumpApplyFrame = Time.frameCount;
                    motor.ForceUnground(0.1f);
                    float appliedJumpSpeed = finalJumpSpeed * Mathf.Max(0f, finalJumpSpeedMultiplier);
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp) + (motor.CharacterUp * appliedJumpSpeed);
                }
            }
            else if (!handled)
            {
                if (_jumpRequested && jumpRequestBufferTime > 0f && Time.time - _jumpRequestTime > jumpRequestBufferTime)
                {
                    _jumpRequested = false;
                    _jumpRequestTime = -999f;
                    lastKccJumpExpiredFrame = Time.frameCount;
                }

                if (effectiveMoveInput.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = effectiveMoveInput * airMaxSpeed;

                    if (motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal), motor.CharacterUp).normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, gravity_);
                    currentVelocity += velocityDiff * finalAirAcceleration * deltaTime;
                }

                float gravityScale = 1f;
                float upVel = Vector3.Dot(currentVelocity, motor.CharacterUp);
                if (upVel > 0.01f)
                    gravityScale = Mathf.Max(0f, finalApexGravityMultiplier);
                else if (upVel < -0.01f)
                    gravityScale = Mathf.Max(0f, finalFallGravityMultiplier);

                currentVelocity += gravity_ * (gravityScale * deltaTime);
                currentVelocity *= (1f / (1f + (finalDrag * deltaTime)));
            }

            if (useRootMotion)
            {
                bool rootMotionFresh = _rootMotionWriteFrame >= 0 && Time.frameCount - _rootMotionWriteFrame <= 1;
                bool canApply = rootMotionFresh && (!rootMotionGroundOnly || motor.GroundingStatus.IsStableOnGround);
                if (canApply)
                    currentVelocity += _rootMotionVelocity * finalRootMotionScale;
                else if (!rootMotionFresh)
                    _rootMotionVelocity = Vector3.zero;
            }

            if (speedLimit > 0f)
            {
                Vector3 up = motor.CharacterUp;
                Vector3 planar = Vector3.ProjectOnPlane(currentVelocity, up);
                float planarMag = planar.magnitude;
                if (planarMag > speedLimit)
                {
                    Vector3 vertical = Vector3.Project(currentVelocity, up);
                    currentVelocity = planar.normalized * speedLimit + vertical;
                }
            }

            _lastVelocity = currentVelocity;
        }

        /// <summary>
        /// 将一个运动能力注册到它实际实现的 KCC 阶段。
        /// 新增运动能力只需要实现对应接口并注册，不再修改 EntityKCCData 的中央字段表。
        /// </summary>
        public EntityKCCMotionRegistration RegisterMotionFeature(
            object feature,
            EntityKCCMotionOrder order)
        {
            EnsureMotionSchedulers();

            EntityKCCMotionRegistration registration = default;
            if (feature is IEntityKCCBeforeMotion beforeMotion)
                registration.beforeHandle = _beforeScheduler.Register(beforeMotion, order.before);
            if (feature is IEntityKCCRotationMotion rotationMotion)
                registration.rotationHandle = _rotationScheduler.Register(rotationMotion, order.rotation);
            if (feature is IEntityKCCVelocityMotion velocityMotion)
                registration.velocityHandle = _velocityScheduler.Register(velocityMotion, order.velocity);

            return registration;
        }

        /// <summary>
        /// 注销一个运动能力的全部阶段注册。重复调用安全。
        /// </summary>
        public void UnregisterMotionFeature(ref EntityKCCMotionRegistration registration)
        {
            if (_beforeScheduler != null && registration.beforeHandle.IsValid)
                _beforeScheduler.Unregister(registration.beforeHandle);
            if (_rotationScheduler != null && registration.rotationHandle.IsValid)
                _rotationScheduler.Unregister(registration.rotationHandle);
            if (_velocityScheduler != null && registration.velocityHandle.IsValid)
                _velocityScheduler.Unregister(registration.velocityHandle);

            registration.Clear();
        }

        private void EnsureMotionSchedulers()
        {
            if (_motionSchedulersReady)
                return;

            if (_beforeScheduler == null)
                _beforeScheduler = new ESWorkScheduler<IEntityKCCBeforeMotion>();
            _beforeScheduler.Warmup(8, 4);

            if (_rotationScheduler == null)
                _rotationScheduler = new ESWorkScheduler<IEntityKCCRotationMotion>();
            _rotationScheduler.Warmup(8, 4);

            if (_velocityScheduler == null)
                _velocityScheduler = new ESWorkScheduler<IEntityKCCVelocityMotion>();
            _velocityScheduler.Warmup(8, 4);

            _motionSchedulersReady = true;
        }

        private void ApplyCrouch()
        {
            if (_crouchRequested == _isCrouched) return;

            _isCrouched = _crouchRequested;
            float radius = motor.Capsule.radius;
            if (_isCrouched)
            {
                motor.SetCapsuleDimensions(radius, crouchedCapsuleHeight, crouchedCapsuleHeight * 0.5f);
            }
            else
            {
                motor.SetCapsuleDimensions(radius, standingCapsuleHeight, standingCapsuleHeight * 0.5f);
            }
        }

        public void PostGroundingUpdate(Entity owner, float deltaTime)
        {
            // 预留扩展
        }

        public void AfterCharacterUpdate(Entity owner, float deltaTime)
        {

            if (preventUpwardDriftWhenIdle)
            {
                Vector3 posDelta = motor.TransientPosition - _lastTransientPosition;
                bool noInput = moveInput.sqrMagnitude <= 0.0001f && Mathf.Abs(verticalInput) <= 0.0001f;
                bool noVelocity = _lastVelocity.sqrMagnitude <= 0.0001f && _rootMotionVelocity.sqrMagnitude <= 0.0001f;
                if (posDelta.y > upwardDriftThreshold && noInput && noVelocity)
                {
                    if (debugMonitor)
                    {
                        Debug.LogWarning($"[KCC-Monitor] Clamp upward drift | deltaY={posDelta.y:F4}");
                    }
                    motor.SetPosition(_lastTransientPosition, true);
                }
            }
            monitor.UpdateFromMotor(motor, _lastVelocity);
        }

        private void LogMotionFeatureException(string phase, Exception exception)
        {
            Debug.LogException(
                new Exception("[EntityKCC] 角色运动能力在 " + phase + " 阶段异常，已隔离并继续本帧。", exception),
                motor);
        }


        public bool IsColliderValidForCollisions(Entity owner, Collider coll)
        {
            return true;
        }

        public void OnGroundHit(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void OnMovementHit(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void ProcessHitStabilityReport(Entity owner, Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            // 预留扩展
        }

        public void OnDiscreteCollisionDetected(Entity owner, Collider hitCollider)
        {
            // 预留扩展
        }


    }

    [Serializable]
    public class EntityKCCMonitor
    {
        [LabelText("是否存在 Motor")]
        public bool hasMotor;

        [LabelText("是否稳定在地面")]
        public bool isStableOnGround;

        [LabelText("速度")]
        public Vector3 velocity;

        [LabelText("位置")]
        public Vector3 position;

        [LabelText("朝向")]
        public Quaternion rotation;

        public void UpdateFromMotor(KinematicCharacterMotor motor, Vector3 currentVelocity)
        {
            hasMotor = motor != null;
            isStableOnGround = motor.GroundingStatus.IsStableOnGround;
            velocity = currentVelocity;
            position = motor.TransientPosition;
            rotation = motor.TransientRotation;
        }
    }

    #endregion
}
