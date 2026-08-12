#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ES
{
    public sealed class ESAgentGraphBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -800;

        public void OnPreprocessBuild(BuildReport report)
        {
            List<string> violations = CollectViolations();
            if (violations.Count == 0)
                return;
            throw new BuildFailedException("Editor-only Agent Graph 进入了 Player 构建依赖：\n"
                + string.Join("\n", violations.Take(12)));
        }

        public static List<string> CollectViolations()
        {
            var violations = new List<string>();
            var sceneDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path))
                    continue;
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
                    sceneDependencies.Add(dependency);
            }

            var preloadedAssets = new HashSet<string>(PlayerSettings.GetPreloadedAssets()
                .Where(asset => asset != null)
                .Select(AssetDatabase.GetAssetPath), StringComparer.OrdinalIgnoreCase);
            string[] graphGuids = AssetDatabase.FindAssets("t:ESGraphAssetBase");
            for (int i = 0; i < graphGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(graphGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                ESGraphAssetBase graph = AssetDatabase.LoadAssetAtPath<ESGraphAssetBase>(path);
                ESGraphAssetDomainAttribute domain = graph == null
                    ? null
                    : (ESGraphAssetDomainAttribute)Attribute.GetCustomAttribute(
                        graph.GetType(), typeof(ESGraphAssetDomainAttribute));
                if (domain == null || !domain.EditorOnly)
                    continue;

                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                    violations.Add(path + " [Resources]");
                if (sceneDependencies.Contains(path))
                    violations.Add(path + " [Build Scene dependency]");
                if (preloadedAssets.Contains(path))
                    violations.Add(path + " [Preloaded Asset]");
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                    violations.Add(path + " [AssetBundle: " + importer.assetBundleName + "]");
            }
            return violations.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
#endif
