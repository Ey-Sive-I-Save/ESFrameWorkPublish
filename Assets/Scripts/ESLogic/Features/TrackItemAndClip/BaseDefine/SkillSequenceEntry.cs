using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES
{
    public interface IEditorTrackSupport_GetSequence
    {
        public ITrackSequence Sequence { get; }
        public TrackItemType trackItemType { get; }

        public string trackName { get; }

        #region  编辑器支持
        // 叶子节点：一个具体的菜单项
        public class MenuLeaf
        {
            public string Name;                               // 显示在菜单第三级上的文字
            public IEditorTrackSupport_GetSequence Target;    // 对应的对象
        }

        // 中间层：分组
        public class MenuGroup
        {
            public string GroupName;                 // 分组名（菜单第二级）
            public List<MenuLeaf> Leaves = new List<MenuLeaf>();
        }

        // 顶层：分类
        public class MenuCategory
        {
            public string CategoryName;              // 分类名（菜单第一级）
            public List<MenuGroup> Groups = new List<MenuGroup>();
        }

        private static List<MenuCategory> _menuCategories = new List<MenuCategory>();

        /// <summary>
        /// 直接添加一个菜单项，自动归入对应的分类/分组，无需事先遍历
        /// </summary>
        /// <param name="category">第一层：分类名</param>
        /// <param name="group">第二层：分组名</param>
        /// <param name="leafName">第三层：菜单项显示名</param>
        /// <param name="item">绑定的对象</param>
        public static void AddMenuItem(string category, string group, string leafName, IEditorTrackSupport_GetSequence item)
        {
            // 查找或创建分类
            MenuCategory cat = _menuCategories.Find(c => c.CategoryName == category);
            if (cat == null)
            {
                cat = new MenuCategory { CategoryName = category };
                _menuCategories.Add(cat);
            }

            // 查找或创建分组
            MenuGroup grp = cat.Groups.Find(g => g.GroupName == group);
            if (grp == null)
            {
                grp = new MenuGroup { GroupName = group };
                cat.Groups.Add(grp);
            }

            // 添加叶子（允许同名，不覆盖）
            grp.Leaves.Add(new MenuLeaf { Name = leafName, Target = item });
        }


#if UNITY_EDITOR
        public static void ShowDynamicMenu(Rect rect, GenericMenu.MenuFunction2 action)
        {
            GenericMenu menu = new GenericMenu();

            foreach (var cat in _menuCategories)
            {
                foreach (var grp in cat.Groups)
                {
                    foreach (var leaf in grp.Leaves)
                    {
                        string path = $"{cat.CategoryName}/{grp.GroupName}/{leaf.Name}";
                        menu.AddItem(new GUIContent(path), false, action, leaf.Target);
                    }
                }
            }

            if (_menuCategories.Count == 0)
                menu.AddDisabledItem(new GUIContent("暂无项"));

            menu.DropDown(rect);
        }
#endif

        private void OnItemSelected(object userData)
        {
            var selected = userData as IEditorTrackSupport_GetSequence;
            if (selected != null)
            {
                Debug.Log($"选中: {selected}");
                // 处理逻辑
            }
        }

        #endregion
    }
    [Serializable]
    public class SkillTrackSequence : TrackSequenceBase<ISkillTrackItem>
    {
        public override string Name => "技能轨道序列";

        [Button("初始化")]
        public override void InitByEditor()
        {
#if UNITY_EDITOR
            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("初始化技能轨道", "清除已经做出的修改并且重置为默认状态"))
            {
                tracks_.Clear();
            }
#endif
        }
    }

    public interface ISkillTrackItem : ITrackItem
    {

    }

    [Serializable]
    public class SkillTrackItem<SkillTrackClipT> : TrackItemBase<SkillTrackClipT>, ISkillTrackItem where SkillTrackClipT : SkillTrackClip
    {

    }

    [Serializable, ESCreatePath("轨道项", "技能标准轨道")]
    public class SkillTrackItemStand : SkillTrackItem<SkillTrackClip>
    {

    }

    [Serializable]
    public class SkillTrackClip : TrackClipBase
    {

    }

}
