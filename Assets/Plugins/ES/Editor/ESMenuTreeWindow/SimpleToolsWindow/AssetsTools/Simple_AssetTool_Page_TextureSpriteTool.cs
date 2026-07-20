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

        [LabelText("操作纹理文件夹"), FolderPath, Space(5)]
        public string textureFolder;

        [LabelText("输出文件夹"), FolderPath, Space(5)]
        public string outputFolder;

        [LabelText("Sprite模式"), Space(5)]
        public SpriteImportMode spriteMode = SpriteImportMode.Single;

        [LabelText("像素每单位"), Space(5)]
        public float pixelsPerUnit = 100f;

        [LabelText("过滤模式"), Space(5)]
        public FilterMode filterMode = FilterMode.Point;

        [LabelText("压缩质量 (0: 最低质量, 100: 最高质量)"), Range(0, 100), Space(5)]
        public int compressionQuality = 50;

        [LabelText("最大纹理尺寸"), Space(5)]
        public int maxTextureSize = 2048;

        [LabelText("生成可读写纹理"), Space(5)]
        public bool isReadable = false;

        [LabelText("生成MipMaps"), Space(5)]
        public bool generateMipMaps = false;

        [LabelText("生成的可读写纹理"), Space(5)]
        public bool generatedReadable = false;

        [LabelText("输出格式"), Space(5)]
        public TextureFormat outputFormat = TextureFormat.PNG;

        [LabelText("JPG质量"), Range(0, 100), Space(5)]
        [ShowIf("@outputFormat == TextureFormat.JPG")]
        public int jpgQuality = 75;

        [LabelText("同名文件自动改名"), Space(5)]
        [InfoBox("开启后不会覆盖已有文件，会自动生成 _1、_2 这类安全文件名。关闭时遇到同名文件会跳过。", InfoMessageType.Info)]
        public bool autoRenameOnConflict = true;

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近贴图处理结果", lastResultSummary, lastResultDetail);
        }

        public override ESWindowPageBase ES_Refresh()
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = (ESGlobalEditorDefaultConfi.Instance?.Path_ResourceParent ?? "Assets") + "/Textures";

            return base.ES_Refresh();

        }


        [Button("处理选中纹理", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void ProcessSelectedTextures()
        {
            var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
            if (selectedTextures.Length == 0)
            {
                EditorUtility.DisplayDialog("需要选择纹理", "先在 Project 窗口选中一个或多个 Texture2D。", "知道了");
                return;
            }

            var texturePaths = selectedTextures
                .Select(t => SimpleToolsSafetyUtility.NormalizeAssetPath(AssetDatabase.GetAssetPath(t)))
                .Where(SimpleToolsSafetyUtility.IsAssetPath)
                .ToArray();
            if (texturePaths.Length == 0)
            {
                EditorUtility.DisplayDialog("没有可处理的项目纹理", "选中的 Texture2D 不在 Assets 目录下，不能修改导入设置。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(texturePaths.Select(Path.GetFileName), 10);
            if (!EditorUtility.DisplayDialog("确认处理选中纹理",
                $"将修改 {texturePaths.Length} 个纹理的 TextureImporter 设置，并重新导入资源。\n\n{preview}\n\n这会改变项目资产导入设置，建议确认已提交或备份。继续吗？",
                "开始处理", "取消"))
                return;

            ProcessTextures(texturePaths);
        }

        [Button("从选中 Sprite 生成纹理", ButtonHeight = 34), GUIColor(0.75f, 0.58f, 0.25f)]
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
            if (!EditorUtility.DisplayDialog("确认从Sprite生成纹理",
                $"将从 {selectedSprites.Length} 个 Sprite 生成纹理文件。\n\n输出目录：{outputFolder}\n格式：{outputFormat}\n同名处理：{(autoRenameOnConflict ? "自动改名" : "跳过")}\n\n{preview}\n\n会写入新文件并导入为 Unity 资产。继续吗？",
                "开始生成", "取消"))
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

        [Button("重置为默认设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
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
