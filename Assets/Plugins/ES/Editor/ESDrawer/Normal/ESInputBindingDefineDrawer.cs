using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES
{
    [CustomPropertyDrawer(typeof(ESInputBindingDefine))]
    public sealed class ESInputBindingDefineDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;

        private static InputActionRebindingExtensions.RebindingOperation listenOperation;
        private static SerializedObject listenTarget;
        private static string listenPropertyPath;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty source = property.FindPropertyRelative("source");
            SerializedProperty isComposite = property.FindPropertyRelative("isComposite");
            SerializedProperty isPartOfComposite = property.FindPropertyRelative("isPartOfComposite");

            int rows = 3;
            bool isVirtual = source != null && source.enumValueIndex == (int)ESInputBindingSource.VirtualControl;
            bool showAdvancedInputSystem = !isVirtual
                                           && isComposite != null
                                           && isPartOfComposite != null
                                           && !isComposite.boolValue
                                           && !isPartOfComposite.boolValue;
            if (showAdvancedInputSystem)
                rows++;

            return Line * rows + Gap * (rows + 1);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty schemeId = property.FindPropertyRelative("schemeId");
            SerializedProperty source = property.FindPropertyRelative("source");
            SerializedProperty path = property.FindPropertyRelative("path");
            SerializedProperty virtualControlId = property.FindPropertyRelative("virtualControlId");
            SerializedProperty interactions = property.FindPropertyRelative("interactions");
            SerializedProperty processors = property.FindPropertyRelative("processors");
            SerializedProperty isComposite = property.FindPropertyRelative("isComposite");
            SerializedProperty isPartOfComposite = property.FindPropertyRelative("isPartOfComposite");
            SerializedProperty name = property.FindPropertyRelative("name");

            EditorGUI.LabelField(NextLine(ref position), label, EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.36f, out Rect left, out Rect right);
            EditorGUI.PropertyField(left, schemeId, new GUIContent("方案"));
            EditorGUI.PropertyField(right, source, new GUIContent("来源"));

            row = NextLine(ref position);
            bool isVirtual = (ESInputBindingSource)source.enumValueIndex == ESInputBindingSource.VirtualControl;
            bool showAdvancedInputSystem = !isVirtual && !isComposite.boolValue && !isPartOfComposite.boolValue;

            DrawSplit(row, 0.5f, out left, out right);
            EditorGUI.PropertyField(left, name, new GUIContent("名称"));
            if (!isVirtual)
            {
                Rect compositeRect = new Rect(right.x, right.y, right.width * 0.5f - Gap * 0.5f, right.height);
                Rect partRect = new Rect(compositeRect.xMax + Gap, right.y, right.width * 0.5f - Gap * 0.5f, right.height);
                EditorGUI.PropertyField(compositeRect, isComposite, new GUIContent("组合"));
                EditorGUI.PropertyField(partRect, isPartOfComposite, new GUIContent("部分"));
            }

            row = NextLine(ref position);
            if (isVirtual)
            {
                DrawPathRow(row, virtualControlId, "虚拟控件", property.serializedObject);
            }
            else
            {
                DrawPathRow(row, path, isComposite.boolValue ? "组合类型" : "路径", property.serializedObject, !isComposite.boolValue);
            }

            if (showAdvancedInputSystem)
            {
                row = NextLine(ref position);
                DrawSplit(row, 0.5f, out left, out right);
                EditorGUI.PropertyField(left, interactions, new GUIContent("交互"));
                EditorGUI.PropertyField(right, processors, new GUIContent("处理器"));
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPathRow(
            Rect row,
            SerializedProperty value,
            string title,
            SerializedObject serializedObject,
            bool allowListen = true)
        {
            Rect labelRect = new Rect(row.x, row.y, 48f, row.height);
            Rect fieldRect = new Rect(row.x + 50f, row.y, allowListen ? row.width - 140f : row.width - 94f, row.height);
            Rect listenRect = new Rect(row.xMax - 84f, row.y, 40f, row.height);
            Rect clearRect = new Rect(row.xMax - 40f, row.y, 40f, row.height);

            EditorGUI.LabelField(labelRect, title);
            EditorGUI.PropertyField(fieldRect, value, GUIContent.none);

            if (allowListen && GUI.Button(listenRect, "听"))
                StartListen(serializedObject, value.propertyPath);

            if (GUI.Button(clearRect, "×"))
            {
                serializedObject.Update();
                value.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
            }

            DrawListenHint(new Rect(fieldRect.x, row.yMax + 1f, fieldRect.width, 14f), value.stringValue);
        }

        private static void DrawListenHint(Rect rect, string currentPath)
        {
            if (listenOperation == null || string.IsNullOrEmpty(listenPropertyPath))
            {
                if (!string.IsNullOrEmpty(currentPath))
                    EditorGUI.LabelField(rect, currentPath, EditorStyles.miniLabel);
                return;
            }

            EditorGUI.LabelField(rect, "正在监听输入...", EditorStyles.miniLabel);
        }

        private static void StartListen(SerializedObject serializedObject, string propertyPath)
        {
            StopListen();

            listenTarget = serializedObject;
            listenPropertyPath = propertyPath;
            listenOperation = new InputActionRebindingExtensions.RebindingOperation()
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Pointer>/press")
                .WithControlsExcluding("<Pointer>/clickCount")
                .WithControlsExcluding("<Touchscreen>/touch*/position")
                .WithControlsExcluding("<Touchscreen>/touch*/delta")
                .OnMatchWaitForAnother(0.05f)
                .OnApplyBinding(OnListenApplyBinding)
                .OnComplete(OnListenComplete)
                .OnCancel(OnListenCancel);
            listenOperation.Start();

            EditorApplication.update -= PumpListen;
            EditorApplication.update += PumpListen;
        }

        private static void PumpListen()
        {
            if (listenOperation == null)
                StopListen();
        }

        private static void OnListenApplyBinding(
            InputActionRebindingExtensions.RebindingOperation operation,
            string path)
        {
            if (listenTarget != null && !string.IsNullOrEmpty(listenPropertyPath) && !string.IsNullOrEmpty(path))
            {
                listenTarget.Update();
                SerializedProperty property = listenTarget.FindProperty(listenPropertyPath);
                if (property != null)
                {
                    property.stringValue = path;
                    listenTarget.ApplyModifiedProperties();
                }
            }
        }

        private static void OnListenComplete(InputActionRebindingExtensions.RebindingOperation operation)
        {
            EditorApplication.delayCall += StopListen;
        }

        private static void OnListenCancel(InputActionRebindingExtensions.RebindingOperation operation)
        {
            EditorApplication.delayCall += StopListen;
        }

        private static void StopListen()
        {
            EditorApplication.update -= PumpListen;

            if (listenOperation != null)
            {
                listenOperation.Dispose();
                listenOperation = null;
            }

            listenTarget = null;
            listenPropertyPath = null;
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect row = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return row;
        }

        private static void DrawSplit(Rect row, float leftRatio, out Rect left, out Rect right)
        {
            float leftWidth = Mathf.Max(80f, row.width * leftRatio);
            left = new Rect(row.x, row.y, leftWidth - Gap * 0.5f, row.height);
            right = new Rect(row.x + leftWidth + Gap * 0.5f, row.y, row.width - leftWidth - Gap * 0.5f, row.height);
        }
    }
}
