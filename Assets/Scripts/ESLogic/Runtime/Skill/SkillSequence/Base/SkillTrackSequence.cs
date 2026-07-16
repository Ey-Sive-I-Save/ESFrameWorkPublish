using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    public interface IEditorTrackSupport_GetSequence
    {
        public ITrackSequence Sequence { get; }
        public TrackItemType trackItemType { get; }
        public string trackName { get; }

        #region 编辑器支持

        public class MenuLeaf
        {
            public string Name;
            public IEditorTrackSupport_GetSequence Target;
        }

        public class MenuGroup
        {
            public string GroupName;
            public List<MenuLeaf> Leaves = new List<MenuLeaf>();
        }

        public class MenuCategory
        {
            public string CategoryName;
            public List<MenuGroup> Groups = new List<MenuGroup>();
        }

        private static List<MenuCategory> _menuCategories = new List<MenuCategory>();

        public static void AddMenuItem(string category, string group, string leafName, IEditorTrackSupport_GetSequence item)
        {
            MenuCategory cat = _menuCategories.Find(c => c.CategoryName == category);
            if (cat == null)
            {
                cat = new MenuCategory { CategoryName = category };
                _menuCategories.Add(cat);
            }

            MenuGroup grp = cat.Groups.Find(g => g.GroupName == group);
            if (grp == null)
            {
                grp = new MenuGroup { GroupName = group };
                cat.Groups.Add(grp);
            }

            grp.Leaves.Add(new MenuLeaf { Name = leafName, Target = item });
        }

#if UNITY_EDITOR
        public static void ShowDynamicMenu(Rect rect, GenericMenu.MenuFunction2 action)
        {
            GenericMenu menu = new GenericMenu();
            HashSet<IEditorTrackSupport_GetSequence> addedTargets = new HashSet<IEditorTrackSupport_GetSequence>();
            int addedCount = 0;

            foreach (var cat in _menuCategories)
            {
                foreach (var grp in cat.Groups)
                {
                    foreach (var leaf in grp.Leaves)
                    {
                        if (leaf == null || leaf.Target == null || leaf.Target.Sequence == null || addedTargets.Contains(leaf.Target))
                            continue;

                        string path = $"{cat.CategoryName}/{grp.GroupName}/{leaf.Name}";
                        menu.AddItem(new GUIContent(path), false, action, leaf.Target);
                        addedTargets.Add(leaf.Target);
                        addedCount++;
                    }
                }
            }

            List<IEditorTrackSupport_GetSequence> projectItems = null;
            try
            {
                projectItems = ESDesignUtility.SafeEditor.FindAllSOAssets<IEditorTrackSupport_GetSequence>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (projectItems != null)
            {
                projectItems.Sort((a, b) =>
                {
                    string an = a != null ? a.trackName : string.Empty;
                    string bn = b != null ? b.trackName : string.Empty;
                    return string.CompareOrdinal(an, bn);
                });

                for (int i = 0; i < projectItems.Count; i++)
                {
                    IEditorTrackSupport_GetSequence item = projectItems[i];
                    if (item == null || item.Sequence == null || addedTargets.Contains(item))
                        continue;

                    string name = string.IsNullOrWhiteSpace(item.trackName) ? item.ToString() : item.trackName;
                    menu.AddItem(new GUIContent("项目扫描/" + name), false, action, item);
                    addedTargets.Add(item);
                    addedCount++;
                }
            }

            if (addedCount == 0)
                menu.AddDisabledItem(new GUIContent("暂无项"));

            menu.DropDown(rect);
        }
#endif

        #endregion
    }

    [Serializable]
    public class SkillProcessTrackSequence : TrackSequenceBase<ISkillTrackItem>
    {
        public override string Name => "技能轨道序列";

        [TitleGroup("技能轨道序列", "技能时间轴过程，只描述一次轨道播放过程，不直接承担冷却/消耗等完整技能体配置。")]
        [Button("初始化默认轨道", ButtonSizes.Medium)]
        [GUIColor(0.85f, 0.72f, 0.36f)]
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
        [TitleGroup("编辑器预览上下文", "仅影响编辑器轨道预览时的目标解析，不直接改变运行时技能目标。")]
        [LabelText("覆盖轨道目标")]
        public bool overrideTrackPreviewTarget;

        [TitleGroup("编辑器预览上下文")]
        [ShowIf(nameof(overrideTrackPreviewTarget))]
        [LabelText("轨道目标表达式")]
        [InfoBox("开启后，本轨道预览会优先使用该表达式解析出的 GameObject/Entity。", InfoMessageType.None)]
        [SerializeReference]
        public ESGetGameObjectExpression trackTargetExpression;

#if UNITY_EDITOR
        public override List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var runtimeTarget = editorTarget as ESRuntimeTargetPack;
            if (!overrideTrackPreviewTarget)
                return base.CreateEditorSamplers(sequence, runtimeTarget);

            ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
            target.SetEntity(runtimeTarget != null ? runtimeTarget.userEntity : null);
            target.SetUser(runtimeTarget != null ? runtimeTarget.userEntity : null);

            GameObject targetObject = trackTargetExpression != null
                ? trackTargetExpression.Evaluate(runtimeTarget, null)
                : null;

            if (targetObject != null)
                target.SetEntityMainTarget(FindEntityInSelfOrParents(targetObject));

            return CreateClipEditorSamplers(sequence, target, true);
        }

        private static Entity FindEntityInSelfOrParents(GameObject gameObject)
        {
            Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }

        private List<IEditorTimeSampler> CreateClipEditorSamplers(ITrackSequence sequence, ESRuntimeTargetPack editorTarget, bool ownsEditorTarget)
        {
            var list = new List<IEditorTimeSampler>
            {
                new TrackEditorSampler(this, editorTarget, ownsEditorTarget)
            };

            if (clips == null)
                return list;

            foreach (var clip in clips)
            {
                if (clip == null || !clip.Enabled)
                    continue;

                var clipSampler = clip.CreateEditorSampler(sequence, this, editorTarget);
                if (clipSampler != null)
                    list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }
#endif
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
