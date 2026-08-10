using System;

namespace ESFramework.ESAITest
{
    public enum ESAITestCapabilityKind
    {
        ToUse = 1,
        ToSee = 2,
        ToVerify = 3,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public abstract class ESAITestCapabilityAttribute : Attribute
    {
        public string CapabilityId { get; }
        public string Description { get; }
        public int Version { get; }
        public string Category { get; }

        protected ESAITestCapabilityAttribute(
            string capabilityId,
            string description = null,
            int version = 1,
            string category = null)
        {
            CapabilityId = capabilityId;
            Description = description;
            Version = version;
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ESAITestToUseAttribute : ESAITestCapabilityAttribute
    {
        public ESAITestToUseAttribute(
            string capabilityId,
            string description = null,
            int version = 1,
            string category = null)
            : base(capabilityId, description, version, category)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ESAITestToSeeAttribute : ESAITestCapabilityAttribute
    {
        public ESAITestToSeeAttribute(
            string capabilityId,
            string description = null,
            int version = 1,
            string category = null)
            : base(capabilityId, description, version, category)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ESAITestToVerifyAttribute : ESAITestCapabilityAttribute
    {
        public ESAITestToVerifyAttribute(
            string capabilityId,
            string description = null,
            int version = 1,
            string category = null)
            : base(capabilityId, description, version, category)
        {
        }
    }

    [Serializable]
    public sealed class ESAITestUseResultDto
    {
        public const string Schema = "esaitest.use-receipt/v1";

        public string schema = Schema;
        public bool accepted;
        public bool executed;
        public bool handlerMatched;
        public string statusCode;
        public string message;
        public string runId;
        public int sceneGeneration;
        public string invocationId;
        public string stepId;
        public string capabilityId;
        public string command;
        public string target;
        public string executionRoute;
        // Identifies the concrete evidence semantics of executionRoute. For example,
        // EventSystem handler dispatch and an ES input lease write are intentionally distinct.
        public string executionEvidenceKind;
        public string leaseOwner;
        public int leaseGeneration;
        public bool leaseHeld;
        public long executedUtcTicks;
        public int frameCount;
        public bool targetActiveBefore;
        public bool targetInteractableBefore;
        public bool targetExistsAfter;
        public bool targetActiveAfter;
        public bool targetInteractableAfter;
        // A successful call receipt proves only that its declared execution route completed.
        // Business effect is true only after an explicit verify/wait Step links evidence to it.
        public bool businessEffectVerified;
        public string followupVerificationStepId;
        public string followupVerificationStatusCode;
        public string followupVerificationMessage;
        public bool followupVerificationEvidenceMatched;
        public string followupVerificationEvidenceFailure;
        public ESAITestValueDto value;
    }

    [Serializable]
    public sealed class ESAITestVerifyResultDto
    {
        public const string Schema = "esaitest.verify-evidence/v1";

        public string schema = Schema;
        public bool passed;
        public string statusCode;
        public string message;
        public string runId;
        public int sceneGeneration;
        public string invocationId;
        public string stepId;
        public string capabilityId;
        public string command;
        public string target;
        public string expectedValue;
        public string actualValue;
        public string evidenceKind;
        public long observedUtcTicks;
        public int frameCount;
        public ESAITestValueDto value;
    }

    [Serializable]
    public sealed class ESAITestAttributedCapabilityDescriptorDto
    {
        public string capabilityId;
        public string kind;
        public string description;
        public int version;
        public string category;
        public bool accepted;
        public string executionStatus;
        public string rejectionCode;
        public string rejectionReason;
        public string assemblyName;
        public string declaringType;
        public string methodName;
        public string methodSignature;
        public string returnType;
        public string[] parameterTypes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ESAITestAttributedCapabilityDiagnosticDto
    {
        public string capabilityId;
        public string kind;
        public string code;
        public string message;
        public string methodSignature;
    }

    [Serializable]
    public sealed class ESAITestAttributedCapabilityManifestDto
    {
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public string source;
        public long generatedUtcTicks;
        public int discoveredCount;
        public int acceptedCount;
        public int rejectedCount;
        public int toUseCount;
        public int toSeeCount;
        public int toVerifyCount;
        public ESAITestAttributedCapabilityDescriptorDto[] acceptedCapabilities =
            Array.Empty<ESAITestAttributedCapabilityDescriptorDto>();
        public ESAITestAttributedCapabilityDescriptorDto[] rejectedCapabilities =
            Array.Empty<ESAITestAttributedCapabilityDescriptorDto>();
        public ESAITestAttributedCapabilityDiagnosticDto[] diagnostics =
            Array.Empty<ESAITestAttributedCapabilityDiagnosticDto>();
    }
}
