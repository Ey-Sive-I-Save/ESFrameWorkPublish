using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.PackageManager;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only 的 URP 资源身份快照。只读，不修改 RenderPipelineAsset 或 RendererData。
    /// </summary>
    [Serializable]
    public sealed class ESRenderBackendResourceSnapshot
    {
        public string pipelineTypeName = string.Empty;
        public string pipelineAssetName = string.Empty;
        public string rendererDataTypeName = string.Empty;
        public string rendererDataName = string.Empty;
        public string compatibilityStatus = string.Empty;
        public string compatibilityReason = string.Empty;
        public string unityVersion = string.Empty;
        public string urpPackageVersion = string.Empty;
        public int rendererFeatureCount;
        public string rendererFeatureTypeFingerprint = string.Empty;

        public bool IsPipelinePresent => !string.IsNullOrWhiteSpace(pipelineAssetName);
        public bool IsRendererIdentityPresent => !string.IsNullOrWhiteSpace(rendererDataName);

        public static bool TryCapture(
            out ESRenderBackendResourceSnapshot snapshot,
            out string reason)
        {
            snapshot = new ESRenderBackendResourceSnapshot();
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                reason = "render-pipeline-asset-missing";
                return false;
            }

            snapshot.pipelineTypeName = pipeline.GetType().AssemblyQualifiedName ?? string.Empty;
            snapshot.pipelineAssetName = pipeline.name ?? string.Empty;
            snapshot.unityVersion = Application.unityVersion ?? string.Empty;
            string packageReason;
            snapshot.urpPackageVersion = TryReadUrpPackageVersion(out packageReason);
            int unityMajor = snapshot.unityVersion.StartsWith("6000", StringComparison.Ordinal) ? 6000 : 2022;
            string compatibilityReason;
            ESUrpCompatibilityStatus compatibility = ESUrpCompatibilityPolicy.Evaluate(
                pipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 ? "URP" : pipeline.GetType().Name,
                unityMajor,
                snapshot.urpPackageVersion,
                out compatibilityReason);
            snapshot.compatibilityStatus = compatibility.ToString();
            snapshot.compatibilityReason = string.IsNullOrEmpty(packageReason)
                ? compatibilityReason
                : packageReason + (string.IsNullOrEmpty(compatibilityReason) ? string.Empty : ";" + compatibilityReason);
            object rendererData = ReadMember(pipeline, "scriptableRendererData");
            if (rendererData is UnityEngine.Object rendererObject)
            {
                snapshot.rendererDataTypeName = rendererObject.GetType().AssemblyQualifiedName ?? string.Empty;
                snapshot.rendererDataName = rendererObject.name ?? string.Empty;
                object features = ReadMember(rendererData, "rendererFeatures");
                if (features is System.Collections.IEnumerable enumerable)
                {
                    var featureTypes = new System.Collections.Generic.List<string>();
                    foreach (object feature in enumerable)
                        if (feature != null) featureTypes.Add(feature.GetType().AssemblyQualifiedName ?? string.Empty);
                    featureTypes.Sort(StringComparer.Ordinal);
                    snapshot.rendererFeatureCount = featureTypes.Count;
                    snapshot.rendererFeatureTypeFingerprint = string.Join("\u001F", featureTypes);
                }
                reason = string.Empty;
                return true;
            }

            reason = "renderer-data-identity-not-exposed-by-pipeline-asset";
            return true;
        }

        private static string TryReadUrpPackageVersion(out string reason)
        {
            try
            {
                PackageInfo package = PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal");
                reason = package == null ? "urp-package-version-unknown" : string.Empty;
                return package != null ? package.version ?? string.Empty : string.Empty;
            }
            catch (Exception exception)
            {
                reason = "urp-package-query-failed-" + exception.GetType().Name;
                return string.Empty;
            }
        }

        private static object ReadMember(object target, string memberName)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target, null);

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) : null;
        }
    }
}
