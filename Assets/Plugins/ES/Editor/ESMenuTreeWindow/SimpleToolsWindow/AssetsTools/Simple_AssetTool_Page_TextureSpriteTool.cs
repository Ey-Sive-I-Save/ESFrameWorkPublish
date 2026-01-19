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

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择纹理文件，\n配置Sprite设置，\n点击处理按钮批量生成";

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
        public override ESWindowPageBase ES_Refresh()
        {
            outputFolder = ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent + "/Textures";

            return base.ES_Refresh();

        }


        [Button("处理选中纹理", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ProcessSelectedTextures()
        {
            var selectedTextures = Selection.objects.OfType<Texture2D>().ToArray();
            if (selectedTextures.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择纹理文件！", "确定");
                return;
            }

            ProcessTextures(selectedTextures.Select(t => AssetDatabase.GetAssetPath(t)).ToArray());
        }

        [Button("从选中Sprite生成纹理", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        public void GenerateTexturesFromSprites()
        {
            var selectedSprites = Selection.objects.OfType<Sprite>().ToArray();
            if (selectedSprites.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择Sprite！", "确定");
                return;
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                Directory.CreateDirectory(outputFolder.Replace("Assets/", Application.dataPath + "/"));
                AssetDatabase.Refresh();
            }

            int generatedCount = 0;
            var generatedPaths = new List<string>();
            Undo.SetCurrentGroupName("Generate Textures from Sprites");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var sprite in selectedSprites)
                {
                    var texture = sprite.texture;
                    if (!texture.isReadable)
                    {
                        EditorUtility.DisplayDialog("错误", $"纹理 '{texture.name}' 不可读。请在纹理导入设置中启用 'Read/Write Enabled'。", "确定");
                        continue;
                    }

                    var rect = sprite.rect;
                    var newTexture = new Texture2D((int)rect.width, (int)rect.height);
                    var pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                    newTexture.SetPixels(pixels);
                    newTexture.Apply();

                    // 根据格式编码
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

                    var assetPath = Path.Combine(outputFolder, sprite.name + "." + extension).Replace("\\", "/");
                    File.WriteAllBytes(assetPath.Replace("Assets/", Application.dataPath + "/"), bytes);
                    AssetDatabase.ImportAsset(assetPath);

                    // 设置导入选项
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
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Undo.CollapseUndoOperations(undoGroup);

            // 选择生成的结果
            var generatedObjects = generatedPaths.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).ToArray();
            if (generatedObjects.Length > 0)
            {
                Selection.objects = generatedObjects;
                EditorGUIUtility.PingObject(generatedObjects[0]);
            }

            EditorUtility.DisplayDialog("成功", $"成功生成 {generatedCount} 个纹理文件到 {outputFolder}！", "确定");
        }


        private void ProcessTextures(string[] texturePaths)
        {
            // if (!AssetDatabase.IsValidFolder(outputFolder))
            // {
            //     Directory.CreateDirectory(outputFolder.Replace("Assets/", ""));
            //     AssetDatabase.Refresh();
            // }

            int processedCount = 0;
            Undo.SetCurrentGroupName("Process Textures");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < texturePaths.Length; i++)
                {
                    var texturePath = texturePaths[i];
                    EditorUtility.DisplayProgressBar("处理纹理", $"处理: {Path.GetFileName(texturePath)}", (float)i / texturePaths.Length);

                    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (importer != null)
                    {
                        Undo.RecordObject(importer, "Modify Texture Importer Settings");

                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = spriteMode;
                        importer.spritePixelsPerUnit = pixelsPerUnit;
                        importer.filterMode = filterMode;
                        importer.maxTextureSize = maxTextureSize;
                        importer.isReadable = isReadable;
                        importer.mipmapEnabled = generateMipMaps;

                        // 设置压缩设置
                        var settings = importer.GetDefaultPlatformTextureSettings();
                        settings.compressionQuality = compressionQuality;
                        importer.SetPlatformTextureSettings(settings);

                        importer.SaveAndReimport();
                        processedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog("成功", $"成功处理 {processedCount} 个纹理文件！", "确定");
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
        }
    }
    #endregion
}