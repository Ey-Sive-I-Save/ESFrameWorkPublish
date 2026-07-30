using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ES
{
    /// <summary>
    /// Makes authoritative stable-Key violations a Player build failure while keeping review warnings visible.
    /// </summary>
    internal sealed class ESKeyGovernanceAuditBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;

        public void OnPreprocessBuild(BuildReport report)
        {
            ESKeyGovernanceAudit.RunAndThrowIfErrors("Player build");
        }
    }

    /// <summary>
    /// Keeps the resource pipeline extensible: ES_Editor exposes the hook and project governance owns the policy.
    /// </summary>
    [InitializeOnLoad]
    internal static class ESKeyGovernanceAuditResourceBuildGate
    {
        static ESKeyGovernanceAuditResourceBuildGate()
        {
            ESAssetBundleBuilder.BeforeBuildValidation += AuditBeforeResourceBake;
        }

        private static void AuditBeforeResourceBake()
        {
            ESKeyGovernanceAudit.RunAndThrowIfErrors("Resource bake");
        }
    }
}
