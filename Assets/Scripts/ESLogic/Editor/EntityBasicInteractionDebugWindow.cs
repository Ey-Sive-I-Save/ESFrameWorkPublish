#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class EntityBasicInteractionDebugWindow : ESSinglePageIMGUIWindow<EntityBasicInteractionDebugWindow>
    {
        private Entity _boundEntity;
        private EntityBasicInteractionModule _boundModule;
        private bool _expanded;
        private Vector2 _scroll;
        private double _nextRepaintTime;

        [MenuItem(MenuItemPathDefine.INTERACTION_RUNTIME_PANEL_PATH, false, 0)]
        public static void Open()
        {
            var window = GetWindow<EntityBasicInteractionDebugWindow>("交互运行时面板");
            window.minSize = new Vector2(420f, 300f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("交互运行时面板", "观察当前 Entity 的基础交互与 IK 写入状态");
        }

        protected override string ESWindow_Subtitle => "Entity 交互诊断";
        protected override Vector2 ESWindow_MinSize => new Vector2(420f, 300f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(720f, 560f);
        protected override string ESWindow_PageStableId => "entity.basic-interaction";
        protected override string ESWindow_PageTitle => "基础交互";
        protected override string ESWindow_PageKeywords => "Entity 交互 IK 运行时 诊断";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "interaction.live",
                    "实时面板",
                    "启用或暂停实时交互数据绘制。",
                    context =>
                    {
                        _expanded = !_expanded;
                        _nextRepaintTime = 0d;
                        context.RefreshPageActions();
                        Repaint();
                    })
                .WithCheckedState(() => _expanded)
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "interaction.bind-selection",
                    "绑定选中",
                    "绑定 Hierarchy 当前选中的 Entity。",
                    context =>
                    {
                        TryBindFromSelection(forceRebind: true);
                        context.SetStatus(_boundModule != null ? "已绑定交互模块" : "当前选择没有交互模块",
                            _boundModule != null ? ESMenuTreePageStatus.Info : ESMenuTreePageStatus.Warning);
                        Repaint();
                    })
                .WithUnityIcon("Linked")
                .WithPriority(90));
            actions.Add(new ESMenuTreePageAction(
                    "interaction.clear",
                    "清除",
                    "清除当前 Entity 与交互模块绑定。",
                    context =>
                    {
                        _boundEntity = null;
                        _boundModule = null;
                        context.SetStatus("已清除交互模块绑定");
                        Repaint();
                    })
                .When(() => _boundEntity != null || _boundModule != null)
                .WithUnityIcon("TreeEditor.Trash")
                .WithPriority(20));
        }

        protected override void ESWindow_OnHostEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        protected override void ESWindow_OnHostDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _nextRepaintTime = 0d;
        }

        private void OnEditorUpdate()
        {
            if (!_expanded)
                return;

            double now = EditorApplication.timeSinceStartup;
            double interval = hasFocus ? 0.1d : 0.25d;
            if (now < _nextRepaintTime)
                return;
            _nextRepaintTime = now + interval;
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (_boundEntity == null)
            {
                TryBindFromSelection();
                ESWindow_CurrentPageContext?.RefreshPageActions();
                Repaint();
            }
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            if (!_expanded)
            {
                EditorGUILayout.HelpBox("点击“展开实时面板”后开始持续显示交互运行数据。", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawBindingInfo();
            DrawRuntimeInfo();
            EditorGUILayout.EndScrollView();
        }

        private void DrawBindingInfo()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("绑定状态", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Entity", _boundEntity, typeof(Entity), true);
                EditorGUILayout.TextField(
                    "InteractionModule",
                    _boundModule != null ? _boundModule.GetType().Name : "<未绑定>");
            }

            if (_boundEntity == null || _boundModule == null)
            {
                EditorGUILayout.HelpBox("未绑定到有效的 EntityBasicInteractionModule。请选择含 Entity 的对象后点击“绑定当前选中”。", MessageType.Warning);
            }
        }

        private void DrawRuntimeInfo()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("运行时数据", EditorStyles.boldLabel);

            if (_boundEntity == null || _boundModule == null)
            {
                return;
            }

            EditorGUILayout.Toggle("交互开关", _boundModule.enableInteraction);
            EditorGUILayout.Toggle("交互中", _boundModule.isInteracting);
            EditorGUILayout.ObjectField("当前候选", _boundModule.currentCandidate, typeof(ESInteractable), true);
            EditorGUILayout.ObjectField("当前激活", _boundModule.activeInteractable, typeof(ESInteractable), true);
            EditorGUILayout.EnumPopup("最近检查结果", _boundModule.lastCheckResult);
            EditorGUILayout.EnumPopup("最近结束原因", _boundModule.lastEndReason);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("IK 写入观测", EditorStyles.boldLabel);
            EditorGUILayout.TextField("最后状态", _boundModule.ikLastStatus ?? string.Empty);
            EditorGUILayout.FloatField("归一化进度", _boundModule.ikLastNormalized01);
            EditorGUILayout.FloatField("评估权重", _boundModule.ikLastEvaluatedWeight);
            EditorGUILayout.FloatField("评估LerpingRate", _boundModule.ikLastEvaluatedLerpingRate);
            EditorGUILayout.ObjectField("IK Target", _boundModule.ikLastTarget, typeof(Transform), true);
            EditorGUILayout.ObjectField("IK Hint Target", _boundModule.ikLastHintTarget, typeof(Transform), true);
            EditorGUILayout.FloatField("目标移动距离", _boundModule.ikLastTargetMoveDistance);
            EditorGUILayout.FloatField("最后写入时刻", _boundModule.ikLastWriteTime);
        }

        private void TryBindFromSelection(bool forceRebind = false)
        {
            if (!forceRebind && _boundEntity != null && _boundModule != null)
            {
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                _boundEntity = null;
                _boundModule = null;
                return;
            }

            var entity = ResolveEntityFromSelection(go);
            if (entity == null)
            {
                _boundEntity = null;
                _boundModule = null;
                return;
            }

            _boundEntity = entity;
            _boundModule = ResolveInteractionModule(entity);
        }

        private static Entity ResolveEntityFromSelection(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            // 先查当前对象，再向上父级，最后向下子级，减少手动选中成本。
            var entity = go.GetComponent<Entity>();
            if (entity != null)
            {
                return entity;
            }

            entity = go.GetComponentInParent<Entity>();
            if (entity != null)
            {
                return entity;
            }

            return go.GetComponentInChildren<Entity>(true);
        }

        private static EntityBasicInteractionModule ResolveInteractionModule(Entity entity)
        {
            if (entity == null || entity.basicDomain == null)
            {
                return null;
            }

            var domain = entity.basicDomain;
            object modulesObj = GetMemberValue(domain, "MyModules");
            if (modulesObj == null)
            {
                return null;
            }

            object valuesObj = GetMemberValue(modulesObj, "ValuesNow");
            if (!(valuesObj is IEnumerable enumerable))
            {
                return null;
            }

            foreach (object item in enumerable)
            {
                if (item is EntityBasicInteractionModule interaction)
                {
                    return interaction;
                }
            }

            return null;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo prop = type.GetProperty(memberName, flags);
            if (prop != null)
            {
                return prop.GetValue(target, null);
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            return null;
        }
    }
}
#endif
