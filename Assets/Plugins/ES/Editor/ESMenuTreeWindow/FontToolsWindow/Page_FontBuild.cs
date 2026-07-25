using System;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class Page_FontBuild : ESWindowPageBase
    {
        [AssetsOnly, InlineEditor(Expanded = true, DrawPreview = false), LabelText("Font Build Profile (EditorOnly)")]
        public ESFontBuildProfile profile;

        [ShowInInspector, ReadOnly, LabelText("Character Summary")]
        private string CharacterSummary
        {
            get
            {
                if (profile == null) return "Select or create an ESFontBuildProfile.";
                int count = 0;
                foreach (var language in profile.languages) count += ESFontBuildProfileEditor.CollectCharacters(profile, language).Length;
                return $"Languages: {profile.languages.Count} | Unique characters: {count}";
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Build Preview")]
        private string BuildPreview => ESFontBuildProfileEditor.Preview(profile);

        [OnInspectorGUI]
        private void DrawTool()
        {
            EditorGUILayout.HelpBox("Configure each profile once in this window: source font + TXT folders. Update does everything else: discovers TXT, collects glyphs, creates or updates TMP assets, builds fallback chains, and reports unresolved glyphs.", MessageType.Info);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(CharacterSummary, EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Profile", GUILayout.Height(30), GUILayout.Width(120))) CreateProfile();
                    using (new EditorGUI.DisabledScope(profile == null))
                    {
                        if (GUILayout.Button("Preview", GUILayout.Height(30), GUILayout.Width(100))) profile.lastBuildReport = ESFontBuildProfileEditor.Preview(profile);
                        if (GUILayout.Button("Update Current Profile", GUILayout.Height(30), GUILayout.Width(160))) ExecuteBuild();
                    }
                    if (GUILayout.Button("Update All Profiles", GUILayout.Height(30), GUILayout.Width(145))) ExecuteUpdateAll();
                }
            }
            if (profile != null && !string.IsNullOrEmpty(profile.lastBuildReport))
            {
                EditorGUILayout.LabelField("Latest Build Report", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(profile.lastBuildReport, GUILayout.MinHeight(110));
            }
        }

        private void CreateProfile()
        {
            const string profileFolder = "Assets/Plugins/ES/Editor/ESFontTools/Profiles";
            const string fontRoot = "Assets/ESNormalAssets/Fonts";
            const string sourceFolder = fontRoot + "/Source";
            const string chineseTextFolder = fontRoot + "/Text/zh-Hans";
            EnsureAssetFolder(profileFolder);
            EnsureAssetFolder(sourceFolder);
            EnsureAssetFolder(chineseTextFolder);
            EnsureAssetFolder(fontRoot + "/Generated");
            var asset = ScriptableObject.CreateInstance<ESFontBuildProfile>();
            asset.profileId = "game_ui";
            asset.outputFolder = fontRoot + "/Generated";
            asset.atlasSize = ESFontAtlasSize.Size4096;
            asset.enableMultiAtlasSupport = true;
            asset.sourceFontFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceFolder);
            asset.languages.Add(new ESFontLanguageBuildEntry
            {
                languageCode = "zh-Hans",
                usage = ESFontUsage.Body,
                textFolders = { AssetDatabase.LoadAssetAtPath<DefaultAsset>(chineseTextFolder) },
                additionalCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz，。！？、】【、】【（）《》：；、】【+-=*/%&@#_~·…"
            });
            string path = AssetDatabase.GenerateUniqueAssetPath(profileFolder + "/ESFontBuildProfile.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            profile = asset;
            Selection.activeObject = asset;
        }

        private void ExecuteBuild()
        {
            try { ESFontBuildProfileEditor.Build(profile); }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("ES Font Build", exception.Message, "OK"); }
        }

        private void ExecuteUpdateAll()
        {
            try
            {
                int count = ESFontBuildProfileEditor.UpdateAllProfiles();
                EditorUtility.DisplayDialog("ES Font Update", $"Updated {count} font profile(s).", "OK");
            }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("ES Font Update", exception.Message, "OK"); }
        }

        private static void EnsureAssetFolder(string folder)
        {
            var parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
