using System;

namespace ESFramework.ESAITest
{
    public enum ESAITestAIPromptPriority
    {
        P0 = 0,
        P1 = 1,
        P2 = 2,
        P3 = 3,
        P4 = 4,
    }

    [Serializable]
    public sealed class ESAITestAIPromptDto
    {
        public string promptId;
        public string source;
        public string message;
        public string priority;
        public long sequence;
        public long publishedUtcTicks;
        public long expiresUtcTicks;
    }

    [Serializable]
    public sealed class ESAITestArtifactDto
    {
        public string relativePath;
        public string kind;
        public long byteLength;
        public string sha256;
    }

    [Serializable]
    public sealed class ESAITestNaturalLanguageRouteDto
    {
        public const string Schema = "esaitest.natural-language-route/v1";

        public string schema = Schema;
        public int protocolVersion = ESAITestProtocol.CurrentVersion;
        public bool accepted;
        public bool requiresClarification;
        public string intent;
        public string normalizedText;
        public string boundRunId;
        public string message;
        public string goal;
        public string priority = ESAITestAIPromptPriority.P2.ToString();
        public float ttlSeconds = 60f;
        public float confidence;
        public string rejectionReason;
    }
}
