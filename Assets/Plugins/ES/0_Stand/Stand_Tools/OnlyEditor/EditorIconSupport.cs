using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    /// <summary>
    /// Unity编辑器常用图标枚举
    /// </summary>
    public enum EditorIconType
    {
        // 文件夹和容器
        Folder,
        FolderOpened,
        FolderEmpty,
        FolderFavorite,
        
        // 基础资源
        File,
        TextAsset,
        ScriptableObject,
        Prefab,
        PrefabVariant,
        PrefabModel,
        Scene,
        
        // 脚本和代码
        CSharpScript,
        JavaScriptAsset,
        BooScript,
        ShaderScript,
        
        // 材质和渲染
        Material,
        Texture,
        Texture2D,
        RenderTexture,
        Cubemap,
        Sprite,
        Shader,
        ComputeShader,
        
        // 模型和动画
        Model,
        Mesh,
        Animation,
        AnimationClip,
        AnimatorController,
        Avatar,
        
        // 音视频
        AudioClip,
        AudioMixer,
        VideoClip,
        
        // UI和字体
        Font,
        GUISkin,
        Canvas,
        
        // 物理
        PhysicMaterial,
        PhysicsMaterial2D,
        
        // 灯光和相机
        Light,
        Camera,
        
        // 地形和场景
        Terrain,
        TerrainLayer,
        
        // 工具栏图标
        Add,
        Remove,
        Plus,
        Minus,
        Refresh,
        Search,
        Settings,
        SettingsIcon,
        
        // 状态图标
        Warning,
        Error,
        Info,
        Success,
        Help,
        
        // 导航图标
        Previous,
        Next,
        Up,
        Down,
        Left,
        Right,
        
        // 编辑器功能
        Eye,
        EyeClosed,
        Favorite,
        Label,
        Lock,
        Unlock,
        Star,
        Tag,
        
        // 包管理
        Package,
        PackageManager,
        
        // 构建和平台
        BuildSettings,
        Android,
        IOS,
        Windows,
        Mac,
        Linux,
        WebGL,
        
        // 其他常用
        GameObject,
        Transform,
        Component,
        Project,
        Hierarchy,
        Console,
        Inspector,
        SceneView,
        GameView,
        Animation_Window,
        Profiler,
    }

    /// <summary>
    /// 编辑器图标支持工具
    /// 提供在编辑器和运行时都能调用的图标获取方法
    /// 运行时返回null，编辑器时返回实际图标
    /// </summary>
    public static class EditorIconSupport
    {
        /// <summary>
        /// 将图标枚举转换为Unity内部图标名称
        /// </summary>
        public static string GetIconName(EditorIconType iconType)
        {
           
            switch (iconType)
            {
                
                // 文件夹和容器
                case EditorIconType.Folder: return "d_Folder Icon";
                case EditorIconType.FolderOpened: return "d_FolderOpened Icon";
                case EditorIconType.FolderEmpty: return "d_FolderEmpty Icon";
                case EditorIconType.FolderFavorite: return "d_Folder On Icon";
                
                // 基础资源
                case EditorIconType.File: return "d_TextAsset Icon";
                case EditorIconType.TextAsset: return "d_TextAsset Icon";
                case EditorIconType.ScriptableObject: return "d_ScriptableObject Icon";
                case EditorIconType.Prefab: return "d_Prefab Icon";
                case EditorIconType.PrefabVariant: return "d_PrefabVariant Icon";
                case EditorIconType.PrefabModel: return "d_PrefabModel Icon";
                case EditorIconType.Scene: return "d_SceneAsset Icon";
                
                // 脚本和代码
                case EditorIconType.CSharpScript: return "d_cs Script Icon";
                case EditorIconType.JavaScriptAsset: return "d_Js Script Icon";
                case EditorIconType.BooScript: return "d_Boo Script Icon";
                case EditorIconType.ShaderScript: return "d_Shader Icon";
                
                // 材质和渲染
                case EditorIconType.Material: return "d_Material Icon";
                case EditorIconType.Texture: return "d_Texture Icon";
                case EditorIconType.Texture2D: return "d_Texture2D Icon";
                case EditorIconType.RenderTexture: return "d_RenderTexture Icon";
                case EditorIconType.Cubemap: return "d_Cubemap Icon";
                case EditorIconType.Sprite: return "d_Sprite Icon";
                case EditorIconType.Shader: return "d_Shader Icon";
                case EditorIconType.ComputeShader: return "d_ComputeShader Icon";
                
                // 模型和动画
                case EditorIconType.Model: return "d_Mesh Icon";
                case EditorIconType.Mesh: return "d_Mesh Icon";
                case EditorIconType.Animation: return "d_Animation Icon";
                case EditorIconType.AnimationClip: return "d_AnimationClip Icon";
                case EditorIconType.AnimatorController: return "d_AnimatorController Icon";
                case EditorIconType.Avatar: return "d_Avatar Icon";
                
                // 音视频
                case EditorIconType.AudioClip: return "d_AudioClip Icon";
                case EditorIconType.AudioMixer: return "d_AudioMixerController Icon";
                case EditorIconType.VideoClip: return "d_VideoClip Icon";
                
                // UI和字体
                case EditorIconType.Font: return "d_Font Icon";
                case EditorIconType.GUISkin: return "d_GUISkin Icon";
                case EditorIconType.Canvas: return "d_Canvas Icon";
                
                // 物理
                case EditorIconType.PhysicMaterial: return "d_PhysicMaterial Icon";
                case EditorIconType.PhysicsMaterial2D: return "d_PhysicsMaterial2D Icon";
                
                // 灯光和相机
                case EditorIconType.Light: return "d_Light Icon";
                case EditorIconType.Camera: return "d_Camera Icon";
                
                // 地形和场景
                case EditorIconType.Terrain: return "d_Terrain Icon";
                case EditorIconType.TerrainLayer: return "d_TerrainLayer Icon";
                
                // 工具栏图标
                case EditorIconType.Add: return "d_Toolbar Plus";
                case EditorIconType.Remove: return "d_Toolbar Minus";
                case EditorIconType.Plus: return "d_Toolbar Plus More";
                case EditorIconType.Minus: return "d_Toolbar Minus";
                case EditorIconType.Refresh: return "d_Refresh";
                case EditorIconType.Search: return "d_Search Icon";
                case EditorIconType.Settings: return "d_Settings";
                case EditorIconType.SettingsIcon: return "d_Settings Icon";
                
                // 状态图标
                case EditorIconType.Warning: return "d_console.warnicon";
                case EditorIconType.Error: return "d_console.erroricon";
                case EditorIconType.Info: return "d_console.infoicon";
                case EditorIconType.Success: return "d_P4_CheckOutLocal";
                case EditorIconType.Help: return "d__Help";
                
                // 导航图标
                case EditorIconType.Previous: return "d_back";
                case EditorIconType.Next: return "d_forward";
                case EditorIconType.Up: return "d_scrollup";
                case EditorIconType.Down: return "d_scrolldown";
                case EditorIconType.Left: return "d_scrollleft";
                case EditorIconType.Right: return "d_scrollright";
                
                // 编辑器功能
                case EditorIconType.Eye: return "d_scenevis_visible_hover";
                case EditorIconType.EyeClosed: return "d_scenevis_hidden_hover";
                case EditorIconType.Favorite: return "d_Favorite";
                case EditorIconType.Label: return "d_FilterByLabel";
                case EditorIconType.Lock: return "d_IN LockButton on";
                case EditorIconType.Unlock: return "d_IN LockButton";
                case EditorIconType.Star: return "d_Favorite";
                case EditorIconType.Tag: return "d_FilterByType";
                
                // 包管理
                case EditorIconType.Package: return "d_Package Manager";
                case EditorIconType.PackageManager: return "d_Package Manager";
                
                // 构建和平台
                case EditorIconType.BuildSettings: return "d_BuildSettings";
                case EditorIconType.Android: return "d_BuildSettings.Android";
                case EditorIconType.IOS: return "d_BuildSettings.iPhone";
                case EditorIconType.Windows: return "d_BuildSettings.Standalone";
                case EditorIconType.Mac: return "d_BuildSettings.Standalone";
                case EditorIconType.Linux: return "d_BuildSettings.Standalone";
                case EditorIconType.WebGL: return "d_BuildSettings.WebGL";
                
                // 其他常用
                case EditorIconType.GameObject: return "d_GameObject Icon";
                case EditorIconType.Transform: return "d_Transform Icon";
                case EditorIconType.Component: return "d_cs Script Icon";
                case EditorIconType.Project: return "d_Project";
                case EditorIconType.Hierarchy: return "d_UnityEditor.SceneHierarchyWindow";
                case EditorIconType.Console: return "d_UnityEditor.ConsoleWindow";
                case EditorIconType.Inspector: return "d_UnityEditor.InspectorWindow";
                case EditorIconType.SceneView: return "d_UnityEditor.SceneView";
                case EditorIconType.GameView: return "d_UnityEditor.GameView";
                case EditorIconType.Animation_Window: return "d_UnityEditor.AnimationWindow";
                case EditorIconType.Profiler: return "d_Profiler";
                
                default: return "d_Folder Icon";
            }
        }

        /// <summary>
        /// 获取Unity内置图标（枚举方式）
        /// </summary>
        public static Texture2D GetIcon(EditorIconType iconType)
        {
            return GetIcon(GetIconName(iconType));
        }

        /// <summary>
        /// 获取Unity内置图标（字符串方式）
        /// </summary>
        public static Texture2D GetIcon(string iconName)
        {
#if UNITY_EDITOR
            var content = EditorGUIUtility.IconContent(iconName);
            return content?.image as Texture2D;
#else
            return null;
#endif
        }

        /// <summary>
        /// 创建带图标的GUIContent（枚举方式）
        /// </summary>
        public static GUIContent CreateContent(string text, EditorIconType iconType)
        {
            return CreateContent(text, GetIconName(iconType));
        }

        /// <summary>
        /// 创建带图标和提示的GUIContent（枚举方式）
        /// </summary>
        public static GUIContent CreateContent(string text, EditorIconType iconType, string tooltip)
        {
            return CreateContent(text, GetIconName(iconType), tooltip);
        }

        /// <summary>
        /// 创建带图标的GUIContent（字符串方式）
        /// </summary>
        public static GUIContent CreateContent(string text, string iconName)
        {
#if UNITY_EDITOR
            var icon = GetIcon(iconName);
            return new GUIContent(text, icon);
#else
            return new GUIContent(text);
#endif
        }

        /// <summary>
        /// 创建带图标和提示的GUIContent（字符串方式）
        /// </summary>
        public static GUIContent CreateContent(string text, string iconName, string tooltip)
        {
#if UNITY_EDITOR
            var icon = GetIcon(iconName);
            return new GUIContent(text, icon, tooltip);
#else
            return new GUIContent(text, tooltip);
#endif
        }

        /// <summary>
        /// 常用图标名称（向后兼容保留）
        /// </summary>
        public static class CommonIcons
        {
            public const string Folder = "d_Folder Icon";
            public const string FolderOpened = "d_FolderOpened Icon";
            public const string File = "d_TextAsset Icon";
            public const string ScriptableObject = "d_ScriptableObject Icon";
            public const string Prefab = "d_Prefab Icon";
            public const string Scene = "d_SceneAsset Icon";
            public const string Material = "d_Material Icon";
            public const string Texture = "d_Texture Icon";
            public const string Sprite = "d_Sprite Icon";
            public const string Animation = "d_AnimationClip Icon";
            public const string Audio = "d_AudioClip Icon";
            public const string Video = "d_VideoClip Icon";
            public const string Font = "d_Font Icon";
            public const string Shader = "d_Shader Icon";
            public const string Model = "d_Mesh Icon";
            public const string Script = "d_cs Script Icon";
            public const string Book = "d_Favorite Icon";
            public const string Settings = "d_Settings Icon";
            public const string Add = "d_Toolbar Plus";
            public const string Remove = "d_Toolbar Minus";
            public const string Refresh = "d_Refresh";
            public const string Search = "d_Search Icon";
            public const string Warning = "d_console.warnicon";
            public const string Error = "d_console.erroricon";
            public const string Info = "d_console.infoicon";
        }
    }
}
