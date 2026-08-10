using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 用于少量结构化输入的通用 Editor 对话框。
    /// 它只收集并校验输入，不执行命令、不读写资产、不授予权限。
    /// </summary>
    public enum ESAdvancedDialogFieldKind : byte
    {
        Text,
        MultilineText,
        Toggle,
        Choice,
        FolderPath,
        FilePath,
        Object,
    }

    public sealed class ESAdvancedDialogField
    {
        public string id;
        public string label;
        public string help;
        public ESAdvancedDialogFieldKind kind;
        public string stringValue;
        public bool boolValue;
        public UnityEngine.Object objectValue;
        public Type objectType;
        public bool allowSceneObjects;
        public bool required;
        public bool readOnly;
        public string fileExtension;
        public string browseStartDirectory;
        // choices 用于显示，choiceValues 是提交给调用方的稳定值。两者分离后，中文文案可以改动而不破坏协议。
        public readonly List<string> choices = new List<string>();
        public readonly List<string> choiceValues = new List<string>();

        public ESAdvancedDialogField(string id, string label, ESAdvancedDialogFieldKind kind)
        {
            this.id = id;
            this.label = label;
            this.kind = kind;
        }
    }

    /// <summary>选择控件的稳定值与显示名称；稳定值是调用方收到的唯一值。</summary>
    public sealed class ESAdvancedDialogChoiceOption
    {
        public string id;
        public string label;

        public ESAdvancedDialogChoiceOption(string id, string label)
        {
            this.id = id;
            this.label = label;
        }
    }

    public sealed class ESAdvancedDialogValues
    {
        private readonly Dictionary<string, string> strings;
        private readonly Dictionary<string, bool> toggles;
        private readonly Dictionary<string, UnityEngine.Object> objects;

        internal ESAdvancedDialogValues(
            Dictionary<string, string> strings,
            Dictionary<string, bool> toggles,
            Dictionary<string, UnityEngine.Object> objects)
        {
            this.strings = strings;
            this.toggles = toggles;
            this.objects = objects;
        }

        public string GetString(string id, string fallback = "")
            => strings != null && strings.TryGetValue(id, out string value) ? value : fallback;

        public bool GetToggle(string id, bool fallback = false)
            => toggles != null && toggles.TryGetValue(id, out bool value) ? value : fallback;

        public T GetObject<T>(string id) where T : UnityEngine.Object
            => objects != null && objects.TryGetValue(id, out UnityEngine.Object value) ? value as T : null;
    }

    public sealed class ESAdvancedDialogResult
    {
        public bool accepted;
        public ESAdvancedDialogValues values;
    }

    public sealed class ESAdvancedDialogRequest
    {
        public string title = "ES 输入";
        public string message = string.Empty;
        public string detail = string.Empty;
        public string confirmText = "确定";
        public string cancelText = "取消";
        public Vector2 minSize = new Vector2(460f, 260f);
        public readonly List<ESAdvancedDialogField> fields = new List<ESAdvancedDialogField>();

        /// <summary>返回空字符串表示通过；必须是无副作用的快速校验。</summary>
        public Func<ESAdvancedDialogValues, string> validate;
        public Action<ESAdvancedDialogResult> completed;

        public ESAdvancedDialogField AddText(string id, string label, string defaultValue = "", bool required = false)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Text)
            {
                stringValue = defaultValue,
                required = required,
            };
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddMultilineText(string id, string label, string defaultValue = "", bool required = false)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.MultilineText)
            {
                stringValue = defaultValue,
                required = required,
            };
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddToggle(string id, string label, bool defaultValue = false)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Toggle)
            {
                boolValue = defaultValue,
            };
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddChoice(string id, string label, IEnumerable<string> choices, string defaultValue = "", bool required = true)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Choice)
            {
                stringValue = defaultValue,
                required = required,
            };
            if (choices != null)
            {
                foreach (string choice in choices.Where(value => !string.IsNullOrWhiteSpace(value)))
                {
                    field.choices.Add(choice);
                    field.choiceValues.Add(choice);
                }
            }
            fields.Add(field);
            return field;
        }

        /// <summary>
        /// 添加带稳定提交值的选择项。显示文本可本地化或调整，提交值必须保持不变。
        /// </summary>
        public ESAdvancedDialogField AddChoiceOptions(string id, string label, IEnumerable<ESAdvancedDialogChoiceOption> options, string defaultValue = "", bool required = true)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Choice)
            {
                stringValue = defaultValue,
                required = required,
            };
            if (options != null)
            {
                foreach (ESAdvancedDialogChoiceOption option in options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.id) || string.IsNullOrWhiteSpace(option.label)) continue;
                    field.choiceValues.Add(option.id);
                    field.choices.Add(option.label);
                }
            }
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddFolderPath(string id, string label, string defaultValue = "", bool required = false)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.FolderPath)
            {
                stringValue = defaultValue,
                required = required,
            };
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddFilePath(string id, string label, string fileExtension = "", string defaultValue = "", bool required = false)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.FilePath)
            {
                stringValue = defaultValue,
                fileExtension = fileExtension,
                required = required,
            };
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddObject<T>(string id, string label, T defaultValue = null, bool required = false, bool allowSceneObjects = false)
            where T : UnityEngine.Object
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Object)
            {
                objectValue = defaultValue,
                objectType = typeof(T),
                allowSceneObjects = allowSceneObjects,
                required = required,
            };
            fields.Add(field);
            return field;
        }
    }

    public sealed class ESAdvancedDialogWindow : EditorWindow
    {
        private ESAdvancedDialogRequest request;
        private Vector2 scrollPosition;
        private string validationMessage = string.Empty;
        private bool initialized;
        private bool completed;

        /// <summary>
        /// 打开一个独立的 Utility 窗口。调用方只能读取确认结果；任何业务动作必须由 completed 回调之后的调用方自行执行。
        /// </summary>
        public static ESAdvancedDialogWindow Show(ESAdvancedDialogRequest request)
        {
            ValidateRequest(request);
            var window = CreateInstance<ESAdvancedDialogWindow>();
            window.Initialize(request);
            window.titleContent = new GUIContent(request.title);
            window.minSize = request.minSize;
            window.ShowUtility();
            window.Focus();
            return window;
        }

        private void Initialize(ESAdvancedDialogRequest value)
        {
            request = value;
            initialized = true;
            RefreshValidation();
        }

        private void OnGUI()
        {
            if (!initialized || request == null) return;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(8f);
            if (!string.IsNullOrWhiteSpace(request.message))
                EditorGUILayout.HelpBox(request.message, MessageType.Info);
            if (!string.IsNullOrWhiteSpace(request.detail))
                EditorGUILayout.LabelField(request.detail, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            foreach (ESAdvancedDialogField field in request.fields) DrawField(field);
            if (EditorGUI.EndChangeCheck())
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Type);
                RefreshValidation();
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrWhiteSpace(validationMessage))
                EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(request.cancelText, GUILayout.MinWidth(96f), GUILayout.Height(26f)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                    Complete(false);
                }
                using (new EditorGUI.DisabledScope(!string.IsNullOrWhiteSpace(validationMessage)))
                {
                    if (GUILayout.Button(request.confirmText, GUILayout.MinWidth(96f), GUILayout.Height(26f)))
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                        Complete(true);
                    }
                }
            }
            EditorGUILayout.Space(6f);
        }

        private void DrawField(ESAdvancedDialogField field)
        {
            using (new EditorGUI.DisabledScope(field.readOnly))
            {
                switch (field.kind)
                {
                    case ESAdvancedDialogFieldKind.Text:
                        field.stringValue = EditorGUILayout.TextField(field.label, field.stringValue ?? string.Empty);
                        break;
                    case ESAdvancedDialogFieldKind.MultilineText:
                        EditorGUILayout.LabelField(field.label);
                        field.stringValue = EditorGUILayout.TextArea(field.stringValue ?? string.Empty, GUILayout.MinHeight(64f));
                        break;
                    case ESAdvancedDialogFieldKind.Toggle:
                        field.boolValue = EditorGUILayout.Toggle(field.label, field.boolValue);
                        break;
                    case ESAdvancedDialogFieldKind.Choice:
                        int selectedIndex = Mathf.Max(0, field.choiceValues.IndexOf(field.stringValue));
                        selectedIndex = EditorGUILayout.Popup(field.label, selectedIndex, field.choices.ToArray());
                        field.stringValue = field.choiceValues[selectedIndex];
                        break;
                    case ESAdvancedDialogFieldKind.FolderPath:
                        DrawPathField(field, true);
                        break;
                    case ESAdvancedDialogFieldKind.FilePath:
                        DrawPathField(field, false);
                        break;
                    case ESAdvancedDialogFieldKind.Object:
                        field.objectValue = EditorGUILayout.ObjectField(field.label, field.objectValue, field.objectType, field.allowSceneObjects);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (!string.IsNullOrWhiteSpace(field.help))
                EditorGUILayout.LabelField(field.help, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);
        }

        private static void DrawPathField(ESAdvancedDialogField field, bool folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                field.stringValue = EditorGUILayout.TextField(field.label, field.stringValue ?? string.Empty);
                if (GUILayout.Button("选择…", GUILayout.Width(58f)))
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    string startDirectory = string.IsNullOrWhiteSpace(field.browseStartDirectory) ? Application.dataPath : field.browseStartDirectory;
                    string selected = folder
                        ? EditorUtility.OpenFolderPanel(field.label, startDirectory, string.Empty)
                        : EditorUtility.OpenFilePanel(field.label, startDirectory, field.fileExtension ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected)) field.stringValue = selected;
                }
            }
        }

        private void RefreshValidation()
        {
            validationMessage = ValidateValues(BuildValues());
        }

        private string ValidateValues(ESAdvancedDialogValues values)
        {
            foreach (ESAdvancedDialogField field in request.fields)
            {
                if (field.required && field.kind == ESAdvancedDialogFieldKind.Object && field.objectValue == null)
                    return "“" + field.label + "”不能为空。";
                if (field.required && field.kind != ESAdvancedDialogFieldKind.Object && field.kind != ESAdvancedDialogFieldKind.Toggle && string.IsNullOrWhiteSpace(field.stringValue))
                    return "“" + field.label + "”不能为空。";
            }

            if (request.validate == null) return string.Empty;
            try
            {
                return request.validate.Invoke(values) ?? string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return "输入校验发生异常；请查看 Console。";
            }
        }

        private ESAdvancedDialogValues BuildValues()
        {
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            var toggles = new Dictionary<string, bool>(StringComparer.Ordinal);
            var objects = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            foreach (ESAdvancedDialogField field in request.fields)
            {
                switch (field.kind)
                {
                    case ESAdvancedDialogFieldKind.Toggle:
                        toggles.Add(field.id, field.boolValue);
                        break;
                    case ESAdvancedDialogFieldKind.Object:
                        objects.Add(field.id, field.objectValue);
                        break;
                    default:
                        strings.Add(field.id, field.stringValue ?? string.Empty);
                        break;
                }
            }
            return new ESAdvancedDialogValues(strings, toggles, objects);
        }

        private void Complete(bool accepted)
        {
            if (completed) return;
            completed = true;
            ESAdvancedDialogResult result = new ESAdvancedDialogResult
            {
                accepted = accepted,
                values = BuildValues(),
            };
            try
            {
                request.completed?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Close();
            }
        }

        private void OnDisable()
        {
            if (initialized && !completed) Complete(false);
        }

        private static void ValidateRequest(ESAdvancedDialogRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.title)) throw new ArgumentException("对话框标题不能为空。", nameof(request));
            if (request.fields == null || request.fields.Count == 0) throw new ArgumentException("高级对话框至少需要一个输入字段。", nameof(request));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAdvancedDialogField field in request.fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.id) || string.IsNullOrWhiteSpace(field.label))
                    throw new ArgumentException("每个输入字段都必须具备稳定 ID 和显示名称。", nameof(request));
                if (!ids.Add(field.id)) throw new ArgumentException("高级对话框存在重复字段 ID：" + field.id, nameof(request));
                if (field.kind == ESAdvancedDialogFieldKind.Choice)
                {
                    if (field.choices.Count == 0) throw new ArgumentException("选择字段必须提供至少一个选项：" + field.id, nameof(request));
                    if (field.choiceValues.Count != field.choices.Count)
                        throw new ArgumentException("选择字段的显示项与稳定值数量不一致：" + field.id, nameof(request));
                    var choiceValueIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string value in field.choiceValues)
                    {
                        if (string.IsNullOrWhiteSpace(value) || !choiceValueIds.Add(value))
                            throw new ArgumentException("选择字段包含空或重复稳定值：" + field.id, nameof(request));
                    }
                    if (!field.choiceValues.Contains(field.stringValue)) field.stringValue = field.choiceValues[0];
                }
                if (field.kind == ESAdvancedDialogFieldKind.Object && (field.objectType == null || !typeof(UnityEngine.Object).IsAssignableFrom(field.objectType)))
                    throw new ArgumentException("Object 字段必须指定 UnityEngine.Object 类型：" + field.id, nameof(request));
            }
        }
    }
}
