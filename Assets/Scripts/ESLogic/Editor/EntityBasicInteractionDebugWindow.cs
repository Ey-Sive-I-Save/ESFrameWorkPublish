#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class EntityBasicInteractionDebugWindow : EditorWindow
    {
        private Entity _boundEntity;
        private EntityBasicInteractionModule _boundModule;
        private bool _expanded;
        private Vector2 _scroll;

        [MenuItem("ES/Debug/Interaction Runtime Panel")]
        public static void Open()
        {
            var window = GetWindow<EntityBasicInteractionDebugWindow>("Interaction Runtime");
            window.minSize = new Vector2(420f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_expanded)
            {
                Repaint();
            }
        }

        private void OnSelectionChange()
        {
            if (_boundEntity == null)
            {
                TryBindFromSelection();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

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

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string expandText = _expanded ? "收起实时面板" : "展开实时面板";
            if (GUILayout.Button(expandText, EditorStyles.toolbarButton, GUILayout.Width(120f)))
            {
                _expanded = !_expanded;
            }

            if (GUILayout.Button("绑定当前选中", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                TryBindFromSelection(forceRebind: true);
            }

            if (GUILayout.Button("清除绑定", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                _boundEntity = null;
                _boundModule = null;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBindingInfo()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("绑定状态", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Entity", _boundEntity, typeof(Entity), true);
                EditorGUILayout.ObjectField("InteractionModule", null, typeof(EntityBasicInteractionModule), false);
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
