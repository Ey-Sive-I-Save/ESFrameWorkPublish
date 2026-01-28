using ES;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ResLibrary
    /// 
    /// ScriptableObject 形式的资源库：
    /// - 继承 SoLibrary&lt;ResBook&gt;，一份 Library 可以包含多本 ResBook；
    /// - 通过 Inspector 决定是否参与构建、是否走远程下载；
    /// - 是上层“资源分组 / 逻辑分类”的载体，真正的 AB / 路径信息由 ResPage 决定。
    /// </summary>
    public class ResLibrary : LibrarySoBase<ResBook>
    {
        protected override IEnumerable<ResBook> GetDefaultBooks()
        {
            return _defaultBooks();
        }
         
        protected override void InitializeDefaultBooks()
        {
            base.InitializeDefaultBooks();
            Debug.Log("[ResLibrary.InitializeDefaultBooks] 初始化默认Books");
            // 为每个默认Book设置合适的图标和编辑权限
              if (DefaultPrefabBook != null)
            {
                DefaultPrefabBook._icon = EditorIconType.Prefab;
                DefaultPrefabBook.WritableDefaultMessageOnEditor = false;
                DefaultPrefabBook.PreferredAssetCategory = ESAssetCategory.Prefab;
            }
            if (DefaultSOBook != null)
            {
                DefaultSOBook._icon = EditorIconType.ScriptableObject;
                DefaultSOBook.WritableDefaultMessageOnEditor = false;
                DefaultSOBook.PreferredAssetCategory = ESAssetCategory.Script;
            }
            
            if (DefaultSoundBook != null)
            {
                DefaultSoundBook._icon = EditorIconType.AudioClip;
                DefaultSoundBook.WritableDefaultMessageOnEditor = false;
                DefaultSoundBook.PreferredAssetCategory = ESAssetCategory.Audio;
            }
            
            if (DefaultTextureBook != null)
            {
                DefaultTextureBook._icon = EditorIconType.Texture;
                DefaultTextureBook.WritableDefaultMessageOnEditor = false;
                DefaultTextureBook.PreferredAssetCategory = ESAssetCategory.Texture;
            }
            
            if (DefaultAnimationBook != null)
            {
                DefaultAnimationBook._icon = EditorIconType.Animation;
                DefaultAnimationBook.WritableDefaultMessageOnEditor = false;
                DefaultAnimationBook.PreferredAssetCategory = ESAssetCategory.Animation;
            }
            
            if (DefaultMaterialBook != null)
            {
                DefaultMaterialBook._icon = EditorIconType.Material;
                DefaultMaterialBook.WritableDefaultMessageOnEditor = false;
                DefaultMaterialBook.PreferredAssetCategory = ESAssetCategory.Material;
            }
            
            if (DefaultSceneBook_ != null)
            {
                DefaultSceneBook_._icon = EditorIconType.Scene;
                DefaultSceneBook_.WritableDefaultMessageOnEditor = false;
                DefaultSceneBook_.PreferredAssetCategory = ESAssetCategory.Scene;
            }
            
            if (DefaultSpriteBook != null)
            {
                DefaultSpriteBook._icon = EditorIconType.Sprite;
                DefaultSpriteBook.WritableDefaultMessageOnEditor = false;
                DefaultSpriteBook.PreferredAssetCategory = ESAssetCategory.Texture;
            }
            
            if (DefaultFontBook != null)
            {
                DefaultFontBook._icon = EditorIconType.Font;
                DefaultFontBook.WritableDefaultMessageOnEditor = false;
                DefaultFontBook.PreferredAssetCategory = ESAssetCategory.Font;
            }
            
            if (DefaultModelBook != null)
            {
                DefaultModelBook._icon = EditorIconType.Model;
                DefaultModelBook.WritableDefaultMessageOnEditor = false;
                DefaultModelBook.PreferredAssetCategory = ESAssetCategory.Model;
            }
            
            if (DefaultShaderBook != null)
            {
                DefaultShaderBook._icon = EditorIconType.Shader;
                DefaultShaderBook.WritableDefaultMessageOnEditor = false;
                DefaultShaderBook.PreferredAssetCategory = ESAssetCategory.Shader;
            }
            
            if (DefaultTextBook != null)
            {
                DefaultTextBook._icon = EditorIconType.TextAsset;
                DefaultTextBook.WritableDefaultMessageOnEditor = false;
                DefaultTextBook.PreferredAssetCategory = ESAssetCategory.Other;
            }
            
            if (DefaultDLLBook != null)
            {
                DefaultDLLBook._icon = EditorIconType.File;
                DefaultDLLBook.WritableDefaultMessageOnEditor = false;
                DefaultDLLBook.PreferredAssetCategory = ESAssetCategory.Script;
            }
            
            if (DefaultOtherBook != null)
            {
                DefaultOtherBook._icon = EditorIconType.Folder;
                DefaultOtherBook.WritableDefaultMessageOnEditor = false;
                DefaultOtherBook.PreferredAssetCategory = ESAssetCategory.Other;
            }
        }
        private  IEnumerable<ResBook> _defaultBooks()
        {
            if( DefaultPrefabBook != null)
            {
               
                yield return DefaultPrefabBook;
            }
            if (DefaultSOBook != null)
            {
               
                yield return DefaultSOBook;
            }

            if (DefaultSoundBook != null)
            {
                yield return DefaultSoundBook;
            }

            if (DefaultTextureBook != null)
            {
                yield return DefaultTextureBook;
            }

            if (DefaultAnimationBook != null)
            {
                yield return DefaultAnimationBook;
            }

            if (DefaultMaterialBook != null)
            {
                yield return DefaultMaterialBook;
            }

            if (DefaultSceneBook_ != null)
            {
                yield return DefaultSceneBook_;
            }

            if (DefaultSpriteBook != null)
            {
                yield return DefaultSpriteBook;
            }

            if (DefaultFontBook != null)
            {
                yield return DefaultFontBook;
            }

            if (DefaultModelBook != null)
            {
                yield return DefaultModelBook;
            }

            if (DefaultShaderBook != null)
            {
                yield return DefaultShaderBook;
            }

            if (DefaultTextBook != null)
            {
                yield return DefaultTextBook;
            }

            if (DefaultDLLBook != null)
            {
                yield return DefaultDLLBook;
            }

            if (DefaultOtherBook != null)
            {
                yield return DefaultOtherBook;
            }
        }
        [ShowInInspector, NonSerialized]
        private bool ShowDefaultPrefabBook = false;
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultPrefabBook = new ResBook() { Name = "默认 默认预制体Book", Desc = "默认预制体资源所在的Book" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultSOBook = new ResBook() { Name = "默认 脚本对象SO Book", Desc = "用于存储游戏逻辑和数据的脚本化对象资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultSoundBook = new ResBook() { Name = "默认 音效 Book", Desc = "游戏中的声音效果资源，如音效文件" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultTextureBook = new ResBook() { Name = "默认 纹理 Book", Desc = "图像纹理资源，用于材质和UI" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultAnimationBook = new ResBook() { Name = "默认 动画 Book", Desc = "角色或对象的动画资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultMaterialBook = new ResBook() { Name = "默认 材质 Book", Desc = "定义物体外观的材质资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultSceneBook_ = new ResBook() { Name = "默认 场景 Book", Desc = "游戏场景文件资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultSpriteBook = new ResBook() { Name = "默认 精灵 Book", Desc = "2D游戏中的精灵图像资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultFontBook = new ResBook() { Name = "默认 字体 Book", Desc = "文本显示的字体资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultModelBook = new ResBook() { Name = "默认 模型 Book", Desc = "3D模型资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultShaderBook = new ResBook() { Name = "默认 着色器 Book", Desc = "用于渲染的着色器资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultTextBook = new ResBook() { Name = "默认 文本 Book", Desc = "文本文件资源，如配置文件或对话" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultDLLBook = new ResBook() { Name = "默认 DLL字节 Book", Desc = "动态链接库的字节数据资源" };
        [ShowIf("ShowDefaultPrefabBook")]
        public ResBook DefaultOtherBook = new ResBook() { Name = "默认 其他 Book", Desc = "其他类型的资源" };

        [LabelText("参与构建")]
        public bool ContainsBuild = true;



        [ESBoolOption("通过远程下载", "是本体库")]
        public bool IsNet = true;
        public override void OnEditorApply()
        {
            base.OnEditorApply();
            Refresh();
        }
        public override void Refresh()
        {
            if (LibFolderName.IsNullOrWhitespace())
            {
                LibFolderName = IESLibrary.DefaultLibFolderName;
            }
            //验证
            ESResMaster.TrySetResLibFolderName(this, LibFolderName, 0);
            base.Refresh();
        }

    }

}

