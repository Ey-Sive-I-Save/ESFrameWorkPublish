using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace ES
{
    public enum TextureFormat { PNG, JPG, TGA, EXR }

    #region 纹理精灵生成工具
    [Serializable]
    public class Page_TextureSpriteTool : ESWindowPageBase
    {
        [Title("纹理精灵生成工具", "批量处理纹理并生成Sprite", bold: true, titleAlignment: TitleAlignments.Centered)]

        [HideInInspector]
        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选中纹理或 Sprite，设置导入参数后批量处理。\n生成文件会自动避开重名，失败项会统一汇总。";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                var selected = Selection.objects ?? Array.Empty<UnityEngine.Object>();
                int textureCount = selected.OfType<Texture2D>().Count();
                int spriteCount = selected.OfType<Sprite>().Count();
                string normalizedOutput = SimpleToolsSafetyUtility.NormalizeAssetPath(outputFolder);
                string outputState = SimpleToolsSafetyUtility.IsAssetPath(normalizedOutput) ? normalizedOutput : "输出目录不在 Assets 下";
                return $"选中纹理: {textureCount} | 选中 Sprite: {spriteCount} | 输出: {outputState} | 同名处理: {(autoRenameOnConflict ? "自动改名" : "跳过")}";
            }
        }

        [FoldoutGroup("参数设置"), LabelText("操作纹理文件夹"), FolderPath, Space(5)]
        public string textureFolder;

        [FoldoutGroup("参数设置"), LabelText("输出文件夹"), FolderPath, Space(5)]
        public string outputFolder;

        [FoldoutGroup("参数设置"), LabelText("Sprite模式"), Space(5)]
        public SpriteImportMode spriteMode = SpriteImportMode.Single;

        [FoldoutGroup("参数设置"), LabelText("像素每单位"), Space(5)]
        public float pixelsPerUnit = 100f;

        [FoldoutGroup("参数设置"), LabelText("过滤模式"), Space(5)]
        public FilterMode filterMode = FilterMode.Point;

        [FoldoutGroup("参数设置"), LabelText("压缩质量 (0: 最低质量, 100: 最高质量)"), Range(0, 100), Space(5)]
        public int compressionQuality = 50;

        [FoldoutGroup("参数设置"), LabelText("最大纹理尺寸"), Space(5)]
        public int maxTextureSize = 2048;

        [FoldoutGroup("参数设置"), LabelText("生成可读写纹理"), Space(5)]
        public bool isReadable = false;

        [FoldoutGroup("参数设置"), LabelText("生成MipMaps"), Space(5)]
        public bool generateMipMaps = false;

        [FoldoutGroup("参数设置"), LabelText("生成的可读写纹理"), Space(5)]
        public bool generatedReadable = false;

        [FoldoutGroup("参数设置"), LabelText("输出格式"), Space(5)]
        public TextureFormat outputFormat = TextureFormat.PNG;

        [FoldoutGroup("参数设置"), LabelText("JPG质量"), Range(0, 100), Space(5)]
        [ShowIf("@outputFormat == TextureFormat.JPG")]
        public int jpgQuality = 75;

        [FoldoutGroup("参数设置"), LabelText("同名文件自动改名"), Space(5)]
        [InfoBox("开启后不会覆盖已有文件，会自动生成 _1、_2 这类安全文件名。关闭时遇到同名文件会跳过。", InfoMessageType.Info)]
        public bool autoRenameOnConflict = true;

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string texturePreviewSearch = "";
        private int texturePreviewPageIndex;
        private const int TexturePreviewPageSize = 12;
        private readonly List<string> cachedTextureFolderPaths = new List<string>();
        private string cachedTextureFolderSignature = "";

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            var previewRows = BuildTexturePreviewRows();
            SimpleToolsPanelUtility.DrawToolHeader(
                "纹理与 Sprite 批处理",
                "用于批量修改 TextureImporter 为 Sprite 设置，或从 Sprite 裁切生成独立纹理文件。",
                SimpleToolsMaturity.Upgrading,
                "修改导入设置会影响项目资产并触发重新导入；从 Sprite 生成纹理会写入新文件。执行前请确认输出目录和同名处理策略。");
            SimpleToolsPanelUtility.DrawLargeListGuard(previewRows.Count, "纹理/Sprite");
            DrawTexturePreviewPanel(previewRows);
            DrawTextureActionPanel(previewRows);
            SimpleToolsPanelUtility.DrawResultSummary("最近贴图处理结果", lastResultSummary, lastResultDetail);
        }

        private void DrawTextureActionPanel(List<TexturePreviewRow> rows)
        {
            int textureCount = rows?.Count(row => row != null && row.Kind == "Texture" && SimpleToolsSafetyUtility.IsAssetPath(row.Path)) ?? 0;
            int spriteCount = rows?.Count(row => row != null && row.Kind == "Sprite") ?? 0;

            SimpleToolsPanelUtility.DrawSectionTitle("执行操作", "导入设置只处理 Texture；Sprite 裁切只处理当前 Project 选中的 Sprite。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"可处理纹理: {textureCount} | 可生成 Sprite: {spriteCount} | 输出目录: {SimpleToolsSafetyUtility.NormalizeAssetPath(outputFolder)}", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = textureCount > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("应用导入设置", SimpleToolsActionTone.Primary, 34, GUILayout.MinWidth(140)))
                        ProcessSelectedTextures();

                    GUI.enabled = spriteCount > 0;
                    if (SimpleToolsPanelUtility.DrawActionButton("从 Sprite 生成纹理", SimpleToolsActionTone.Warning, 34, GUILayout.MinWidth(150)))
                        GenerateTexturesFromSprites();

                    GUI.enabled = true;
                    if (SimpleToolsPanelUtility.DrawActionButton("重置参数", SimpleToolsActionTone.Neutral, 34, GUILayout.Width(90)))
                        ResetToDefaults();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawTexturePreviewPanel(List<TexturePreviewRow> rows)
        {
            SimpleToolsPanelUtility.DrawSectionTitle("选中/文件夹资源预览", "按资源名、路径、类型和来源搜索；Project 选区与纹理文件夹会合并去重。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string normalizedOutput = SimpleToolsSafetyUtility.NormalizeAssetPath(outputFolder);
                EditorGUILayout.LabelField($"输出目录: {(SimpleToolsSafetyUtility.IsAssetPath(normalizedOutput) ? normalizedOutput : "不可用，必须在 Assets 下")}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"导入设置: SpriteMode={spriteMode} | PPU={pixelsPerUnit:0.##} | Filter={filterMode} | MaxSize={maxTextureSize} | MipMaps={(generateMipMaps ? "开" : "关")}", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    texturePreviewSearch = EditorGUILayout.TextField(texturePreviewSearch);
                    if (GUILayout.Button("刷新文件夹", EditorStyles.miniButton, GUILayout.Width(76)))
                    {
                        RefreshTextureFolderCache(true);
                        texturePreviewPageIndex = 0;
                    }
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        texturePreviewSearch = string.Empty;
                        texturePreviewPageIndex = 0;
                    }
                }

                rows = FilterTexturePreviewRows(rows);
                if (rows.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前 Project 选区没有 Texture2D 或 Sprite，或搜索条件没有命中。");
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("资源路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(200));
                    EditorGUILayout.LabelField("类型", EditorStyles.miniBoldLabel, GUILayout.Width(58));
                    EditorGUILayout.LabelField("尺寸", EditorStyles.miniBoldLabel, GUILayout.Width(72));
                    EditorGUILayout.LabelField("状态", EditorStyles.miniBoldLabel, GUILayout.Width(120));
                    EditorGUILayout.LabelField("来源", EditorStyles.miniBoldLabel, GUILayout.Width(58));
                    GUILayout.Space(48);
                }

                foreach (var row in SimpleToolsPanelUtility.PageItems(rows, ref texturePreviewPageIndex, TexturePreviewPageSize, out _))
                    DrawTexturePreviewRow(row);

                SimpleToolsPanelUtility.DrawPager(ref texturePreviewPageIndex, rows.Count, TexturePreviewPageSize);
            }
        }

        private List<TexturePreviewRow> BuildTexturePreviewRows()
        {
            var rows = new List<TexturePreviewRow>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in Selection.objects ?? Array.Empty<UnityEngine.Object>())
            {
                if (obj is Texture2D texture)
                {
                    string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
                    if (!seenPaths.Add(path))
                        continue;
                    rows.Add(new TexturePreviewRow
                    {
                        Asset = texture,
                        Path = path,
                        Kind = "Texture",
                        Size = $"{texture.width}x{texture.height}",
                        State = SimpleToolsSafetyUtility.IsAssetPath(path) ? "可改导入设置" : "非Assets资源",
                        Source = "选中"
                    });
                }
                else if (obj is Sprite sprite)
                {
                    string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(sprite));
                    if (!seenPaths.Add(path + "#" + sprite.name))
                        continue;
                    rows.Add(new TexturePreviewRow
                    {
                        Asset = sprite,
                        Path = path,
                        Kind = "Sprite",
                        Size = $"{Mathf.RoundToInt(sprite.rect.width)}x{Mathf.RoundToInt(sprite.rect.height)}",
                        State = sprite.texture != null && sprite.texture.isReadable ? "可生成" : "源纹理不可读",
                        Source = "选中"
                    });
                }
            }

            foreach (var path in RefreshTextureFolderCache(false))
            {
                if (!seenPaths.Add(path))
                    continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                    continue;

                rows.Add(new TexturePreviewRow
                {
                    Asset = texture,
                    Path = path,
                    Kind = "Texture",
                    Size = $"{texture.width}x{texture.height}",
                    State = "可改导入设置",
                    Source = "文件夹"
                });
            }

            return rows;
        }

        private List<TexturePreviewRow> FilterTexturePreviewRows(List<TexturePreviewRow> rows)
        {
            if (rows == null)
                return new List<TexturePreviewRow>();

            if (string.IsNullOrWhiteSpace(texturePreviewSearch))
                return rows;

            string keyword = texturePreviewSearch.Trim();
            return rows.Where(row =>
                ContainsIgnoreCase(row.Path, keyword) ||
                ContainsIgnoreCase(row.Kind, keyword) ||
                ContainsIgnoreCase(row.State, keyword) ||
                ContainsIgnoreCase(row.Source, keyword)).ToList();
        }

        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(keyword) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawTexturePreviewRow(TexturePreviewRow row)
        {
            if (row == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.Path, EditorStyles.miniLabel, GUILayout.MinWidth(200));
                EditorGUILayout.LabelField(row.Kind, EditorStyles.miniLabel, GUILayout.Width(58));
                EditorGUILayout.LabelField(row.Size, EditorStyles.miniLabel, GUILayout.Width(72));
                EditorGUILayout.LabelField(row.State, EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField(row.Source, EditorStyles.miniLabel, GUILayout.Width(58));
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Selection.activeObject = row.Asset;
                    EditorGUIUtility.PingObject(row.Asset);
                }
            }
        }

        private class TexturePreviewRow
        {
            public UnityEngine.Object Asset;
            public string Path;
            public string Kind;
            public string Size;
            public string State;
            public string Source;
        }

        private List<string> RefreshTextureFolderCache(bool forceRefresh)
        {
            string normalizedFolder = SimpleToolsSafetyUtility.NormalizeAssetPath(textureFolder);
            string signature = AssetDatabase.IsValidFolder(normalizedFolder) ? normalizedFolder : string.Empty;
            if (!forceRefresh && signature == cachedTextureFolderSignature)
                return new List<string>(cachedTextureFolderPaths);

            cachedTextureFolderPaths.Clear();
            cachedTextureFolderSignature = signature;
            if (string.IsNullOrEmpty(signature))
                return new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { signature }))
            {
                string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (SimpleToolsSafetyUtility.IsAssetPath(path))
                    cachedTextureFolderPaths.Add(path);
            }

            cachedTextureFolderPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return new List<string>(cachedTextureFolderPaths);
        }

        public override ESWindowPageBase ES_Refresh()
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = (ESGlobalEditorDefaultConfi.Instance?.Path_ResourceParent ?? "Assets") + "/Textures";

            return base.ES_Refresh();

        }


        public void ProcessSelectedTextures()
        {
            var texturePaths = CollectTextureImporterPaths().ToArray();
            if (texturePaths.Length == 0)
            {
                EditorUtility.DisplayDialog("没有可处理的纹理", "请先在 Project 窗口选中 Texture2D，或设置一个 Assets 下的纹理文件夹。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(texturePaths.Select(Path.GetFileName), 10);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认处理选中纹理",
                texturePaths.Length,
                $"修改 {texturePaths.Length} 个纹理的 TextureImporter 设置，并重新导入资源。\n\n{preview}",
                "会改变项目资产导入设置，影响所有引用这些纹理的地方。建议先确认版本控制或备份。"))
                return;

            ProcessTextures(texturePaths);
        }

        private List<string> CollectTextureImporterPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var texture in Selection.objects.OfType<Texture2D>())
            {
                string path = SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
                if (SimpleToolsSafetyUtility.IsAssetPath(path))
                    paths.Add(path);
            }

            foreach (var path in RefreshTextureFolderCache(false))
                paths.Add(path);

            return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void GenerateTexturesFromSprites()
        {
            var selectedSprites = Selection.objects.OfType<Sprite>().ToArray();
            if (selectedSprites.Length == 0)
            {
                EditorUtility.DisplayDialog("需要选择 Sprite", "先在 Project 窗口选中一个或多个 Sprite。", "知道了");
                return;
            }

            outputFolder = SimpleToolsSafetyUtility.NormalizeAssetPath(outputFolder);
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(outputFolder, out var folderError))
            {
                EditorUtility.DisplayDialog("输出路径不可用", folderError, "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(selectedSprites.Select(sprite => sprite.name), 10);
            if (!SimpleToolsPanelUtility.ConfirmHeavyOperation(
                "确认从 Sprite 生成纹理",
                selectedSprites.Length,
                $"从 {selectedSprites.Length} 个 Sprite 生成纹理文件。\n\n输出目录：{outputFolder}\n格式：{outputFormat}\n同名处理：{(autoRenameOnConflict ? "自动改名" : "跳过")}\n\n{preview}",
                "会在项目中写入新文件并导入为 Unity 资产。源纹理不可读的 Sprite 会被跳过。"))
                return;

            int generatedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;
            var generatedPaths = new List<string>();
            var messages = new List<string>();
            Undo.SetCurrentGroupName("Generate Textures from Sprites");
            int undoGroup = Undo.GetCurrentGroup();

            SimpleToolsSafetyUtility.RunAssetEditing(() =>
            {
                foreach (var sprite in selectedSprites)
                {
                    try
                    {
                        var texture = sprite.texture;
                        if (texture == null)
                        {
                            skippedCount++;
                            messages.Add($"{sprite.name}: 找不到源纹理，已跳过。");
                            continue;
                        }

                        if (!texture.isReadable)
                        {
                            skippedCount++;
                            messages.Add($"{sprite.name}: 源纹理不可读，请先打开 Read/Write Enabled。");
                            continue;
                        }

                        var rect = sprite.rect;
                        var newTexture = new Texture2D((int)rect.width, (int)rect.height);
                        try
                        {
                            var pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                            newTexture.SetPixels(pixels);
                            newTexture.Apply();

                            byte[] bytes;
                            string extension;
                            switch (outputFormat)
                            {
                                case TextureFormat.PNG:
                                    bytes = newTexture.EncodeToPNG();
                                    extension = "png";
                                    break;
                                case TextureFormat.JPG:
                                    bytes = newTexture.EncodeToJPG(jpgQuality);
                                    extension = "jpg";
                                    break;
                                case TextureFormat.TGA:
                                    bytes = newTexture.EncodeToTGA();
                                    extension = "tga";
                                    break;
                                case TextureFormat.EXR:
                                    bytes = newTexture.EncodeToEXR();
                                    extension = "exr";
                                    break;
                                default:
                                    bytes = newTexture.EncodeToPNG();
                                    extension = "png";
                                    break;
                            }

                            var assetPath = Path.Combine(outputFolder, SanitizeFileName(sprite.name) + "." + extension).Replace("\\", "/");
                            if (File.Exists(SimpleToolsSafetyUtility.AssetPathToFullPath(assetPath)))
                            {
                                if (!autoRenameOnConflict)
                                {
                                    skippedCount++;
                                    messages.Add($"{assetPath}: 已存在，已跳过。");
                                    continue;
                                }
                                assetPath = SimpleToolsSafetyUtility.GetUniqueAssetPath(assetPath);
                            }

                            File.WriteAllBytes(SimpleToolsSafetyUtility.AssetPathToFullPath(assetPath), bytes);
                            AssetDatabase.ImportAsset(assetPath);

                            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                            if (importer != null)
                            {
                                Undo.RecordObject(importer, "Set Texture Importer Settings");
                                importer.isReadable = generatedReadable;
                                importer.SaveAndReimport();
                            }

                            generatedPaths.Add(assetPath);
                            generatedCount++;
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(newTexture);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        messages.Add($"{sprite.name}: {ex.Message}");
                    }
                }
            });

            Undo.CollapseUndoOperations(undoGroup);

            // 选择生成的结果
            var generatedObjects = generatedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).ToArray();
            if (generatedObjects.Length > 0)
            {
                Selection.objects = generatedObjects;
                EditorGUIUtility.PingObject(generatedObjects[0]);
            }

            string detail = messages.Count > 0 ? "\n\n详情：\n" + SimpleToolsSafetyUtility.JoinPreview(messages, 8) : string.Empty;
            lastResultSummary = $"Sprite 生成完成: 生成 {generatedCount} 个 | 跳过 {skippedCount} 个 | 失败 {failedCount} 个 | 输出 {outputFolder}";
            lastResultDetail = "生成文件:\n" + SimpleToolsSafetyUtility.JoinPreview(generatedPaths, 12);
            if (messages.Count > 0)
                lastResultDetail += "\n\n详情:\n" + SimpleToolsSafetyUtility.JoinPreview(messages, 8);

            EditorUtility.DisplayDialog("Sprite 纹理生成完成", $"生成 {generatedCount} 个，跳过 {skippedCount} 个，失败 {failedCount} 个。\n输出：{outputFolder}{detail}", "完成");
        }


        private void ProcessTextures(string[] texturePaths)
        {
            // if (!AssetDatabase.IsValidFolder(outputFolder))
            // {
            //     Directory.CreateDirectory(outputFolder.Replace("Assets/", ""));
            //     AssetDatabase.Refresh();
            // }

            int processedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;
            var messages = new List<string>();
            Undo.SetCurrentGroupName("Process Textures");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                SimpleToolsSafetyUtility.RunAssetEditing(() =>
                {
                    for (int i = 0; i < texturePaths.Length; i++)
                    {
                        var texturePath = SimpleToolsSafetyUtility.NormalizeAssetPath(texturePaths[i]);
                        EditorUtility.DisplayProgressBar("处理纹理", $"处理: {Path.GetFileName(texturePath)}", (float)i / texturePaths.Length);

                        try
                        {
                            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                            if (importer == null)
                            {
                                skippedCount++;
                                messages.Add($"{texturePath}: 不是可处理的纹理导入器。");
                                continue;
                            }

                            Undo.RecordObject(importer, "Modify Texture Importer Settings");
                            importer.textureType = TextureImporterType.Sprite;
                            importer.spriteImportMode = spriteMode;
                            importer.spritePixelsPerUnit = pixelsPerUnit;
                            importer.filterMode = filterMode;
                            importer.maxTextureSize = maxTextureSize;
                            importer.isReadable = isReadable;
                            importer.mipmapEnabled = generateMipMaps;

                            var settings = importer.GetDefaultPlatformTextureSettings();
                            settings.compressionQuality = compressionQuality;
                            importer.SetPlatformTextureSettings(settings);

                            importer.SaveAndReimport();
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            messages.Add($"{texturePath}: {ex.Message}");
                        }
                    }
                });
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);

            string detail = messages.Count > 0 ? "\n\n详情：\n" + SimpleToolsSafetyUtility.JoinPreview(messages, 8) : string.Empty;
            lastResultSummary = $"纹理导入设置完成: 处理 {processedCount} 个 | 跳过 {skippedCount} 个 | 失败 {failedCount} 个";
            lastResultDetail = "处理文件:\n" + SimpleToolsSafetyUtility.JoinPreview(texturePaths, 12);
            if (messages.Count > 0)
                lastResultDetail += "\n\n详情:\n" + SimpleToolsSafetyUtility.JoinPreview(messages, 8);

            EditorUtility.DisplayDialog("纹理处理完成", $"处理 {processedCount} 个，跳过 {skippedCount} 个，失败 {failedCount} 个。{detail}", "完成");
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "SpriteTexture";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalidChar, '_');

            fileName = fileName.Trim();
            return string.IsNullOrEmpty(fileName) ? "SpriteTexture" : fileName;
        }

        public void ResetToDefaults()
        {
            spriteMode = SpriteImportMode.Single;
            pixelsPerUnit = 100f;
            filterMode = FilterMode.Point;
            compressionQuality = 50;
            maxTextureSize = 2048;
            isReadable = false;
            generateMipMaps = false;
            generatedReadable = false;
            outputFormat = TextureFormat.PNG;
            jpgQuality = 75;
            autoRenameOnConflict = true;
        }
    }
    #endregion
}
