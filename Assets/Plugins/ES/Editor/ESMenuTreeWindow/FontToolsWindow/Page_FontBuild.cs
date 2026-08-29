using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class Page_FontBuild : ESWindowPageBase
    {
        [AssetsOnly, InlineEditor(Expanded = true, DrawPreview = false), LabelText("字体构建方案（仅编辑器）")]
        public ESFontBuildProfile profile;

        [ShowInInspector, ReadOnly, LabelText("字符汇总")]
        private string CharacterSummary
        {
            get
            {
                if (profile == null) return "请选择或创建字体构建方案。";
                int count = 0;
                foreach (var language in profile.languages ?? new List<ESFontLanguageBuildEntry>())
                {
                    if (language == null) continue;
                    count += ESFontBuildProfileEditor.CountUnicodeScalars(
                        ESFontBuildProfileEditor.CollectCharacters(profile, language));
                }
                return "启用语言：" + (profile.enabledLanguages?.Count ?? 0)
                    + " | 字体角色：" + (profile.enabledUsages?.Count ?? 0)
                    + " | 生成条目：" + (profile.languages?.Count ?? 0)
                    + " | Unicode 标量总数：" + count;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("构建预检")]
        private string BuildPreview => ESFontBuildProfileEditor.Preview(profile);

        [ShowInInspector, ReadOnly, LabelText("发布链状态")]
        private string PublishState => ESPresentationAssetPipelineStatus.Describe(profile?.runtimeCatalog, residentConsumer);

        [NonSerialized] private ESAssetLibraryConsumer residentConsumer;

        [OnInspectorGUI]
        private void DrawTool()
        {
            EditorGUILayout.HelpBox("只需配置 ES 字体族、启用语言/角色和文本来源。生成器负责字符收集、字体资产、自动回退链与运行时目录；普通开发者无需操作 TMP。", MessageType.Info);
            EditorGUILayout.HelpBox("受管目录可使用稳定文件名直接绑定：ESFont_Latin、ESFont_Cyrillic、ESFont_ChineseSimplified、ESFont_ChineseTraditional、ESFont_Japanese、ESFont_Korean、ESFont_Symbols；角色专用字体在末尾追加 _Body、_Title、_Number 或 _Icon。", MessageType.None);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(CharacterSummary, EditorStyles.wordWrappedMiniLabel);
                residentConsumer = (ESAssetLibraryConsumer)EditorGUILayout.ObjectField(
                    "启动 Consumer", residentConsumer, typeof(ESAssetLibraryConsumer), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建十语言方案", GUILayout.Height(30), GUILayout.Width(145))) CreateProfile();
                    using (new EditorGUI.DisabledScope(profile == null))
                    {
                        if (GUILayout.Button("同步语言与角色", GUILayout.Height(30), GUILayout.Width(135))) SynchronizeProfile();
                        if (GUILayout.Button("预检", GUILayout.Height(30), GUILayout.Width(100))) PreviewCurrentProfile();
                        if (GUILayout.Button("生成当前方案", GUILayout.Height(30), GUILayout.Width(145))) ExecuteBuild();
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(profile == null))
                    {
                        if (GUILayout.Button("迁移旧字体配置", GUILayout.Height(24))) MigrateLegacyProfile();
                        if (GUILayout.Button("绑定受管源字体", GUILayout.Height(24))) BindManagedSourceFonts();
                        if (GUILayout.Button("定位受管源字体目录", GUILayout.Height(24))) LocateSourceFontFolder();
                    }
                    if (GUILayout.Button("生成全部方案", GUILayout.Height(24))) ExecuteUpdateAll();
                }
                using (new EditorGUI.DisabledScope(profile == null || profile.runtimeCatalog == null))
                {
                    if (GUILayout.Button("登记运行时字体目录", GUILayout.Height(24)))
                        ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(profile.runtimeCatalog);
                    using (new EditorGUI.DisabledScope(residentConsumer == null))
                    {
                        if (GUILayout.Button("将运行时字体目录登记为 Consumer 启动常驻", GUILayout.Height(24)))
                        {
                            if (!ESAssetConsumerReferenceAuthoring.TryAddResidentAsset(residentConsumer, profile.runtimeCatalog, out string error))
                                EditorUtility.DisplayDialog("ES 字体目录", "登记失败：" + error, "确定");
                            else
                                EditorUtility.DisplayDialog("ES 字体目录", "运行时字体目录已登记为 Consumer 启动常驻资产。", "确定");
                        }
                    }
                }
            }
            if (profile != null && !string.IsNullOrEmpty(profile.lastBuildReport))
            {
                EditorGUILayout.LabelField("最近构建报告", EditorStyles.boldLabel);
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
            asset.generationQuality = ESFontGenerationQuality.HighDefinition;
            asset.sourceFontFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceFolder);
            asset.fontFamily.familyId = "game_ui";
            ESLocalizationCatalog defaultCatalog = AssetDatabase.LoadAssetAtPath<ESLocalizationCatalog>(
                "Assets/ESNormalAssets/Localization/ESLocalizationCatalog.asset");
            if (defaultCatalog != null) asset.localizationCatalogs.Add(defaultCatalog);
            asset.languages.Add(new ESFontLanguageBuildEntry
            {
                language = EnumCollect.Envir_LanguageType.ChineseSimplified,
                usage = ESFontUsage.Body,
                textFolders = { AssetDatabase.LoadAssetAtPath<DefaultAsset>(chineseTextFolder) },
                additionalCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz，。！？、】【、】【（）《》：；、】【+-=*/%&@#_~·…"
            });
            ESFontBuildProfileEditor.ApplyStandardTenLanguageTemplate(asset);
            string path = AssetDatabase.GenerateUniqueAssetPath(profileFolder + "/ESFontBuildProfile.asset");
            try
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            catch
            {
                if (asset != null && !EditorUtility.IsPersistent(asset))
                    UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
            AssetDatabase.SaveAssetIfDirty(asset);
            profile = asset;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void SynchronizeProfile()
        {
            try
            {
                Undo.RecordObject(profile, "同步 ES 字体语言与角色");
                ESFontBuildProfileEditor.SynchronizeLanguageEntries(profile);
                EditorUtility.SetDirty(profile);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("ES 字体配置", exception.Message, "确定");
            }
        }

        private void MigrateLegacyProfile()
        {
            try
            {
                string report = ESFontBuildProfileEditor.MigrateLegacyConfiguration(profile);
                EditorUtility.DisplayDialog("ES 字体配置迁移", report, "确定");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("ES 字体配置迁移", "迁移未执行：" + exception.Message, "确定");
            }
        }

        private void LocateSourceFontFolder()
        {
            if (profile?.sourceFontFolder == null)
            {
                EditorUtility.DisplayDialog("ES 字体配置", "尚未绑定受管源字体目录。", "确定");
                return;
            }
            Selection.activeObject = profile.sourceFontFolder;
            EditorGUIUtility.PingObject(profile.sourceFontFolder);
        }

        private void BindManagedSourceFonts()
        {
            try
            {
                string report = ESFontBuildProfileEditor.BindManagedSourceFonts(profile);
                EditorUtility.DisplayDialog("ES 字体配置", report, "确定");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("ES 字体配置", "绑定未执行：" + exception.Message, "确定");
            }
        }

        private void ExecuteBuild()
        {
            try { ESFontBuildProfileEditor.Build(profile); }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("ES 字体构建", exception.Message, "确定"); }
        }

        private void PreviewCurrentProfile()
        {
            if (profile == null)
                return;
            Undo.RecordObject(profile, "预览字体构建配置");
            profile.lastBuildReport = ESFontBuildProfileEditor.Preview(profile);
            EditorUtility.SetDirty(profile);
        }

        private void ExecuteUpdateAll()
        {
            try
            {
                int count = ESFontBuildProfileEditor.UpdateAllProfiles();
                EditorUtility.DisplayDialog("ES 字体更新", $"已更新 {count} 个字体方案。", "确定");
            }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("ES 字体更新", exception.Message, "确定"); }
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

    [Serializable]
    public sealed class Page_FontPreview : ESWindowPageBase
    {
        [AssetsOnly, InlineEditor(Expanded = true, DrawPreview = false), LabelText("字体构建方案（仅编辑器）")]
        public ESFontBuildProfile profile;

        [ShowInInspector, LabelText("条目")]
        [ValueDropdown(nameof(GetLanguageOptions))]
        public int selectedLanguageIndex;

        [ShowInInspector, MultiLineProperty(3), LabelText("采样文本")]
        public string sampleText = "简体中文 / 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español / Português / Русский 0123456789";

        [NonSerialized] private ESEditorPreviewRenderContext previewContext;
        [NonSerialized] private ESEditorPreviewModelHandle previewModel;
        [NonSerialized] private GameObject previewObject;
        [NonSerialized] private TextMeshPro previewText;
        [NonSerialized] private TMP_FontAsset previewFont;
        [NonSerialized] private string renderedSample;

        [ShowInInspector, ReadOnly, LabelText("当前字体")]
        private TMP_FontAsset CurrentFont => GetSelectedEntry()?.outputFont;

        [ShowInInspector, ReadOnly, LabelText("覆盖率")]
        private string Coverage => BuildCoverageReport(GetSelectedEntry());

        [ShowInInspector, ReadOnly, LabelText("运行时绑定")]
        private string RuntimeBinding => BuildRuntimeBindingReport(GetSelectedEntry());

        [OnInspectorGUI]
        private void DrawPreview()
        {
            EditorGUILayout.HelpBox("此页只读检查生成字体、图集、自动回退链和十语言覆盖率；修改配置请回到“方案与构建”。", MessageType.Info);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("请选择字体构建方案。", MessageType.Warning);
                return;
            }

            IReadOnlyList<ESFontLanguageBuildEntry> entries = profile.languages == null
                ? (IReadOnlyList<ESFontLanguageBuildEntry>)Array.Empty<ESFontLanguageBuildEntry>()
                : profile.languages.Where(item => item != null).ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("当前配置没有字体条目。", MessageType.Warning);
                return;
            }
            selectedLanguageIndex = Mathf.Clamp(selectedLanguageIndex, 0, entries.Count - 1);
            ESFontLanguageBuildEntry entry = entries[selectedLanguageIndex];
            EditorGUILayout.LabelField("身份", ESFontBuildProfileEditor.GetEntryIdentity(entry));
            EditorGUILayout.LabelField("输出", entry.outputFont == null ? "未生成" : AssetDatabase.GetAssetPath(entry.outputFont));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("采样文本", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(sampleText ?? string.Empty, GUILayout.MinHeight(46f));
                DrawCoverageList(entry);
            }

            TMP_FontAsset font = entry.outputFont;
            if (font == null) return;
            DrawRenderedPreview(font);
            EditorGUILayout.LabelField("生成字体资产（只读）", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(font, typeof(TMP_FontAsset), false);
            DrawAtlas(font);
            if (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count > 0)
            {
                EditorGUILayout.LabelField("自动回退链", EditorStyles.boldLabel);
                foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable.Where(item => item != null))
                    EditorGUILayout.ObjectField(fallback, typeof(TMP_FontAsset), false);
            }
        }

        private IEnumerable<ValueDropdownItem<int>> GetLanguageOptions()
        {
            if (profile?.languages == null) yield break;
            int index = 0;
            foreach (ESFontLanguageBuildEntry entry in profile.languages.Where(item => item != null))
                yield return new ValueDropdownItem<int>(ESFontBuildProfileEditor.GetEntryIdentity(entry), index++);
        }

        private ESFontLanguageBuildEntry GetSelectedEntry()
        {
            if (profile?.languages == null) return null;
            int target = Mathf.Max(0, selectedLanguageIndex);
            int current = 0;
            foreach (ESFontLanguageBuildEntry entry in profile.languages)
            {
                if (entry == null) continue;
                if (current++ == target) return entry;
            }
            return null;
        }

        private string BuildRuntimeBindingReport(ESFontLanguageBuildEntry entry)
        {
            if (entry == null || profile == null) return "未选择条目。";
            if (entry.usage == ESFontUsage.Custom) return "Custom 条目不进入 Runtime Font Catalog。";
            if (!ESLocalizationRuntime.IsConcreteLanguage(entry.language)) return "语言身份无效。";
            return profile.runtimeCatalog != null && profile.runtimeCatalog.TryResolve(entry.language, ConvertRole(entry.usage), out TMP_FontAsset font)
                ? "已绑定：" + AssetDatabase.GetAssetPath(font)
                : "未绑定到当前 Runtime Font Catalog。";
        }

        private string BuildCoverageReport(ESFontLanguageBuildEntry entry)
        {
            if (entry == null || profile == null) return "未选择条目。";
            if (entry.outputFont == null) return "尚未生成字体资产。";
            string characters = GetPreviewCharacters(entry);
            int missing = 0;
            int total = 0;
            var visited = new HashSet<TMP_FontAsset>();
            var scalars = new SortedSet<uint>();
            ESFontBuildProfileEditor.AddUnicodeScalars(scalars, characters);
            foreach (uint scalar in scalars)
            {
                total++;
                visited.Clear();
                if (!ESFontBuildProfileEditor.CanResolveUnicodeScalar(entry.outputFont, scalar, visited)) missing++;
            }
            return total == 0 ? "没有采样字符。" : $"{total - missing}/{total} ({(total - missing) * 100f / total:0.0}%)，未解析 {missing} 个";
        }

        private void DrawCoverageList(ESFontLanguageBuildEntry entry)
        {
            if (entry == null || entry.outputFont == null) return;
            string characters = GetPreviewCharacters(entry);
            var missing = new List<uint>();
            var visited = new HashSet<TMP_FontAsset>();
            var scalars = new SortedSet<uint>();
            ESFontBuildProfileEditor.AddUnicodeScalars(scalars, characters);
            foreach (uint scalar in scalars)
            {
                visited.Clear();
                if (!ESFontBuildProfileEditor.CanResolveUnicodeScalar(entry.outputFont, scalar, visited)) missing.Add(scalar);
            }
            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox("当前采样字符均能由主字体或 Fallback 链解析。", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("未解析字符（前 120 个）：" +
                ESFontBuildProfileEditor.BuildUnicodeString(missing.Take(120)), MessageType.Warning);
        }

        private string GetPreviewCharacters(ESFontLanguageBuildEntry entry)
        {
            if (entry == null || profile == null) return string.Empty;
            string configured = ESFontBuildProfileEditor.CollectCharacters(profile, entry);
            string preview = sampleText ?? string.Empty;
            var characters = new SortedSet<uint>();
            ESFontBuildProfileEditor.AddUnicodeScalars(characters, configured);
            ESFontBuildProfileEditor.AddUnicodeScalars(characters, preview);
            return ESFontBuildProfileEditor.BuildUnicodeString(characters);
        }

        private static void DrawAtlas(TMP_FontAsset font)
        {
            Texture2D atlas = font.atlasTextures?.FirstOrDefault(texture => texture != null);
            if (atlas == null)
            {
                EditorGUILayout.HelpBox("当前字体没有可预览的 Atlas。", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Atlas 预览", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetAspectRect((float)atlas.width / Mathf.Max(1, atlas.height), GUILayout.MaxHeight(300f));
            EditorGUI.DrawPreviewTexture(rect, atlas, null, ScaleMode.ScaleToFit);
        }

        private void DrawRenderedPreview(TMP_FontAsset font)
        {
            EditorGUILayout.LabelField("实际文本预览", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(260f, 190f, GUILayout.ExpandWidth(true));
            try
            {
                EnsurePreview(font);
                if (previewContext == null || previewModel == null || previewText == null)
                {
                    EditorGUI.HelpBox(rect, "无法创建字体预览。", MessageType.Warning);
                    return;
                }
                if (!previewContext.RenderCurrentCameraGUI(rect, ESEditorPreviewRenderOptions.Fast))
                    EditorGUI.HelpBox(rect, "字体预览渲染失败。", MessageType.Warning);
            }
            catch (Exception exception)
            {
                DisposePreview();
                EditorGUI.HelpBox(rect, "字体预览失败：" + exception.Message, MessageType.Warning);
            }
        }

        private void EnsurePreview(TMP_FontAsset font)
        {
            if (previewContext == null)
            {
                previewContext = new ESEditorPreviewRenderContext(
                    "ES Font Preview",
                    ESEditorPreviewSceneMode.PreviewScene,
                    ESEditorPreviewUtility.DefaultPreviewLayer,
                    ESEditorPreviewEnhancerSet.LowEnd);
                previewContext.Ensure();
                previewContext.Camera.clearFlags = CameraClearFlags.Color;
                previewContext.Camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
                previewContext.Camera.orthographic = true;
                previewContext.Camera.orthographicSize = 1.45f;
                previewContext.Camera.transform.position = new Vector3(0f, 0f, -10f);
                previewContext.Camera.transform.rotation = Quaternion.identity;

                previewObject = ESEditorPreviewUtility.CreatePreviewGameObject("__ESFontPreview__");
                previewText = previewObject.AddComponent<TextMeshPro>();
                previewText.alignment = TextAlignmentOptions.Center;
                previewText.fontSize = 0.22f;
                previewText.rectTransform.sizeDelta = new Vector2(7.5f, 2.4f);
                previewModel = previewContext.AdoptModelGroup(
                    previewObject,
                    previewObject,
                    "ES Font Preview",
                    samplingTarget: false,
                    copyRendererState: false,
                    disableRuntimeBehaviours: false,
                    ensureRenderersEnabled: true,
                    activateInstance: true,
                    moveToGroupOrigin: true);
                previewObject = null;
            }

            if (previewFont == font && string.Equals(renderedSample, sampleText, StringComparison.Ordinal))
                return;
            previewFont = font;
            renderedSample = sampleText ?? string.Empty;
            previewText.font = font;
            previewText.text = renderedSample;
            previewText.ForceMeshUpdate();
        }

        public override void OnPageDisable()
        {
            DisposePreview();
            base.OnPageDisable();
        }

        private void DisposePreview()
        {
            previewText = null;
            previewFont = null;
            renderedSample = null;
            ESEditorPreviewModelHandle model = previewModel;
            previewModel = null;
            try { model?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            ESEditorPreviewRenderContext context = previewContext;
            previewContext = null;
            try { context?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            GameObject objectToDestroy = previewObject;
            previewObject = null;
            if (objectToDestroy != null)
            {
                try
                {
                    UnityEngine.Object.DestroyImmediate(objectToDestroy);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static ESRuntimeFontRole ConvertRole(ESFontUsage usage)
        {
            switch (usage)
            {
                case ESFontUsage.Title: return ESRuntimeFontRole.Title;
                case ESFontUsage.Number: return ESRuntimeFontRole.Number;
                case ESFontUsage.Icon: return ESRuntimeFontRole.Icon;
                default: return ESRuntimeFontRole.Body;
            }
        }
    }
}
