#if UNITY_EDITOR
using System;
namespace ES
{
    /// <summary>
    /// Unified Editor-only authoring graph. AICommand and AISkill are output capabilities of the
    /// same requirement graph, not separate ScriptableObject lifecycles.
    /// </summary>
    [ESGraphAssetDomain(ESAgentGraphStableIds.DomainId, editorOnly: true)]
    public sealed partial class ESAgentAuthoringGraphAsset : ESGraphAssetBase
    {
        public override ESGraphDomainKey DomainKey => ESAgentGraphStableIds.Domain;
        public override bool CanEnableCycles => false;

        protected override bool ValidateDomainConnection(ESGraphNodeRecord outputNode,
            ESGraphPortRecord outputPort, ESGraphNodeRecord inputNode,
            ESGraphPortRecord inputPort, out string error)
        {
            if (ESAgentRelationSemantics.TryResolve(outputNode?.typeId, inputNode?.typeId,
                    outputPort?.stableKey, out ESAgentRelationKind relationKind))
            {
                if (relationKind == ESAgentRelationKind.ExecutesNext
                    && (!string.Equals(outputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)
                        || !string.Equals(inputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)))
                {
                    error = "AISkill 控制流只能连接到控制输入。";
                    return false;
                }
                if (relationKind == ESAgentRelationKind.BindsValue
                    && (string.Equals(outputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)
                        || string.Equals(inputPort.valueTypeId, ESAgentGraphStableIds.SkillControlPort,
                            StringComparison.Ordinal)))
                {
                    error = "AISkill 值流不能连接到控制端口。";
                    return false;
                }
                error = null;
                return true;
            }
            error = "当前 AI 节点阶段不允许该关系：" + (outputNode?.title ?? "输出节点")
                + " → " + (inputNode?.title ?? "输入节点");
            return false;
        }
    }
}
#endif
