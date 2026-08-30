using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only VolumeProfile 资产库存快照。只读枚举，不代表场景中实际生效的 Volume。
    /// </summary>
    [Serializable]
    public sealed class ESRenderVolumeResourceSnapshot
    {
        public int profileAssetCount;
        public string profileGuidFingerprint = string.Empty;
        public string profileNameFingerprint = string.Empty;
        public int componentCount;
        public string componentTypeFingerprint = string.Empty;

        public static bool TryCapture(
            out ESRenderVolumeResourceSnapshot snapshot,
            out string reason)
        {
            snapshot = new ESRenderVolumeResourceSnapshot();
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:VolumeProfile") ?? Array.Empty<string>();
                string[] sortedGuids = guids.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] names = sortedGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(path => System.IO.Path.GetFileNameWithoutExtension(path) ?? string.Empty)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                snapshot.profileAssetCount = sortedGuids.Length;
                snapshot.profileGuidFingerprint = string.Join("\u001F", sortedGuids);
                snapshot.profileNameFingerprint = string.Join("\u001F", names);
                var componentTypes = sortedGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(path => AssetDatabase.LoadAssetAtPath<VolumeProfile>(path))
                    .Where(profile => profile != null)
                    .SelectMany(profile => profile.components ?? new List<VolumeComponent>())
                    .Select(component => component != null ? component.GetType().AssemblyQualifiedName : string.Empty)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                snapshot.componentCount = componentTypes.Length;
                snapshot.componentTypeFingerprint = string.Join("\u001F", componentTypes);
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "volume-profile-inventory-capture-threw-" + exception.GetType().Name;
                return false;
            }
        }
    }
}
