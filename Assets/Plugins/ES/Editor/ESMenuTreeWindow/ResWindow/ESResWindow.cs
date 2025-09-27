using ES;
using ES.ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES
{
    public class ESResWindow : ESMenuTreeWindowAB<ESResWindow> //OdinMenuEditorWindow
    {
        [MenuItem("Tools/ES工具/ES资源窗口")]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }


        #region 数据缓存
        public const string MenuNameForLibraryRoot = "资源库";
        public Page_Root_Library page_root_Library;

        #endregion

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            PartPage_Library(tree);
        }
        void PartPage_Library(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, MenuNameForLibraryRoot,ref page_root_Library,Sirenix.OdinInspector.SdfIconType.KeyboardFill);
            var libs = ESEditorSO.SOS.GetGroup<ResLibrary>();
            foreach(var i in libs)
            {
                if(i!=null)
                tree.Add(MenuNameForLibraryRoot + $"/库：{i.Name}",new Page_Index_Library() { library=i }.ES_Refresh(), SdfIconType.Cart);
            }
        }


        public class Page_Root_Library : ESWindowPageBase
        {
            public override ESWindowPageBase ES_Refresh()
            {
                return base.ES_Refresh();
            }
        }

        public class Page_Index_Library : ESWindowPageBase
        {
            [HideInInspector]
            public ResLibrary library;
        }
    }
}
