using System;

namespace ES
{
    public enum ESUrpCompatibilityStatus
    {
        Rejected = 0,
        CurrentBaseline = 1,
        ForwardCandidateUnverified = 2
    }

    /// <summary>
    /// ES 的渲染管线边界：只接管 URP；当前项目版本为基线，Unity 6 仅保留前向候选，不伪造验证结果。
    /// </summary>
    public static class ESUrpCompatibilityPolicy
    {
        public const string CurrentUrpPackage = "14.0.11";
        public const int MinimumUnityMajor = 2022;
        public const int CurrentUnityMajor = 2022;

        public static ESUrpCompatibilityStatus Evaluate(string pipeline, int unityMajor, string urpPackageVersion, out string reason)
        {
            reason = string.Empty;
            if (!string.Equals(pipeline ?? string.Empty, "URP", StringComparison.OrdinalIgnoreCase))
            {
                reason = "pipeline-not-supported";
                return ESUrpCompatibilityStatus.Rejected;
            }
            if (unityMajor == CurrentUnityMajor && string.Equals(urpPackageVersion ?? string.Empty, CurrentUrpPackage, StringComparison.Ordinal))
                return ESUrpCompatibilityStatus.CurrentBaseline;
            // Unity's 6000.x editor reports a numeric major of 6000 in some probes and 6 in others.
            // Both forms are inside the user's 2022+ URP scope, but remain unverified until runtime evidence exists.
            if ((unityMajor >= MinimumUnityMajor || unityMajor == 6 || unityMajor == 6000)
                && !string.IsNullOrWhiteSpace(urpPackageVersion))
            {
                reason = unityMajor == 6 || unityMajor == 6000
                    ? "unity6-forward-candidate-requires-runtime-verification"
                    : "urp-forward-candidate-requires-runtime-verification";
                return ESUrpCompatibilityStatus.ForwardCandidateUnverified;
            }
            reason = "unity-version-out-of-scope";
            return ESUrpCompatibilityStatus.Rejected;
        }
    }
}
