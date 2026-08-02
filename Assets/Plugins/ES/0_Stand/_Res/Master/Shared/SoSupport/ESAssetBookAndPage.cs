using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [Serializable]
    public class ESAssetBook : BookBase<ESAssetPage>
    {
        [NonSerialized]
        public EditorIconType _icon = EditorIconType.Folder;

        public override EditorIconType Icon { get => _icon; }

        public override ESAssetPage CreateNewPage(UnityEngine.Object uo)
        {
            return ESAssetPage.Create(uo);
        }
    }

    [Serializable, Obsolete("Use ESAssetBook.")]
    public class ResBook : ESAssetBook
    {
    }
    [Serializable]
    public class ESAssetPage : PageBase, IString, IAssetPage
    {
        [LabelText("分AB包命名方式")]
        public ABNamedOption namedOption;
        
        [LabelText("绑定资源"),AssetsOnly]
        public UnityEngine.Object OB;

        [LabelText("资产引用类型")]
        public ESAssetReferKind Kind = ESAssetReferKind.None;

        [LabelText("枚举键")]
        public int EnumKey;

        [LabelText("字符串键")]
        public string StringKey = "";

        [LabelText("GUID"), ReadOnly]
        public string AssetGuid = "";

        [LabelText("Local File ID"), ReadOnly]
        public long LocalFileId;

        [LabelText("资产路径"), ReadOnly]
        public string AssetPath = "";

        [LabelText("资产类型"), ReadOnly]
        public string AssetTypeName = "";

        [LabelText("来源库"), ReadOnly]
        public string SourceLibrary = "";

        [LabelText("来源目录"), ReadOnly]
        public string SourceBook = "";

        public string EffectiveStringKey => ResolveEffectiveStringKey();
        public bool HasAssetAuthority => !string.IsNullOrEmpty(AssetGuid) || OB != null;
        
        // 实现IAssetPage接口
        UnityEngine.Object IAssetPage.OB => OB;
        
        private bool foldVisiable;
        private bool refresh = true;

        private List<string> paths = new List<string>();
        public override bool Draw()
        {
#if UNITY_EDITOR
            bool dirty = false;
            var preNamedOption = namedOption;
            namedOption = (ABNamedOption)EditorGUILayout.EnumPopup("分AB包命名方式", namedOption);
            if (preNamedOption != namedOption)
            {
                dirty = true;
            }
            var pre = OB;
            OB = EditorGUILayout.ObjectField("文件夹或资源", OB, typeof(UnityEngine.Object), allowSceneObjects: false);
            if (OB != null)
            {
                if (pre != OB)
                {
                    Name = OB.name;
                    RefreshAssetIdentityEditor();
                    dirty = true;
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("资源键", EditorStyles.boldLabel);
                var preKind = Kind;
                Kind = (ESAssetReferKind)EditorGUILayout.EnumPopup("资产引用类型", Kind);
                if (preKind != Kind)
                {
                    dirty = true;
                }

                var preEnumKey = EnumKey;
                EnumKey = EditorGUILayout.IntField("枚举键", EnumKey);
                if (preEnumKey != EnumKey)
                {
                    dirty = true;
                }

                var preStringKey = StringKey;
                StringKey = EditorGUILayout.TextField("字符串键", StringKey);
                if (preStringKey != StringKey)
                {
                    dirty = true;
                }

                EditorGUILayout.TextField("GUID", AssetGuid);
                EditorGUILayout.LongField("Local File ID", LocalFileId);
                EditorGUILayout.TextField("资产类型", AssetTypeName);

                if (Kind == ESAssetReferKind.None)
                {
                    if (GUILayout.Button("按绑定资源推断类型", GUILayout.Height(22)))
                    {
                        Kind = DetermineKind(OB);
                        dirty = true;
                    }
                }

                var path = ESStandUtility.SafeEditor.Wrap_GetAssetPath(OB);
                bool isFolder = (ESStandUtility.SafeEditor.Wrap_IsValidFolder(path));

                {
                    SirenixEditorGUI.BeginBox();
                    SirenixEditorGUI.BeginBoxHeader();
                    bool ago = foldVisiable;
                    foldVisiable = SirenixEditorGUI.Foldout(foldVisiable, "路径");
                    if (ago != foldVisiable)
                    {
                        refresh = true;
                    }
                    SirenixEditorGUI.EndBoxHeader();
                    if (foldVisiable)
                    {

                        if (isFolder)
                        {
                            if (refresh)
                            {
                                dirty = true;
                                paths = ESStandUtility.SafeEditor.Quick_System_GetFilePaths_AlwaysSafe(path);
                                refresh = false;
                            }
                            foreach (var i in paths)
                            {
                                EditorGUILayout.LabelField(i, GUILayout.MaxWidth(400));
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField(path, GUILayout.MaxWidth(400));
                        }
                    }
                    SirenixEditorGUI.EndBox();
                }
                ;


            }
            if (dirty)
            {
                ESAssetRegistry.RegisterAsset(this, SourceLibrary, SourceBook, startOrderIndex: 0);
                ESAssetRegistry.MarkSourceLibraryDirtyByPage(this, startOrderIndex: 0);
            }

            return dirty;
#else
            return false;
#endif
        }

        public static ESAssetPage Create(UnityEngine.Object asset)
        {
            return new ESAssetPage()
            {
                Name = asset != null ? asset.name : "资源页名",
                OB = asset,
                Kind = DetermineKind(asset),
                StringKey = asset != null ? asset.name : ""
            }.RefreshAssetIdentityEditor();
        }

        public ESAssetPage CloneForPaste()
        {
            return new ESAssetPage()
            {
                Name = Name,
                ColorTag = ColorTag,
                namedOption = namedOption,
                OB = OB,
                Kind = Kind,
                EnumKey = EnumKey,
                StringKey = StringKey,
                AssetGuid = AssetGuid,
                LocalFileId = LocalFileId,
                AssetPath = AssetPath,
                AssetTypeName = AssetTypeName,
                SourceLibrary = SourceLibrary,
                SourceBook = SourceBook
            };
        }

        public ESAssetPage RefreshAssetIdentityEditor()
        {
#if UNITY_EDITOR
            if (OB == null)
                return this;

            Kind = DetermineKind(OB);
            AssetPath = AssetDatabase.GetAssetPath(OB);
            TryGetAssetIdentityEditor(OB, out string guid, out long localFileId);
            AssetGuid = guid;
            LocalFileId = localFileId;
            AssetTypeName = OB.GetType().FullName;
            if (string.IsNullOrEmpty(Name))
                Name = OB.name;
            if (string.IsNullOrEmpty(StringKey))
                StringKey = ResolveEffectiveStringKey();
#endif
            return this;
        }

        /// <summary>
        /// ES 资产身份统一规则：主资产使用 GUID + 0，只有独立子资产保留 LocalFileId。
        /// Unity 也会为主资产返回非零 FileId，调用方不能直接使用该原始值。
        /// </summary>
        public static bool TryGetAssetIdentityEditor(UnityEngine.Object asset, out string guid, out long localFileId)
        {
#if UNITY_EDITOR
            guid = string.Empty;
            localFileId = 0;
            if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out long unityLocalFileId))
                return false;

            localFileId = AssetDatabase.IsSubAsset(asset) ? unityLocalFileId : 0;
            return true;
#else
            guid = string.Empty;
            localFileId = 0;
            return false;
#endif
        }

        public string ResolveEffectiveStringKey()
        {
            if (!string.IsNullOrEmpty(StringKey))
                return StringKey;

            if (!string.IsNullOrEmpty(Name))
                return Name;

            if (OB != null && !string.IsNullOrEmpty(OB.name))
                return OB.name;

            if (!string.IsNullOrEmpty(AssetPath))
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(AssetPath);
                if (!string.IsNullOrEmpty(fileName))
                    return fileName;
            }

            if (!string.IsNullOrEmpty(AssetGuid))
                return "asset_" + AssetGuid.Substring(0, Math.Min(8, AssetGuid.Length));

            return string.Empty;
        }

        public bool RefreshAssetIdentityEditor(out string oldGuid, out long oldLocalFileId, out string oldPath, out string oldTypeName)
        {
            oldGuid = AssetGuid;
            oldLocalFileId = LocalFileId;
            oldPath = AssetPath;
            oldTypeName = AssetTypeName;

            RefreshAssetIdentityEditor();
            return oldGuid != AssetGuid
                || oldLocalFileId != LocalFileId
                || oldPath != AssetPath
                || oldTypeName != AssetTypeName;
        }

        public static ESAssetReferKind DetermineKind(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (asset == null)
                return ESAssetReferKind.None;

            if (asset is GameObject)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? ESAssetReferKind.Prefab : ESAssetReferKind.Other;
            }

            if (asset is SceneAsset) return ESAssetReferKind.Scene;
            if (asset is Sprite) return ESAssetReferKind.Sprite;
            if (asset is UnityEngine.U2D.SpriteAtlas) return ESAssetReferKind.SpriteAtlas;
            if (asset is TextAsset) return ESAssetReferKind.Raw;
            if (asset is Texture2D) return ESAssetReferKind.Texture2D;
            if (asset is Texture) return ESAssetReferKind.Texture;
            if (asset is Material) return ESAssetReferKind.Material;
            if (asset is Mesh) return ESAssetReferKind.Mesh;
            if (asset is AnimationClip) return ESAssetReferKind.AnimationClip;
            if (asset is RuntimeAnimatorController) return ESAssetReferKind.AnimatorController;
            if (asset is Avatar) return ESAssetReferKind.Avatar;
            if (asset is AudioClip) return ESAssetReferKind.AudioClip;
            if (asset is UnityEngine.Video.VideoClip) return ESAssetReferKind.VideoClip;
            if (asset is UnityEngine.Timeline.TimelineAsset) return ESAssetReferKind.TimelineAsset;
            if (asset is UnityEngine.Playables.PlayableAsset) return ESAssetReferKind.PlayableAsset;
            if (asset is TerrainData) return ESAssetReferKind.TerrainData;
            if (asset is ScriptableObject scriptableObject)
                return ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.Internal
                    ? ESAssetReferKind.Other
                    : ESAssetReferKind.ScriptableObject;
#endif
            return ESAssetReferKind.Other;
        }
    }

    [Serializable, Obsolete("Use ESAssetPage.")]
    public class ResPage : ESAssetPage
    {
        public new static ResPage Create(UnityEngine.Object asset)
        {
            return ((ResPage)new ResPage()
            {
                Name = asset != null ? asset.name : "资源页名",
                OB = asset,
                Kind = DetermineKind(asset),
                StringKey = asset != null ? asset.name : ""
            }.RefreshAssetIdentityEditor());
        }

        public new ResPage CloneForPaste()
        {
            return new ResPage()
            {
                Name = Name,
                ColorTag = ColorTag,
                namedOption = namedOption,
                OB = OB,
                Kind = Kind,
                EnumKey = EnumKey,
                StringKey = StringKey,
                AssetGuid = AssetGuid,
                LocalFileId = LocalFileId,
                AssetPath = AssetPath,
                AssetTypeName = AssetTypeName,
                SourceLibrary = SourceLibrary,
                SourceBook = SourceBook
            };
        }
    }

    public enum ABNamedOption
    {
        [InspectorName("包名-使用自己的文件路径")] UseFilePath,
        [InspectorName("包名-使用自己的父文件夹路径")] UseParentPath,
        [InspectorName("包名-使用页的顶级路径")] UsePageFolder,
        [InspectorName("包名-使用Page命名")] UsePageName,

    }
}
