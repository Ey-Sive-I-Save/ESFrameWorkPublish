using System;
using System.Collections.Generic;
using ES;
using ES.Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES.EditorInternal
{
    [CustomPropertyDrawer(typeof(ESInputBindingDefine))]
    public sealed class ESInputBindingDefineDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float HintLine = 14f;
        private const float Gap = 4f;

        private static InputActionRebindingExtensions.RebindingOperation listenOperation;
        private static SerializedObject listenTarget;
        private static string listenPropertyPath;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty source = property.FindPropertyRelative("source");
            SerializedProperty isComposite = property.FindPropertyRelative("isComposite");
            SerializedProperty isPartOfComposite = property.FindPropertyRelative("isPartOfComposite");

            int rows = 5;
            bool isVirtual = source != null && source.enumValueIndex == (int)ESInputBindingSource.VirtualControl;
            bool showAdvancedInputSystem = !isVirtual
                                           && isComposite != null
                                           && isPartOfComposite != null
                                           && !isComposite.boolValue
                                           && !isPartOfComposite.boolValue;
            if (property.isExpanded)
            {
                rows += showAdvancedInputSystem ? 1 : 0;
            }

            return Line * rows + HintLine + Gap * (rows + 2);
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

            bool isVirtual = (ESInputBindingSource)source.enumValueIndex == ESInputBindingSource.VirtualControl;
            bool showAdvancedInputSystem = !isVirtual && !isComposite.boolValue && !isPartOfComposite.boolValue;

            Rect titleRow = NextLine(ref position);
            property.isExpanded = EditorGUI.Foldout(
                new Rect(titleRow.x, titleRow.y, 14f, titleRow.height),
                property.isExpanded,
                GUIContent.none,
                true);
            EditorGUI.LabelField(
                new Rect(titleRow.x + 18f, titleRow.y, titleRow.width - 18f, titleRow.height),
                BuildBindingTitle(schemeId, source, path, virtualControlId, name, isComposite, isPartOfComposite),
                EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.42f, out Rect left, out Rect right);
            EditorGUI.PropertyField(left, schemeId, new GUIContent("输入方案"));
            EditorGUI.PropertyField(right, source, new GUIContent("输入来源"));

            row = NextLine(ref position);
            Rect hintRow = NextHintLine(ref position);
            if (isVirtual)
            {
                DrawPathRow(row, hintRow, virtualControlId, "虚拟控件", property.serializedObject, false);
            }
            else
            {
                bool allowPathTools = !isComposite.boolValue;
                DrawPathRow(row, hintRow, path, isComposite.boolValue ? "组合类型" : "按键/控件", property.serializedObject, allowPathTools, schemeId, property.propertyPath);
            }

            row = NextLine(ref position);
            DrawSplit(row, 0.42f, out left, out right);
            EditorGUI.PropertyField(left, name, new GUIContent("绑定名称"));
            if (!isVirtual)
            {
                DrawBindingKindButtons(right, isComposite, isPartOfComposite);
            }
            else
            {
                EditorGUI.LabelField(right, "UI/触摸等虚拟输入入口", EditorStyles.miniLabel);
            }

            row = NextLine(ref position);
            EditorGUI.LabelField(row, GetBindingHelpText(isVirtual, isComposite.boolValue, isPartOfComposite.boolValue), EditorStyles.miniLabel);

            if (property.isExpanded && showAdvancedInputSystem)
            {
                row = NextLine(ref position);
                DrawSplit(row, 0.5f, out left, out right);
                EditorGUI.PropertyField(left, interactions, new GUIContent("InputSystem 交互"));
                EditorGUI.PropertyField(right, processors, new GUIContent("InputSystem 处理器"));
            }

            EditorGUI.EndProperty();
        }

        private static void DrawPathRow(
            Rect row,
            Rect hintRow,
            SerializedProperty value,
            string title,
            SerializedObject serializedObject,
            bool allowInputSystemTools = true,
            SerializedProperty schemeId = null,
            string bindingPropertyPath = null)
        {
            Rect labelRect = new Rect(row.x, row.y, 48f, row.height);
            float toolsWidth = allowInputSystemTools ? 178f : 44f;
            Rect fieldRect = new Rect(row.x + 50f, row.y, row.width - 52f - toolsWidth, row.height);
            Rect clearRect = new Rect(row.xMax - 40f, row.y, 40f, row.height);

            EditorGUI.LabelField(labelRect, title);
            EditorGUI.PropertyField(fieldRect, value, GUIContent.none);

            if (allowInputSystemTools)
            {
                Rect listenRect = new Rect(row.xMax - 172f, row.y, 40f, row.height);
                Rect selectRect = new Rect(row.xMax - 128f, row.y, 40f, row.height);
                Rect importRect = new Rect(row.xMax - 84f, row.y, 40f, row.height);

                if (GUI.Button(listenRect, "监听", EditorStyles.miniButton))
                {
                    StartListen(serializedObject, value.propertyPath);
                }

                if (GUI.Button(selectRect, "选择", EditorStyles.miniButton))
                {
                    ShowControlMenu(serializedObject, value.propertyPath, schemeId != null ? schemeId.stringValue : null, bindingPropertyPath);
                }

                if (GUI.Button(importRect, "导入", EditorStyles.miniButton))
                {
                    ESInputActionBindingImportWindow.Open(serializedObject, bindingPropertyPath);
                }
            }

            if (GUI.Button(clearRect, "清", EditorStyles.miniButton))
            {
                RecordUndo(serializedObject, "清空输入绑定");
                serializedObject.Update();
                value.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                MarkDirty(serializedObject);
            }

            DrawListenHint(hintRow, value.stringValue);
        }

        private static void DrawBindingKindButtons(Rect row, SerializedProperty isComposite, SerializedProperty isPartOfComposite)
        {
            float labelWidth = 58f;
            float buttonWidth = Mathf.Max(50f, (row.width - labelWidth - Gap * 3f) / 3f);
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Rect normalRect = new Rect(labelRect.xMax + Gap, row.y, buttonWidth, row.height);
            Rect compositeRect = new Rect(normalRect.xMax + Gap, row.y, buttonWidth, row.height);
            Rect partRect = new Rect(compositeRect.xMax + Gap, row.y, row.xMax - compositeRect.xMax - Gap, row.height);

            EditorGUI.LabelField(labelRect, "绑定结构");
            bool normal = !isComposite.boolValue && !isPartOfComposite.boolValue;
            if (GUI.Toggle(normalRect, normal, "普通", EditorStyles.miniButtonLeft))
            {
                isComposite.boolValue = false;
                isPartOfComposite.boolValue = false;
            }

            if (GUI.Toggle(compositeRect, isComposite.boolValue, "组合", EditorStyles.miniButtonMid))
            {
                isComposite.boolValue = true;
                isPartOfComposite.boolValue = false;
            }

            if (GUI.Toggle(partRect, isPartOfComposite.boolValue, "子项", EditorStyles.miniButtonRight))
            {
                isComposite.boolValue = false;
                isPartOfComposite.boolValue = true;
            }
        }

        private static string BuildBindingTitle(
            SerializedProperty schemeId,
            SerializedProperty source,
            SerializedProperty path,
            SerializedProperty virtualControlId,
            SerializedProperty name,
            SerializedProperty isComposite,
            SerializedProperty isPartOfComposite)
        {
            string schemeName = GetSchemeDisplayName(schemeId != null ? schemeId.stringValue : string.Empty);
            string bindName = name != null ? name.stringValue : string.Empty;
            ESInputBindingSource bindingSource = source != null
                ? (ESInputBindingSource)source.enumValueIndex
                : ESInputBindingSource.InputSystem;

            if (bindingSource == ESInputBindingSource.VirtualControl)
            {
                string virtualId = virtualControlId != null ? virtualControlId.stringValue : string.Empty;
                return schemeName + "：虚拟控件 " + (string.IsNullOrEmpty(virtualId) ? "未设置" : virtualId);
            }

            string rawPath = path != null ? path.stringValue : string.Empty;
            string controlName = GetControlDisplayName(rawPath);
            if (isComposite != null && isComposite.boolValue)
                return schemeName + "：组合 " + (string.IsNullOrEmpty(bindName) ? controlName : bindName) + "（" + controlName + "）";

            if (isPartOfComposite != null && isPartOfComposite.boolValue)
                return schemeName + "：" + (string.IsNullOrEmpty(bindName) ? "组合子项" : bindName) + " = " + controlName;

            return schemeName + "：" + controlName;
        }

        private static string GetSchemeDisplayName(string schemeId)
        {
            if (schemeId == ESInputSchemeIds.KeyboardMouse)
                return "键鼠";
            if (schemeId == ESInputSchemeIds.Gamepad)
                return "手柄";
            if (schemeId == ESInputSchemeIds.Touch)
                return "触摸";
            return string.IsNullOrEmpty(schemeId) ? "默认方案" : schemeId;
        }

        private static string GetControlDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "未设置";

            if (ESInputControlCatalog.TryFindByPath(path, out ESInputControlOption option))
                return string.IsNullOrEmpty(option.displayName) ? path : option.displayName;

            return path;
        }

        private static string GetBindingHelpText(bool isVirtual, bool isComposite, bool isPartOfComposite)
        {
            if (isVirtual)
                return "用于 UI 按钮、虚拟摇杆、触摸区域等代码或组件主动写入输入。";
            if (isComposite)
                return "组合父项只定义组合类型，例如 2DVector；下面的 Up/Down/Left/Right 才绑定具体按键。";
            if (isPartOfComposite)
                return "组合子项绑定具体按键，例如 WASD 的 Up = W。";
            return "普通绑定：一个动作直接对应一个键、鼠标键、手柄键或轴。";
        }

        private static void ShowControlMenu(SerializedObject serializedObject, string propertyPath, string schemeId, string bindingPropertyPath)
        {
            GenericMenu menu = new GenericMenu();
            bool any = false;
            bool hasValueTypeFilter = TryGetExpectedValueType(serializedObject, bindingPropertyPath, out ESInputValueType expectedValueType);

            if (string.IsNullOrEmpty(schemeId) || schemeId == ESInputSchemeIds.KeyboardMouse)
            {
                any |= AddControlOptions(menu, "键盘", ESInputControlCatalog.KeyboardOptions, serializedObject, propertyPath, hasValueTypeFilter, expectedValueType);
                any |= AddControlOptions(menu, "鼠标", ESInputControlCatalog.MouseOptions, serializedObject, propertyPath, hasValueTypeFilter, expectedValueType);
            }

            if (string.IsNullOrEmpty(schemeId) || schemeId == ESInputSchemeIds.Gamepad)
            {
                any |= AddControlOptions(menu, "手柄", ESInputControlCatalog.GamepadOptions, serializedObject, propertyPath, hasValueTypeFilter, expectedValueType);
            }

            if (!any)
            {
                menu.AddDisabledItem(new GUIContent("没有可选控件"));
            }

            menu.ShowAsContext();
        }

        private static bool AddControlOptions(
            GenericMenu menu,
            string group,
            IReadOnlyList<ESInputControlOption> options,
            SerializedObject serializedObject,
            string propertyPath,
            bool hasValueTypeFilter,
            ESInputValueType expectedValueType)
        {
            if (options == null || options.Count == 0)
            {
                return false;
            }

            bool added = false;
            ESInputValueType currentType = (ESInputValueType)(-1);
            for (int i = 0; i < options.Count; i++)
            {
                ESInputControlOption option = options[i];
                if (hasValueTypeFilter && option.valueType != expectedValueType)
                    continue;

                if (option.valueType != currentType)
                {
                    currentType = option.valueType;
                    if (added)
                    {
                        menu.AddSeparator(group + "/");
                    }
                }

                string typeName = GetValueTypeMenuName(option.valueType);
                string label = group + "/" + typeName + "/" + option.displayName + "  " + option.path;
                string selectedPath = option.path;
                menu.AddItem(new GUIContent(label), false, () => SetStringProperty(serializedObject, propertyPath, selectedPath));
                added = true;
            }

            return added;
        }

        private static bool TryGetExpectedValueType(SerializedObject serializedObject, string bindingPropertyPath, out ESInputValueType valueType)
        {
            valueType = ESInputValueType.Button;
            if (serializedObject == null || string.IsNullOrEmpty(bindingPropertyPath))
                return false;

            serializedObject.Update();
            SerializedProperty bindingProperty = serializedObject.FindProperty(bindingPropertyPath);
            if (bindingProperty != null)
            {
                SerializedProperty isPartOfComposite = bindingProperty.FindPropertyRelative("isPartOfComposite");
                if (isPartOfComposite != null && isPartOfComposite.boolValue)
                {
                    valueType = ESInputValueType.Button;
                    return true;
                }
            }

            int markerIndex = bindingPropertyPath.LastIndexOf(".bindings.Array.data[", System.StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            string actionPath = bindingPropertyPath.Substring(0, markerIndex);
            SerializedProperty actionProperty = serializedObject.FindProperty(actionPath);
            SerializedProperty valueTypeProperty = actionProperty != null
                ? actionProperty.FindPropertyRelative("valueType")
                : null;
            if (valueTypeProperty == null)
                return false;

            valueType = (ESInputValueType)valueTypeProperty.enumValueIndex;
            return true;
        }

        private static string GetValueTypeMenuName(ESInputValueType valueType)
        {
            switch (valueType)
            {
                case ESInputValueType.Axis:
                    return "单轴";
                case ESInputValueType.Vector2:
                    return "二维向量";
                default:
                    return "按钮";
            }
        }

        private static void SetStringProperty(SerializedObject serializedObject, string propertyPath, string value)
        {
            if (serializedObject == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
            {
                RecordUndo(serializedObject, "选择输入绑定");
                property.stringValue = value;
                serializedObject.ApplyModifiedProperties();
                MarkDirty(serializedObject);
            }
        }

        private static void DrawListenHint(Rect rect, string currentPath)
        {
            if (listenOperation == null || string.IsNullOrEmpty(listenPropertyPath))
            {
                if (!string.IsNullOrEmpty(currentPath))
                {
                    EditorGUI.LabelField(rect, currentPath, EditorStyles.miniLabel);
                }

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
            {
                StopListen();
            }
        }

        private static void OnListenApplyBinding(
            InputActionRebindingExtensions.RebindingOperation operation,
            string path)
        {
            if (listenTarget == null || string.IsNullOrEmpty(listenPropertyPath) || string.IsNullOrEmpty(path))
            {
                return;
            }

            listenTarget.Update();
            SerializedProperty property = listenTarget.FindProperty(listenPropertyPath);
            if (property != null)
            {
                RecordUndo(listenTarget, "监听输入绑定");
                property.stringValue = path;
                listenTarget.ApplyModifiedProperties();
                MarkDirty(listenTarget);
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

        internal static void CleanupGlobalListenState()
        {
            StopListen();
        }

        private static Rect NextLine(ref Rect position)
        {
            Rect row = new Rect(position.x, position.y, position.width, Line);
            position.y += Line + Gap;
            return row;
        }

        private static Rect NextHintLine(ref Rect position)
        {
            Rect row = new Rect(position.x + 50f, position.y - Gap, Mathf.Max(0f, position.width - 54f), HintLine);
            position.y += HintLine + Gap;
            return row;
        }

        private static void DrawSplit(Rect row, float leftRatio, out Rect left, out Rect right)
        {
            float leftWidth = Mathf.Max(80f, row.width * leftRatio);
            left = new Rect(row.x, row.y, leftWidth - Gap * 0.5f, row.height);
            right = new Rect(row.x + leftWidth + Gap * 0.5f, row.y, row.width - leftWidth - Gap * 0.5f, row.height);
        }

        private static void RecordUndo(SerializedObject serializedObject, string undoName)
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            Undo.RecordObject(serializedObject.targetObject, undoName);
        }

        private static void MarkDirty(SerializedObject serializedObject)
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        private sealed class ESInputActionBindingImportWindow : EditorWindow
        {
            private SerializedObject targetObject;
            private string bindingPropertyPath;
            private Holder holder;
            private SerializedObject holderObject;
            private Vector2 scroll;

            public static void Open(SerializedObject targetObject, string bindingPropertyPath)
            {
                if (targetObject == null || string.IsNullOrEmpty(bindingPropertyPath))
                {
                    return;
                }

                ESInputActionBindingImportWindow window = CreateInstance<ESInputActionBindingImportWindow>();
                window.titleContent = new GUIContent("导入 InputAction 绑定");
                window.minSize = new Vector2(560f, 380f);
                window.targetObject = targetObject;
                window.bindingPropertyPath = bindingPropertyPath;
                window.holder = CreateInstance<Holder>();
                window.holder.action = BuildActionFromCurrentBinding(targetObject, bindingPropertyPath);
                window.holderObject = new SerializedObject(window.holder);
                window.ShowUtility();
            }

            private void OnDisable()
            {
                if (holder != null)
                {
                    if (holder.action != null)
                    {
                        holder.action.Dispose();
                    }

                    DestroyImmediate(holder);
                    holder = null;
                }
            }

            private void OnGUI()
            {
                if (holder == null || holderObject == null)
                {
                    Close();
                    return;
                }

                EditorGUILayout.LabelField("临时 InputAction", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("这个 InputAction 只用于配置辅助。使用 Unity 自带的 InputAction 绑定界面添加 Binding，然后从下方选择一条导入到当前 ES 绑定。", MessageType.Info);

                holderObject.Update();
                EditorGUILayout.PropertyField(holderObject.FindProperty("action"), new GUIContent("InputAction"), true);
                holderObject.ApplyModifiedProperties();

                EditorGUILayout.Space(6f);
                DrawBindingList();
            }

            private void DrawBindingList()
            {
                InputAction action = holder.action;
                if (action == null || action.bindings.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前 InputAction 没有 Binding。请先在上方添加绑定。", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("可导入 Binding", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string path = GetBindingPath(binding);
                        EditorGUILayout.LabelField(MakeBindingTitle(i, binding, path), GUILayout.MinWidth(340f));

                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                        {
                            if (GUILayout.Button("导入", GUILayout.Width(64f)))
                            {
                                ImportBinding(binding);
                            }
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            private void ImportBinding(InputBinding binding)
            {
                if (targetObject == null || string.IsNullOrEmpty(bindingPropertyPath))
                {
                    return;
                }

                string path = GetBindingPath(binding);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                UnityEngine.Object undoTarget = targetObject.targetObject;
                if (undoTarget != null)
                {
                    Undo.RecordObject(undoTarget, "导入 InputAction 绑定");
                }

                targetObject.Update();
                SerializedProperty bindingProperty = targetObject.FindProperty(bindingPropertyPath);
                if (bindingProperty == null)
                {
                    return;
                }

                SetRelativeString(bindingProperty, "path", path);
                SetRelativeString(bindingProperty, "schemeId", string.IsNullOrEmpty(binding.groups) ? GetRelativeString(bindingProperty, "schemeId") : binding.groups);
                SetRelativeEnum(bindingProperty, "source", ESInputBindingSource.InputSystem);
                SetRelativeString(bindingProperty, "virtualControlId", string.Empty);
                SetRelativeString(bindingProperty, "interactions", binding.interactions);
                SetRelativeString(bindingProperty, "processors", binding.processors);
                SetRelativeString(bindingProperty, "name", binding.name);

                SerializedProperty isComposite = bindingProperty.FindPropertyRelative("isComposite");
                if (isComposite != null)
                {
                    isComposite.boolValue = binding.isComposite;
                }

                SerializedProperty isPartOfComposite = bindingProperty.FindPropertyRelative("isPartOfComposite");
                if (isPartOfComposite != null)
                {
                    isPartOfComposite.boolValue = binding.isPartOfComposite;
                }

                targetObject.ApplyModifiedProperties();
                if (undoTarget != null)
                {
                    EditorUtility.SetDirty(undoTarget);
                }

                Close();
            }

            private static void SetRelativeString(SerializedProperty root, string relativePath, string value)
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                if (property != null)
                {
                    property.stringValue = value ?? string.Empty;
                }
            }

            private static string GetBindingPath(InputBinding binding)
            {
                if (!string.IsNullOrEmpty(binding.path))
                {
                    return binding.path;
                }

                return !string.IsNullOrEmpty(binding.effectivePath) ? binding.effectivePath : string.Empty;
            }

            private static string MakeBindingTitle(int index, InputBinding binding, string path)
            {
                string kind = binding.isComposite ? "组合" : binding.isPartOfComposite ? "组合部分" : "普通";
                string name = string.IsNullOrEmpty(binding.name) ? "未命名" : binding.name;
                return index + "  " + kind + "  " + name + "  " + path;
            }

            private static InputAction BuildActionFromCurrentBinding(SerializedObject targetObject, string bindingPropertyPath)
            {
                InputAction action = new InputAction("临时动作", InputActionType.Button);
                if (targetObject == null || string.IsNullOrEmpty(bindingPropertyPath))
                {
                    return action;
                }

                targetObject.Update();
                SerializedProperty bindingProperty = targetObject.FindProperty(bindingPropertyPath);
                if (bindingProperty == null)
                {
                    return action;
                }

                if (TryBuildActionFromSiblingBindings(action, targetObject, bindingPropertyPath))
                {
                    return action;
                }

                bool hasComposite = false;
                InputActionSetupExtensions.CompositeSyntax composite = default;
                AddBindingFromSerializedProperty(action, bindingProperty, ref composite, ref hasComposite);
                return action;
            }

            private static bool TryBuildActionFromSiblingBindings(InputAction action, SerializedObject targetObject, string bindingPropertyPath)
            {
                int markerIndex = bindingPropertyPath.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    return false;
                }

                string arrayPath = bindingPropertyPath.Substring(0, markerIndex);
                SerializedProperty arrayProperty = targetObject.FindProperty(arrayPath);
                if (arrayProperty == null || !arrayProperty.isArray || arrayProperty.arraySize == 0)
                {
                    return false;
                }

                bool hasComposite = false;
                InputActionSetupExtensions.CompositeSyntax composite = default;
                for (int i = 0; i < arrayProperty.arraySize; i++)
                {
                    SerializedProperty item = arrayProperty.GetArrayElementAtIndex(i);
                    AddBindingFromSerializedProperty(action, item, ref composite, ref hasComposite);
                }

                return action.bindings.Count > 0;
            }

            private static void AddBindingFromSerializedProperty(
                InputAction action,
                SerializedProperty bindingProperty,
                ref InputActionSetupExtensions.CompositeSyntax composite,
                ref bool hasComposite)
            {
                string path = GetRelativeString(bindingProperty, "path");
                string interactions = GetRelativeString(bindingProperty, "interactions");
                string processors = GetRelativeString(bindingProperty, "processors");
                string schemeId = GetRelativeString(bindingProperty, "schemeId");
                string name = GetRelativeString(bindingProperty, "name");
                bool isComposite = GetRelativeBool(bindingProperty, "isComposite");
                bool isPartOfComposite = GetRelativeBool(bindingProperty, "isPartOfComposite");

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (isComposite)
                {
                    composite = action.AddCompositeBinding(path, interactions, processors);
                    hasComposite = true;
                }
                else if (isPartOfComposite)
                {
                    if (!hasComposite)
                    {
                        composite = action.AddCompositeBinding("2DVector");
                        hasComposite = true;
                    }

                    composite.With(string.IsNullOrEmpty(name) ? "Part" : name, path, schemeId, processors);
                }
                else
                {
                    InputActionSetupExtensions.BindingSyntax binding = action.AddBinding(path, interactions, processors, schemeId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        binding.WithName(name);
                    }
                    hasComposite = false;
                }
            }

            private static string GetRelativeString(SerializedProperty root, string relativePath)
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                return property != null ? property.stringValue : string.Empty;
            }

            private static bool GetRelativeBool(SerializedProperty root, string relativePath)
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                return property != null && property.boolValue;
            }

            private static void SetRelativeEnum<T>(SerializedProperty root, string relativePath, T value) where T : Enum
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                if (property != null)
                    property.enumValueIndex = Convert.ToInt32(value);
            }

            private sealed class Holder : ScriptableObject
            {
                public InputAction action;
            }
        }
    }

    public sealed class ESInputBindingDefineDrawerLifecycleInitializer : EditorInvoker_Level2
    {
        private static bool registered;

        public override void InitInvoke()
        {
            if (registered)
            {
                AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
                EditorApplication.quitting -= CleanupBeforeEditorQuit;
                EditorApplication.playModeStateChanged -= CleanupOnPlayModeChanged;
            }

            registered = true;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.quitting += CleanupBeforeEditorQuit;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeChanged;
        }

        private static void CleanupBeforeAssemblyReload()
        {
            ESInputBindingDefineDrawer.CleanupGlobalListenState();
        }

        private static void CleanupBeforeEditorQuit()
        {
            ESInputBindingDefineDrawer.CleanupGlobalListenState();
        }

        private static void CleanupOnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ESInputBindingDefineDrawer.CleanupGlobalListenState();
        }
    }
}
