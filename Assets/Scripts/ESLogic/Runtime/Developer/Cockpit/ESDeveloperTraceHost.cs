using System;
using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// Runtime-safe trace host. It owns only sequence counters and the optional provider;
    /// it never stores UnityEngine.Object references or Editor state.
    /// </summary>
    public static class ESDeveloperTraceHost
    {
        private static IESDeveloperTraceProvider provider;
        private static ESDeveloperRunId currentRunId;
        private static ESDeveloperCorrelationId currentCorrelationId;
        private static readonly Dictionary<string, long> Sequences =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> SourceEpochs =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public static bool IsEnabled =>
            provider != null
            && provider.IsEnabled
            && currentRunId.IsValid
            && currentCorrelationId.IsValid;

        public static ESDeveloperRunId CurrentRunId => currentRunId;

        public static void SetProvider(IESDeveloperTraceProvider value)
        {
            provider = value;
            Sequences.Clear();
            SourceEpochs.Clear();
        }

        public static void BeginRun()
        {
            currentRunId = ESDeveloperRunId.CreateNew();
            currentCorrelationId = ESDeveloperCorrelationId.CreateNew();
            Sequences.Clear();
            SourceEpochs.Clear();
        }

        public static void EndRun()
        {
            currentRunId = new ESDeveloperRunId(string.Empty);
            currentCorrelationId = new ESDeveloperCorrelationId(string.Empty);
            Sequences.Clear();
            SourceEpochs.Clear();
        }

        public static void ResetSourceEpoch(string sourceInstanceId)
        {
            if (string.IsNullOrWhiteSpace(sourceInstanceId))
            {
                return;
            }

            if (!SourceEpochs.TryGetValue(sourceInstanceId, out long epoch))
            {
                SourceEpochs[sourceInstanceId] = 1;
                return;
            }

            SourceEpochs[sourceInstanceId] = epoch + 1;
            Sequences[sourceInstanceId] = 0;
        }

        public static void Emit(
            ESDeveloperEventKind eventKind,
            string sourceId,
            string sourceInstanceId,
            string ownerRef,
            string evidenceKey,
            ESDeveloperEvidenceLevel evidenceLevel =
                ESDeveloperEvidenceLevel.LocalEphemeral)
        {
            if (!IsEnabled
                || string.IsNullOrWhiteSpace(sourceId)
                || string.IsNullOrWhiteSpace(sourceInstanceId)
                || string.IsNullOrWhiteSpace(ownerRef)
                || string.IsNullOrWhiteSpace(evidenceKey))
            {
                return;
            }

            if (!SourceEpochs.TryGetValue(sourceInstanceId, out long epoch))
            {
                epoch = 1;
                SourceEpochs[sourceInstanceId] = epoch;
            }

            long sequence = 1;
            if (Sequences.TryGetValue(sourceInstanceId, out long lastSequence))
            {
                sequence = lastSequence + 1;
            }

            Sequences[sourceInstanceId] = sequence;
            var source = new ESDeveloperSourceId(sourceId, sourceInstanceId, epoch);
            var evidence = new ESDeveloperEvidenceRef(
                source,
                sequence,
                evidenceKey,
                evidenceLevel);
            var envelope = ESDeveloperEventEnvelope.Create(
                currentRunId,
                currentCorrelationId,
                source,
                sequence,
                eventKind,
                ownerRef,
                evidence);
            provider.Emit(envelope);
        }
    }
}
