using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    public abstract class ESAgentPayloadInspector<T> : IESGraphPayloadInspector, IESGraphNodeCardProvider
        where T : class, new()
    {
        protected static readonly List<string> CardOperationLabels = new List<string>
        {
            "自动创建或更新",
            "仅创建",
            "仅更新"
        };

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public abstract ESGraphNodeTypeKey NodeType { get; }
        public virtual int Priority => 0;
        public VisualElement Create(string payloadJson, Action<string> commitPayload)
        {
            if (!ESAgentAuthoringGraphValidator.TryRead(payloadJson, out T payload, out _)) payload = new T();
            VisualElement root = Build(payload, () => commitPayload?.Invoke(JsonUtility.ToJson(payload)));
            ESGraphInspectorVisuals.StylePayloadRoot(root);
            return root;
        }

        public VisualElement CreateCard(ESGraphNodeCardContext context)
        {
            if (context == null)
                return null;
            if (!ESAgentAuthoringGraphValidator.TryRead(context.PayloadJson, out T payload, out _)) payload = new T();
            VisualElement root = BuildCard(payload, context,
                () => context.CommitPayload(JsonUtility.ToJson(payload)));
            if (root == null)
                return null;
            root.name = "es-node-key-fields";
            root.userData = context;
            root.style.marginTop = 3f;
            root.style.marginBottom = 4f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 4f;
            root.style.paddingLeft = 5f;
            root.style.paddingRight = 5f;
            root.style.backgroundColor = new Color(0.075f, 0.09f, 0.12f, 0.88f);
            root.style.borderTopWidth = 1f;
            root.style.borderBottomWidth = 1f;
            root.style.borderLeftWidth = 1f;
            root.style.borderRightWidth = 1f;
            Color border = new Color(0.25f, 0.31f, 0.39f, 0.9f);
            root.style.borderTopColor = border;
            root.style.borderBottomColor = border;
            root.style.borderLeftColor = border;
            root.style.borderRightColor = border;
            ESEditorPresentation.ApplyCornerRadius(
                root, ESEditorPresentation.ESCornerRadiusToken.Card);
            return root;
        }

        protected abstract VisualElement Build(T payload, Action commit);
        protected virtual VisualElement BuildCard(T payload, ESGraphNodeCardContext context, Action commit)
        {
            return null;
        }

        protected static TextField CardText(ESGraphNodeCardContext context, string name, string label,
            string value, string tooltip, Action<string> set, Action commit, string fieldName = null)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true,
                tooltip = tooltip ?? string.Empty
            };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.isReadOnly = !(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                string next = evt.newValue ?? string.Empty;
                set?.Invoke(next);
                commit?.Invoke();
            });
            return field;
        }

        protected static TextField CardReadOnlyText(string name, string label, string value, string tooltip,
            string fieldName = null)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isReadOnly = true,
                tooltip = tooltip ?? value ?? string.Empty
            };
            field.style.minHeight = 20f;
            field.style.fontSize = 10f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            return field;
        }

        protected static PopupField<string> CardPopup(ESGraphNodeCardContext context, string name, string label,
            List<string> choices, int selectedIndex, Action<int> set, Action commit, string fieldName = null)
        {
            var field = new PopupField<string>(label, choices,
                Mathf.Clamp(selectedIndex, 0, Math.Max(0, choices.Count - 1)))
            {
                name = name
            };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, false);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                set?.Invoke(Math.Max(0, choices.IndexOf(evt.newValue)));
                commit?.Invoke();
            });
            return field;
        }

        protected static Toggle CardToggle(ESGraphNodeCardContext context, string name, string label, bool value,
            Action<bool> set, Action commit, string fieldName = null)
        {
            var field = new Toggle(label) { name = name, value = value };
            field.style.minHeight = 20f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, !value);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                set?.Invoke(evt.newValue);
                commit?.Invoke();
            });
            return field;
        }

        protected static IntegerField CardInteger(ESGraphNodeCardContext context, string name, string label,
            int value, int min, int max, Action<int> set, Action commit, string fieldName = null)
        {
            var field = new IntegerField(label) { name = name, value = value, isDelayed = true };
            field.style.minHeight = 22f;
            field.style.fontSize = 11f;
            field.style.marginTop = 1f;
            field.style.marginBottom = 1f;
            field.labelElement.style.minWidth = 48f;
            field.labelElement.style.maxWidth = 64f;
            field.labelElement.style.fontSize = 9f;
            field.SetEnabled(context?.CanEditPayload ?? false);
            StyleField(field, field.labelElement, fieldName, false);
            field.RegisterValueChangedCallback(evt =>
            {
                if (context?.CanEditPayload != true)
                    return;
                int next = Mathf.Clamp(evt.newValue, min, max);
                field.SetValueWithoutNotify(next);
                set?.Invoke(next);
                commit?.Invoke();
            });
            return field;
        }

        protected static VisualElement CardPathActions(ESGraphNodeCardContext context, string elementName,
            Func<string> getPath)
        {
            var row = new VisualElement { name = elementName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 3f;

            Button copy = CardButton(elementName + "-copy", "复制路径", "复制完整项目路径。",
                () => context?.CopyText(getPath?.Invoke() ?? string.Empty));
            row.Add(copy);

            string initialPath = getPath?.Invoke() ?? string.Empty;
            Button locate = CardButton(elementName + "-locate", "定位", "在 Project 窗口定位当前项目资产。", () =>
            {
                string path = (getPath?.Invoke() ?? string.Empty).Replace('\\', '/');
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                    return;
                UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (target == null)
                    return;
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            });
            locate.SetEnabled(initialPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal));
            locate.style.marginLeft = 4f;
            row.Add(locate);
            return row;
        }

        protected static TextField CardArtifactStatus(string name, ESAgentArtifactKind kind,
            ESAgentArtifactOperationMode operationMode, string projectPath)
        {
            string status;
            string tooltip;
            if (!TryResolveArtifactPath(kind, projectPath, out string fullPath, out string error))
            {
                status = "路径非法 · " + error;
                tooltip = error;
            }
            else
            {
                bool exists = kind == ESAgentArtifactKind.AICommand
                    ? File.Exists(fullPath)
                    : Directory.Exists(fullPath);
                if (exists && operationMode == ESAgentArtifactOperationMode.CreateOnly)
                {
                    status = "已存在 · 仅创建将阻断";
                    tooltip = "目标已经存在，当前“仅创建”方式会在生成前阻断。";
                }
                else if (!exists && operationMode == ESAgentArtifactOperationMode.UpdateOnly)
                {
                    status = "尚未创建 · 仅更新将阻断";
                    tooltip = "目标尚不存在，当前“仅更新”方式会在生成前阻断。";
                }
                else
                {
                    status = exists ? "已存在 · 将更新" : "尚未创建 · 将新建";
                    tooltip = exists ? "目标路径当前存在。" : "目标路径当前不存在。";
                }
            }
            return CardReadOnlyText(name, "状态", status, tooltip);
        }

        protected static VisualElement CardArtifactActions(ESGraphNodeCardContext context, string elementName,
            ESAgentArtifactKind kind, Func<string> getPath, Func<string> getSuggestedPath, Action synchronizePath,
            Func<string> getInvocationToken = null)
        {
            var row = new VisualElement { name = elementName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 3f;

            string useLabel = kind == ESAgentArtifactKind.AICommand ? "单次使用" : "临时使用";
            Button use = CardButton(elementName + "-use", useLabel,
                kind == ESAgentArtifactKind.AICommand
                    ? "只执行当前 AICommand Output 对应的 Graph 分支，不生成永久产物。"
                    : "只在本次任务中使用当前 Agent Skill Output 对应的 Graph 分支，不安装技能。",
                () => context?.ExecuteNodeAction(ESAgentNodeCardActionKeys.UseOnce));
            use.SetEnabled(context?.CanExecuteNodeAction(ESAgentNodeCardActionKeys.UseOnce) ?? false);
            row.Add(use);

            Button candidate = CardButton(elementName + "-candidate", "生成候选",
                "只为当前 Output 及其 Goal、Reference、Constraint、Validation 关系分支创建隔离候选。",
                () => context?.ExecuteNodeAction(ESAgentNodeCardActionKeys.SaveCandidate));
            candidate.SetEnabled(context?.CanExecuteNodeAction(ESAgentNodeCardActionKeys.SaveCandidate) ?? false);
            candidate.style.marginLeft = 4f;
            row.Add(candidate);

            Button synchronize = CardButton(elementName + "-sync", kind == ESAgentArtifactKind.AICommand
                    ? "同步路径" : "同步目录",
                "按当前名称生成受支持的正式目标路径。", () =>
                {
                    if (context?.CanEditPayload != true)
                        return;
                    synchronizePath?.Invoke();
                });
            synchronize.SetEnabled((context?.CanEditPayload ?? false)
                && ESAgentArtifactPathPolicy.IsAllowedTarget(kind, getSuggestedPath?.Invoke(), out _));
            synchronize.style.marginLeft = 4f;
            row.Add(synchronize);

            if (getInvocationToken != null)
            {
                Button invocation = CardButton(elementName + "-invocation", "复制调用",
                    "复制该 Agent Skill 的调用标记。",
                    () => context?.CopyText(getInvocationToken() ?? string.Empty));
                invocation.style.marginLeft = 4f;
                row.Add(invocation);
            }

            Button copy = CardButton(elementName + "-copy", "复制路径", "复制完整项目路径。",
                () => context?.CopyText(getPath?.Invoke() ?? string.Empty));
            copy.style.marginLeft = 4f;
            row.Add(copy);

            Button locate = CardButton(elementName + "-locate",
                kind == ESAgentArtifactKind.AICommand ? "定位" : "打开目录",
                kind == ESAgentArtifactKind.AICommand
                    ? "在 Project 窗口定位文件；文件不存在时打开目标目录。"
                    : "在文件管理器中打开技能目录；目录不存在时打开它的安全父目录。",
                () => RevealArtifactPath(context, kind, getPath?.Invoke()));
            locate.SetEnabled(ESAgentArtifactPathPolicy.IsAllowedTarget(kind, getPath?.Invoke(), out _));
            locate.style.marginLeft = 4f;
            row.Add(locate);
            return row;
        }

        private static void RevealArtifactPath(ESGraphNodeCardContext context, ESAgentArtifactKind kind,
            string projectPath)
        {
            if (!TryResolveArtifactPath(kind, projectPath, out string fullPath, out string error))
            {
                context?.Report(error);
                return;
            }
            if (kind == ESAgentArtifactKind.AICommand && File.Exists(fullPath))
            {
                string assetPath = (projectPath ?? string.Empty).Replace('\\', '/').Trim();
                UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (target != null)
                {
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                    return;
                }
                EditorUtility.RevealInFinder(fullPath);
                return;
            }

            string revealPath = kind == ESAgentArtifactKind.AgentSkill && Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(revealPath) && Directory.Exists(revealPath))
                EditorUtility.RevealInFinder(revealPath);
            else
                context?.Report("目标及其安全父目录尚不存在。请先检查名称和目标路径。");
        }

        private static bool TryResolveArtifactPath(ESAgentArtifactKind kind, string projectPath,
            out string fullPath, out string error)
        {
            fullPath = string.Empty;
            if (!ESAgentArtifactPathPolicy.IsAllowedTarget(kind, projectPath, out error))
                return false;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string normalized = (projectPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar).Trim();
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
                string rootPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = string.Empty;
                    error = "目标路径超出当前项目目录。";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                fullPath = string.Empty;
                error = "目标路径无法解析：" + exception.Message;
                return false;
            }
        }

        protected static Button CardButton(string name, string text, string tooltip, Action action)
        {
            var button = new Button(() => action?.Invoke())
            {
                name = name,
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            StyleCardButton(button);
            return button;
        }

        private static void StyleCardButton(Button button)
        {
            button.style.minWidth = 48f;
            button.style.minHeight = 20f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            button.style.fontSize = 10f;
            button.style.flexShrink = 0f;
        }
        protected static TextField Text(string label, string value, bool multiline = false,
            string fieldName = null)
        {
            var field = new TextField(label) { value = value ?? string.Empty, multiline = multiline };
            ESGraphInspectorVisuals.StyleTextField(field);
            StyleField(field, field.labelElement, fieldName, IsEmptyValue(value));
            return field;
        }

        protected static VisualElement FieldSummary(T payload, string title = "关键字段")
        {
            int core = 0;
            int coreReady = 0;
            int important = 0;
            int importantReady = 0;
            IReadOnlyList<ESFieldPresentationMetadata> summaryFields
                = ESFieldPresentationMetadataCache.GetSummaryFields(typeof(T));
            for (int i = 0; i < summaryFields.Count; i++)
            {
                ESFieldPresentationMetadata metadata = summaryFields[i];
                bool ready = IsReadyValue(metadata.Field.GetValue(payload), metadata.Required);
                if (metadata.Level == ESFieldLevel.Core)
                {
                    core++;
                    if (ready) coreReady++;
                }
                else
                {
                    important++;
                    if (ready) importantReady++;
                }
            }

            var box = new VisualElement { name = "es-field-summary" };
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.marginTop = 2f;
            box.style.marginBottom = 5f;
            box.style.paddingLeft = 7f;
            box.style.paddingRight = 7f;
            box.style.paddingTop = 4f;
            box.style.paddingBottom = 4f;
            box.style.borderLeftWidth = 3f;
            Color accent = coreReady == core
                ? ESEditorPresentation.GetFieldLevelAccent(ESFieldLevel.Core)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            box.style.borderLeftColor = accent;
            Color background = accent;
            background.a = EditorGUIUtility.isProSkin ? 0.11f : 0.07f;
            box.style.backgroundColor = background;
            var caption = new Label(title + " " + coreReady + "/" + core)
            {
                tooltip = coreReady == core
                    ? "核心字段已经完整。"
                    : "还有 " + (core - coreReady) + " 个核心字段需要补充。"
            };
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.fontSize = 10f;
            caption.style.color = accent;
            caption.style.flexGrow = 1f;
            box.Add(caption);
            if (important > 0)
            {
                var secondary = new Label("重点 " + importantReady + "/" + important);
                secondary.style.fontSize = 9f;
                secondary.style.color = new Color(0.64f, 0.7f, 0.8f, 0.95f);
                box.Add(secondary);
            }
            return box;
        }

        protected static void StyleField(VisualElement field, Label label, string fieldName, bool isEmpty)
        {
            if (field == null
                || !ESFieldPresentationMetadataCache.TryGet(
                    typeof(T), fieldName, out ESFieldPresentationMetadata metadata))
                return;
            ESEditorPresentation.StyleField(field, label, metadata.Level,
                metadata.Required, isEmpty, metadata.Hint);
        }

        private static bool IsEmptyValue(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        private static bool IsReadyValue(object value, bool required)
        {
            if (!required)
                return true;
            if (value == null)
                return false;
            if (value is string text)
                return !string.IsNullOrWhiteSpace(text);
            if (value is bool enabled)
                return enabled;
            return true;
        }
        protected static void CommitOnFocusOut(TextField field, Action<string> set, Action commit)
        {
            if (field == null)
                return;
            string lastCommitted = field.value ?? string.Empty;
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                string next = field.value ?? string.Empty;
                if (string.Equals(lastCommitted, next, StringComparison.Ordinal))
                    return;
                set?.Invoke(next);
                commit?.Invoke();
                lastCommitted = next;
            });
        }

        protected static VisualElement SearchPicker(string labelText, string buttonText, string tooltip,
            Action refresh, out Button pickerButton)
        {
            var row = new VisualElement();
            var label = new Label(labelText);
            row.Add(label);

            pickerButton = new Button
            {
                text = buttonText + "  ▼",
                tooltip = tooltip
            };
            row.Add(pickerButton);
            ESGraphInspectorVisuals.StylePickerRow(row, label, pickerButton);

            if (refresh != null)
            {
                var refreshButton = new Button(refresh)
                {
                    text = "刷新",
                    tooltip = "重新扫描项目中的可选项；扫描只在点击后执行。"
                };
                refreshButton.style.width = 48f;
                refreshButton.style.minWidth = 48f;
                refreshButton.style.minHeight = 24f;
                refreshButton.style.flexGrow = 0f;
                refreshButton.style.marginLeft = 3f;
                row.Add(refreshButton);
            }
            return row;
        }

        protected static VisualElement OperationPicker(ESAgentArtifactOperationMode current,
            Action<ESAgentArtifactOperationMode> onSelected)
        {
            VisualElement row = SearchPicker(
                "创建 / 更新方式",
                OperationLabel(current),
                "自动创建或更新最常用；仅创建与仅更新会在目标状态不匹配时阻断。",
                null,
                out Button pickerButton);
            ESAgentArtifactOperationMode selectedValue = current;
            pickerButton.clicked += () =>
            {
                Action<ESAgentArtifactOperationMode> select = value =>
                {
                    selectedValue = value;
                    pickerButton.text = OperationLabel(value) + "  ▼";
                    onSelected?.Invoke(value);
                };
                ESSearchDropdown.Open(
                    pickerButton,
                    "选择创建 / 更新方式",
                    new[]
                    {
                        OperationEntry(ESAgentArtifactOperationMode.CreateOrUpdate, selectedValue,
                            "自动创建或更新", "目标不存在时创建；通过稳定 ArtifactId 找到已有目标时更新。", select,
                            "推荐"),
                        OperationEntry(ESAgentArtifactOperationMode.CreateOnly, selectedValue,
                            "仅创建", "目标或目录已经存在时立即阻断，避免覆盖。", select),
                        OperationEntry(ESAgentArtifactOperationMode.UpdateOnly, selectedValue,
                            "仅更新", "找不到携带相同 ArtifactId 的正式产物时立即阻断。", select)
                    },
                    minimumWindowSize: new Vector2(500f, 280f));
            };
            return row;
        }

        private static ESSearchDropdown.Entry OperationEntry(ESAgentArtifactOperationMode value,
            ESAgentArtifactOperationMode current, string label, string description,
            Action<ESAgentArtifactOperationMode> onSelected, string badge = null)
        {
            bool selected = value == current;
            return ESSearchDropdown.Entry.Item(
                label,
                () => onSelected?.Invoke(value),
                subtitle: description,
                badge: selected ? "当前" : badge,
                selected: selected);
        }

        private static string OperationLabel(ESAgentArtifactOperationMode value)
        {
            switch (value)
            {
                case ESAgentArtifactOperationMode.CreateOnly:
                    return "仅创建";
                case ESAgentArtifactOperationMode.UpdateOnly:
                    return "仅更新";
                default:
                    return "自动创建或更新";
            }
        }

        protected static IEnumerable<ESSearchDropdown.Entry> PathEntries(IEnumerable<string> paths,
            string currentPath, Action<string> onSelected)
        {
            string current = NormalizePickerPath(currentPath);
            if (paths == null)
                yield break;
            foreach (string rawPath in paths)
            {
                string path = NormalizePickerPath(rawPath);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.StartsWith("<", StringComparison.Ordinal))
                {
                    yield return ESSearchDropdown.Entry.Disabled(path, tooltip: "请刷新列表或确认对应目录中已有可用内容。");
                    continue;
                }

                string captured = path;
                bool selected = string.Equals(current, path, StringComparison.Ordinal);
                yield return ESSearchDropdown.Entry.Item(
                    GetPickerDisplayName(path),
                    () => onSelected?.Invoke(captured),
                    GetPickerGroup(path),
                    subtitle: GetPickerParentCaption(path),
                    badge: selected ? "当前" : null,
                    selected: selected);
            }
        }

        private static string NormalizePickerPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string GetPickerDisplayName(string path)
        {
            string trimmed = path.TrimEnd('/');
            int separator = trimmed.LastIndexOf('/');
            return separator >= 0 ? trimmed.Substring(separator + 1) : trimmed;
        }

        private static string GetPickerParentCaption(string path)
        {
            string[] segments = path.TrimEnd('/').Split('/');
            int parentCount = segments.Length - 1;
            if (parentCount <= 0)
                return string.Empty;
            if (parentCount <= 2)
                return string.Join("/", segments.Take(parentCount));
            return "…/" + segments[parentCount - 2] + "/" + segments[parentCount - 1];
        }

        private static string GetPickerGroup(string path)
        {
            const string warningsRoot = "Assets/Plugins/ES/AIWarnings/";
            if (path.StartsWith(warningsRoot, StringComparison.Ordinal))
            {
                string tail = path.Substring(warningsRoot.Length);
                int separator = tail.IndexOf('/');
                return separator > 0 ? "项目规则/" + tail.Substring(0, separator) : "项目规则";
            }
            if (path.StartsWith("Assets/Plugins/ES/AICommands/", StringComparison.Ordinal))
                return "AICommand 命令";
            if (path.StartsWith(".agents/skills/", StringComparison.Ordinal))
            {
                if (path.EndsWith("/", StringComparison.Ordinal))
                    return "Agent Skill 技能";
                string[] segments = path.Split('/');
                return segments.Length > 2 ? "Agent Skill 技能/" + segments[2] : "Agent Skill 技能";
            }
            if (path.StartsWith("Assets/Scripts/", StringComparison.Ordinal))
                return "C# 源码/项目逻辑";
            if (path.StartsWith("Assets/Plugins/ES/", StringComparison.Ordinal))
                return "ES 插件内容";
            if (path.StartsWith("Documentation/", StringComparison.Ordinal)
                || path.StartsWith("ES/Documentation/", StringComparison.Ordinal))
                return "项目文档";
            return "项目资产";
        }
    }

}
