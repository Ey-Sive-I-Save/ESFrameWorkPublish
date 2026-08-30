using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only Shader 资产与 Keyword 空间指纹。它不证明编译 Variant 数量或运行时效果。
    /// </summary>
    [Serializable]
    public sealed class ESRenderShaderResourceSnapshot
    {
        public int shaderAssetCount;
        public int keywordSpaceShaderCount;
        public string shaderGuidFingerprint = string.Empty;
        public string keywordFingerprint = string.Empty;

        public static bool TryCapture(
            out ESRenderShaderResourceSnapshot snapshot,
            out string reason)
        {
            snapshot = new ESRenderShaderResourceSnapshot();
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Shader") ?? Array.Empty<string>();
                string[] sortedGuids = guids.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var keywordLines = new List<string>();
                foreach (string guid in sortedGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    string[] keywords = ReadKeywordNames(shader);
                    if (keywords == null)
                        continue;
                    snapshot.keywordSpaceShaderCount++;
                    keywordLines.Add(guid + "=" + string.Join(",", keywords));
                }

                snapshot.shaderAssetCount = sortedGuids.Length;
                snapshot.shaderGuidFingerprint = string.Join("\u001F", sortedGuids);
                snapshot.keywordFingerprint = string.Join("\u001F", keywordLines.OrderBy(value => value, StringComparer.Ordinal));
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "shader-resource-inventory-capture-threw-" + exception.GetType().Name;
                return false;
            }
        }

        private static string[] ReadKeywordNames(Shader shader)
        {
            if (shader == null)
                return null;
            PropertyInfo spaceProperty = typeof(Shader).GetProperty(
                "keywordSpace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object space = spaceProperty?.GetValue(shader, null);
            PropertyInfo namesProperty = space?.GetType().GetProperty(
                "keywordNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(namesProperty?.GetValue(space, null) is IEnumerable names))
                return null;

            var values = new List<string>();
            foreach (object name in names)
            {
                if (name != null)
                    values.Add(name.ToString());
            }
            return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }
}
