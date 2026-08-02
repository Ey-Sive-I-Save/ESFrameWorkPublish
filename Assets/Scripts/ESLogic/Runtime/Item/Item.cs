using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/内容制作/道具/Item")]
    public partial class Item : Core, IESGameObjectPoolLifecycle, IESEffectLeaseOwner
    {
        [Title("物品定义")]
        [LabelText("Prefab 物品定义")]
        [Tooltip("Item 固有 Tag 的唯一配置来源。通用池模板可留空，由生成器在本次生成时调用 BindDefinition。")]
        public ItemDataInfo prefabDefinition;

        [System.NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Item长期OpSupport")]
        public ESOpSupport opSupport;

        [NonSerialized] private ESTagCollection tags;
        [NonSerialized] private readonly ESTagLeaseSet intrinsicTagLeases = new ESTagLeaseSet();
        [NonSerialized] private IReadOnlyList<ESTagStableReference> intrinsicTags;
        [NonSerialized] private ItemDataInfo intrinsicTagDefinition;
        [NonSerialized] private string intrinsicTagError;
        [NonSerialized] private bool waitsForTagCatalog;
        [NonSerialized] private ESTagDefinitionState intrinsicTagState;

        /// <summary>Item is a Tag Host only when its own runtime facts must be queried.</summary>
        public ESTagCollection Tags => tags ??= new ESTagCollection();
        public ESTagDefinitionState IntrinsicTagState => intrinsicTagState;
        public ItemDataInfo IntrinsicTagDefinition => intrinsicTagDefinition;
        public string IntrinsicTagError => intrinsicTagError ?? string.Empty;

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureItemOpSupport();
                return opSupport;
            }
        }

        [Title("Item Basic Domain")]
        [HideLabel, SerializeReference]
        public ItemBasicDomain basicDomain = new ItemBasicDomain();

        protected override void OnBeforeAwakeRegister()
        {
            EnsureItemOpSupport();
            EnsureItemAttributes();
            // An Item without authored or runtime Tag facts stays allocation-free here. Binding a
            // non-empty definition will create and warm its Collection only when it is applied.
            TryBindPrefabDefinition();
        }

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            EnsureItemOpSupport();
            if (basicDomain == null)
                basicDomain = new ItemBasicDomain();
            RegisterDomain(basicDomain);
        }

        protected virtual void FixedUpdate()
        {
            basicDomain?.FixedUpdateExpand();
        }

        protected override void OnDestroy()
        {
            ResetItemAttributesForLifecycleEnd();
            UnsubscribeFromAttributeCatalog();
            UnsubscribeFromTagCatalog();
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
            opSupport?.Dispose();
            opSupport = null;
        }

        /// <summary>Called before the pooled Item is deactivated; ends the current Tag lifetime.</summary>
        public void OnPoolDespawned()
        {
            ResetItemAttributesForLifecycleEnd();
            UnsubscribeFromAttributeCatalog();
            itemAttributeDefinition = null;
            UnsubscribeFromTagCatalog();
            intrinsicTagLeases.ReleaseAll();
            intrinsicTags = null;
            intrinsicTagDefinition = null;
            intrinsicTagError = null;
            intrinsicTagState = ESTagDefinitionState.Empty;
            tags?.ResetForReuse();
        }

        /// <summary>Called while inactive before the pooled Item is activated again.</summary>
        public void OnPoolSpawned()
        {
            EnsureItemOpSupport();
            EnsureItemAttributes();
            TryBindPrefabDefinition();
        }

        /// <summary>Binds the ItemDataInfo that is the sole authority for this Item's birth Tags.</summary>
        public bool BindDefinition(ItemDataInfo itemDefinition)
        {
            if (!CanBindItemAttributeDefinition(itemDefinition, out string attributeError))
            {
                itemAttributeError = attributeError;
                return false;
            }

            bool changed = !ReferenceEquals(intrinsicTagDefinition, itemDefinition)
                           || !ReferenceEquals(intrinsicTags, itemDefinition != null ? itemDefinition.tags : null);
            intrinsicTagDefinition = itemDefinition;
            intrinsicTags = itemDefinition != null ? itemDefinition.tags : null;
            BindItemAttributeDefinition(itemDefinition);
            if (changed && !ESTagRuntimeCatalog.IsBound)
                intrinsicTagLeases.ReleaseAll();

            return ApplyIntrinsicTags();
        }

        /// <summary>Applies the current definition without releasing and reacquiring an unchanged Tag set.</summary>
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

        public void ReleaseIntrinsicTags()
        {
            intrinsicTagLeases.ReleaseAll();
            intrinsicTagState = intrinsicTags == null || intrinsicTags.Count == 0
                ? ESTagDefinitionState.Empty
                : (ESTagRuntimeCatalog.IsBound ? ESTagDefinitionState.Failed : ESTagDefinitionState.Pending);
        }

        public bool HasIntrinsicTag(ESTagStableReference tag)
        {
            return intrinsicTagState == ESTagDefinitionState.Applied
                   && ESTagRuntimeCatalog.TryGetRuntimeKey(tag, out int runtimeKey)
                   && intrinsicTagLeases.Contains(ESTagId.FromInt32(runtimeKey));
        }

        /// <summary>Read-only presence checks do not materialize an empty Item Tag container.</summary>
        public bool HasTag(ESTagId tag) => tags != null && tags.Has(tag);

        public bool MatchesTagCondition(ESTagConditionConfig condition) => Tags.Matches(condition);

        public bool TryMatchesTagCondition(ESTagConditionConfig condition, out bool matches, out string error)
        {
            return Tags.TryMatches(condition, out matches, out error);
        }

        public ESTagDebugSnapshot GetTagDebugSnapshot() => Tags.GetDebugSnapshot();

        /// <summary>Creates a stable Item snapshot without Tags rebuilt from its bound definition.</summary>
        public bool TryCreateNonIntrinsicTagSnapshot(
            ESTagStableTransferScope scope,
            out ESTagStableSnapshot snapshot,
            out string error)
        {
            return intrinsicTagLeases.TryCreateSnapshotWithoutOwnedTags(Tags, scope, out snapshot, out error);
        }

        private void TryBindPrefabDefinition()
        {
            if (prefabDefinition != null)
                BindDefinition(prefabDefinition);
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

        public void EnsureItemOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.CreateStandalone();

            if (opSupport.Kind != ESOpSupportKind.Item || opSupport.OwnerItem != this)
                opSupport.InitializeItemOwner(this, GetInstanceID());
        }
    }
}
