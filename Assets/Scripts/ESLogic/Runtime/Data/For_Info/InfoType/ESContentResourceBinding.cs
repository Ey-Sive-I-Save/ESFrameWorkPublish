using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Thin, reusable content-to-resource declaration. The Consumer asset is editor authority;
    /// Player code uses only the baked stable ID and never resolves the asset reference.
    /// </summary>
    [Serializable]
    public sealed class ESContentResourceBinding
    {
        [SerializeField] private ESAssetLibraryConsumer consumer;
        [SerializeField, HideInInspector] private string bakedConsumerId = string.Empty;
        [SerializeField, HideInInspector] private bool requiresConsumer;
        [SerializeField] private ESResourcePlanInfo activePlan;
        [SerializeField] private ESResourcePlanInfo exitTransitionPlan;

        public ESAssetLibraryConsumer Consumer => consumer;
        public string BakedConsumerId => bakedConsumerId;
        public ESResourcePlanInfo ActivePlan => activePlan;
        public ESResourcePlanInfo ExitTransitionPlan => exitTransitionPlan;
        public bool HasConsumerReference => consumer != null;
        public bool HasBakedConsumerId => !string.IsNullOrWhiteSpace(bakedConsumerId);
        public bool RequiresConsumer => requiresConsumer;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void SetBakedConsumerId(string consumerId, bool hasConsumer)
        {
            bakedConsumerId = consumerId?.Trim() ?? string.Empty;
            requiresConsumer = hasConsumer;
        }
    }
}
