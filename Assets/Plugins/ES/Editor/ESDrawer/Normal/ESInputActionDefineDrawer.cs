using System;
using System.Collections.Generic;
using System.IO;
using ES;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ES.EditorInternal
{
    [CustomPropertyDrawer(typeof(ESInputActionDefine))]
    public sealed class ESInputActionDefineDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 4f;
        private const string ActionIdFilePath = "Assets/Plugins/ES/1_Design/Input/ENUM_ESInputActionId.cs";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!HasRequiredProperties(property))
                return EditorGUIUtility.singleLineHeight * 2f + Gap;

            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty triggerFeatures = property.FindPropertyRelative("triggerFeatures");
            SerializedProperty bindings = property.FindPropertyRelative("bindings");

            float height = Line * 5f + Gap * 6f;
            if (IsButton(valueType))
            {
                height += Line + Gap;
                height += Line + Gap;
                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.LongPress))
                {
                    height += Line + Gap;
                }

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.DoublePress))
                {
                    height += Line + Gap;
                }
            }

            if (bindings != null)
            {
                height += EditorGUI.GetPropertyHeight(bindings, true) + Gap;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (!HasRequiredProperties(property))
            {
                Rect errorRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(errorRect, "ESInputActionDefine 的序列化结构不完整，请重新导入或迁移该资产。", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            SerializedProperty id = property.FindPropertyRelative("id");
            SerializedProperty actionName = property.FindPropertyRelative("actionName");
            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty category = property.FindPropertyRelative("category");
            SerializedProperty allowRebind = property.FindPropertyRelative("allowRebind");
            SerializedProperty displayName = property.FindPropertyRelative("displayName");
            SerializedProperty triggerFeatures = property.FindPropertyRelative("triggerFeatures");
            SerializedProperty pressPolicy = property.FindPropertyRelative("pressPolicy");
            SerializedProperty longPressDuration = property.FindPropertyRelative("longPressDuration");
            SerializedProperty doublePressWindow = property.FindPropertyRelative("doublePressWindow");
            SerializedProperty bindings = property.FindPropertyRelative("bindings");

            EditorGUI.LabelField(NextLine(ref position), label, EditorStyles.boldLabel);

            Rect row = NextLine(ref position);
            DrawSplit(row, 0.28f, out Rect left, out Rect right);
            DrawActionIdField(left, id);
            EditorGUI.PropertyField(right, actionName, new GUIContent("内部名"));

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            EditorGUI.PropertyField(left, valueType, new GUIContent("值类型"));
            EditorGUI.PropertyField(right, category, new GUIContent("分类"));

            row = NextLine(ref position);
            DrawSplit(row, 0.5f, out left, out right);
            EditorGUI.PropertyField(left, allowRebind, new GUIContent("允许改键"));
            EditorGUI.PropertyField(right, displayName, new GUIContent("显示名"));

            row = NextLine(ref position);
            DrawInputActionTools(row, property);

            if (IsButton(valueType))
            {
                row = NextLine(ref position);
                DrawTriggerFeatures(row, triggerFeatures);

                row = NextLine(ref position);
                EditorGUI.PropertyField(row, pressPolicy, new GUIContent("短按策略"));

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.LongPress))
                {
                    row = NextLine(ref position);
                    EditorGUI.PropertyField(row, longPressDuration, new GUIContent("长按秒数"));
                }

                if (HasTriggerFeature(triggerFeatures, ESInputTriggerFeature.DoublePress))
                {
                    row = NextLine(ref position);
                    EditorGUI.PropertyField(row, doublePressWindow, new GUIContent("双击窗口"));
                }
            }

            if (bindings != null)
            {
                float bindingsHeight = EditorGUI.GetPropertyHeight(bindings, true);
                Rect bindingsRect = new Rect(position.x, position.y, position.width, bindingsHeight);
                EditorGUI.PropertyField(bindingsRect, bindings, new GUIContent("绑定列表"), true);
            }

            EditorGUI.EndProperty();
        }

        private static bool HasRequiredProperties(SerializedProperty property)
        {
            if (property == null)
                return false;

            return property.FindPropertyRelative("id") != null
                && property.FindPropertyRelative("actionName") != null
                && property.FindPropertyRelative("valueType") != null
                && property.FindPropertyRelative("category") != null
                && property.FindPropertyRelative("allowRebind") != null
                && property.FindPropertyRelative("displayName") != null
                && property.FindPropertyRelative("triggerFeatures") != null
                && property.FindPropertyRelative("pressPolicy") != null
                && property.FindPropertyRelative("longPressDuration") != null
                && property.FindPropertyRelative("doublePressWindow") != null;
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

        private static void DrawActionIdField(Rect rect, SerializedProperty id)
        {
            Rect fieldRect = new Rect(rect.x, rect.y, Mathf.Max(40f, rect.width - 44f), rect.height);
            Rect jumpRect = new Rect(fieldRect.xMax + Gap, rect.y, 40f, rect.height);

            EditorGUI.PropertyField(fieldRect, id, new GUIContent("动作"));
            using (new EditorGUI.DisabledScope(id == null || id.enumValueIndex < 0 || id.enumValueIndex >= id.enumNames.Length))
            {
                if (GUI.Button(jumpRect, "定义", EditorStyles.miniButton))
                {
                    JumpToActionIdDefine(id);
                }
            }
        }

        private static void DrawInputActionTools(Rect row, SerializedProperty actionProperty)
        {
            Rect labelRect = new Rect(row.x, row.y, 72f, row.height);
            Rect openRect = new Rect(labelRect.xMax + Gap, row.y, 138f, row.height);
            Rect hintRect = new Rect(openRect.xMax + Gap, row.y, row.xMax - openRect.xMax - Gap, row.height);

            EditorGUI.LabelField(labelRect, "绑定辅助");
            if (GUI.Button(openRect, "打开 InputAction", EditorStyles.miniButton))
            {
                ESInputActionImportWindow.Open(actionProperty.serializedObject, actionProperty.propertyPath);
            }

            EditorGUI.LabelField(hintRect, "用 Unity 原生绑定界面编辑，再导回 ES 配置", EditorStyles.miniLabel);
        }

        private static void JumpToActionIdDefine(SerializedProperty id)
        {
            if (id == null || id.enumValueIndex < 0 || id.enumValueIndex >= id.enumNames.Length)
            {
                return;
            }

            string enumName = id.enumNames[id.enumValueIndex];
            int line = FindEnumMemberLine(ActionIdFilePath, enumName);
            ESDesignUtility.SafeEditor.OpenCodeAtLine(ActionIdFilePath, line);
        }

        private static int FindEnumMemberLine(string assetPath, string enumName)
        {
            if (string.IsNullOrEmpty(enumName))
            {
                return 1;
            }

            string fullPath = ToFullProjectPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return 1;
            }

            string[] lines = File.ReadAllLines(fullPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith(enumName, StringComparison.Ordinal) &&
                    (line.Length == enumName.Length || !IsIdentifierPart(line[enumName.Length])))
                {
                    return i + 1;
                }
            }

            return 1;
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string ToFullProjectPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static bool IsButton(SerializedProperty valueType)
        {
            return valueType != null && valueType.enumValueIndex == (int)ESInputValueType.Button;
        }

        private static bool HasTriggerFeature(SerializedProperty triggerFeatures, ESInputTriggerFeature feature)
        {
            return triggerFeatures != null && (triggerFeatures.intValue & (int)feature) != 0;
        }

        private static void DrawTriggerFeatures(Rect row, SerializedProperty triggerFeatures)
        {
            Rect labelRect = new Rect(row.x, row.y, 56f, row.height);
            Rect content = new Rect(row.x + 58f, row.y, row.width - 58f, row.height);
            EditorGUI.LabelField(labelRect, "触发");

            ESInputTriggerFeature current = triggerFeatures == null
                ? ESInputTriggerFeature.None
                : (ESInputTriggerFeature)triggerFeatures.intValue;

            const int count = 5;
            float itemWidth = Mathf.Max(52f, content.width / count);
            DrawTriggerToggle(content, 0, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Pressed, "点击");
            DrawTriggerToggle(content, 1, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Released, "松开");
            DrawTriggerToggle(content, 2, itemWidth, current, triggerFeatures, ESInputTriggerFeature.Held, "按住");
            DrawTriggerToggle(content, 3, itemWidth, current, triggerFeatures, ESInputTriggerFeature.LongPress, "长按");
            DrawTriggerToggle(content, 4, itemWidth, current, triggerFeatures, ESInputTriggerFeature.DoublePress, "双击");
        }

        private static void DrawTriggerToggle(
            Rect content,
            int index,
            float itemWidth,
            ESInputTriggerFeature current,
            SerializedProperty triggerFeatures,
            ESInputTriggerFeature feature,
            string text)
        {
            Rect rect = new Rect(content.x + itemWidth * index, content.y, itemWidth - 2f, content.height);
            bool next = GUI.Toggle(rect, (current & feature) != 0, text, EditorStyles.miniButton);
            if (triggerFeatures == null)
            {
                return;
            }

            if (next != ((current & feature) != 0))
            {
                int value = triggerFeatures.intValue;
                if (next)
                {
                    value |= (int)feature;
                }
                else
                {
                    value &= ~(int)feature;
                }

                triggerFeatures.intValue = value;
            }
        }

        [ESWindowSleepContract(
            ESWindowSleepMode.Transient,
            ESWindowSurfaceKind.Utility,
            "Unity 原生 InputAction 导入器由调用方控制生命周期")]
        private sealed class ESInputActionImportWindow : ESSinglePageIMGUIWindow<ESInputActionImportWindow>
        {
            // Native InputAction importer is a modal utility surface. It is
            // owner-scoped and must not acquire an independent sleep slot.
            protected override bool ESWindow_SupportsSemiSleep => false;

            private SerializedObject targetObject;
            private string actionPropertyPath;
            private Holder holder;
            private SerializedObject holderObject;
            private string importSchemeId = ESInputSchemeIds.KeyboardMouse;
            private Vector2 scroll;

            public static void Open(SerializedObject targetObject, string actionPropertyPath)
            {
                if (targetObject == null || string.IsNullOrEmpty(actionPropertyPath))
                    return;
                UnityEngine.Object[] targets;
                try { targets = targetObject.targetObjects; }
                catch { return; }
                if (targets == null || targets.Length != 1 || targets[0] == null)
                {
                    Debug.LogWarning("[ESInputAction] 导入窗口仅支持单对象编辑，已拒绝多对象或失效目标。");
                    return;
                }

                ESInputActionImportWindow window = GetWindow<ESInputActionImportWindow>(
                    true,
                    "InputAction 绑定辅助",
                    true);
                window.ReleaseTemporaryInputAction();
                window.titleContent = new GUIContent("InputAction 绑定辅助");
                window.minSize = new Vector2(640f, 440f);
                window.maxSize = new Vector2(1400f, 1000f);
                window.targetObject = targetObject;
                window.actionPropertyPath = actionPropertyPath;
                try
                {
                    window.holder = CreateInstance<Holder>();
                    window.holder.action = window.BuildActionFromCurrentConfig();
                    window.holderObject = new SerializedObject(window.holder);
                    window.ShowUtility();
                }
                catch (Exception exception)
                {
                    window.ReleaseTemporaryInputAction();
                    window.targetObject = null;
                    window.actionPropertyPath = null;
                    Debug.LogException(new InvalidOperationException(
                        "ES InputAction 辅助窗口初始化失败，已回收临时对象。", exception));
                    window.Close();
                }
            }

            public override GUIContent ESWindow_GetWindowGUIContent()
            {
                return new GUIContent("InputAction 绑定辅助", "用 Unity 原生 InputAction 绘制器生成 ES 输入绑定");
            }

            protected override string ESWindow_Subtitle => "临时 InputAction 到 ES 绑定列表";
            protected override Vector2 ESWindow_MinSize => new Vector2(640f, 440f);
            protected override Vector2 ESWindow_DefaultSize => new Vector2(760f, 620f);
            protected override string ESWindow_PageStableId => "input.action-import";
            protected override string ESWindow_PageTitle => "InputAction 绑定辅助";
            protected override string ESWindow_PageKeywords => "InputAction 输入 Binding 导入 覆盖 追加";

            protected override void ESWindow_BuildPageActions(
                ICollection<ESMenuTreePageAction> actions)
            {
                actions.Add(new ESMenuTreePageAction(
                        "input-action.rebuild",
                        "从 ES 重建",
                        "从当前 ES 输入动作配置重新生成临时 InputAction。",
                        context =>
                        {
                            InputAction rebuiltAction = null;
                            SerializedObject rebuiltObject = null;
                            try
                            {
                                rebuiltAction = BuildActionFromCurrentConfig();
                                rebuiltObject = new SerializedObject(holder);
                                InputAction previousAction = holder.action;
                                SerializedObject previousObject = holderObject;
                                holder.action = rebuiltAction;
                                holderObject = rebuiltObject;
                                rebuiltAction = null;
                                rebuiltObject = null;
                                previousAction?.Dispose();
                                previousObject?.Dispose();
                                context.RefreshPageActions();
                                context.SetStatus("已从 ES 配置重建临时 InputAction");
                                Repaint();
                            }
                            catch (Exception exception)
                            {
                                rebuiltAction?.Dispose();
                                rebuiltObject?.Dispose();
                                context.SetStatus("重建临时 InputAction 失败，已保留旧状态。", ESMenuTreePageStatus.Error);
                                Debug.LogException(new InvalidOperationException(
                                    "ES InputAction 辅助窗口重建失败。", exception));
                            }
                        })
                    .When(() => holder != null && targetObject != null)
                    .WithUnityIcon("Refresh")
                    .WithPriority(100));
                actions.Add(new ESMenuTreePageAction(
                        "input-action.overwrite",
                        "覆盖绑定",
                        "清空当前 ES 绑定列表，并用临时 InputAction 全部重建。",
                        _ =>
                        {
                            if (EditorUtility.DisplayDialog(
                                    "确认覆盖绑定列表",
                                    "这会清空当前动作的 ES 绑定列表，并用上方 InputAction 的全部 Binding 重建。\n\n继续吗？",
                                    "覆盖",
                                    "取消"))
                            {
                                ImportAllBindings(clearOld: true);
                            }
                        })
                    .When(CanImportBindings)
                    .WithUnityIcon("SaveAs")
                    .WithPriority(90));
                actions.Add(new ESMenuTreePageAction(
                        "input-action.append",
                        "追加绑定",
                        "把临时 InputAction 的全部 Binding 追加到当前 ES 动作。",
                        _ => ImportAllBindings(clearOld: false))
                    .When(CanImportBindings)
                    .WithUnityIcon("Toolbar Plus")
                    .WithPriority(80));
            }

            protected override void ESWindow_OnHostDisable()
            {
                ReleaseTemporaryInputAction();
            }

            private void ReleaseTemporaryInputAction()
            {
                SerializedObject serializedHolder = holderObject;
                holderObject = null;
                try
                {
                    serializedHolder?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "ES InputAction 辅助窗口释放序列化视图失败。", exception));
                }

                Holder holderToRelease = holder;
                holder = null;
                if (holderToRelease != null)
                {
                    try
                    {
                        holderToRelease.action?.Dispose();
                    }
                    finally
                    {
                        DestroyImmediate(holderToRelease);
                    }
                }
            }

            protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
            {
                if (holder == null || holderObject == null)
                {
                    Close();
                    return;
                }

                EditorGUILayout.LabelField("InputAction 辅助配置", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("这里使用 Unity 原生 InputAction 绘制器生成合法 binding。它只作为配置工具，不会成为运行时主数据源。", MessageType.Info);

                try
                {
                    holderObject.Update();
                    SerializedProperty actionProperty = holderObject.FindProperty("action");
                    if (actionProperty == null)
                        throw new InvalidOperationException("临时 InputAction 序列化字段不存在。");
                    EditorGUILayout.PropertyField(actionProperty, new GUIContent("临时 InputAction"), true);
                    holderObject.ApplyModifiedProperties();
                }
                catch (ExitGUIException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    context.SetStatus("临时 InputAction 已失效，窗口将安全关闭。", ESMenuTreePageStatus.Error);
                    Debug.LogException(new InvalidOperationException(
                        "ES InputAction 辅助窗口绘制失败。", exception));
                    ReleaseTemporaryInputAction();
                    Close();
                    return;
                }

                EditorGUILayout.Space(4f);
                importSchemeId = EditorGUILayout.TextField("导入方案", importSchemeId);

                DrawPreview();
            }

            private bool CanImportBindings()
            {
                return HasLiveTarget() && holder?.action != null && holder.action.bindings.Count > 0;
            }

            private InputAction BuildActionFromCurrentConfig()
            {
                if (!HasLiveTarget())
                    return new InputAction("临时动作", InputActionType.Button);

                targetObject.Update();
                SerializedProperty actionProperty = targetObject.FindProperty(actionPropertyPath);
                if (actionProperty == null)
                    return new InputAction("临时动作", InputActionType.Button);

                string actionName = GetRelativeString(actionProperty, "actionName");
                ESInputValueType valueType = GetRelativeEnum<ESInputValueType>(actionProperty, "valueType");
                InputAction action = new InputAction(
                    string.IsNullOrEmpty(actionName) ? "临时动作" : actionName,
                    ToInputActionType(valueType));

                SerializedProperty bindings = actionProperty.FindPropertyRelative("bindings");
                if (bindings == null || !bindings.isArray)
                    return action;

                InputActionSetupExtensions.CompositeSyntax composite = default;
                bool hasComposite = false;
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    AddBindingFromSerializedProperty(action, bindings.GetArrayElementAtIndex(i), ref composite, ref hasComposite);
                }

                return action;
            }

            private void ImportAllBindings(bool clearOld)
            {
                if (!HasLiveTarget() || holder?.action == null)
                    return;

                targetObject.Update();
                SerializedProperty actionProperty = targetObject.FindProperty(actionPropertyPath);
                SerializedProperty bindingsProperty = actionProperty?.FindPropertyRelative("bindings");
                if (bindingsProperty == null || !bindingsProperty.isArray)
                    return;

                UnityEngine.Object[] targets = targetObject.targetObjects;
                if (targets == null || targets.Length == 0)
                    return;
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] == null)
                        return;
                Undo.RecordObjects(targets, clearOld ? "覆盖 ES 输入绑定" : "追加 ES 输入绑定");

                if (clearOld)
                    bindingsProperty.ClearArray();

                for (int i = 0; i < holder.action.bindings.Count; i++)
                {
                    InputBinding binding = holder.action.bindings[i];
                    string path = GetBindingPath(binding);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    int index = bindingsProperty.arraySize;
                    bindingsProperty.InsertArrayElementAtIndex(index);
                    SerializedProperty item = bindingsProperty.GetArrayElementAtIndex(index);
                    WriteBindingToSerializedProperty(item, binding);
                }

                targetObject.ApplyModifiedProperties();
                foreach (UnityEngine.Object target in targets)
                {
                    EditorUtility.SetDirty(target);
                    if (PrefabUtility.IsPartOfPrefabInstance(target))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
                Close();
            }

            private bool HasLiveTarget()
            {
                if (targetObject == null || string.IsNullOrEmpty(actionPropertyPath))
                    return false;
                try
                {
                    UnityEngine.Object[] targets = targetObject.targetObjects;
                    if (targets == null || targets.Length == 0)
                        return false;
                    for (int i = 0; i < targets.Length; i++)
                        if (targets[i] == null)
                            return false;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private void DrawPreview()
            {
                InputAction action = holder.action;
                if (action == null)
                    return;

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("将导入的 Binding", EditorStyles.boldLabel);
                if (action.bindings.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前 InputAction 没有 Binding。请先在上方添加。", MessageType.Warning);
                    return;
                }

                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    EditorGUILayout.LabelField(MakeBindingTitle(i, binding), EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }

            private static void AddBindingFromSerializedProperty(
                InputAction action,
                SerializedProperty bindingProperty,
                ref InputActionSetupExtensions.CompositeSyntax composite,
                ref bool hasComposite)
            {
                ESInputBindingSource source = GetRelativeEnum<ESInputBindingSource>(bindingProperty, "source");
                if (source != ESInputBindingSource.InputSystem)
                    return;

                string path = GetRelativeString(bindingProperty, "path");
                string interactions = GetRelativeString(bindingProperty, "interactions");
                string processors = GetRelativeString(bindingProperty, "processors");
                string schemeId = GetRelativeString(bindingProperty, "schemeId");
                string name = GetRelativeString(bindingProperty, "name");
                bool isComposite = GetRelativeBool(bindingProperty, "isComposite");
                bool isPartOfComposite = GetRelativeBool(bindingProperty, "isPartOfComposite");

                if (string.IsNullOrEmpty(path))
                    return;

                if (isComposite)
                {
                    composite = action.AddCompositeBinding(path, interactions, processors);
                    hasComposite = true;
                    return;
                }

                if (isPartOfComposite)
                {
                    if (!hasComposite)
                    {
                        composite = action.AddCompositeBinding("2DVector");
                        hasComposite = true;
                    }

                    composite.With(string.IsNullOrEmpty(name) ? "Part" : name, path, schemeId, processors);
                    return;
                }

                action.AddBinding(path, interactions, processors, schemeId);
                hasComposite = false;
            }

            private void WriteBindingToSerializedProperty(SerializedProperty item, InputBinding binding)
            {
                SetRelativeEnum(item, "source", ESInputBindingSource.InputSystem);
                SetRelativeString(item, "schemeId", string.IsNullOrEmpty(binding.groups) ? importSchemeId : binding.groups);
                SetRelativeString(item, "path", GetBindingPath(binding));
                SetRelativeString(item, "virtualControlId", string.Empty);
                SetRelativeString(item, "interactions", binding.interactions);
                SetRelativeString(item, "processors", binding.processors);
                SetRelativeString(item, "name", binding.name);
                SetRelativeBool(item, "isComposite", binding.isComposite);
                SetRelativeBool(item, "isPartOfComposite", binding.isPartOfComposite);
            }

            private static string MakeBindingTitle(int index, InputBinding binding)
            {
                string kind = binding.isComposite ? "组合" : binding.isPartOfComposite ? "组合子项" : "普通";
                string name = string.IsNullOrEmpty(binding.name) ? "未命名" : binding.name;
                string groups = string.IsNullOrEmpty(binding.groups) ? "未指定方案" : binding.groups;
                return $"{index}  {kind}  {name}  {GetBindingPath(binding)}  [{groups}]";
            }

            private static string GetBindingPath(InputBinding binding)
            {
                return !string.IsNullOrEmpty(binding.path) ? binding.path : binding.effectivePath;
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

            private static T GetRelativeEnum<T>(SerializedProperty root, string relativePath) where T : Enum
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                return property != null ? (T)Enum.ToObject(typeof(T), property.enumValueIndex) : default;
            }

            private static void SetRelativeString(SerializedProperty root, string relativePath, string value)
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                if (property != null)
                    property.stringValue = value ?? string.Empty;
            }

            private static void SetRelativeBool(SerializedProperty root, string relativePath, bool value)
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                if (property != null)
                    property.boolValue = value;
            }

            private static void SetRelativeEnum<T>(SerializedProperty root, string relativePath, T value) where T : Enum
            {
                SerializedProperty property = root.FindPropertyRelative(relativePath);
                if (property != null)
                    property.enumValueIndex = Convert.ToInt32(value);
            }

            private static InputActionType ToInputActionType(ESInputValueType valueType)
            {
                return valueType == ESInputValueType.Button ? InputActionType.Button : InputActionType.Value;
            }

            private sealed class Holder : ScriptableObject
            {
                public InputAction action;
            }
        }
    }
}
