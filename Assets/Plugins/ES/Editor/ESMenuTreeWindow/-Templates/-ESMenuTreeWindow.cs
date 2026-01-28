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
        
        /// <summary>
        /// 收集所有注册的页面，用于统一调用OnPageDisable
        /// </summary>
        private static List<ESWindowPageBase> registeredPages = new List<ESWindowPageBase>();
        
        /// <summary>
        /// 获取当前注册的页面数量（用于调试）
        /// </summary>
        public static int GetRegisteredPageCount()
        {
            return registeredPages?.Count ?? 0;
        }
        
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
            
            // 注册页面到列表，用于窗口关闭时统一调用OnPageDisable
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, Texture texture) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), texture).Last();
            page.ES_Refresh();
            
            // 注册页面到列表
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        public void QuickBuildRootMenu<P>(OdinMenuTree tree, string name, ref P page, EditorIcon icon) where P : ESWindowPageBase, new()
        {
            MenuItems[name] = tree.Add(name, (page ??= new P()), icon).Last();
            page.ES_Refresh();
            
            // 注册页面到列表
            if (page != null && !registeredPages.Contains(page))
            {
                registeredPages.Add(page);
            }
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（用于动态创建的页面）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, SdfIconType icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表，用于窗口关闭时统一调用OnPageDisable
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（Texture重载）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, Texture icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
        }
        
        /// <summary>
        /// 注册并添加已创建的页面实例到菜单树（EditorIcon重载）
        /// </summary>
        public OdinMenuItem RegisterAndAddPage(OdinMenuTree tree, string path, ESWindowPageBase page, EditorIcon icon)
        {
            if (page == null)
            {
                Debug.LogError("[ESMenuTreeWindow] RegisterAndAddPage: page is null");
                return null;
            }
            
            var menuItem = tree.Add(path, page, icon).Last();
            
            // 注册页面到列表
            if (!registeredPages.Contains(page))
            {
                registeredPages.Add(page);
                // Debug.Log($"[ESMenuTreeWindow] 注册页面: {page.GetType().Name} - {path}");
            }
            
            return menuItem;
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
        
        /// <summary>
        /// 窗口销毁时统一调用所有注册页面的OnPageDisable
        /// </summary>
        protected override void OnDestroy()
        {
            // Debug.Log($"[ESMenuTreeWindow] 窗口销毁，开始调用 {registeredPages.Count} 个页面的OnPageDisable");
            
            int callCount = 0;
            foreach (var page in registeredPages)
            {
                if (page != null)
                {
                    try
                    {
                        page.OnPageDisable();
                        callCount++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[ESMenuTreeWindow] 页面 {page.GetType().Name} 的OnPageDisable调用失败: {e.Message}");
                    }
                }
            }
            
            // Debug.Log($"[ESMenuTreeWindow] OnPageDisable调用完成，成功调用 {callCount}/{registeredPages.Count} 个页面");
            
            // 清理列表
            registeredPages.Clear();
        }
    }

    [Serializable]
    public abstract class ESWindowPageBase
    {
        public virtual ESWindowPageBase ES_Refresh()
        {
            return this;
        }
        
        /// <summary>
        /// 窗口关闭或页面销毁时调用，用于清理资源和保存数据
        /// </summary>
        public virtual void OnPageDisable()
        {
            // 子类可重写此方法进行清理工作
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
