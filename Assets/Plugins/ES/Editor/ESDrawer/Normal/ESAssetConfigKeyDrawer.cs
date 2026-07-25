using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public abstract class ESAssetConfigKeyDrawerBase : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Line * 5f + Gap * 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            Type enumType = ResolveEnumType();

            EditorGUI.LabelField(NextLine(ref position), label, EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.46f, out Rect left, out Rect right);
            EditorGUI.PropertyField(left, enumKey, new GUIContent("Enum Key"));
            EditorGUI.PropertyField(right, stringKey, new GUIContent("String Key"));

            ESAssetCatalogKeyPicker.Draw(NextLine(ref position), ResolveKind(), enumKey, stringKey);

            row = NextLine(ref position);
            if (GUI.Button(row, "Copy ConfigKey", EditorStyles.miniButton))
                CopyKey(enumKey, stringKey, enumType);

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            if (GUI.Button(left, "Open Enum Append", EditorStyles.miniButtonLeft))
                ESEnumScriptJump.OpenEnumAppendPosition(enumType);

            if (GUI.Button(right, "Copy AI Enum Request", EditorStyles.miniButtonRight))
                CopyAiEnumRequest(enumType, enumKey, stringKey, ResolveSuggestedStringKey(property));

            EditorGUI.EndProperty();
        }

        protected abstract Type ResolveEnumType();
        protected abstract ESAssetReferKind ResolveKind();

        private static Rect NextLine(ref Rect position)
        {
            Rect rect = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return rect;
        }

        private static void DrawSplit(Rect rect, float leftRatio, out Rect left, out Rect right)
        {
            float width = Mathf.Floor((rect.width - Gap) * leftRatio);
            left = new Rect(rect.x, rect.y, width, rect.height);
            right = new Rect(left.xMax + Gap, rect.y, rect.width - width - Gap, rect.height);
        }

        private static string ResolveSuggestedStringKey(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target is SoDataInfo info && !string.IsNullOrEmpty(info.KeyName))
                return info.KeyName;

            return target != null ? target.name : string.Empty;
        }

        private static void CopyKey(SerializedProperty enumKey, SerializedProperty stringKey, Type enumType)
        {
            string enumName = enumType != null && enumKey != null
                ? enumType.Name + "." + enumKey.enumDisplayNames[enumKey.enumValueIndex]
                : "UnknownEnum";

            EditorGUIUtility.systemCopyBuffer =
                "enumKey: " + enumName + Environment.NewLine +
                "stringKey: " + (stringKey != null ? stringKey.stringValue : string.Empty);
        }

        private static void CopyAiEnumRequest(Type enumType, SerializedProperty enumKey, SerializedProperty stringKey, string fallbackStringKey)
        {
            string desiredStringKey = stringKey != null && !string.IsNullOrEmpty(stringKey.stringValue)
                ? stringKey.stringValue
                : fallbackStringKey;
            string current = enumKey != null ? enumKey.enumDisplayNames[enumKey.enumValueIndex] : "Unknown";
            ESEnumScriptJump.CopyAppendRequest(enumType, desiredStringKey, current);
        }
    }

    /// <summary>只读取阶段①的 Catalog，供业务配置安全选择键；不修改 Library 或 RuntimeKey。</summary>
    internal static class ESAssetCatalogKeyPicker
    {
        private sealed class Candidate
        {
            public int enumKey;
            public string stringKey;
            public string label;
        }

        private static readonly Dictionary<ESAssetReferKind, List<Candidate>> CandidatesByKind = new Dictionary<ESAssetReferKind, List<Candidate>>();
        private static bool loaded;

        public static void Draw(Rect position, ESAssetReferKind kind, SerializedProperty enumKey, SerializedProperty stringKey)
        {
            Rect refreshRect = new Rect(position.x, position.y, 110f, position.height);
            if (GUI.Button(refreshRect, "Bake / Refresh", EditorStyles.miniButton))
            {
                try { ESAssetReferenceBaker.Bake(); Reload(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            position.xMin = refreshRect.xMax + 4f;
            if (!loaded) Reload();
            if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates) || candidates.Count == 0)
            {
                EditorGUI.LabelField(position, "Collected Asset", "Bake Catalog 后可在此选择");
                return;
            }

            int current = candidates.FindIndex(item => item.enumKey == enumKey.intValue && string.Equals(item.stringKey, stringKey.stringValue, StringComparison.Ordinal));
            string[] labels = new string[candidates.Count + 1];
            labels[0] = current >= 0 ? candidates[current].label : "Select collected asset...";
            for (int i = 0; i < candidates.Count; i++) labels[i + 1] = candidates[i].label;
            int selected = EditorGUI.Popup(position, "Collected Asset", 0, labels);
            if (selected <= 0) return;

            Candidate candidate = candidates[selected - 1];
            enumKey.intValue = candidate.enumKey;
            stringKey.stringValue = candidate.stringKey;
        }

        private static void Reload()
        {
            loaded = true;
            CandidatesByKind.Clear();
            if (!Directory.Exists(ESAssetPipelineIO.BakeRoot)) return;
            foreach (string path in Directory.GetFiles(ESAssetPipelineIO.BakeRoot, ESAssetPipelineIO.CatalogFileName, SearchOption.AllDirectories))
            {
                ESAssetLibraryCatalog catalog;
                try { catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(path); }
                catch { continue; }
                if (catalog == null || catalog.errors.Count > 0) continue;
                foreach (ESAssetCatalogEntry entry in catalog.assets.Where(item => item != null && item.isBusinessAsset))
                {
                    if (!Enum.TryParse(entry.kind, out ESAssetReferKind kind) || kind == ESAssetReferKind.None) continue;
                    if (entry.enumKey == 0 && string.IsNullOrEmpty(entry.stringKey)) continue;
                    if (!CandidatesByKind.TryGetValue(kind, out List<Candidate> candidates))
                        CandidatesByKind.Add(kind, candidates = new List<Candidate>());
                    candidates.Add(new Candidate
                    {
                        enumKey = entry.enumKey,
                        stringKey = entry.stringKey,
                        label = $"{entry.libraryName}/{entry.pageName}  [E:{entry.enumKey}, S:{entry.stringKey}]"
                    });
                }
            }
            foreach (List<Candidate> candidates in CandidatesByKind.Values)
                candidates.Sort((left, right) => string.CompareOrdinal(left.label, right.label));
        }
    }

    [CustomPropertyDrawer(typeof(ESAssetReferPrefabConfigKey))]
    public sealed class ESAssetReferPrefabConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferPrefabEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Prefab; }

    [CustomPropertyDrawer(typeof(ESAssetReferSceneConfigKey))]
    public sealed class ESAssetReferSceneConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSceneEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Scene; }

    [CustomPropertyDrawer(typeof(ESAssetReferSpriteConfigKey))]
    public sealed class ESAssetReferSpriteConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSpriteEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Sprite; }

    [CustomPropertyDrawer(typeof(ESAssetReferSpriteAtlasConfigKey))]
    public sealed class ESAssetReferSpriteAtlasConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferSpriteAtlasEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.SpriteAtlas; }

    [CustomPropertyDrawer(typeof(ESAssetReferTextureConfigKey))]
    public sealed class ESAssetReferTextureConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTextureEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Texture; }

    [CustomPropertyDrawer(typeof(ESAssetReferTexture2DConfigKey))]
    public sealed class ESAssetReferTexture2DConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTexture2DEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Texture2D; }

    [CustomPropertyDrawer(typeof(ESAssetReferMaterialConfigKey))]
    public sealed class ESAssetReferMaterialConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferMaterialEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Material; }

    [CustomPropertyDrawer(typeof(ESAssetReferMeshConfigKey))]
    public sealed class ESAssetReferMeshConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferMeshEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Mesh; }

    [CustomPropertyDrawer(typeof(ESAssetReferAnimationClipConfigKey))]
    public sealed class ESAssetReferAnimationClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAnimationClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AnimationClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferAnimatorControllerConfigKey))]
    public sealed class ESAssetReferAnimatorControllerConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAnimatorControllerEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AnimatorController; }

    [CustomPropertyDrawer(typeof(ESAssetReferAvatarConfigKey))]
    public sealed class ESAssetReferAvatarConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAvatarEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.Avatar; }

    [CustomPropertyDrawer(typeof(ESAssetReferAudioClipConfigKey))]
    public sealed class ESAssetReferAudioClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferAudioClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.AudioClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferVideoClipConfigKey))]
    public sealed class ESAssetReferVideoClipConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferVideoClipEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.VideoClip; }

    [CustomPropertyDrawer(typeof(ESAssetReferTimelineAssetConfigKey))]
    public sealed class ESAssetReferTimelineAssetConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTimelineAssetEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.TimelineAsset; }

    [CustomPropertyDrawer(typeof(ESAssetReferPlayableAssetConfigKey))]
    public sealed class ESAssetReferPlayableAssetConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferPlayableAssetEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.PlayableAsset; }

    [CustomPropertyDrawer(typeof(ESAssetReferTerrainDataConfigKey))]
    public sealed class ESAssetReferTerrainDataConfigKeyDrawer : ESAssetConfigKeyDrawerBase { protected override Type ResolveEnumType() => typeof(ESAssetReferTerrainDataEnumKey); protected override ESAssetReferKind ResolveKind() => ESAssetReferKind.TerrainData; }
}
