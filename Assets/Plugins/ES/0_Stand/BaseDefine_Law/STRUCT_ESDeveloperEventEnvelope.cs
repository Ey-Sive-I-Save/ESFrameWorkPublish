using System;

namespace ES
{
    [Serializable]
    public readonly struct ESDeveloperEventEnvelope
    {
        public const int SchemaVersion = 1;

        public readonly string EventId;
        public readonly ESDeveloperRunId RunId;
        public readonly ESDeveloperCorrelationId CorrelationId;
        public readonly ESDeveloperSourceId SourceId;
        public readonly long Sequence;
        public readonly DateTime OccurredUtc;
        public readonly ESDeveloperEventKind EventKind;
        public readonly string OwnerRef;
        public readonly ESDeveloperEvidenceRef EvidenceRef;
        public readonly int Version;

        public ESDeveloperEventEnvelope(
            string eventId,
            ESDeveloperRunId runId,
            ESDeveloperCorrelationId correlationId,
            ESDeveloperSourceId sourceId,
            long sequence,
            DateTime occurredUtc,
            ESDeveloperEventKind eventKind,
            string ownerRef,
            ESDeveloperEvidenceRef evidenceRef,
            int version = SchemaVersion)
        {
            EventId = eventId ?? string.Empty;
            RunId = runId;
            CorrelationId = correlationId;
            SourceId = sourceId;
            Sequence = sequence;
            OccurredUtc = occurredUtc;
            EventKind = eventKind;
            OwnerRef = ownerRef ?? string.Empty;
            EvidenceRef = evidenceRef;
            Version = version;
        }

        public static ESDeveloperEventEnvelope Create(
            ESDeveloperRunId runId,
            ESDeveloperCorrelationId correlationId,
            ESDeveloperSourceId sourceId,
            long sequence,
            ESDeveloperEventKind eventKind,
            string ownerRef,
            ESDeveloperEvidenceRef evidenceRef)
        {
            return new ESDeveloperEventEnvelope(
                Guid.NewGuid().ToString("N"),
                runId,
                correlationId,
                sourceId,
                sequence,
                DateTime.UtcNow,
                eventKind,
                ownerRef,
                evidenceRef,
                SchemaVersion);
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(EventId)
            && RunId.IsValid
            && CorrelationId.IsValid
            && SourceId.IsValid
            && Sequence >= 1
            && EvidenceRef.IsValid
            && Version == SchemaVersion;
    }
}
