using System;

namespace ES.EditorInternal
{
    [Serializable]
    public sealed class ESAgentArtifactGenerationRequest
    {
        public const int CurrentSchemaVersion = 6;
        public int schemaVersion = CurrentSchemaVersion;
        public string requestId;
        public string createdAtUtc;
        public string requestDirectory;
        public string candidateDirectory;
        public ESAgentArtifactGenerationSpec spec;
    }

    [Serializable]
    public sealed class ESAgentGraphClipboardPackage
    {
        public const int CurrentSchemaVersion = 3;
        public int schemaVersion = CurrentSchemaVersion;
        public ESAgentGraphCopyFormat format;
        public string requestId;
        public string generatedAtUtc;
        public ESAgentArtifactGenerationSpec spec;
    }

    [Serializable]
    public sealed class ESAgentArtifactCandidateManifest
    {
        public int schemaVersion = 1;
        public string requestId;
        public string summary;
        public ESAgentArtifactCandidateFile[] files = Array.Empty<ESAgentArtifactCandidateFile>();
    }

    [Serializable]
    public sealed class ESAgentArtifactCandidateFile
    {
        public ESAgentArtifactKind artifactKind;
        public string candidateRelativePath;
        public string targetProjectPath;
        public string summary;
    }

    [Serializable]
    public sealed class ESAgentArtifactApprovalManifest
    {
        public const int CurrentSchemaVersion = 4;
        public int schemaVersion = CurrentSchemaVersion;
        public string requestId;
        public string approvedAtUtc;
        public string sourceGraphId;
        public string sourceContentSignature;
        public ESGraphRiskAcceptance riskAcceptance;
        public ESAgentArtifactApprovedFile[] files = Array.Empty<ESAgentArtifactApprovedFile>();
    }

    [Serializable]
    public sealed class ESAgentArtifactApprovedFile
    {
        public ESAgentArtifactKind artifactKind;
        public string sourceGraphId;
        public string outputNodeId;
        public string artifactId;
        public string targetProjectPath;
        public string sha256;
    }

    [Serializable]
    public sealed class ESGraphSnapshotArtifact
    {
        public const int CurrentSchemaVersion = 5;
        public int schemaVersion = CurrentSchemaVersion;
        public string createdAtUtc;
        public int graphSchemaVersion;
        public string graphId;
        public string originGraphId;
        public string domainId;
        public bool allowCycles;
        public string contentSignature;
        public ESGraphRiskAcceptance riskAcceptance;
        public ESGraphSnapshotNodeArtifact[] nodes = Array.Empty<ESGraphSnapshotNodeArtifact>();
        public ESGraphSnapshotEdgeArtifact[] edges = Array.Empty<ESGraphSnapshotEdgeArtifact>();
        public ESGraphSnapshotRouteArtifact[] routes = Array.Empty<ESGraphSnapshotRouteArtifact>();
    }

    [Serializable]
    public sealed class ESGraphSnapshotNodeArtifact
    {
        public string nodeId;
        public string typeId;
        public int version;
        public string title;
        public string payloadJson;
        public ESGraphSnapshotPortArtifact[] ports = Array.Empty<ESGraphSnapshotPortArtifact>();
    }

    [Serializable]
    public sealed class ESGraphSnapshotPortArtifact
    {
        public string nodeId;
        public string portId;
        public string stableKey;
        public string name;
        public string meaning;
        public string valueTypeId;
        public ESGraphPortDirection direction;
        public ESGraphPortCapacity capacity;
        public ESGraphPortAggregation aggregation;
    }

    [Serializable]
    public sealed class ESGraphSnapshotEdgeArtifact
    {
        public string edgeId;
        public int order;
        public string outputPortId;
        public string inputPortId;
    }

    [Serializable]
    public sealed class ESGraphSnapshotRouteArtifact
    {
        public string edgeId;
        public int order;
        public string sourceNodeId;
        public string sourcePortId;
        public string sourcePortKey;
        public string sourceMeaning;
        public string sourceValueTypeId;
        public ESGraphPortAggregation sourceAggregation;
        public string targetNodeId;
        public string targetPortId;
        public string targetPortKey;
        public string targetMeaning;
        public string targetValueTypeId;
        public ESGraphPortAggregation targetAggregation;
    }

    public enum ESAgentArtifactRequestState : byte
    {
        None = 0,
        AwaitingCandidate = 1,
        AwaitingApproval = 2,
        Approved = 3,
        Stale = 4,
        Invalid = 5
    }

    public sealed class ESAgentArtifactRequestStatus
    {
        public ESAgentArtifactRequestState State { get; internal set; }
        public string RequestDirectory { get; internal set; }
        public string Message { get; internal set; }
        public string NextAction { get; internal set; }
        public bool CanReview => State == ESAgentArtifactRequestState.AwaitingCandidate
            || State == ESAgentArtifactRequestState.AwaitingApproval
            || State == ESAgentArtifactRequestState.Approved;
    }
}
