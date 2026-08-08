using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    [CustomEditor(typeof(ESAssetLibraryConsumer))]
    public sealed class ESAssetLibraryConsumerEditor : OdinEditor
    {
        private ScriptableObject pendingManualGameCore;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            DrawGameCoreOverview((ESAssetLibraryConsumer)target);
        }

        private void DrawGameCoreOverview(ESAssetLibraryConsumer consumer)
        {
            if (consumer == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("启动 GameCore", EditorStyles.boldLabel);
                if (GUILayout.Button("重新同步", GUILayout.Width(76f)))
                    Sync(consumer);
                EditorGUILayout.EndHorizontal();

                List<ESAssetReferBase> collected = (consumer.GameCoreAssets ?? new List<ESAssetReferBase>())
                    .Where(item => item != null).ToList();
                var manualIdentities = new HashSet<ESAssetIdentity>((consumer.ManualGameCoreAssets ?? new List<ESAssetReferBase>())
                    .Where(item => item != null && item.IsValid).Select(item => item.AssetIdentity));
                int manualCount = collected.Count(item => item.IsValid && manualIdentities.Contains(item.AssetIdentity));
                int errorCount = consumer.GameCoreValidationErrors?.Count ?? 0;

                EditorGUILayout.LabelField(
                    $"已收集 {collected.Count} 个 · 自动 {Mathf.Max(0, collected.Count - manualCount)} · 手动 {manualCount} · 错误 {errorCount}",
                    errorCount == 0 ? EditorStyles.miniBoldLabel : EditorStyles.boldLabel);

                if (collected.Count == 0)
                    EditorGUILayout.HelpBox("尚未同步启动 GameCore。", MessageType.Info);
                else
                    foreach (ESAssetReferBase refer in collected)
                        DrawGameCoreRow(refer, manualIdentities.Contains(refer.AssetIdentity));

                if (errorCount > 0)
                    foreach (string error in consumer.GameCoreValidationErrors.Where(item => !string.IsNullOrWhiteSpace(item)))
                        EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            DrawManualSection(consumer);
        }

        private void DrawManualSection(ESAssetLibraryConsumer consumer)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("手动补充启动 GameCore", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                pendingManualGameCore = EditorGUILayout.ObjectField(
                    new GUIContent("GameCore 资产", "只接受实现 IGameCoreSO 的 ScriptableObject，支持子资产。"),
                    pendingManualGameCore,
                    typeof(ScriptableObject),
                    false) as ScriptableObject;
                using (new EditorGUI.DisabledScope(pendingManualGameCore == null))
                {
                    if (GUILayout.Button("添加", GUILayout.Width(48f)))
                    {
                        Undo.RecordObject(consumer, "Add Manual GameCore");
                        if (!ESAssetConsumerReferenceAuthoring.TryAddManualGameCoreAsset(consumer, pendingManualGameCore))
                            Debug.LogWarning("[ESRes][Consumer] 只能添加有效的 IGameCoreSO 资产。", pendingManualGameCore);
                        else
                        {
                            pendingManualGameCore = null;
                            Sync(consumer);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                List<ESAssetReferBase> manual = consumer.ManualGameCoreAssets ?? new List<ESAssetReferBase>();
                if (manual.Count == 0)
                    EditorGUILayout.LabelField("无手动补充项。", EditorStyles.miniLabel);
                else
                    for (int i = 0; i < manual.Count; i++)
                    {
                        ESAssetReferBase refer = manual[i];
                        EditorGUILayout.BeginHorizontal();
                        DrawCompactIdentity(refer);
                        if (GUILayout.Button("移除", EditorStyles.miniButton, GUILayout.Width(42f)))
                        {
                            Undo.RecordObject(consumer, "Remove Manual GameCore");
                            manual.RemoveAt(i);
                            EditorUtility.SetDirty(consumer);
                            Sync(consumer);
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
            }
        }

        private static void DrawGameCoreRow(ESAssetReferBase refer, bool manual)
        {
            ScriptableObject asset = ResolveExact(refer);
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : AssetDatabase.GUIDToAssetPath(refer?.GUID ?? string.Empty);
            bool valid = asset != null;
            string relativePath = string.IsNullOrEmpty(path) ? "<资产丢失>" : (path.StartsWith("Assets/", StringComparison.Ordinal) ? path.Substring(7) : path);
            string type = asset != null ? ResolveGameCoreType(asset) : "Missing";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(valid ? "✓" : "×", valid ? EditorStyles.boldLabel : EditorStyles.whiteBoldLabel, GUILayout.Width(18f));
                EditorGUILayout.LabelField(asset != null ? asset.name : "资产丢失", EditorStyles.boldLabel, GUILayout.MinWidth(90f));
                EditorGUILayout.LabelField(type, GUILayout.Width(96f));
                EditorGUILayout.LabelField(relativePath, EditorStyles.miniLabel);
                if (manual) GUILayout.Label("手动", EditorStyles.miniBoldLabel, GUILayout.Width(30f));
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("定位", EditorStyles.miniButtonLeft, GUILayout.Width(38f))) Ping(asset);
                    if (GUILayout.Button("查看", EditorStyles.miniButtonRight, GUILayout.Width(38f))) ESGameCoreDefinitionEditorWindow.Open(asset);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawCompactIdentity(ESAssetReferBase refer)
        {
            ScriptableObject asset = ResolveExact(refer);
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            string text = asset != null
                ? asset.name + " · " + ResolveGameCoreType(asset) + " · " + path
                : "缺失资产 · " + (refer?.GUID ?? "<null>");
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }

        private static void Sync(ESAssetLibraryConsumer consumer)
        {
            try
            {
                Undo.RecordObject(consumer, "Sync Consumer GameCore");
                ESAssetConsumerReferenceAuthoring.SyncConsumerGameCoreAssets(consumer);
                EditorUtility.SetDirty(consumer);
                Debug.Log("[ESRes][Consumer] GameCore 已重新同步：" + consumer.Name, consumer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, consumer);
            }
        }

        private static ScriptableObject ResolveExact(ESAssetReferBase refer)
        {
            if (refer == null || !refer.IsValid) return null;
            string path = AssetDatabase.GUIDToAssetPath(refer.GUID);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
                if (loaded is ScriptableObject asset
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                    && string.Equals(guid, refer.GUID, StringComparison.Ordinal)
                    && localFileId == refer.LocalFileId)
                    return asset;
            return null;
        }

        private static string ResolveGameCoreType(ScriptableObject asset)
        {
            if (asset is ItemDataInfo item && item.baseConfig != null)
                return "Item/" + item.baseConfig.kind;
            string name = asset.GetType().Name;
            name = name.Replace("DefinitionDataInfo", string.Empty).Replace("DataInfo", string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "GameCore" : name;
        }

        private static void Ping(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
