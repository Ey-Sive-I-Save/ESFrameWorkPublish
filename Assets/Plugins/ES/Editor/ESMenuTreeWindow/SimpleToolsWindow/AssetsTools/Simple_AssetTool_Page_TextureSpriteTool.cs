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
    #region 纹理精灵生成工具
    [Serializable]
    public class Page_TextureSpriteTool : ESWindowPageBase
    {
        [Title("纹理精灵生成工具", "批量处理纹理并生成Sprite", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择纹理文件，\n配置Sprite设置，\n点击处理按钮批量生成";

        [LabelText("操作纹理文件夹"), FolderPath, Space(5)]
        public string textureFolder;

       // [LabelText("输出文件夹"), FolderPath, Space(5)]
      //  public string outputFolder;

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
        public override ESWindowPageBase ES_Refresh()
        {
            //outputFolder = ESGlobalEditorDefaultConfi.Instance.Path_ResourceParent + "/Sprites";

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

        [Button("处理文件夹中的纹理", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void ProcessFolderTextures()
        {
            if (!AssetDatabase.IsValidFolder(textureFolder))
            {
                EditorUtility.DisplayDialog("错误", "请选择有效的纹理文件夹！", "确定");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
            var texturePaths = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToArray();

            ProcessTextures(texturePaths);
        }


        private void ProcessTextures(string[] texturePaths)
        {
            // if (!AssetDatabase.IsValidFolder(outputFolder))
            // {
            //     Directory.CreateDirectory(outputFolder.Replace("Assets/", ""));
            //     AssetDatabase.Refresh();
            // }

            int processedCount = 0;
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
        }
    }
    #endregion
}