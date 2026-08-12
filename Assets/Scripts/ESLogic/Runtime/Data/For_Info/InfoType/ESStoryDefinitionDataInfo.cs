using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESStoryKind : byte { Quest, Dialogue, Story }
    public enum ESStoryNodeKind : byte { Start, Dialogue, Choice, Condition, Action, Complete, Fail }
    public enum ESStoryValidationSeverity : byte { Info, Warning, Error }

    [Serializable]
    public sealed class ESStoryOptionDefinition
    {
        [LabelText("选项 ID")]
        public string optionId;
        [LabelText("显示文本"), TextArea(2, 5)]
        public string text;
        [LabelText("目标节点 ID")]
        public string nextNodeId;
    }

    [Serializable]
    public sealed class ESStoryNodeDefinition
    {
        [HorizontalGroup("Id", Width = 0.56f), LabelText("节点 ID")]
        public string nodeId;
        [HorizontalGroup("Id"), LabelText("节点类型")]
        public ESStoryNodeKind nodeKind;

        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Dialogue || nodeKind == ES.ESStoryNodeKind.Choice")]
        [LabelText("说话者")]
        public string speakerName;
        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Dialogue || nodeKind == ES.ESStoryNodeKind.Choice")]
        [LabelText("文本"), TextArea(3, 8)]
        public string text;

        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Start || nodeKind == ES.ESStoryNodeKind.Dialogue || nodeKind == ES.ESStoryNodeKind.Action")]
        [LabelText("下一节点 ID")]
        public string nextNodeId;

        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Choice")]
        [LabelText("选项"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<ESStoryOptionDefinition> options = new List<ESStoryOptionDefinition>();

        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Condition")]
        [LabelText("交互者 Tag 条件")]
        public ESTagConditionConfig tagCondition = new ESTagConditionConfig();
        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Condition"), LabelText("条件成立节点")]
        public string trueNodeId;
        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Condition"), LabelText("条件不成立节点")]
        public string falseNodeId;

        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Action"), LabelText("Action ID")]
        public string actionId;
        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Action"), LabelText("设置 Tag")]
        public ESTagStableReference setTag;
        [ShowIf("@nodeKind == ES.ESStoryNodeKind.Action"), LabelText("绝对状态")]
        public bool setTagActive = true;
    }

    [Serializable]
    public sealed class ESStoryValidationIssue
    {
        public ESStoryValidationSeverity severity;
        public string code;
        public string message;
        public string nodeId;
    }

    [ESCreatePath("数据信息/GameCore", "任务与剧情定义")]
    public sealed class ESStoryDefinitionDataInfo : SoDataInfo, IGameCoreSO
    {
        [ESConfigKeyUsage(ESConfigKeyUsage.Declaration)]
        [TitleGroup("任务与剧情"), LabelText("Definition ID"), InlineProperty]
        public ESStoryConfigKey definitionId = new ESStoryConfigKey();
        [TitleGroup("任务与剧情"), LabelText("类型")]
        public ESStoryKind storyKind = ESStoryKind.Quest;
        [TitleGroup("任务与剧情"), LabelText("显示名称")]
        public string displayName;
        [TitleGroup("任务与剧情"), LabelText("说明"), TextArea(2, 6)]
        public string description;

        [TitleGroup("版本与入口"), LabelText("内容版本"), MinValue(1)]
        public int contentVersion = 1;
        [TitleGroup("版本与入口"), LabelText("入口节点 ID")]
        public string entryNodeId;

        [TitleGroup("节点")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "nodeId")]
        public List<ESStoryNodeDefinition> nodes = new List<ESStoryNodeDefinition>();

        [ShowInInspector, ReadOnly, LabelText("内容签名")]
        public string ContentSignature => ESStoryDefinitionSnapshot.CalculateSignature(this);

        [Button("校验任务与剧情定义"), PropertyOrder(-10)]
        public void ValidateInInspector()
        {
            List<ESStoryValidationIssue> issues = ESStoryDefinitionValidator.Validate(this);
            if (issues.Count == 0)
            {
                Debug.Log("[Story] 定义校验通过：" + name, this);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ESStoryValidationIssue issue = issues[i];
                string text = $"[Story][{issue.severity}] {issue.code}: {issue.message}";
                if (issue.severity == ESStoryValidationSeverity.Error) Debug.LogError(text, this);
                else if (issue.severity == ESStoryValidationSeverity.Warning) Debug.LogWarning(text, this);
                else Debug.Log(text, this);
            }
        }

        public void InjectGameCoreTables()
        {
            ESStoryDefinitionCatalog.Inject(this);
        }
    }

    public sealed class ESStoryOptionSnapshot
    {
        public string OptionId { get; }
        public string Text { get; }
        public string NextNodeId { get; }
        internal ESStoryOptionSnapshot(ESStoryOptionDefinition source)
        {
            OptionId = source.optionId;
            Text = source.text ?? string.Empty;
            NextNodeId = source.nextNodeId;
        }
    }

    public sealed class ESStoryNodeSnapshot
    {
        private readonly ESStoryOptionSnapshot[] options;
        public string NodeId { get; }
        public ESStoryNodeKind NodeKind { get; }
        public string SpeakerName { get; }
        public string Text { get; }
        public string NextNodeId { get; }
        public IReadOnlyList<ESStoryOptionSnapshot> Options => options;
        public ESTagConditionRuntime TagCondition { get; }
        public string TrueNodeId { get; }
        public string FalseNodeId { get; }
        public string ActionId { get; }
        public ESTagStableReference SetTag { get; }
        public bool SetTagActive { get; }

        internal ESStoryNodeSnapshot(ESStoryNodeDefinition source)
        {
            NodeId = source.nodeId;
            NodeKind = source.nodeKind;
            SpeakerName = source.speakerName ?? string.Empty;
            Text = source.text ?? string.Empty;
            NextNodeId = source.nextNodeId;
            TrueNodeId = source.trueNodeId;
            FalseNodeId = source.falseNodeId;
            ActionId = source.actionId;
            SetTag = source.setTag;
            SetTagActive = source.setTagActive;
            ESTagConditionRuntime condition = default;
            if (source.tagCondition != null && !source.tagCondition.TryCompile(out condition, out string error))
                throw new InvalidOperationException("Story Tag 条件编译失败：" + source.nodeId + "，" + error);
            TagCondition = condition;
            int count = source.options != null ? source.options.Count : 0;
            options = new ESStoryOptionSnapshot[count];
            for (int i = 0; i < count; i++) options[i] = new ESStoryOptionSnapshot(source.options[i]);
        }
    }

    public sealed class ESStoryDefinitionSnapshot
    {
        private readonly ESStoryNodeSnapshot[] nodes;
        private readonly Dictionary<string, ESStoryNodeSnapshot> byId;
        public string DefinitionId { get; }
        public ESStoryKind StoryKind { get; }
        public int ContentVersion { get; }
        public string ContentSignature { get; }
        public string EntryNodeId { get; }
        public IReadOnlyList<ESStoryNodeSnapshot> Nodes => nodes;

        private ESStoryDefinitionSnapshot(ESStoryDefinitionDataInfo source)
        {
            DefinitionId = source.definitionId.StringKey;
            StoryKind = source.storyKind;
            ContentVersion = source.contentVersion;
            ContentSignature = CalculateSignature(source);
            EntryNodeId = source.entryNodeId;
            nodes = new ESStoryNodeSnapshot[source.nodes.Count];
            byId = new Dictionary<string, ESStoryNodeSnapshot>(source.nodes.Count, StringComparer.Ordinal);
            for (int i = 0; i < source.nodes.Count; i++)
            {
                ESStoryNodeSnapshot node = new ESStoryNodeSnapshot(source.nodes[i]);
                nodes[i] = node;
                byId.Add(node.NodeId, node);
            }
        }

        public bool TryGetNode(string nodeId, out ESStoryNodeSnapshot node) => byId.TryGetValue(nodeId, out node);

        public static bool TryBake(ESStoryDefinitionDataInfo source, out ESStoryDefinitionSnapshot snapshot, out string error)
        {
            snapshot = null;
            List<ESStoryValidationIssue> issues = ESStoryDefinitionValidator.Validate(source);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].severity != ESStoryValidationSeverity.Error) continue;
                error = issues[i].message;
                return false;
            }
            try { snapshot = new ESStoryDefinitionSnapshot(source); error = null; return true; }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static string CalculateSignature(ESStoryDefinitionDataInfo source)
        {
            if (source == null) return string.Empty;
            byte[] hash;
            using (MemoryStream stream = new MemoryStream(1024))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteString(writer, source.definitionId?.StringKey);
                writer.Write(source.contentVersion);
                writer.Write((byte)source.storyKind);
                WriteString(writer, source.entryNodeId);
                List<ESStoryNodeDefinition> orderedNodes = source.nodes != null
                    ? new List<ESStoryNodeDefinition>(source.nodes)
                    : new List<ESStoryNodeDefinition>();
                orderedNodes.Sort((left, right) => string.CompareOrdinal(left?.nodeId, right?.nodeId));
                writer.Write(orderedNodes.Count);
                for (int i = 0; i < orderedNodes.Count; i++)
                {
                    ESStoryNodeDefinition n = orderedNodes[i];
                    writer.Write(n != null);
                    if (n == null) continue;
                    WriteString(writer, n.nodeId);
                    writer.Write((byte)n.nodeKind);
                    WriteString(writer, n.nextNodeId);
                    WriteString(writer, n.trueNodeId);
                    WriteString(writer, n.falseNodeId);
                    WriteString(writer, n.actionId);
                    WriteTag(writer, n.setTag);
                    writer.Write(n.setTagActive);
                    WriteString(writer, n.speakerName);
                    WriteString(writer, n.text);
                    WriteCondition(writer, n.tagCondition);
                    int optionCount = n.options != null ? n.options.Count : 0;
                    writer.Write(optionCount);
                    for (int j = 0; j < optionCount; j++)
                    {
                        ESStoryOptionDefinition o = n.options[j];
                        writer.Write(o != null);
                        if (o == null) continue;
                        WriteString(writer, o.optionId);
                        WriteString(writer, o.nextNodeId);
                        WriteString(writer, o.text);
                    }
                }
                writer.Flush();
                stream.Position = 0;
                using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(stream);
            }
            StringBuilder hex = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
            return hex.ToString();
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private static void WriteTag(BinaryWriter writer, ESTagStableReference tag)
        {
            writer.Write((byte)tag.enumGroup);
            writer.Write(tag.enumValue);
            WriteString(writer, tag.stringKey);
        }

        private static void WriteCondition(BinaryWriter writer, ESTagConditionConfig condition)
        {
            writer.Write(condition != null);
            if (condition == null) return;
            WriteTags(writer, condition.required);
            WriteTags(writer, condition.requiredAny);
            WriteTags(writer, condition.forbidden);
        }

        private static void WriteTags(BinaryWriter writer, List<ESTagStableReference> tags)
        {
            List<ESTagStableReference> ordered = tags != null
                ? new List<ESTagStableReference>(tags)
                : new List<ESTagStableReference>();
            ordered.Sort((left, right) =>
            {
                int result = ((byte)left.enumGroup).CompareTo((byte)right.enumGroup);
                if (result != 0) return result;
                result = left.enumValue.CompareTo(right.enumValue);
                return result != 0 ? result : string.CompareOrdinal(left.stringKey, right.stringKey);
            });
            writer.Write(ordered.Count);
            for (int i = 0; i < ordered.Count; i++) WriteTag(writer, ordered[i]);
        }
    }
}
