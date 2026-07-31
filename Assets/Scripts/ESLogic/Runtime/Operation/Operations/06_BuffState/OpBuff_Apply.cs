using System;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// Applies one configured Buff operation to the current Entity main target. Targeting is
    /// intentionally separate: use the existing Targeting Ops to select self or another Entity
    /// before this Op runs.
    /// </summary>
    [Serializable, TypeRegistryItem("应用 Buff 到主目标", OperationTypeRegistryNames.Buff)]
    public sealed class OpBuff_ApplyToMainTarget : ESOutputOp
    {
        [InlineProperty, LabelText("Buff")]
        public ESBuffConfigKey buff = new ESBuffConfigKey();

        [InlineProperty, LabelText("操作")]
        public ESBuffOperation operation = ESBuffOperation.Default;

        [LabelText("自定义来源 ID（仅自定义来源隔离）")]
        public int customSourceId;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            Entity recipient = target != null ? target.entityMainTarget : null;
            if (recipient == null || buff == null || !buff.IsConfigured)
                return;

            recipient.buffDomain.ApplyBuff(
                buff,
                operation,
                target,
                RuntimeSupport(scopeSupport, hostSupport),
                customSourceId);
        }
    }
}
