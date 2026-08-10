using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESActionSliceABuilder
    {
        private const string ActionFolder = "Assets/ESNormalAssets/Data/Action";
        private const string ActionPath = ActionFolder + "/MeleeAttack_SliceA.asset";
        private const string MappingPath = ActionFolder + "/MeleeAttack_PresentationMapping_SliceA.asset";
        private const string ActionGroupPath = ActionFolder + "/ActionTemplateDataGroup_SliceA.asset";
        private const string MappingGroupPath = ActionFolder + "/ActionPresentationMappingDataGroup_SliceA.asset";
        private const string ActionGroupEntryKey = "action.melee.slice_a";
        private const string MappingGroupEntryKey = "action.presentation_mapping.slice_a";
        [MenuItem("【ES】/内容制作/动作/创建切片 A 近战候选资产", false, 150)]
        public static void CreateSliceAAssets()
        {
            BuildBaseAssets(
                out ActionTemplateDataInfo action,
                out _,
                out _,
                out _);

            Selection.activeObject = action;
            EditorGUIUtility.PingObject(action);
            Debug.LogWarning(
                "[ESAction] 切片 A 候选资产与 Group 已创建：" + ActionPath
                + "。它们尚未进入运行时 Table；请通过明确的正式 GameCore Consumer 接入两个 Group，"
                + "并由音频内容流程创建 ESAudioCue/AssetTable 引用。",
                action);
        }

        private static void BuildBaseAssets(
            out ActionTemplateDataInfo action,
            out ActionPresentationMappingDataInfo mapping,
            out ActionTemplateDataGroup actionGroup,
            out ActionPresentationMappingDataGroup mappingGroup)
        {
            EnsureFolder(ActionFolder);

            action = LoadOrCreate<ActionTemplateDataInfo>(ActionPath, "MeleeAttack_SliceA", out bool actionCreated);
            mapping = LoadOrCreate<ActionPresentationMappingDataInfo>(
                MappingPath,
                "MeleeAttack_PresentationMapping_SliceA",
                out bool mappingCreated);
            actionGroup = LoadOrCreate<ActionTemplateDataGroup>(
                ActionGroupPath,
                "ActionTemplateDataGroup_SliceA",
                out _);
            mappingGroup = LoadOrCreate<ActionPresentationMappingDataGroup>(
                MappingGroupPath,
                "ActionPresentationMappingDataGroup_SliceA",
                out _);

            if (actionCreated)
            {
                ConfigureAction(action);
                action.SetKey(ActionGroupEntryKey);
            }
            if (mappingCreated)
            {
                ConfigureMapping(mapping);
                mapping.SetKey(MappingGroupEntryKey);
            }
            AddToGroup(actionGroup, action);
            AddToGroup(mappingGroup, mapping);

            EditorUtility.SetDirty(action);
            EditorUtility.SetDirty(mapping);
            EditorUtility.SetDirty(actionGroup);
            EditorUtility.SetDirty(mappingGroup);

            AssetDatabase.SaveAssetIfDirty(action);
            AssetDatabase.SaveAssetIfDirty(mapping);
            AssetDatabase.SaveAssetIfDirty(actionGroup);
            AssetDatabase.SaveAssetIfDirty(mappingGroup);
            AssetDatabase.Refresh();

        }

        private static void ConfigureAction(ActionTemplateDataInfo action)
        {
            action.actionKey = "melee.attack";
            action.category = ESActionCategory.Attack;
            action.allowBufferedInput = true;
            action.globalInputBufferWindow = 0.08f;
            action.phases.Clear();
            action.phases.Add(new ESActionPhaseData
            {
                kind = ESActionPhaseKind.Startup,
                duration = 0.12f,
                inputBufferWindow = 0.08f,
            });
            action.phases.Add(new ESActionPhaseData
            {
                kind = ESActionPhaseKind.Active,
                duration = 0.14f,
                hitWindow = new ESActionHitWindowData
                {
                    enabled = true,
                    radius = 1f,
                    forwardDistance = 1f,
                    damageMultiplier = 1f,
                },
            });
            action.phases.Add(new ESActionPhaseData
            {
                kind = ESActionPhaseKind.Recovery,
                duration = 0.22f,
            });

            action.comboTransitions.Clear();
            action.comboTransitions.Add(new ESActionComboTransitionData
            {
                fromStep = 0,
                toStep = 1,
                targetActionKey = "melee.attack",
                inputBufferWindow = 0.1f,
            });

            action.cancelRules.Clear();
            action.presentationBindings.Clear();
        }

        private static void ConfigureMapping(ActionPresentationMappingDataInfo mapping)
        {
            mapping.entries.Clear();
        }

        private static T LoadOrCreate<T>(string path, string assetName, out bool created)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                created = false;
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        private static void AddToGroup<T>(SoDataGroup<T> group, T info) where T : ScriptableObject, ISoDataInfo
        {
            if (group == null || info == null)
                return;

            string key = info.GetKey();
            if (string.IsNullOrWhiteSpace(key))
                throw new System.InvalidOperationException(
                    "[ESAction] Group 成员缺少编辑器组织 Key：" + info.name);
            if (group.Infos == null)
                group.Infos = new Dictionary<string, T>();

            if (group.Infos.TryGetValue(key, out T existing))
            {
                if (!ReferenceEquals(existing, info))
                    Debug.LogError("[ESAction] Group Key 冲突：" + key + " " + group.name);
                return;
            }

            group.Infos.Add(key, info);
        }

        private static void EnsureFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                current += "/" + parts[i];
                if (AssetDatabase.IsValidFolder(current))
                    continue;

                string parent = current.Substring(0, current.LastIndexOf('/'));
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
