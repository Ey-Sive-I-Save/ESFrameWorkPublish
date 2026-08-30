using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>
    /// The only bridge from a completed Codex App Server result into the ABCD
    /// candidate pipeline. This object is deliberately not an approval or apply
    /// contract: every authority-bearing effect remains false until ABCD creates
    /// a patch plan and the user explicitly authorizes an apply request.
    /// </summary>
    [Serializable]
    public sealed class ESCodexCandidateEnvelope
    {
        public int schemaVersion = 1;
        public string contractId = "es://automation/contracts/codex/candidate-envelope/v1";
        public string providerId = "es-codex";
        public string threadId = string.Empty;
        public string sessionId = string.Empty;
        public string turnId = string.Empty;
        public string runId = string.Empty;
        public string taskId = string.Empty;
        public int taskVersion;
        public string planHash = string.Empty;
        public string sourceScopeHash = string.Empty;
        public string currentHead = string.Empty;
        public string candidateSetHash = string.Empty;
        public string generationMode = string.Empty;
        public string status = "candidate";
        public string claimLevel = "candidate-only";
        public bool canApply;
        public string finalAuthority = "ABCD-audit-only";
        public ESCodexCandidateEffects effects = new ESCodexCandidateEffects();
        public List<string> evidenceRefs = new List<string>();
        public List<string> failureCodes = new List<string>();
        public List<ESCodexCandidate> candidates = new List<ESCodexCandidate>();
    }

    [Serializable]
    public sealed class ESCodexCandidate
    {
        public string candidateId = string.Empty;
        public string candidateType = "abc-generation";
        public List<ESCodexChangedFile> changedFiles = new List<ESCodexChangedFile>();
        public ESCodexCandidatePreconditions preconditions = new ESCodexCandidatePreconditions();
        public ESCodexCandidateEffects effects = new ESCodexCandidateEffects();
        public List<string> evidenceRefs = new List<string>();
        public List<string> failureCodes = new List<string>();
        public string claimLevel = "candidate-only";
        public bool canApply;
        // Lower-case, patch-planner-compatible projection of the parsed proposal.
        public List<ESCodexProposedChange> proposedChanges = new List<ESCodexProposedChange>();
        // Retain the parsed ABC candidate so PatchPlanning can consume the same
        // validated object without reparsing untrusted Codex text.
        public ESABCGenerationCandidate candidate;
    }

    [Serializable]
    public sealed class ESCodexProposedChange
    {
        public string path = string.Empty;
        public string changeId = string.Empty;
        public string afterContent = string.Empty;
    }

    [Serializable]
    public sealed class ESCodexChangedFile
    {
        public string path = string.Empty;
        public string changeId = string.Empty;
        public string beforeHash = string.Empty;
    }

    [Serializable]
    public sealed class ESCodexCandidatePreconditions
    {
        public string currentHead = string.Empty;
        public string planHash = string.Empty;
        public string sourceScopeHash = string.Empty;
        public string candidateSetHash = string.Empty;
        public bool requiresCurrentHeadRecheck = true;
        public bool requiresAbcdAudit = true;
        public bool requiresExplicitApply = true;
    }

    [Serializable]
    public sealed class ESCodexCandidateEffects
    {
        public bool writesAllowed;
        public bool runtimeAllowed;
        public bool gitAllowed;
        public bool releaseAllowed;
    }

    internal sealed class ESCodexResultIdentity
    {
        public string threadId = string.Empty;
        public string sessionId = string.Empty;
        public string turnId = string.Empty;
        public string runId = string.Empty;
        public string finalMessage = string.Empty;
    }

    /// <summary>
    /// Normalizes the Worker result identity before any provider text is parsed.
    /// A result that is merely "Passed" is not enough: identity, authority and
    /// mutation fields must also prove it is a Codex candidate contribution.
    /// </summary>
    internal static class ESCodexResultNormalizer
    {
        private const string TaskId = "es.codex.app-server";
        private const int TaskVersion = 1;
        private const int MaximumFinalMessageCharacters = 128000;

        internal static bool TryNormalize(
            JObject result,
            string expectedPlanHash,
            out ESCodexResultIdentity identity,
            out string error)
        {
            identity = null;
            error = string.Empty;
            if (result == null)
            {
                error = "CODEX_RESULT_NULL";
                return false;
            }
            if (!string.Equals(result.Value<string>("taskId"), TaskId, StringComparison.Ordinal)
                || result.Value<int?>("taskVersion") != TaskVersion)
            {
                error = "CODEX_RESULT_TASK_ID_MISMATCH";
                return false;
            }
            if (!string.Equals(result.Value<string>("providerDeclaration"), "es-codex", StringComparison.Ordinal)
                || !string.Equals(result.Value<string>("workerId"), TaskId, StringComparison.Ordinal)
                || !string.Equals(result.Value<string>("workerVersion"), "1.0.0", StringComparison.Ordinal))
            {
                error = "CODEX_RESULT_PROVIDER_IDENTITY_MISMATCH";
                return false;
            }
            if (!string.Equals(result.Value<string>("status"), "Passed", StringComparison.Ordinal))
            {
                error = "CODEX_RESULT_NOT_COMPLETED";
                return false;
            }
            string runId = result.Value<string>("runId")?.Trim() ?? string.Empty;
            if (!IsRunId(runId))
            {
                error = "CODEX_RESULT_RUN_ID_INVALID";
                return false;
            }
            string threadId = result.Value<string>("threadId")?.Trim() ?? string.Empty;
            string turnId = result.Value<string>("turnId")?.Trim() ?? string.Empty;
            string sessionId = result.Value<string>("sessionId")?.Trim() ?? string.Empty;
            if (!IsSafeIdentity(threadId) || !IsSafeIdentity(turnId) || !IsSafeIdentity(sessionId, allowEmpty: true))
            {
                error = "CODEX_RESULT_THREAD_TURN_ID_INVALID";
                return false;
            }
            string planHash = expectedPlanHash?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!IsSha256(planHash)
                || !string.Equals(result.Value<string>("brainPlanHash")?.Trim(), planHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "CODEX_RESULT_PLAN_HASH_MISMATCH";
                return false;
            }
            JToken mutationApplied = result["mutationApplied"];
            if (mutationApplied != null
                && (mutationApplied.Type != JTokenType.Boolean || mutationApplied.Value<bool>()))
            {
                error = "CODEX_RESULT_MUTATION_APPLIED";
                return false;
            }
            if (result["completionDecision"] != null && result["completionDecision"].Type != JTokenType.Null)
            {
                error = "CODEX_RESULT_COMPLETION_AUTHORITY_PRESENT";
                return false;
            }
            string finalMessage = result.Value<string>("finalMessage")?.Trim() ?? string.Empty;
            if (finalMessage.Length == 0 || finalMessage.Length > MaximumFinalMessageCharacters)
            {
                error = "CODEX_RESULT_FINAL_MESSAGE_INVALID";
                return false;
            }
            identity = new ESCodexResultIdentity
            {
                runId = runId,
                threadId = threadId,
                sessionId = sessionId,
                turnId = turnId,
                finalMessage = finalMessage,
            };
            return true;
        }

        private static bool IsRunId(string value)
            => value.Length == 32 && value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'));

        private static bool IsSafeIdentity(string value, bool allowEmpty = false)
            => (allowEmpty && value.Length == 0)
                || (value.Length > 0 && value.Length <= 160
                    && value.All(character => (character >= 'A' && character <= 'Z')
                        || (character >= 'a' && character <= 'z')
                        || (character >= '0' && character <= '9')
                        || "._:-".IndexOf(character) >= 0));

        private static bool IsSha256(string value)
            => value.Length == 64 && value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'));
    }

    /// <summary>
    /// Binds proposed project-relative paths to the exact source snapshot that
    /// the later ESABCD patch planner will re-check. It never accepts an absolute
    /// path or a sourceAbsolutePath supplied by a provider.
    /// </summary>
    internal static class ESCodexEvidenceBinder
    {
        internal static bool TryBindChangedFiles(
            ESABCGenerationCandidate candidate,
            string projectRoot,
            string currentHead,
            string planHash,
            string sourceScopeHash,
            string candidateSetHash,
            out List<ESCodexChangedFile> changedFiles,
            out string error)
        {
            changedFiles = new List<ESCodexChangedFile>();
            error = string.Empty;
            if (candidate == null)
            {
                error = "CODEX_CANDIDATE_NULL";
                return false;
            }
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
            {
                error = "CODEX_PROJECT_ROOT_MUST_BE_ABSOLUTE";
                return false;
            }
            string root;
            try { root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch (Exception) { error = "CODEX_PROJECT_ROOT_INVALID"; return false; }
            if (!Directory.Exists(root) || ESManagedFileIO.ContainsExistingReparsePoint(root))
            {
                error = "CODEX_PROJECT_ROOT_UNSAFE";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ESABCGenerationChange change in candidate.ProposedChanges ?? new List<ESABCGenerationChange>())
            {
                string relative = change?.Path?.Trim().Replace('\\', '/') ?? string.Empty;
                string[] segments = relative.Split('/');
                if (change == null || relative.Length == 0 || Path.IsPathRooted(relative)
                    || segments.Any(segment => segment == "..") || !seen.Add(relative))
                {
                    error = "CODEX_CANDIDATE_PATH_INVALID";
                    return false;
                }
                string full;
                try { full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))); }
                catch (Exception) { error = "CODEX_CANDIDATE_PATH_INVALID"; return false; }
                bool withinRoot;
                try { withinRoot = ESAutomationPathPolicy.IsWithin(full, new[] { root }); }
                catch (Exception) { error = "CODEX_CANDIDATE_PATH_INVALID"; return false; }
                if (!withinRoot || ESManagedFileIO.ContainsExistingReparsePoint(full) || !File.Exists(full))
                {
                    error = "CODEX_CANDIDATE_PATH_OUT_OF_SCOPE_OR_MISSING";
                    return false;
                }
                changedFiles.Add(new ESCodexChangedFile
                {
                    path = relative,
                    changeId = change.ChangeId?.Trim() ?? string.Empty,
                    beforeHash = ComputeFileSha256(full),
                });
            }
            return true;
        }

        private static string ComputeFileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    /// <summary>
    /// Converts one exact Codex Worker result into a candidate-only envelope.
    /// Callers must provide the plan/source snapshot and current head explicitly;
    /// missing precondition hashes are a hard rejection.
    /// </summary>
    public static class ESCodexCandidateEnvelopeAdapter
    {
        public static bool TryNormalize(
            JObject codexResult,
            string generationMode,
            string currentHead,
            string planHash,
            string sourceScopeHash,
            string projectRoot,
            out ESCodexCandidateEnvelope envelope,
            out string error)
        {
            envelope = null;
            error = string.Empty;
            string mode = generationMode?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedHead = currentHead?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedPlan = planHash?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedSource = sourceScopeHash?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!IsSha1(normalizedHead)) { error = "CODEX_PRECONDITION_CURRENT_HEAD_REQUIRED"; return false; }
            if (!IsSha256(normalizedPlan)) { error = "CODEX_PRECONDITION_PLAN_HASH_REQUIRED"; return false; }
            if (!IsSha256(normalizedSource)) { error = "CODEX_PRECONDITION_SOURCE_SCOPE_HASH_REQUIRED"; return false; }
            if (ContainsForbiddenProviderClaim(codexResult))
            {
                error = "CODEX_CANDIDATE_AUTHORITY_OR_SOURCE_PATH_FORBIDDEN";
                return false;
            }
            if (!ESCodexResultNormalizer.TryNormalize(codexResult, normalizedPlan, out ESCodexResultIdentity identity, out error))
                return false;
            if (!TryReadProviderJson(identity.finalMessage, out JObject providerRoot, out error))
                return false;
            if (ContainsForbiddenProviderClaim(providerRoot))
            {
                error = "CODEX_CANDIDATE_AUTHORITY_OR_SOURCE_PATH_FORBIDDEN";
                return false;
            }
            if (!ESABCModelProviderAdapter.TryParseGenerationResponse(identity.finalMessage, mode,
                    out ESABCGenerationResponseEnvelope parsed, out error))
                return false;
            if (parsed == null || parsed.Candidates == null || parsed.Candidates.Count == 0)
            {
                error = "CODEX_CANDIDATE_SET_EMPTY";
                return false;
            }

            var candidates = new List<ESCodexCandidate>();
            var rootEvidence = new List<string>
            {
                "codex-run:" + identity.runId + ":result",
                "codex-run:" + identity.runId + ":thread:" + identity.threadId,
                "codex-run:" + identity.runId + ":turn:" + identity.turnId,
            };
            foreach (ESABCGenerationCandidate candidate in parsed.Candidates)
            {
                if (!ESCodexEvidenceBinder.TryBindChangedFiles(candidate, projectRoot, normalizedHead,
                        normalizedPlan, normalizedSource, parsed.CandidateSetHash,
                        out List<ESCodexChangedFile> changedFiles, out error))
                    return false;
                candidates.Add(new ESCodexCandidate
                {
                    candidateId = candidate.CandidateId,
                    candidateType = "abc-generation",
                    changedFiles = changedFiles,
                    preconditions = new ESCodexCandidatePreconditions
                    {
                        currentHead = normalizedHead,
                        planHash = normalizedPlan,
                        sourceScopeHash = normalizedSource,
                        candidateSetHash = parsed.CandidateSetHash,
                        requiresCurrentHeadRecheck = true,
                        requiresAbcdAudit = true,
                        requiresExplicitApply = true,
                    },
                    effects = new ESCodexCandidateEffects(),
                    evidenceRefs = new List<string>(rootEvidence) { "codex-candidate:" + candidate.CandidateId },
                    failureCodes = new List<string>(),
                    claimLevel = "candidate-only",
                    canApply = false,
                    proposedChanges = (candidate.ProposedChanges ?? new List<ESABCGenerationChange>())
                        .Select(change => new ESCodexProposedChange
                        {
                            path = change.Path,
                            changeId = change.ChangeId,
                            afterContent = change.AfterContent,
                        }).ToList(),
                    candidate = candidate,
                });
            }
            envelope = new ESCodexCandidateEnvelope
            {
                providerId = "es-codex",
                threadId = identity.threadId,
                sessionId = identity.sessionId,
                turnId = identity.turnId,
                runId = identity.runId,
                taskId = "es.codex.app-server",
                taskVersion = 1,
                planHash = normalizedPlan,
                sourceScopeHash = normalizedSource,
                currentHead = normalizedHead,
                candidateSetHash = parsed.CandidateSetHash,
                generationMode = parsed.GenerationMode,
                status = "candidate",
                claimLevel = "candidate-only",
                canApply = false,
                finalAuthority = "ABCD-audit-only",
                effects = new ESCodexCandidateEffects(),
                evidenceRefs = rootEvidence,
                failureCodes = new List<string>(),
                candidates = candidates,
            };
            return true;
        }

        public static bool TryNormalizeJson(
            JObject codexResult,
            string generationMode,
            string currentHead,
            string planHash,
            string sourceScopeHash,
            string projectRoot,
            out string envelopeJson,
            out string error)
        {
            envelopeJson = string.Empty;
            if (!TryNormalize(codexResult, generationMode, currentHead, planHash, sourceScopeHash,
                    projectRoot, out ESCodexCandidateEnvelope envelope, out error))
                return false;
            envelopeJson = JsonConvert.SerializeObject(envelope, Formatting.Indented);
            return true;
        }

        private static bool IsSha1(string value)
            => value.Length == 40 && value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'));

        private static bool IsSha256(string value)
            => value.Length == 64 && value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'));

        private static bool TryReadProviderJson(string message, out JObject root, out string error)
        {
            root = null;
            error = string.Empty;
            string text = message?.Trim() ?? string.Empty;
            string json = text;
            if (!(text.StartsWith("{", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal)))
            {
                int start = text.IndexOf('{');
                int end = text.LastIndexOf('}');
                if (start < 0 || end <= start) { error = "CODEX_CANDIDATE_JSON_INVALID"; return false; }
                json = text.Substring(start, end - start + 1);
            }
            try { root = JObject.Parse(json); return true; }
            catch (JsonException) { error = "CODEX_CANDIDATE_JSON_INVALID"; return false; }
        }

        private static bool ContainsForbiddenProviderClaim(JToken token)
        {
            JObject objectToken = token as JObject;
            if (objectToken != null)
            {
                foreach (JProperty property in objectToken.Properties())
                {
                    if (string.Equals(property.Name, "sourceAbsolutePath", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(property.Name, "canApply", StringComparison.OrdinalIgnoreCase)
                        && (property.Value.Type != JTokenType.Boolean || property.Value.Value<bool>())) return true;
                    if (string.Equals(property.Name, "status", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(property.Value.Value<string>(), "accepted", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(property.Name, "claimLevel", StringComparison.OrdinalIgnoreCase)
                        && property.Value.Type == JTokenType.String
                        && !string.Equals(property.Value.Value<string>(), "candidate-only", StringComparison.OrdinalIgnoreCase)) return true;
                    if ((string.Equals(property.Name, "runtimeAccepted", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(property.Name, "unityAccepted", StringComparison.OrdinalIgnoreCase))
                        && (property.Value.Type != JTokenType.Boolean || property.Value.Value<bool>())) return true;
                    if (string.Equals(property.Name, "runtimeStatus", StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(property.Value.Value<string>(), "accepted", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(property.Value.Value<string>(), "runtime-accepted", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(property.Value.Value<string>(), "unity-accepted", StringComparison.OrdinalIgnoreCase))) return true;
                    if (ContainsForbiddenProviderClaim(property.Value)) return true;
                }
            }
            JArray arrayToken = token as JArray;
            if (arrayToken != null)
                foreach (JToken child in arrayToken)
                    if (ContainsForbiddenProviderClaim(child)) return true;
            return false;
        }
    }
}
