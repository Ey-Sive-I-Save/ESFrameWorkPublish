using ES;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace ES {
    public abstract class ESMenuTreeWindowAB<This> : OdinMenuEditorWindow where This : ESMenuTreeWindowAB<This>
    {
        public static This UsingWindow;
        public static OdinMenuTree menuTree;
        private static Texture2D blackTexture;
        public static Dictionary<string, OdinMenuItem> MenuItems = new Dictionary<string, OdinMenuItem>();
        public virtual GUIContent ESWindow_GetWindowGUIContent()
        {
            var content = new GUIContent("ES窗口", "使用ES工具完成快速开发");
            return content;
        }
        protected override void Initialize()
        {
            base.Initialize();
            blackTexture = new Texture2D(1, 1);
            blackTexture.SetPixel(0, 0, Color.black + new Color(0.05f, 0.05f, 0.05f));
            blackTexture.Apply();
        }

        public virtual void ESWindow_OnOpen()
        {

        }
        public static void OpenWindow()
        {
            UsingWindow = GetWindow<This>();
            UsingWindow.ESWindow_OnOpen();
            UsingWindow.titleContent = UsingWindow.ESWindow_GetWindowGUIContent();
            UsingWindow.minSize = new Vector2(500, 600);
            UsingWindow.maxSize = new Vector2(2500, 1800);
            UsingWindow.maximized = true;
            UsingWindow.MenuWidth = 200;
            UsingWindow.Show();
            UsingWindow.OnClose += () => { UsingWindow.ES_SaveData(); };
        }
        protected sealed override OdinMenuTree BuildMenuTree()
        {
            OdinMenuTree tree = menuTree = new OdinMenuTree();
            ES_OnBuildMenuTree(tree);
            ES_LoadData();
            return tree;
        }
        protected virtual void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
 
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, SdfIconType sdfIcon) where P : ESWindowPageBase, new()
        {
            // Odin 的 Add("父/子", obj) 会返回多个菜单项，最后一个才是真正绑定页面的叶子节点
            MenuItems[name] = tree.Add(name, (page ??= new P()), sdfIcon).Last();
            page.ES_Refresh();
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, Texture texture) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), texture).Last();
            page.ES_Refresh();
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, EditorIcon icon) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), icon).Last();
            page.ES_Refresh();
        }
        protected override void OnImGUI()
        {
            if (UsingWindow == null)
            {
                UsingWindow = this as This;
            }
            if(blackTexture)GUI.DrawTexture(new Rect(0, 0, position.width, position.height), blackTexture);
            base.OnImGUI();
        }
        public static void ES_RefreshWindow()
        {
            if (UsingWindow == null) OpenWindow();
            UsingWindow.ESWindow_RefreshWindow();
        }
        public virtual void ESWindow_RefreshWindow()
        {
            ES_SaveData();
            this.ForceMenuTreeRebuild();
            ES_LoadData();
        }
        public virtual void ES_LoadData()
        {

        }
        public virtual void ES_SaveData()
        {

        }
    }

    [Serializable]
    public abstract class ESWindowPageBase
    {
        public virtual ESWindowPageBase ES_Refresh()
        {
            return this;
        }
    }

    public class BlackBackgroundDrawer : OdinValueDrawer<ESWindowPageBase>
    {
        private ESAreaSolver area = new ESAreaSolver();
        public static Color color = new Color(0.05f,0.05f,0.05f,1);
        protected override void DrawPropertyLayout(GUIContent label)
        {
            area.UpdateAtFisrt();
            var rect = area.TargetArea;
            SirenixEditorGUI.DrawBorders(rect, (int)rect.width, 0, (int)rect.height + 2, 0, color);
            this.CallNextDrawer(label);
            area.UpdateAtLast();
        }

    }
}
