using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    public enum ESAdvancedDialogActionRole : byte
    {
        Secondary,
        Primary,
        Danger,
    }

    public enum ESAdvancedDialogPositionMode : byte
    {
        CenterOwner = 0,
        OwnerTopLeft = 1,
        OwnerTopRight = 2,
        OwnerBottomLeft = 3,
        OwnerBottomRight = 4,
        CustomScreenPosition = 5,
    }

    public sealed class ESAdvancedDialogValidation
    {
        public string fieldId;
        public string message;

        public ESAdvancedDialogValidation(string message, string fieldId = null)
        {
            this.message = message ?? string.Empty;
            this.fieldId = fieldId?.Trim() ?? string.Empty;
        }
    }

    public sealed class ESAdvancedDialogAction
    {
        public string id;
        public string text;
        public string tooltip;
        public ESAdvancedDialogActionRole role = ESAdvancedDialogActionRole.Secondary;
        public bool closeDialogAfterExecution;
        public Action<ESAdvancedDialogValues> execute;
        public Func<ESAdvancedDialogValues, ESProgressHandle, CancellationToken, Task> executeAsync;

        public ESAdvancedDialogAction(string id, string text, Action<ESAdvancedDialogValues> execute)
        {
            this.id = id;
            this.text = text;
            this.execute = execute;
        }

        public ESAdvancedDialogAction(
            string id,
            string text,
            Func<ESAdvancedDialogValues, ESProgressHandle, CancellationToken, Task> executeAsync)
        {
            this.id = id;
            this.text = text;
            this.executeAsync = executeAsync;
        }
    }

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
        MultiChoice,
        Recommendation,
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
        public int intValue;
        public int minIntValue;
        public int maxIntValue;
        public string lowValueLabel;
        public string highValueLabel;
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
        public readonly List<string> selectedChoiceValues = new List<string>();
        public int minimumSelections;
        public int maximumSelections;

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
        private readonly Dictionary<string, int> integers;
        private readonly Dictionary<string, IReadOnlyList<string>> selections;
        private readonly Dictionary<string, UnityEngine.Object> objects;

        internal ESAdvancedDialogValues(
            Dictionary<string, string> strings,
            Dictionary<string, bool> toggles,
            Dictionary<string, int> integers,
            Dictionary<string, IReadOnlyList<string>> selections,
            Dictionary<string, UnityEngine.Object> objects)
        {
            this.strings = strings;
            this.toggles = toggles;
            this.integers = integers;
            this.selections = selections;
            this.objects = objects;
        }

        public string GetString(string id, string fallback = "")
            => strings != null && strings.TryGetValue(id, out string value) ? value : fallback;

        public bool GetToggle(string id, bool fallback = false)
            => toggles != null && toggles.TryGetValue(id, out bool value) ? value : fallback;

        public int GetInteger(string id, int fallback = 0)
            => integers != null && integers.TryGetValue(id, out int value) ? value : fallback;

        public int GetRecommendation(string id, int fallback = 0)
            => GetInteger(id, fallback);

        public IReadOnlyList<string> GetSelections(string id)
            => selections != null && selections.TryGetValue(id, out IReadOnlyList<string> value)
                ? value
                : Array.Empty<string>();

        public bool HasSelection(string id, string optionId)
        {
            if (string.IsNullOrWhiteSpace(optionId))
                return false;
            IReadOnlyList<string> values = GetSelections(id);
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], optionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public T GetObject<T>(string id) where T : UnityEngine.Object
            => objects != null && objects.TryGetValue(id, out UnityEngine.Object value) ? value as T : null;
    }

    public sealed class ESAdvancedDialogResult
    {
        public bool accepted;
        public bool cancelled;
        public string actionId;
        public ESAdvancedDialogValues values;
        public Exception exception;
    }

    public sealed class ESAdvancedDialogRequest
    {
        public string dialogId = string.Empty;
        public string title = "ES 输入";
        public string subtitle = "ES 专用对话框";
        public string message = string.Empty;
        public string detail = string.Empty;
        public string confirmText = "确定";
        public string cancelText = "取消";
        public Vector2 minSize = new Vector2(460f, 260f);
        public Vector2 preferredSize = new Vector2(560f, 440f);
        public ESAdvancedDialogPositionMode positionMode = ESAdvancedDialogPositionMode.CenterOwner;
        // CustomScreenPosition 是“对话框左上角”的桌面坐标，不是点击控件的中心点，
        // 也不是一个可以让服务自行猜测方向的锚点。调用方必须先按触发控件和可用屏幕空间
        // 计算好最终方向。特别是从右侧停靠 Inspector 的按钮触发大型独立窗口时，默认应优先
        // 放在按钮左侧或左上方；禁止把按钮右上/右下坐标直接当作对话框左上角，再依赖越界钳制补救。
        public Vector2 customScreenPosition;
        public Vector2 positionOffset;
        public ESDialogTone tone = ESDialogTone.Info;
        public bool showCancel = true;
        public bool animateOpening = true;
        public bool closeOnEscape = true;
        public bool allowOperationCancellation = true;
        public bool queueBehindActiveDialog;
        /// <summary>
        /// 允许没有可靠 EditorWindow owner 的调用落到主编辑器工作区。
        /// 这是显式例外，不是 owner 缺失时的隐式猜测；调用方必须说明原因。
        /// </summary>
        public bool allowMainWorkspaceFallback;
        public int asyncValidationDelayMs = 180;
        public string initialFocusFieldId = string.Empty;
        public EditorWindow owner;
        public ESDialogDuplicatePolicy duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting;
        public readonly List<ESAdvancedDialogField> fields = new List<ESAdvancedDialogField>();
        public readonly List<ESAdvancedDialogAction> auxiliaryActions = new List<ESAdvancedDialogAction>();

        /// <summary>返回空字符串表示通过；必须是无副作用的快速校验。</summary>
        public Func<ESAdvancedDialogValues, string> validate;
        public Func<ESAdvancedDialogValues, ESAdvancedDialogValidation> validateDetailed;
        public Func<ESAdvancedDialogValues, CancellationToken, Task<ESAdvancedDialogValidation>> validateAsync;
        public Func<ESAdvancedDialogValues, ESProgressHandle, CancellationToken, Task> confirmAsync;
        public Func<ESAdvancedDialogValues, VisualElement> createCustomContent;
        public Action<VisualElement> releaseCustomContent;
        public Action<ESAdvancedDialogResult> completed;

        public ESAdvancedDialogAction AddAuxiliaryAction(
            string id,
            string text,
            Action<ESAdvancedDialogValues> execute,
            string tooltip = null,
            ESAdvancedDialogActionRole role = ESAdvancedDialogActionRole.Secondary,
            bool closeDialogAfterExecution = false)
        {
            var action = new ESAdvancedDialogAction(id, text, execute)
            {
                tooltip = tooltip,
                role = role,
                closeDialogAfterExecution = closeDialogAfterExecution,
            };
            auxiliaryActions.Add(action);
            return action;
        }

        public ESAdvancedDialogAction AddAuxiliaryActionAsync(
            string id,
            string text,
            Func<ESAdvancedDialogValues, ESProgressHandle, CancellationToken, Task> execute,
            string tooltip = null,
            ESAdvancedDialogActionRole role = ESAdvancedDialogActionRole.Secondary,
            bool closeDialogAfterExecution = false)
        {
            var action = new ESAdvancedDialogAction(id, text, execute)
            {
                tooltip = tooltip,
                role = role,
                closeDialogAfterExecution = closeDialogAfterExecution,
            };
            auxiliaryActions.Add(action);
            return action;
        }

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

        public ESAdvancedDialogField AddMultiChoice(
            string id,
            string label,
            IEnumerable<string> choices,
            IEnumerable<string> defaultValues = null,
            int minimumSelections = 0,
            int maximumSelections = 0)
        {
            var options = choices?.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new ESAdvancedDialogChoiceOption(value, value));
            return AddMultiChoiceOptions(
                id,
                label,
                options,
                defaultValues,
                minimumSelections,
                maximumSelections);
        }

        /// <summary>添加带稳定 OptionId 的多选字段；maximumSelections 为 0 表示不限制。</summary>
        public ESAdvancedDialogField AddMultiChoiceOptions(
            string id,
            string label,
            IEnumerable<ESAdvancedDialogChoiceOption> options,
            IEnumerable<string> defaultValues = null,
            int minimumSelections = 0,
            int maximumSelections = 0)
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.MultiChoice)
            {
                minimumSelections = minimumSelections,
                maximumSelections = maximumSelections,
                required = minimumSelections > 0,
            };
            if (options != null)
            {
                foreach (ESAdvancedDialogChoiceOption option in options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.id) || string.IsNullOrWhiteSpace(option.label))
                        continue;
                    field.choiceValues.Add(option.id);
                    field.choices.Add(option.label);
                }
            }
            if (defaultValues != null)
            {
                foreach (string value in defaultValues)
                {
                    if (!string.IsNullOrWhiteSpace(value)
                        && !field.selectedChoiceValues.Contains(value))
                        field.selectedChoiceValues.Add(value);
                }
            }
            fields.Add(field);
            return field;
        }

        public ESAdvancedDialogField AddRecommendation(
            string id,
            string label,
            int defaultValue = 3,
            int minimum = 0,
            int maximum = 5,
            string lowLabel = "不推荐",
            string highLabel = "强烈推荐")
        {
            var field = new ESAdvancedDialogField(id, label, ESAdvancedDialogFieldKind.Recommendation)
            {
                intValue = defaultValue,
                minIntValue = minimum,
                maxIntValue = maximum,
                lowValueLabel = lowLabel,
                highValueLabel = highLabel,
            };
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

    public enum ESProgressState : byte
    {
        Running,
        Succeeded,
        Failed,
        Cancelled,
    }

    public sealed class ESProgressSnapshot
    {
        public string id;
        public string title;
        public string summary;
        public float progress;
        public ESProgressState state;
        public bool cancellable;
        public IReadOnlyList<string> details;
        public DateTime startedAtUtc;
        public DateTime finishedAtUtc;
    }

    public readonly struct ESProgressStep
    {
        public readonly float progress;
        public readonly string summary;
        public readonly string detail;

        public ESProgressStep(float progress, string summary, string detail = null)
        {
            this.progress = Mathf.Clamp01(progress);
            this.summary = summary ?? string.Empty;
            this.detail = detail;
        }
    }

    public sealed class ESProgressHandle : IDisposable, IProgress<float>
    {
        private readonly ESProgressCenter.ProgressRecord record;
        private int finished;

        internal ESProgressHandle(ESProgressCenter.ProgressRecord record)
        {
            this.record = record;
        }

        public string Id => record.id;
        public CancellationToken CancellationToken => record.cancellation.Token;
        public bool IsCancellationRequested => record.cancellation.IsCancellationRequested;

        public void Report(float value) => Report(value, null);

        public void Report(float value, string summary)
        {
            ESProgressCenter.Update(record, Mathf.Clamp01(value), summary, null);
        }

        public void AddDetail(string detail)
        {
            ESProgressCenter.Update(record, null, null, detail);
        }

        public void RequestCancel()
        {
            if (record.cancellable && !record.cancellation.IsCancellationRequested)
                record.cancellation.Cancel();
        }

        public void Complete(string summary = null)
        {
            Finish(ESProgressState.Succeeded, summary, null);
        }

        public void Cancel(string summary = null)
        {
            Finish(ESProgressState.Cancelled, summary ?? "任务已取消", null);
        }

        public void Fail(Exception exception, string summary = null)
        {
            Finish(
                ESProgressState.Failed,
                summary ?? exception?.Message ?? "任务执行失败",
                exception?.ToString());
        }

        public void Dispose()
        {
            Complete();
        }

        private void Finish(ESProgressState state, string summary, string detail)
        {
            if (Interlocked.Exchange(ref finished, 1) != 0)
                return;
            ESProgressCenter.Finish(record, state, summary, detail);
        }
    }

    public static class ESProgressCenter
    {
        internal sealed class ProgressRecord
        {
            internal string id;
            internal string title;
            internal string summary;
            internal float progress;
            internal ESProgressState state;
            internal bool cancellable;
            internal readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            internal readonly List<string> details = new List<string>();
            internal DateTime startedAtUtc;
            internal DateTime finishedAtUtc;
        }

        private const int MaximumRecords = 24;
        private const int MaximumDetailsPerTask = 80;
        private const double CompletedVisibilitySeconds = 4d;
        private const double FailedVisibilitySeconds = 12d;
        private static readonly object gate = new object();
        private static readonly List<ProgressRecord> records = new List<ProgressRecord>();
        private static readonly List<EditorApplication.CallbackFunction> stepTicks =
            new List<EditorApplication.CallbackFunction>();
        private static readonly List<Action> stepShutdowns = new List<Action>();
        private static bool updateSubscribed;
        private static bool shuttingDown;
        private static double nextRefreshAt;
        private static ESProgressCenterWindow window;

        static ESProgressCenter()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static ESProgressHandle Begin(
            string id,
            string title,
            string summary = "正在准备",
            bool cancellable = true)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("进度任务必须提供稳定 ID。", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("进度任务标题不能为空。", nameof(title));

            ProgressRecord record;
            lock (gate)
            {
                record = records.LastOrDefault(item =>
                    item.state == ESProgressState.Running
                    && string.Equals(item.id, id.Trim(), StringComparison.Ordinal));
                if (record != null)
                    throw new InvalidOperationException("已有同 ID 进度任务正在运行：" + id.Trim());
                record = new ProgressRecord
                {
                    id = id.Trim(),
                    title = title.Trim(),
                    summary = summary?.Trim() ?? string.Empty,
                    progress = 0f,
                    state = ESProgressState.Running,
                    cancellable = cancellable,
                    startedAtUtc = DateTime.UtcNow,
                };
                records.Add(record);
                TrimRecordsLocked();
            }
            EnsureUpdateSubscription();
            return new ESProgressHandle(record);
        }

        public static void Run(
            string id,
            string title,
            Action<ESProgressHandle> operation,
            bool cancellable = true)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            using ESProgressHandle progress = Begin(id, title, cancellable: cancellable);
            try
            {
                operation(progress);
                if (progress.IsCancellationRequested)
                    progress.Cancel();
            }
            catch (OperationCanceledException)
            {
                progress.Cancel();
            }
            catch (Exception exception)
            {
                progress.Fail(exception);
                throw;
            }
        }

        public static async Task RunAsync(
            string id,
            string title,
            Func<ESProgressHandle, CancellationToken, Task> operation,
            bool cancellable = true)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            using ESProgressHandle progress = Begin(id, title, cancellable: cancellable);
            try
            {
                await operation(progress, progress.CancellationToken);
                if (progress.IsCancellationRequested)
                    progress.Cancel();
            }
            catch (OperationCanceledException)
            {
                progress.Cancel();
            }
            catch (Exception exception)
            {
                progress.Fail(exception);
                throw;
            }
        }

        /// <summary>
        /// 在 Editor update 中每帧推进一个同步步骤，适合不能一次性阻塞主线程的旧式循环。
        /// </summary>
        public static Task RunSteps(
            string id,
            string title,
            IEnumerable<ESProgressStep> steps,
            bool cancellable = true)
        {
            if (steps == null)
                throw new ArgumentNullException(nameof(steps));
            IEnumerator<ESProgressStep> iterator = steps.GetEnumerator();
            ESProgressHandle progress = Begin(id, title, cancellable: cancellable);
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int stepsFinished = 0;
            void TickSteps()
            {
                try
                {
                    if (progress.IsCancellationRequested)
                    {
                        progress.Cancel();
                        completion.TrySetCanceled(progress.CancellationToken);
                        FinishSteps();
                        return;
                    }
                    if (!iterator.MoveNext())
                    {
                        progress.Complete();
                        completion.TrySetResult(true);
                        FinishSteps();
                        return;
                    }
                    ESProgressStep step = iterator.Current;
                    progress.Report(step.progress, step.summary);
                    progress.AddDetail(step.detail);
                }
                catch (Exception exception)
                {
                    progress.Fail(exception);
                    completion.TrySetException(exception);
                    FinishSteps();
                }
            }
            void FinishSteps()
            {
                if (Interlocked.Exchange(ref stepsFinished, 1) != 0)
                    return;
                EditorApplication.update -= TickSteps;
                stepTicks.Remove(TickSteps);
                stepShutdowns.Remove(CancelSteps);
                iterator.Dispose();
                progress.Dispose();
            }
            void CancelSteps()
            {
                progress.Cancel("Editor Domain 正在结束");
                completion.TrySetCanceled();
                FinishSteps();
            }
            stepTicks.Add(TickSteps);
            stepShutdowns.Add(CancelSteps);
            EditorApplication.update += TickSteps;
            return completion.Task;
        }

        public static IReadOnlyList<ESProgressSnapshot> GetSnapshot()
        {
            lock (gate)
            {
                return records.Select(ToSnapshot).ToArray();
            }
        }

        internal static bool TryGetProgress(
            string id,
            out float progress,
            out string summary)
        {
            progress = 0f;
            summary = string.Empty;
            if (string.IsNullOrWhiteSpace(id))
                return false;
            lock (gate)
            {
                for (int i = records.Count - 1; i >= 0; i--)
                {
                    ProgressRecord record = records[i];
                    if (!string.Equals(record.id, id, StringComparison.Ordinal))
                        continue;
                    progress = record.progress;
                    summary = record.summary ?? string.Empty;
                    return true;
                }
            }
            return false;
        }

        public static bool RequestCancel(string id)
        {
            CancellationTokenSource cancellation = null;
            lock (gate)
            {
                ProgressRecord record = records.LastOrDefault(item =>
                    item.state == ESProgressState.Running
                    && string.Equals(item.id, id, StringComparison.Ordinal));
                if (record == null || !record.cancellable)
                    return false;
                cancellation = record.cancellation;
            }
            cancellation.Cancel();
            return true;
        }

        public static void DismissCompleted()
        {
            lock (gate)
                RemoveRecordsLocked(item => item.state != ESProgressState.Running);
            window?.RefreshNow();
        }

        internal static void Update(
            ProgressRecord record,
            float? progress,
            string summary,
            string detail)
        {
            if (record == null)
                return;
            lock (gate)
            {
                if (record.state != ESProgressState.Running)
                    return;
                if (progress.HasValue)
                    record.progress = Mathf.Clamp01(progress.Value);
                if (!string.IsNullOrWhiteSpace(summary))
                    record.summary = summary.Trim();
                AddDetailLocked(record, detail);
            }
        }

        internal static void Finish(
            ProgressRecord record,
            ESProgressState state,
            string summary,
            string detail)
        {
            if (record == null)
                return;
            lock (gate)
            {
                if (record.state != ESProgressState.Running)
                    return;
                record.state = state;
                record.progress = state == ESProgressState.Succeeded ? 1f : record.progress;
                record.finishedAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(summary))
                    record.summary = summary.Trim();
                AddDetailLocked(record, detail);
            }
        }

        private static void EnsureUpdateSubscription()
        {
            if (updateSubscribed)
                return;
            updateSubscribed = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextRefreshAt)
                return;
            nextRefreshAt = now + 0.1d;

            bool hasVisible;
            bool hasRunning;
            lock (gate)
            {
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-CompletedVisibilitySeconds);
                DateTime failedCutoff = DateTime.UtcNow.AddSeconds(-FailedVisibilitySeconds);
                for (int i = records.Count - 1; i >= 0; i--)
                {
                    ProgressRecord record = records[i];
                    if (record.state == ESProgressState.Running
                        || record.finishedAtUtc >= (record.state == ESProgressState.Failed
                            ? failedCutoff
                            : cutoff))
                        continue;
                    record.cancellation.Dispose();
                    records.RemoveAt(i);
                }
                hasRunning = false;
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].state != ESProgressState.Running)
                        continue;
                    hasRunning = true;
                    break;
                }
                hasVisible = records.Count > 0;
            }

            if (hasVisible)
            {
                if (window == null)
                    window = ESProgressCenterWindow.OpenAtBottomRight();
                else
                    window.RefreshNow();
            }
            else if (window != null)
            {
                window.Close();
                window = null;
            }

            if (!hasRunning && !hasVisible)
            {
                EditorApplication.update -= Tick;
                updateSubscribed = false;
            }
        }

        private static void AddDetailLocked(ProgressRecord record, string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return;
            record.details.Add(detail.Trim());
            if (record.details.Count > MaximumDetailsPerTask)
                record.details.RemoveRange(0, record.details.Count - MaximumDetailsPerTask);
        }

        private static void TrimRecordsLocked()
        {
            while (records.Count > MaximumRecords)
            {
                int removable = records.FindIndex(item => item.state != ESProgressState.Running);
                if (removable < 0)
                    break;
                records[removable].cancellation.Dispose();
                records.RemoveAt(removable);
            }
        }

        private static void RemoveRecordsLocked(Predicate<ProgressRecord> predicate)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (!predicate(records[i]))
                    continue;
                records[i].cancellation.Dispose();
                records.RemoveAt(i);
            }
        }

        private static void Shutdown()
        {
            if (shuttingDown)
                return;
            shuttingDown = true;
            EditorApplication.update -= Tick;
            updateSubscribed = false;
            Action[] shutdowns = stepShutdowns.ToArray();
            for (int i = 0; i < shutdowns.Length; i++)
                shutdowns[i]?.Invoke();
            stepTicks.Clear();
            stepShutdowns.Clear();
            lock (gate)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    ProgressRecord record = records[i];
                    if (!record.cancellation.IsCancellationRequested)
                        record.cancellation.Cancel();
                    record.cancellation.Dispose();
                }
                records.Clear();
            }
            if (window != null)
            {
                window.Close();
                window = null;
            }
        }

        private static ESProgressSnapshot ToSnapshot(ProgressRecord record)
        {
            return new ESProgressSnapshot
            {
                id = record.id,
                title = record.title,
                summary = record.summary,
                progress = record.progress,
                state = record.state,
                cancellable = record.cancellable,
                details = record.details.ToArray(),
                startedAtUtc = record.startedAtUtc,
                finishedAtUtc = record.finishedAtUtc,
            };
        }
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Utility,
        "跨任务全局进度聚合面不参与自动半休眠")]
    [ESWindowPresentationShortTitle("进度")]
    public sealed class ESProgressCenterWindow : EditorWindow
    {
        private VisualElement content;
        private readonly HashSet<string> expandedIds = new HashSet<string>(StringComparer.Ordinal);

        internal static ESProgressCenterWindow OpenAtBottomRight()
        {
            EditorWindow previous = focusedWindow;
            var result = GetWindow<ESProgressCenterWindow>(
                true,
                "ES 任务进度",
                false);
            result.titleContent = new GUIContent("ES 任务进度");
            result.minSize = new Vector2(320f, 120f);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            result.maxSize = new Vector2(
                Mathf.Max(result.minSize.x, main.width - 24f),
                Mathf.Max(result.minSize.y, main.height - 24f));
            float width = Mathf.Min(410f, Mathf.Max(320f, main.width - 24f));
            int visibleTasks = Mathf.Clamp(ESProgressCenter.GetSnapshot().Count, 1, 3);
            float desiredHeight = 72f + visibleTasks * 68f;
            float height = Mathf.Min(desiredHeight, Mathf.Max(120f, main.height - 24f));
            result.position = new Rect(
                main.xMax - width - 12f,
                main.yMax - height - 12f,
                width,
                height);
            result.ShowUtility();
            if (previous != null)
                previous.Focus();
            ES.EditorInternal.ESWindowFrameActivation.Play(result, result.position);
            return result;
        }

        public void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor =
                ES.EditorInternal.ESEditorPresentation.WindowSurfaceColor;
            var header = new VisualElement();
            header.AddToClassList("es-progress-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 10f;
            header.style.paddingRight = 8f;
            header.style.paddingTop = 7f;
            header.style.paddingBottom = 7f;
            var title = new Label("ES 任务进度");
            title.style.flexGrow = 1f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            header.Add(ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                "清理",
                "隐藏已结束任务；运行中的任务不受影响。",
                ESProgressCenter.DismissCompleted));
            rootVisualElement.Add(header);
            content = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            content.AddToClassList("es-progress-content");
            content.style.flexGrow = 1f;
            content.style.minWidth = 0f;
            rootVisualElement.Add(content);
            RefreshNow();
            ESWindowFoundation.BindTransient(this);
        }

        internal void RefreshNow()
        {
            if (content == null)
                return;
            IReadOnlyList<ESProgressSnapshot> snapshots = ESProgressCenter.GetSnapshot();
            content.Clear();
            for (int i = snapshots.Count - 1; i >= 0; i--)
                content.Add(CreateTaskRow(snapshots[i]));
        }

        private VisualElement CreateTaskRow(ESProgressSnapshot snapshot)
        {
            var block = new VisualElement();
            block.AddToClassList("es-progress-task");
            block.style.marginLeft = 8f;
            block.style.marginRight = 8f;
            block.style.marginBottom = 7f;
            block.style.paddingLeft = 9f;
            block.style.paddingRight = 9f;
            block.style.paddingTop = 7f;
            block.style.paddingBottom = 7f;
            block.style.backgroundColor =
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                block,
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                ES.EditorInternal.ESEditorPresentation.DividerColor);
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            var label = new Label(snapshot.title);
            label.style.flexGrow = 1f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            top.Add(label);
            if (snapshot.state == ESProgressState.Running && snapshot.cancellable)
                top.Add(ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    "取消", "请求任务安全取消。", () => ESProgressCenter.RequestCancel(snapshot.id)));
            if (snapshot.details.Count > 0)
                top.Add(ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    expandedIds.Contains(snapshot.id) ? "收起" : "详情",
                    "显示或隐藏任务细节。",
                    () =>
                    {
                        if (!expandedIds.Add(snapshot.id))
                            expandedIds.Remove(snapshot.id);
                        RefreshNow();
                    }));
            block.Add(top);
            var progress = new ProgressBar
            {
                value = Mathf.Clamp01(snapshot.progress) * 100f,
                title = snapshot.summary ?? string.Empty,
            };
            progress.style.marginTop = 4f;
            block.Add(progress);
            if ((expandedIds.Contains(snapshot.id) || snapshot.state == ESProgressState.Failed)
                && snapshot.details.Count > 0)
            {
                var detail = new Label(string.Join("\n", snapshot.details));
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.marginTop = 5f;
                detail.style.fontSize = 10f;
                detail.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                block.Add(detail);
            }
            return block;
        }

        private void OnDisable()
        {
            ESWindowFoundation.Suspend(this);
        }

        private void OnDestroy()
        {
            ESWindowFoundation.Close(this);
        }
    }

    public static class ESDialogService
    {
        private const int MaximumActiveDialogs = 8;
        private const int MaximumPendingDialogs = 64;
        private const double CloseRetryIntervalSeconds = 0.25d;
        internal enum DialogOperationState : byte
        {
            Queued,
            Scheduled,
            Opening,
            Active,
            Closing,
            Completed,
        }

        internal sealed class DialogSubscriber
        {
            private readonly Action<ESAdvancedDialogResult> callback;
            private readonly TaskCompletionSource<ESAdvancedDialogResult> completion;
            private CancellationTokenRegistration cancellationRegistration;
            private bool cancellationRegistered;
            private bool completed;

            internal DialogSubscriber(
                Action<ESAdvancedDialogResult> callback,
                TaskCompletionSource<ESAdvancedDialogResult> completion)
            {
                this.callback = callback;
                this.completion = completion;
            }

            internal Task<ESAdvancedDialogResult> Task => completion?.Task;
            internal bool IsCompleted => completed;

            internal void RegisterCancellation(
                DialogOperation operation,
                CancellationToken cancellationToken,
                SynchronizationContext editorContext)
            {
                if (completed || !cancellationToken.CanBeCanceled)
                    return;

                CancellationTokenRegistration registration = cancellationToken.Register(() =>
                    editorContext.Post(
                        _ => CancelSubscriber(operation, this, cancellationToken),
                        null));
                cancellationRegistration = registration;
                cancellationRegistered = true;
                if (completed)
                    DisposeCancellation();
            }

            internal void Complete(ESAdvancedDialogResult result)
            {
                if (completed)
                    return;
                completed = true;
                DisposeCancellation();
                InvokeCallback(result);
                completion?.TrySetResult(result);
            }

            internal void Cancel(
                ESAdvancedDialogResult result,
                CancellationToken cancellationToken)
            {
                if (completed)
                    return;
                completed = true;
                DisposeCancellation();
                InvokeCallback(result);
                completion?.TrySetCanceled(cancellationToken);
            }

            private void InvokeCallback(ESAdvancedDialogResult result)
            {
                if (callback == null)
                    return;
                try
                {
                    callback.Invoke(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            private void DisposeCancellation()
            {
                if (!cancellationRegistered)
                    return;
                cancellationRegistration.Dispose();
                cancellationRegistration = default;
                cancellationRegistered = false;
            }
        }

        internal sealed class DialogOperation
        {
            internal readonly ESAdvancedDialogRequest request;
            internal readonly List<DialogSubscriber> subscribers =
                new List<DialogSubscriber>(2);
            internal DialogOperationState state;
            internal ESAdvancedDialogWindow window;
            internal EditorApplication.CallbackFunction scheduledCallback;
            internal ESAdvancedDialogResult terminalResult;
            internal double nextCloseRetryAt;

            internal DialogOperation(ESAdvancedDialogRequest request)
            {
                this.request = request ?? throw new ArgumentNullException(nameof(request));
                state = DialogOperationState.Queued;
            }

            internal DialogSubscriber AddSubscriber(
                Action<ESAdvancedDialogResult> callback,
                TaskCompletionSource<ESAdvancedDialogResult> completion)
            {
                var subscriber = new DialogSubscriber(callback, completion);
                subscribers.Add(subscriber);
                return subscriber;
            }

            internal void TransferSubscribersTo(DialogOperation target)
            {
                if (target == null)
                    throw new ArgumentNullException(nameof(target));
                target.subscribers.AddRange(subscribers);
                subscribers.Clear();
                state = DialogOperationState.Completed;
            }

            internal ESAdvancedDialogResult CaptureTerminalResult(
                ESAdvancedDialogResult result)
            {
                if (terminalResult == null && result != null)
                    terminalResult = result;
                return terminalResult;
            }

            internal bool BeginClosing(ESAdvancedDialogResult result)
            {
                if (state == DialogOperationState.Completed)
                    return false;
                CaptureTerminalResult(result);
                if (state != DialogOperationState.Closing)
                    nextCloseRetryAt = 0d;
                state = DialogOperationState.Closing;
                return true;
            }

            internal bool CancelSubscriber(
                DialogSubscriber subscriber,
                CancellationToken cancellationToken,
                ESAdvancedDialogResult result)
            {
                if (subscriber == null || !subscribers.Remove(subscriber))
                    return false;
                subscriber.Cancel(result, cancellationToken);
                return subscribers.Count == 0;
            }

            internal bool CompleteOnce(ESAdvancedDialogResult result)
            {
                if (state == DialogOperationState.Completed)
                    return false;
                ESAdvancedDialogResult resolvedResult = CaptureTerminalResult(result) ?? result;
                state = DialogOperationState.Completed;
                DialogSubscriber[] completionSnapshot = subscribers.ToArray();
                subscribers.Clear();
                for (int i = 0; i < completionSnapshot.Length; i++)
                    completionSnapshot[i]?.Complete(resolvedResult);
                return true;
            }
        }

        private static readonly List<ESAdvancedDialogWindow> activeWindows =
            new List<ESAdvancedDialogWindow>();
        private static readonly List<DialogOperation> activeOperations =
            new List<DialogOperation>();
        private static readonly List<DialogOperation> pendingDialogs =
            new List<DialogOperation>();
        private static readonly List<DialogOperation> invalidPendingScratch =
            new List<DialogOperation>(4);
        private static readonly List<ESAdvancedDialogWindow> invalidOwnerScratch =
            new List<ESAdvancedDialogWindow>(4);
        private static readonly List<DialogOperation> closingOperationScratch =
            new List<DialogOperation>(4);
        private static DialogOperation scheduledOperation;
        private static bool openingReplacement;
        private static bool shuttingDown;
        private static bool ownerLifetimeMonitorInstalled;

        internal static void InitializeLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update -= MonitorOwnerLifetime;
            ownerLifetimeMonitorInstalled = false;
            UpdateOwnerLifetimeMonitor();
        }

        private static void MonitorOwnerLifetime()
        {
            if (shuttingDown
                || pendingDialogs.Count == 0
                && activeWindows.Count == 0
                && activeOperations.Count == 0)
            {
                UpdateOwnerLifetimeMonitor();
                return;
            }

            invalidPendingScratch.Clear();
            for (int i = 0; i < pendingDialogs.Count; i++)
            {
                DialogOperation pending = pendingDialogs[i];
                if (pending?.request == null || !IsOwnerInvalid(pending.request))
                    continue;
                invalidPendingScratch.Add(pending);
            }
            for (int i = 0; i < invalidPendingScratch.Count; i++)
                CompleteOperation(invalidPendingScratch[i], CancelledResult());
            invalidPendingScratch.Clear();

            PruneDeadActiveOperations();
            if (shuttingDown)
            {
                UpdateOwnerLifetimeMonitor();
                return;
            }

            closingOperationScratch.Clear();
            double closeRetryNow = EditorApplication.timeSinceStartup;
            for (int i = 0; i < activeOperations.Count; i++)
            {
                DialogOperation operation = activeOperations[i];
                if (operation?.state == DialogOperationState.Closing)
                    closingOperationScratch.Add(operation);
            }
            for (int i = 0; i < closingOperationScratch.Count; i++)
            {
                DialogOperation operation = closingOperationScratch[i];
                if (operation == null
                    || operation.state != DialogOperationState.Closing
                    || !activeOperations.Contains(operation))
                    continue;
                if (closeRetryNow < operation.nextCloseRetryAt)
                    continue;
                operation.nextCloseRetryAt = closeRetryNow + CloseRetryIntervalSeconds;
                ESAdvancedDialogWindow window = operation.window;
                bool windowClosed = CloseWindowBestEffort(window);
                if (operation.state != DialogOperationState.Completed && windowClosed)
                {
                    ESAdvancedDialogResult result = ResolveOperationResult(operation, null)
                        ?? CancelledResult();
                    CompleteOperation(operation, result);
                }
            }
            closingOperationScratch.Clear();

            invalidOwnerScratch.Clear();
            for (int i = activeWindows.Count - 1; i >= 0; i--)
            {
                ESAdvancedDialogWindow window = activeWindows[i];
                if (!IsLive(window))
                {
                    activeWindows.RemoveAt(i);
                    continue;
                }
                DialogOperation operation = FindActiveOperation(window);
                if (operation == null || window.HasInvalidOwner)
                    invalidOwnerScratch.Add(window);
            }
            for (int i = 0; i < invalidOwnerScratch.Count; i++)
            {
                ESAdvancedDialogWindow window = invalidOwnerScratch[i];
                DialogOperation operation = FindActiveOperation(window);
                if (operation?.state == DialogOperationState.Closing)
                    continue;
                if (operation != null)
                    CancelOperation(operation, CancelledResult());
                else
                {
                    if (closeRetryNow < window.NextServiceCloseRetryAt)
                        continue;
                    window.NextServiceCloseRetryAt =
                        closeRetryNow + CloseRetryIntervalSeconds;
                    CloseWindowBestEffort(window);
                }
            }
            invalidOwnerScratch.Clear();
            TryDrainQueue();
            UpdateOwnerLifetimeMonitor();
        }

        private static void UpdateOwnerLifetimeMonitor()
        {
            bool shouldInstall = !shuttingDown
                && (pendingDialogs.Count > 0
                    || activeWindows.Count > 0
                    || activeOperations.Count > 0);
            if (shouldInstall == ownerLifetimeMonitorInstalled)
                return;
            EditorApplication.update -= MonitorOwnerLifetime;
            if (shouldInstall)
                EditorApplication.update += MonitorOwnerLifetime;
            ownerLifetimeMonitorInstalled = shouldInstall;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode
                || state == PlayModeStateChange.EnteredPlayMode)
            {
                // Dialogs own editor focus, async validation and sometimes a
                // nested modal loop. None of those resources may survive into
                // PlayMode; cancel active and queued requests at the boundary.
                Shutdown();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Shutdown is a boundary cancellation, not a permanent
                // service disable. New requests are valid again in EditMode.
                shuttingDown = false;
                UpdateOwnerLifetimeMonitor();
            }
        }

        private static void OnCompilationFinished(object context)
        {
            // beforeAssemblyReload also fires for a compile that Unity later
            // rejects. In that case this AppDomain survives and the presenter
            // must be usable again instead of remaining permanently shut down.
            if (!EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling)
            {
                shuttingDown = false;
                UpdateOwnerLifetimeMonitor();
            }
        }

        public static ESAdvancedDialogWindow Show(ESAdvancedDialogRequest request)
        {
            ValidateServiceRequest(request);
            request = PrepareRequest(request);
            DialogOperation operation = CreateOperation(request, null, out _);
            if (shuttingDown)
            {
                operation.CompleteOnce(CancelledResult());
                return null;
            }
            return SubmitOperation(operation, out _);
        }

        public static Task<ESAdvancedDialogResult> ShowAsync(
            ESAdvancedDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateServiceRequest(request);
            request = PrepareRequest(request);
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SynchronizationContext editorContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "ESDialogService.ShowAsync 必须从 Unity Editor 主线程调用。");
            DialogOperation operation = CreateOperation(
                request,
                completion,
                out DialogSubscriber subscriber);
            if (cancellationToken.IsCancellationRequested)
            {
                operation.CancelSubscriber(
                    subscriber,
                    cancellationToken,
                    CancelledResult());
                operation.CompleteOnce(CancelledResult());
                return completion.Task;
            }
            if (shuttingDown)
            {
                operation.CompleteOnce(CancelledResult());
                return completion.Task;
            }
            SubmitOperation(operation, out DialogOperation acceptedOperation);
            subscriber.RegisterCancellation(
                acceptedOperation,
                cancellationToken,
                editorContext);
            return completion.Task;
        }

        /// <summary>
        /// 原生同步兼容入口。只适合短确认；耗时工作必须使用 ShowAsync，避免阻塞 Editor 主线程。
        /// </summary>
        public static ESAdvancedDialogResult ShowModal(ESAdvancedDialogRequest request)
        {
            ValidateServiceRequest(request);
            if (shuttingDown)
                throw new InvalidOperationException(
                    "ES 对话框当前处于 ReloadDomain、PlayMode 或退出阶段，不能打开模态窗口。");
            request = PrepareRequest(request);
            if (request.queueBehindActiveDialog
                || request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
                throw new InvalidOperationException("ShowModal 不支持队列策略；请使用 ShowAsync。");
            if (request.duplicatePolicy == ESDialogDuplicatePolicy.AllowParallel)
                throw new InvalidOperationException("ShowModal 不允许并行实例；请使用稳定 dialogId 的 ShowAsync。");
            if (FindDuplicate(request.dialogId) != null)
                throw new InvalidOperationException("同 ID 对话框已经打开；ShowModal 不会阻塞等待已有窗口。");
            if (FindPendingDuplicate(request.dialogId) != null)
                throw new InvalidOperationException("同 ID 对话框已经排队；ShowModal 不会越过队列。");
            if (request.confirmAsync != null || request.validateAsync != null
                || request.auxiliaryActions.Any(action => action?.executeAsync != null))
                throw new InvalidOperationException("ShowModal 不接受异步校验或异步动作；请使用 ShowAsync。");
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            DialogOperation operation = CreateOperation(request, completion, out _);
            OpenNow(operation, true);
            return completion.Task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 显示受 ES 对话框生命周期治理的同步单行文本输入框。
        /// 调用方必须提供稳定 dialogId，并显式选择 owner 或主工作区 fallback。
        /// </summary>
        public static bool TryShowTextInputModal(
            string dialogId,
            string title,
            string message,
            string initialValue,
            out string value,
            EditorWindow owner,
            bool allowMainWorkspaceFallback,
            string fieldLabel = "输入",
            string confirmText = "确定",
            string cancelText = "取消",
            bool required = false)
        {
            const string valueFieldId = "value";
            value = string.Empty;
            var request = new ESAdvancedDialogRequest
            {
                dialogId = dialogId,
                title = title,
                message = message ?? string.Empty,
                confirmText = confirmText,
                cancelText = cancelText,
                initialFocusFieldId = valueFieldId,
                owner = owner,
                allowMainWorkspaceFallback = allowMainWorkspaceFallback,
                minSize = new Vector2(420f, 240f),
                preferredSize = new Vector2(520f, 300f),
            };
            request.AddText(
                valueFieldId,
                string.IsNullOrWhiteSpace(fieldLabel) ? "输入" : fieldLabel.Trim(),
                initialValue ?? string.Empty,
                required);

            ESAdvancedDialogResult result = ShowModal(request);
            if (result == null || !result.accepted || result.values == null)
                return false;

            value = result.values.GetString(valueFieldId);
            return true;
        }

        private static ESAdvancedDialogRequest PrepareRequest(ESAdvancedDialogRequest request)
        {
            ESAdvancedDialogRequest snapshot = SnapshotRequest(request);
            return snapshot;
        }

        private static DialogOperation CreateOperation(
            ESAdvancedDialogRequest request,
            TaskCompletionSource<ESAdvancedDialogResult> completion,
            out DialogSubscriber subscriber)
        {
            Action<ESAdvancedDialogResult> callback = request.completed;
            request.completed = null;
            var operation = new DialogOperation(request);
            subscriber = operation.AddSubscriber(callback, completion);
            return operation;
        }

        private static ESAdvancedDialogWindow SubmitOperation(
            DialogOperation operation,
            out DialogOperation acceptedOperation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            acceptedOperation = operation;
            ESAdvancedDialogRequest request = operation.request;
            if (IsOwnerInvalid(request))
            {
                CompleteOperation(operation, CancelledResult());
                return null;
            }

            DialogOperation[] pendingDuplicates = FindPendingDuplicates(request.dialogId);
            DialogOperation pendingDuplicate = pendingDuplicates.FirstOrDefault();
            DialogOperation[] liveDuplicates = FindLiveDuplicateOperations(request.dialogId);
            DialogOperation activeDuplicate = liveDuplicates.FirstOrDefault(item =>
                !item.window.IsClosingOrCompleted
                && (item.state == DialogOperationState.Opening
                    || item.state == DialogOperationState.Active));
            DialogOperation closingDuplicate = liveDuplicates.FirstOrDefault(item =>
                item.state == DialogOperationState.Closing
                || item.window.IsClosingOrCompleted);
            if (request.duplicatePolicy == ESDialogDuplicatePolicy.ReplaceExisting
                && (pendingDuplicates.Length > 0 || liveDuplicates.Length > 0))
            {
                openingReplacement = true;
                try
                {
                    for (int i = 0; i < pendingDuplicates.Length; i++)
                        DetachOperation(pendingDuplicates[i]);
                    Enqueue(operation, true);
                    for (int i = 0; i < pendingDuplicates.Length; i++)
                        pendingDuplicates[i]?.CompleteOnce(CancelledResult());
                    for (int i = 0; i < liveDuplicates.Length; i++)
                        CancelOperation(liveDuplicates[i], CancelledResult());
                }
                finally
                {
                    openingReplacement = false;
                }
                TryDrainQueue();
                return operation.window;
            }

            if (pendingDuplicate != null)
            {
                if (request.duplicatePolicy == ESDialogDuplicatePolicy.FocusExisting)
                {
                    operation.TransferSubscribersTo(pendingDuplicate);
                    acceptedOperation = pendingDuplicate;
                    return pendingDuplicate.window;
                }
                if (request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
                {
                    Enqueue(operation, false);
                    TryDrainQueue();
                    return null;
                }
            }

            if (activeDuplicate == null
                && closingDuplicate != null
                && request.duplicatePolicy != ESDialogDuplicatePolicy.AllowParallel)
            {
                Enqueue(operation, false);
                TryDrainQueue();
                return null;
            }

            if (activeDuplicate != null)
            {
                if (request.duplicatePolicy == ESDialogDuplicatePolicy.FocusExisting)
                {
                    operation.TransferSubscribersTo(activeDuplicate);
                    acceptedOperation = activeDuplicate;
                    activeDuplicate.window?.Focus();
                    return activeDuplicate.window;
                }
                if (request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
                {
                    Enqueue(operation, false);
                    TryDrainQueue();
                    return null;
                }
            }

            if (request.queueBehindActiveDialog && HasLiveActiveWindow())
            {
                Enqueue(operation, false);
                TryDrainQueue();
                return null;
            }

            return OpenNow(operation, false);
        }

        internal static void NotifyClosed(
            ESAdvancedDialogWindow window,
            ESAdvancedDialogResult result)
        {
            DialogOperation operation = FindActiveOperation(window);
            operation?.BeginClosing(result ?? CancelledResult());
            CancelOwnedDialogs(window, operation);

            if (operation != null)
                CompleteOperation(
                    operation,
                    ResolveOperationResult(operation, result ?? CancelledResult()));
            else
                activeWindows.RemoveAll(item =>
                    !IsLive(item) || ReferenceEquals(item, window));
            UpdateOwnerLifetimeMonitor();
            if (shuttingDown)
                return;
            if (openingReplacement)
                return;
            TryDrainQueue();
        }

        internal static Rect ResolveOwnerBounds(ESAdvancedDialogRequest request)
        {
            EditorWindow owner = ResolveOwner(request);
            if (IsLive(owner) && IsUsableBounds(owner.position))
                return owner.position;
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            return IsUsableBounds(main) ? main : new Rect(80f, 80f, 1280f, 800f);
        }

        internal static EditorWindow ResolveOwner(ESAdvancedDialogRequest request)
        {
            return IsLive(request?.owner) ? request.owner : null;
        }

        internal static Rect ResolvePlacementWorkArea(Rect ownerBounds)
        {
            try
            {
                Rect desktop = UnityEditorInternal.InternalEditorUtility
                    .GetBoundsOfDesktopAtPoint(ownerBounds.center);
                if (IsUsableBounds(desktop))
                    return desktop;
            }
            catch (Exception)
            {
            }
            try
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                if (IsUsableBounds(main))
                    return main;
            }
            catch (Exception)
            {
            }
            Vector2 fallbackSize = new Vector2(
                Mathf.Max(1024f, ownerBounds.width),
                Mathf.Max(720f, ownerBounds.height));
            return new Rect(ownerBounds.center - fallbackSize * 0.5f, fallbackSize);
        }

        private static bool IsUsableBounds(Rect bounds)
        {
            return bounds.width > 1f
                && bounds.height > 1f
                && !float.IsNaN(bounds.x)
                && !float.IsNaN(bounds.y)
                && !float.IsInfinity(bounds.x)
                && !float.IsInfinity(bounds.y);
        }

        internal static int ResolveOwnerDepth(ESAdvancedDialogRequest request)
        {
            int depth = 0;
            EditorWindow current = ResolveOwner(request);
            var visited = new HashSet<int>();
            while (current is ESAdvancedDialogWindow dialog && depth < 8)
            {
                if (!visited.Add(dialog.GetInstanceID()))
                    break;
                depth++;
                current = dialog.Owner;
            }
            return depth;
        }

        internal static Rect OffsetChildDialog(Rect position, int ownerDepth)
        {
            float offset = Mathf.Clamp(ownerDepth, 0, 4) * 18f;
            position.x += offset;
            position.y += offset;
            return position;
        }

        internal static int ActiveCount => activeWindows.Count(IsLive);
        internal static int PendingCount => pendingDialogs.Count;

        private static ESAdvancedDialogWindow OpenNow(
            DialogOperation operation,
            bool modal)
        {
            if (operation == null || operation.state == DialogOperationState.Completed)
                return null;
            if (IsOwnerInvalid(operation.request))
            {
                CompleteOperation(operation, CancelledResult());
                return null;
            }
            PruneDeadActiveOperations();
            activeWindows.RemoveAll(item => !IsLive(item));
            if (activeWindows.Count >= MaximumActiveDialogs)
            {
                var exception = new InvalidOperationException(
                    "ES 对话框活动窗口已达到上限 " + MaximumActiveDialogs
                    + "；请复用稳定 dialogId 或启用队列，而不是继续并行打开。");
                CompleteOperation(operation, FailedResult(exception));
                throw exception;
            }
            ESAdvancedDialogWindow window = null;
            try
            {
                operation.state = DialogOperationState.Opening;
                RemoveScheduledCallback(operation);
                if (!pendingDialogs.Contains(operation))
                    pendingDialogs.Insert(0, operation);
                UpdateOwnerLifetimeMonitor();
                window = ESAdvancedDialogWindow.Create(operation.request, null);
                if (operation.state == DialogOperationState.Completed)
                {
                    RemovePendingRegistration(operation);
                    if (!CloseWindowBestEffort(window))
                        TrackUnclosedWindow(
                            window,
                            "ES 对话框创建期间 operation 已被重入完成，但新建窗口尚未退出。");
                    return null;
                }
                operation.window = window;
                pendingDialogs.Remove(operation);
                activeWindows.Add(window);
                activeOperations.Add(operation);
                UpdateOwnerLifetimeMonitor();
                window.Internal_OpenFromDialogService(modal);
                if (operation.state == DialogOperationState.Opening)
                    operation.state = DialogOperationState.Active;
                return window;
            }
            catch (Exception exception)
            {
                if (operation.state != DialogOperationState.Completed)
                {
                    ESAdvancedDialogResult failure = FailedResult(exception);
                    operation.BeginClosing(
                        ResolveOperationResult(operation, WindowResult(window) ?? failure));
                }
                bool windowClosed = CloseWindowBestEffort(window);
                if (operation.state != DialogOperationState.Completed && windowClosed)
                    CompleteOperation(operation, ResolveOperationResult(operation, FailedResult(exception)));
                else if (operation.state != DialogOperationState.Completed)
                {
                    ScheduleCloseRetry(operation);
                    Debug.LogError(
                        "ES 对话框打开失败且窗口无法关闭；operation 保留在治理表中，禁止留下未跟踪窗口。",
                        window);
                }
                else if (!windowClosed)
                    TrackUnclosedWindow(
                        window,
                        "ES 对话框重入完成后打开流程抛出异常，窗口仍未退出。");
                UpdateOwnerLifetimeMonitor();
                throw;
            }
        }

        private static void TryDrainQueue()
        {
            if (shuttingDown
                || openingReplacement
                || scheduledOperation != null)
                return;
            PruneDeadActiveOperations();
            if (pendingDialogs.Count == 0)
            {
                UpdateOwnerLifetimeMonitor();
                return;
            }
            if (HasLiveActiveWindow())
                return;

            while (pendingDialogs.Count > 0)
            {
                DialogOperation next = pendingDialogs[0];
                if (next == null || next.state == DialogOperationState.Completed)
                {
                    pendingDialogs.RemoveAt(0);
                    continue;
                }
                if (IsOwnerInvalid(next.request))
                {
                    CompleteOperation(next, CancelledResult());
                    continue;
                }

                next.state = DialogOperationState.Scheduled;
                scheduledOperation = next;
                next.scheduledCallback = () => OpenScheduled(next);
                EditorApplication.delayCall += next.scheduledCallback;
                UpdateOwnerLifetimeMonitor();
                return;
            }
            UpdateOwnerLifetimeMonitor();
        }

        private static void OpenScheduled(DialogOperation operation)
        {
            if (operation == null)
                return;
            RemoveScheduledCallback(operation);
            if (operation.state == DialogOperationState.Completed
                || !pendingDialogs.Contains(operation))
            {
                TryDrainQueue();
                return;
            }

            operation.state = DialogOperationState.Queued;
            if (shuttingDown)
            {
                CompleteOperation(operation, CancelledResult());
                return;
            }
            PruneDeadActiveOperations();
            if (HasLiveActiveWindow())
            {
                TryDrainQueue();
                return;
            }
            if (IsOwnerInvalid(operation.request))
            {
                CompleteOperation(operation, CancelledResult());
                TryDrainQueue();
                return;
            }

            try
            {
                OpenNow(operation, false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                TryDrainQueue();
            }
        }

        private static ESAdvancedDialogWindow FindDuplicate(string dialogId)
        {
            return FindLiveDuplicateOperations(dialogId).FirstOrDefault()?.window;
        }

        private static DialogOperation FindActiveDuplicateOperation(string dialogId)
        {
            return FindLiveDuplicateOperations(dialogId).FirstOrDefault(item =>
                !item.window.IsClosingOrCompleted
                && (item.state == DialogOperationState.Opening
                    || item.state == DialogOperationState.Active));
        }

        private static DialogOperation[] FindLiveDuplicateOperations(string dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
                return Array.Empty<DialogOperation>();
            string normalized = dialogId.Trim();
            PruneDeadActiveOperations();
            return activeOperations
                .Where(item => item != null
                    && item.state != DialogOperationState.Completed
                    && IsLive(item.window)
                    && string.Equals(
                        item.request.dialogId?.Trim(),
                        normalized,
                        StringComparison.Ordinal))
                .ToArray();
        }

        private static DialogOperation FindActiveOperation(ESAdvancedDialogWindow window)
        {
            if (ReferenceEquals(window, null))
                return null;
            for (int i = 0; i < activeOperations.Count; i++)
            {
                DialogOperation operation = activeOperations[i];
                if (ReferenceEquals(operation?.window, window))
                    return operation;
            }
            return null;
        }

        private static DialogOperation FindPendingDuplicate(string dialogId)
        {
            return FindPendingDuplicates(dialogId).FirstOrDefault();
        }

        private static DialogOperation[] FindPendingDuplicates(string dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
                return Array.Empty<DialogOperation>();
            string normalized = dialogId.Trim();
            return pendingDialogs
                .Where(item => item?.state != DialogOperationState.Completed
                    && item?.request != null
                    && string.Equals(
                        item.request.dialogId?.Trim(),
                        normalized,
                        StringComparison.Ordinal))
                .ToArray();
        }

        private static bool HasLiveActiveWindow()
        {
            for (int i = 0; i < activeWindows.Count; i++)
                if (IsLive(activeWindows[i]))
                    return true;
            return false;
        }

        private static void PruneDeadActiveOperations()
        {
            while (true)
            {
                int deadIndex = -1;
                for (int i = 0; i < activeOperations.Count; i++)
                {
                    DialogOperation candidate = activeOperations[i];
                    if (candidate == null
                        || candidate.state == DialogOperationState.Completed
                        || !IsLive(candidate.window))
                    {
                        deadIndex = i;
                        break;
                    }
                }
                if (deadIndex < 0)
                    break;
                DialogOperation operation = activeOperations[deadIndex];
                if (operation == null)
                    activeOperations.RemoveAt(deadIndex);
                else if (operation.state == DialogOperationState.Completed)
                    DetachOperation(operation);
                else
                {
                    ESAdvancedDialogResult result = ResolveOperationResult(operation, null)
                        ?? CancelledResult();
                    operation.BeginClosing(result);
                    CancelOwnedDialogs(operation.window, operation);
                    CompleteOperation(
                        operation,
                        ResolveOperationResult(operation, result));
                }
            }
            activeWindows.RemoveAll(item => !IsLive(item));
        }

        private static void CancelOwnedDialogs(
            ESAdvancedDialogWindow owner,
            DialogOperation ownerOperation)
        {
            if (ReferenceEquals(owner, null))
                return;

            DialogOperation[] pendingChildren = pendingDialogs
                .Where(item => item?.request != null
                    && ReferenceEquals(item.request.owner, owner))
                .ToArray();
            for (int i = 0; i < pendingChildren.Length; i++)
                CancelOperation(pendingChildren[i], CancelledResult());

            DialogOperation[] activeChildren = activeOperations
                .Where(item => item != null
                    && !ReferenceEquals(item, ownerOperation)
                    && item.request != null
                    && ReferenceEquals(item.request.owner, owner))
                .ToArray();
            for (int i = 0; i < activeChildren.Length; i++)
                CancelOperation(activeChildren[i], CancelledResult());

            ESAdvancedDialogWindow[] untrackedChildren = activeWindows
                .Where(item => IsLive(item)
                    && !ReferenceEquals(item, owner)
                    && ReferenceEquals(item.Owner, owner)
                    && FindActiveOperation(item) == null)
                .ToArray();
            for (int i = 0; i < untrackedChildren.Length; i++)
                CloseWindowBestEffort(untrackedChildren[i]);
        }

        private static void CancelSubscriber(
            DialogOperation operation,
            DialogSubscriber subscriber,
            CancellationToken cancellationToken)
        {
            if (operation == null || subscriber == null)
                return;
            bool noSubscribersRemain = operation.CancelSubscriber(
                subscriber,
                cancellationToken,
                CancelledResult());
            if (!noSubscribersRemain)
                return;
            CancelOperation(operation, CancelledResult());
            if (!shuttingDown && !openingReplacement)
                TryDrainQueue();
        }

        private static void CancelOperation(
            DialogOperation operation,
            ESAdvancedDialogResult result)
        {
            if (operation == null || operation.state == DialogOperationState.Completed)
                return;
            operation.BeginClosing(
                ResolveOperationResult(operation, result ?? CancelledResult()));
            ESAdvancedDialogWindow window = operation.window;
            if (IsLive(window))
            {
                try
                {
                    window.CancelAndClose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                // CancelAndClose can return without closing when the window has
                // already published a build failure. Shutdown and owner loss must
                // still remove that native surface before detaching the operation.
                bool windowClosed = CloseWindowBestEffort(window);
                if (operation.state != DialogOperationState.Completed && !windowClosed)
                {
                    ScheduleCloseRetry(operation);
                    Debug.LogError(
                        "ES 对话框取消后窗口仍然存活；operation 保留在治理表中，等待真实关闭。",
                        window);
                    UpdateOwnerLifetimeMonitor();
                    return;
                }
            }
            if (operation.state != DialogOperationState.Completed)
                CompleteOperation(
                    operation,
                    ResolveOperationResult(operation, result ?? CancelledResult()));
        }

        private static bool CloseWindowBestEffort(ESAdvancedDialogWindow window)
        {
            if (!IsLive(window))
                return true;
            try
            {
                window.Close();
            }
            catch (Exception closeException)
            {
                Debug.LogException(closeException);
            }
            if (!IsLive(window))
                return true;
            try
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
            catch (Exception destroyException)
            {
                Debug.LogException(destroyException);
            }
            return !IsLive(window);
        }

        private static void CompleteOperation(
            DialogOperation operation,
            ESAdvancedDialogResult result)
        {
            if (operation == null || operation.state == DialogOperationState.Completed)
                return;
            ESAdvancedDialogResult resolvedResult = operation.CaptureTerminalResult(
                result ?? CancelledResult());
            DetachOperation(operation);
            operation.CompleteOnce(resolvedResult ?? CancelledResult());
            UpdateOwnerLifetimeMonitor();
        }

        internal static void CloseCompletedWindow(ESAdvancedDialogWindow window)
        {
            if (ReferenceEquals(window, null))
                return;
            DialogOperation operation = FindActiveOperation(window);
            ESAdvancedDialogResult result = WindowResult(window) ?? CancelledResult();
            operation?.BeginClosing(ResolveOperationResult(operation, result));
            bool windowClosed = CloseWindowBestEffort(window);
            if (operation != null
                && operation.state != DialogOperationState.Completed
                && windowClosed)
            {
                CompleteOperation(operation, ResolveOperationResult(operation, result));
            }
            else if (!windowClosed)
            {
                TrackUnclosedWindow(
                    window,
                    "ES 对话框提交结果后仍然存活；窗口已保留在治理表中继续收敛。");
            }
            UpdateOwnerLifetimeMonitor();
        }

        private static void DetachOperation(DialogOperation operation)
        {
            if (operation == null)
                return;
            RemovePendingRegistration(operation);
            activeOperations.Remove(operation);
            ESAdvancedDialogWindow window = operation.window;
            for (int i = activeWindows.Count - 1; i >= 0; i--)
            {
                if (!IsLive(activeWindows[i])
                    || ReferenceEquals(activeWindows[i], window))
                    activeWindows.RemoveAt(i);
            }
            operation.window = null;
        }

        private static void RemovePendingRegistration(DialogOperation operation)
        {
            if (operation == null)
                return;
            RemoveScheduledCallback(operation);
            pendingDialogs.Remove(operation);
        }

        private static void RemoveScheduledCallback(DialogOperation operation)
        {
            if (operation == null)
                return;
            EditorApplication.CallbackFunction callback = operation.scheduledCallback;
            if (callback != null)
                EditorApplication.delayCall -= callback;
            operation.scheduledCallback = null;
            if (ReferenceEquals(scheduledOperation, operation))
                scheduledOperation = null;
        }

        private static bool IsLive(EditorWindow window) => window != null;

        private static bool IsOwnerInvalid(ESAdvancedDialogRequest request)
        {
            if (request == null || ReferenceEquals(request.owner, null))
                return false;
            if (IsClosingOwner(request.owner))
                return true;
            return !request.allowMainWorkspaceFallback && request.owner == null;
        }

        private static bool IsClosingOwner(EditorWindow owner)
        {
            if (!(owner is ESAdvancedDialogWindow dialog))
                return false;
            if (dialog.IsClosingOrCompleted)
                return true;
            DialogOperation operation = FindActiveOperation(dialog);
            return operation?.state == DialogOperationState.Closing
                || operation?.state == DialogOperationState.Completed;
        }

        private static ESAdvancedDialogResult ResolveOperationResult(
            DialogOperation operation,
            ESAdvancedDialogResult fallback)
        {
            if (operation?.terminalResult != null)
                return operation.terminalResult;
            ESAdvancedDialogResult windowResult = WindowResult(operation?.window);
            return windowResult ?? fallback;
        }

        private static ESAdvancedDialogResult WindowResult(
            ESAdvancedDialogWindow window)
        {
            return ReferenceEquals(window, null) ? null : window.LastResult;
        }

        private static void TrackUnclosedWindow(
            ESAdvancedDialogWindow window,
            string message)
        {
            if (!IsLive(window))
                return;
            if (!activeWindows.Contains(window))
                activeWindows.Add(window);
            ScheduleCloseRetry(FindActiveOperation(window));
            window.NextServiceCloseRetryAt =
                EditorApplication.timeSinceStartup + CloseRetryIntervalSeconds;
            Debug.LogError(message, window);
            UpdateOwnerLifetimeMonitor();
        }

        private static void ScheduleCloseRetry(DialogOperation operation)
        {
            if (operation == null || operation.state != DialogOperationState.Closing)
                return;
            operation.nextCloseRetryAt =
                EditorApplication.timeSinceStartup + CloseRetryIntervalSeconds;
        }

        private static ESAdvancedDialogResult CancelledResult()
        {
            return new ESAdvancedDialogResult { accepted = false, cancelled = true };
        }

        private static ESAdvancedDialogResult FailedResult(Exception exception)
        {
            return new ESAdvancedDialogResult
            {
                accepted = false,
                cancelled = false,
                actionId = string.Empty,
                exception = exception,
            };
        }

        private static void ValidateServiceRequest(ESAdvancedDialogRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ESAdvancedDialogWindow.ValidateRequest(request);
        }

        private static void Enqueue(DialogOperation operation, bool insertFirst)
        {
            if (pendingDialogs.Count >= MaximumPendingDialogs)
            {
                var exception = new InvalidOperationException(
                    "ES 对话框等待队列已达到上限 " + MaximumPendingDialogs
                    + "；请检查是否在循环或高频回调中重复提交对话框。");
                operation?.CompleteOnce(FailedResult(exception));
                throw exception;
            }
            operation.state = DialogOperationState.Queued;
            if (insertFirst)
                pendingDialogs.Insert(0, operation);
            else
                pendingDialogs.Add(operation);
            UpdateOwnerLifetimeMonitor();
        }

        internal static ESAdvancedDialogRequest SnapshotRequest(ESAdvancedDialogRequest source)
        {
            var snapshot = new ESAdvancedDialogRequest
            {
                dialogId = source.dialogId?.Trim() ?? string.Empty,
                title = source.title,
                subtitle = source.subtitle,
                message = source.message,
                detail = source.detail,
                confirmText = source.confirmText,
                cancelText = source.cancelText,
                minSize = source.minSize,
                preferredSize = source.preferredSize,
                positionMode = source.positionMode,
                customScreenPosition = source.customScreenPosition,
                positionOffset = source.positionOffset,
                tone = source.tone,
                showCancel = source.showCancel,
                animateOpening = source.animateOpening,
                closeOnEscape = source.closeOnEscape,
                allowOperationCancellation = source.allowOperationCancellation,
                queueBehindActiveDialog = source.queueBehindActiveDialog,
                allowMainWorkspaceFallback = source.allowMainWorkspaceFallback,
                asyncValidationDelayMs = source.asyncValidationDelayMs,
                initialFocusFieldId = source.initialFocusFieldId,
                owner = source.owner,
                duplicatePolicy = source.duplicatePolicy,
                validate = source.validate,
                validateDetailed = source.validateDetailed,
                validateAsync = source.validateAsync,
                confirmAsync = source.confirmAsync,
                createCustomContent = source.createCustomContent,
                releaseCustomContent = source.releaseCustomContent,
                completed = source.completed,
            };
            for (int i = 0; i < source.fields.Count; i++)
                snapshot.fields.Add(CloneField(source.fields[i]));
            for (int i = 0; i < source.auxiliaryActions.Count; i++)
                snapshot.auxiliaryActions.Add(CloneAction(source.auxiliaryActions[i]));
            return snapshot;
        }

        private static ESAdvancedDialogField CloneField(ESAdvancedDialogField source)
        {
            var field = new ESAdvancedDialogField(source.id, source.label, source.kind)
            {
                help = source.help,
                stringValue = source.stringValue,
                boolValue = source.boolValue,
                intValue = source.intValue,
                minIntValue = source.minIntValue,
                maxIntValue = source.maxIntValue,
                lowValueLabel = source.lowValueLabel,
                highValueLabel = source.highValueLabel,
                objectValue = source.objectValue,
                objectType = source.objectType,
                allowSceneObjects = source.allowSceneObjects,
                required = source.required,
                readOnly = source.readOnly,
                fileExtension = source.fileExtension,
                browseStartDirectory = source.browseStartDirectory,
                minimumSelections = source.minimumSelections,
                maximumSelections = source.maximumSelections,
            };
            field.choices.AddRange(source.choices);
            field.choiceValues.AddRange(source.choiceValues);
            field.selectedChoiceValues.AddRange(source.selectedChoiceValues);
            return field;
        }

        private static ESAdvancedDialogAction CloneAction(ESAdvancedDialogAction source)
        {
            var action = new ESAdvancedDialogAction(source.id, source.text, source.execute)
            {
                tooltip = source.tooltip,
                role = source.role,
                closeDialogAfterExecution = source.closeDialogAfterExecution,
                executeAsync = source.executeAsync,
            };
            return action;
        }

        internal static void Shutdown()
        {
            if (shuttingDown)
                return;
            shuttingDown = true;
            EditorApplication.update -= MonitorOwnerLifetime;
            ownerLifetimeMonitorInstalled = false;
            var cancelled = new ESAdvancedDialogResult
            {
                accepted = false,
                cancelled = true,
                actionId = string.Empty,
            };
            while (pendingDialogs.Count > 0)
            {
                DialogOperation operation = pendingDialogs[0];
                if (operation == null)
                    pendingDialogs.RemoveAt(0);
                else
                    CompleteOperation(operation, cancelled);
            }
            DialogOperation[] activeSnapshot = activeOperations.ToArray();
            for (int i = 0; i < activeSnapshot.Length; i++)
            {
                DialogOperation operation = activeSnapshot[i];
                if (operation != null)
                    CancelOperation(operation, cancelled);
            }
            scheduledOperation = null;
            activeWindows.RemoveAll(item => !IsLive(item));
            invalidPendingScratch.Clear();
            invalidOwnerScratch.Clear();
            closingOperationScratch.Clear();
            if (activeOperations.Count > 0 || HasLiveActiveWindow())
                Debug.LogError(
                    "ES 对话框关闭阶段仍有窗口无法退出；治理记录已保留，未将存活窗口静默遗弃。");
        }

        internal static void RestartAfterPresenterRegistration()
        {
            shuttingDown = false;
            UpdateOwnerLifetimeMonitor();
        }
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Dialog,
        "对话框由集中服务治理")]
    [ESWindowPresentationShortTitle("对话框")]
    public sealed class ESAdvancedDialogWindow : EditorWindow, IESWindowMultiInstanceContract
    {
        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => nameof(ESDialogService);

        private ESAdvancedDialogRequest request;
        private string validationMessage = string.Empty;
        private bool initialized;
        private bool completed;
        private Button confirmButton;
        private Label validationLabel;
        private VisualElement validationPanel;
        private ScrollView bodyScroll;
        private readonly Dictionary<string, VisualElement> fieldBlocks =
            new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> fieldControls =
            new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private string invalidFieldId = string.Empty;
        private ES.EditorInternal.ESWindowShell shell;
        private VisualElement dialogContent;
        private VisualElement customContent;
        private VisualElement busyOverlay;
        private Label busyLabel;
        private ProgressBar busyProgress;
        private Button cancelBusyButton;
        private VisualElement decisionActions;
        private VisualElement auxiliaryActions;
        private CancellationTokenSource operationCancellation;
        private CancellationTokenSource validationCancellation;
        private ESProgressHandle activeProgress;
        private IDisposable ownerInteractionHold;
        private IVisualElementScheduledItem busyRefreshSchedule;
        private int validationGeneration;
        private bool busy;
        private string busyMessage = string.Empty;
        private bool asyncValidationPending;
        private bool customContentReleased;
        private bool resultPublished;
        private bool buildFailureCloseScheduled;
        private bool initialPositionReapplyUpdateSubscribed;
        private bool initialPositionReapplyDelayScheduled;
        private IVisualElementScheduledItem initialPositionReapplySchedule;
        private bool initialPositionReappliedAfterAttach;
        private int initialPositionReapplyPasses;
        private double initialPositionReapplyDeadline;
        private bool hasInitialScreenPosition;
        private Rect initialScreenPosition;
        private bool openingAnimationStarted;
        private bool modalMode;
        private ESAdvancedDialogResult lastResult;
        private const int InitialPositionReapplyMaxPasses = 48;
        private const double InitialPositionReapplyDurationSeconds = 0.75d;
        private readonly List<TaskCompletionSource<ESAdvancedDialogResult>> completionObservers =
            new List<TaskCompletionSource<ESAdvancedDialogResult>>();

        internal string DialogId => request?.dialogId?.Trim() ?? string.Empty;
        internal EditorWindow Owner => request?.owner;
        internal bool HasInvalidOwner
            => request != null
                && !request.allowMainWorkspaceFallback
                && !ReferenceEquals(request.owner, null)
                && request.owner == null;
        internal ESAdvancedDialogResult LastResult => lastResult;
        internal bool IsClosingOrCompleted => completed;
        internal double NextServiceCloseRetryAt { get; set; }

        // Dialogs deliberately use a dedicated mint-green surface family so they remain
        // visually distinct from ordinary ES tool windows. Semantic warning/error colors
        // still win for validation, danger actions and tone indicators.
        private static Color DialogBaseSurface => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.070f, 0.125f, 0.095f, 1f)
            : new Color(0.875f, 0.955f, 0.895f, 1f);
        private static Color DialogRaisedSurface => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.095f, 0.165f, 0.120f, 1f)
            : new Color(0.925f, 0.975f, 0.940f, 1f);
        private static Color DialogInsetSurface => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.050f, 0.105f, 0.078f, 1f)
            : new Color(0.825f, 0.925f, 0.855f, 1f);
        private static Color DialogControlSurface => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.115f, 0.205f, 0.145f, 1f)
            : new Color(0.790f, 0.905f, 0.825f, 1f);
        private static Color DialogBorderColor => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.275f, 0.510f, 0.355f, 0.82f)
            : new Color(0.390f, 0.635f, 0.455f, 0.88f);
        private static Color DialogIdentityAccent => ES.EditorInternal.ESEditorPresentation.IsProSkin
            ? new Color(0.315f, 0.720f, 0.455f, 1f)
            : new Color(0.175f, 0.545f, 0.285f, 1f);

        internal void AddCompletionObserver(
            TaskCompletionSource<ESAdvancedDialogResult> completion)
        {
            if (completion == null)
                return;
            if (resultPublished && lastResult != null)
                completion.TrySetResult(lastResult);
            else
                completionObservers.Add(completion);
        }

        internal void RemoveCompletionObserver(
            TaskCompletionSource<ESAdvancedDialogResult> completion)
        {
            if (completion != null)
                completionObservers.Remove(completion);
        }

        internal void CancelAndClose()
        {
            if (busy && operationCancellation != null)
                operationCancellation.Cancel();
            Complete(false);
        }

        /// <summary>
        /// 打开一个独立的 Utility 窗口。调用方只能读取确认结果；任何业务动作必须由 completed 回调之后的调用方自行执行。
        /// </summary>
        [Obsolete("Use ESDialogService.Show instead.", false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ESAdvancedDialogWindow Show(ESAdvancedDialogRequest request)
        {
            return ESDialogService.Show(request);
        }

        [Obsolete("Use ESDialogService.ShowAsync instead.", false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static Task<ESAdvancedDialogResult> ShowAsync(
            ESAdvancedDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            return ESDialogService.ShowAsync(request, cancellationToken);
        }

        [Obsolete("Use ESDialogService.ShowModal instead.", false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ESAdvancedDialogResult ShowModal(ESAdvancedDialogRequest request)
        {
            return ESDialogService.ShowModal(request);
        }

        internal static ESAdvancedDialogWindow Create(
            ESAdvancedDialogRequest request,
            TaskCompletionSource<ESAdvancedDialogResult> completion)
        {
            ValidateRequest(request);
            var window = CreateInstance<ESAdvancedDialogWindow>();
            try
            {
                if (completion != null)
                    window.completionObservers.Add(completion);
                window.Initialize(request);
                window.titleContent = new GUIContent(BuildNativeTitle(request.title));
                window.ApplyInitialPosition();
                return window;
            }
            catch
            {
                try
                {
                    if (window != null)
                        ESDialogService.CloseCompletedWindow(window);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException);
                }
                if (window != null)
                {
                    try
                    {
                        window.ReleaseWindowResources();
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogException(cleanupException);
                    }
                }
                throw;
            }
        }

        internal void Internal_OpenFromDialogService(bool modal)
        {
            modalMode = modal;
            titleContent = new GUIContent(
                BuildNativeTitle(request?.title, modalMode, request?.tone ?? ESDialogTone.Info));
            initialPositionReappliedAfterAttach = false;
            ScheduleInitialPositionReapply();
            if (modal)
            {
                // Apply once before entering Unity's native modal host as well as
                // during the bounded post-attach repair window. Some Unity versions
                // keep the modal call on a nested loop and do not dispatch Editor.update
                // until that loop yields.
                ReapplyInitialPosition(false);
                ShowModalUtility();
                // ShowModalUtility pumps a nested editor loop and may return only after
                // the user closes the window. The update loop above is the actual
                // opening guard; this final write also covers a host that returns while
                // the native window is still live.
                ReapplyInitialPosition(false);
                StopInitialPositionReapplyLoop();
                return;
            }
            ShowUtility();
            ReapplyInitialPosition(false);
            Focus();
        }

        private void ScheduleInitialPositionReapply()
        {
            if (!hasInitialScreenPosition || initialPositionReapplyUpdateSubscribed)
                return;
            initialPositionReapplyPasses = 0;
            initialPositionReapplyDeadline =
                EditorApplication.timeSinceStartup + InitialPositionReapplyDurationSeconds;
            initialPositionReapplyUpdateSubscribed = true;
            EditorApplication.update -= ReapplyInitialPositionOnEditorUpdate;
            EditorApplication.update += ReapplyInitialPositionOnEditorUpdate;
            initialPositionReapplyDelayScheduled = true;
            EditorApplication.delayCall -= ReapplyInitialPositionOnDelayCall;
            EditorApplication.delayCall += ReapplyInitialPositionOnDelayCall;

            // ShowModalUtility can enter Unity's nested native loop before the
            // outer EditorApplication.update stream gets a turn. A bounded
            // UI Toolkit schedule is attached to this window as a second,
            // local repair path so the native host gets the target position
            // after the panel is mounted without leaving a global callback.
            initialPositionReapplySchedule?.Pause();
            initialPositionReapplySchedule = rootVisualElement.schedule
                .Execute(ReapplyInitialPositionOnScheduledLayout)
                .Every(16);
        }

        private void ReapplyInitialPositionOnDelayCall()
        {
            initialPositionReapplyDelayScheduled = false;
            if (this == null || !hasInitialScreenPosition)
                return;
            try
            {
                ReapplyInitialPosition(false);
                initialPositionReapplyPasses++;
                // Unity's native utility host can perform one more geometry pass
                // after ShowUtility/ShowModalUtility. Keep a tiny delay-call tail
                // so that pass cannot leave a valid dialog stranded at (0, 0).
                if (initialPositionReapplyPasses < 3
                    && EditorApplication.timeSinceStartup < initialPositionReapplyDeadline)
                {
                    initialPositionReapplyDelayScheduled = true;
                    EditorApplication.delayCall -= ReapplyInitialPositionOnDelayCall;
                    EditorApplication.delayCall += ReapplyInitialPositionOnDelayCall;
                }
            }
            catch (MissingReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
            catch (NullReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
        }

        private void ReapplyInitialPositionOnScheduledLayout()
        {
            if (this == null || !hasInitialScreenPosition)
            {
                StopInitialPositionReapplyLoop();
                return;
            }

            try
            {
                ReapplyInitialPosition(false);
                initialPositionReapplyPasses++;
                if (initialPositionReapplyPasses >= InitialPositionReapplyMaxPasses
                    || EditorApplication.timeSinceStartup >= initialPositionReapplyDeadline)
                    CompleteInitialPositionReapply();
            }
            catch (MissingReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
            catch (NullReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
        }

        private void ReapplyInitialPositionOnEditorUpdate()
        {
            if (this == null || !hasInitialScreenPosition)
            {
                StopInitialPositionReapplyLoop();
                return;
            }

            try
            {
                ReapplyInitialPosition(false);
                initialPositionReapplyPasses++;
                if (initialPositionReapplyPasses >= InitialPositionReapplyMaxPasses
                    || EditorApplication.timeSinceStartup >= initialPositionReapplyDeadline)
                    CompleteInitialPositionReapply();
            }
            catch (MissingReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
            catch (NullReferenceException)
            {
                StopInitialPositionReapplyLoop();
            }
        }

        private void ReapplyInitialPosition(bool finalizeOpening)
        {
            if (this == null || !hasInitialScreenPosition)
                return;
            position = initialScreenPosition;
            if (finalizeOpening)
                CompleteInitialPositionReapply();
        }

        private void CompleteInitialPositionReapply()
        {
            StopInitialPositionReapplyLoop();
            if (this == null || !hasInitialScreenPosition)
                return;

            position = initialScreenPosition;
            if (request?.animateOpening == true && !openingAnimationStarted)
            {
                ES.EditorInternal.ESWindowFrameActivation.Stop(this, true);
                openingAnimationStarted = true;
                Focus();
                ES.EditorInternal.ESWindowFrameActivation.Play(this, initialScreenPosition);
            }
        }

        private void StopInitialPositionReapplyLoop()
        {
            if (initialPositionReapplyUpdateSubscribed)
                EditorApplication.update -= ReapplyInitialPositionOnEditorUpdate;
            if (initialPositionReapplyDelayScheduled)
                EditorApplication.delayCall -= ReapplyInitialPositionOnDelayCall;
            initialPositionReapplyUpdateSubscribed = false;
            initialPositionReapplyDelayScheduled = false;
            initialPositionReapplySchedule?.Pause();
            initialPositionReapplySchedule = null;
            initialPositionReapplyPasses = 0;
            initialPositionReapplyDeadline = 0d;
        }

        private void Initialize(ESAdvancedDialogRequest value)
        {
            ownerInteractionHold?.Dispose();
            ownerInteractionHold = null;
            request = value;
            initialized = true;
            RefreshValidation();
            ownerInteractionHold = ESWindowFoundation.HoldInteraction(
                request.owner,
                "ESAdvancedDialog");
        }

        public void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            PrepareForVisualTreeRebuild();
            rootVisualElement.Clear();
            fieldBlocks.Clear();
            fieldControls.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.minWidth = 0f;
            rootVisualElement.style.backgroundColor = DialogBaseSurface;

            if (!initialized || request == null)
            {
                BuildExpiredView();
                ESWindowFoundation.BindTransient(this);
                return;
            }

            Texture titleIcon = ES.EditorInternal.ESEditorPresentation.ResolveDefaultWindowIcon(
                this,
                request.title,
                null);
            shell = new ES.EditorInternal.ESWindowShell(
                request.title,
                request.subtitle,
                false,
                titleIcon);
            shell.Root.AddToClassList("es-dialog-window");
            shell.Root.AddToClassList("es-dialog-branded");
            shell.Root.AddToClassList(GetToneClass(request.tone));
            ApplyDialogPalette();
            BuildDialogIdentityStrip();
            shell.Toolbar.style.display = DisplayStyle.None;
            shell.Content.style.flexDirection = FlexDirection.Column;
            Texture closeIcon = LoadFirstUnityIcon(
                "d_winbtn_win_close", "winbtn_win_close", "d_CloseButton", "CloseButton");
            shell.HeaderToolbar.Add(ES.EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                closeIcon,
                closeIcon == null ? "×" : string.Empty,
                "关闭对话框",
                () =>
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                    Complete(false);
                }));
            rootVisualElement.Add(shell.Root);

            dialogContent = new VisualElement { name = "ESDialogContent" };
            dialogContent.AddToClassList("es-dialog-content");
            dialogContent.style.flexGrow = 1f;
            dialogContent.style.flexShrink = 1f;
            dialogContent.style.minWidth = 0f;
            dialogContent.style.minHeight = 0f;
            dialogContent.style.flexDirection = FlexDirection.Column;
            dialogContent.style.backgroundColor = DialogBaseSurface;
            shell.Content.Add(dialogContent);

            bodyScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ESDialogBodyScroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            bodyScroll.AddToClassList("es-dialog-body");
            bodyScroll.style.flexGrow = 1f;
            bodyScroll.style.flexShrink = 1f;
            bodyScroll.style.minWidth = 0f;
            bodyScroll.style.minHeight = 0f;
            bodyScroll.style.backgroundColor = DialogBaseSurface;
            bodyScroll.contentContainer.style.backgroundColor = DialogBaseSurface;
            bodyScroll.contentContainer.style.paddingLeft = 18f;
            bodyScroll.contentContainer.style.paddingRight = 18f;
            bodyScroll.contentContainer.style.paddingTop = 16f;
            bodyScroll.contentContainer.style.paddingBottom = 14f;
            dialogContent.Add(bodyScroll);

            BuildSummary(bodyScroll);
            for (int i = 0; i < request.fields.Count; i++)
                BuildField(bodyScroll, request.fields[i]);
            if (!BuildCustomContent(bodyScroll))
            {
                ESWindowFoundation.BindTransient(this);
                return;
            }
            BuildValidation(bodyScroll);
            BuildFooter(dialogContent);
            BuildBusyOverlay(shell.Content);
            RestoreBusyPresentation();

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            ESWindowFoundation.BindTransient(this);
            RefreshValidation();
            ScheduleInitialFocus();
        }

        private void PrepareForVisualTreeRebuild()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = null;
            CancelAsyncValidation();
            ReleaseCustomContent();
            customContentReleased = false;
            customContent = null;
            shell = null;
            dialogContent = null;
            bodyScroll = null;
            confirmButton = null;
            validationLabel = null;
            validationPanel = null;
            busyOverlay = null;
            busyLabel = null;
            busyProgress = null;
            cancelBusyButton = null;
            decisionActions = null;
            auxiliaryActions = null;
        }

        private void ApplyDialogPalette()
        {
            if (shell == null)
                return;

            shell.Root.style.backgroundColor = DialogBaseSurface;
            shell.Header.style.backgroundColor = DialogRaisedSurface;
            shell.Header.style.borderBottomColor = DialogBorderColor;
            shell.Toolbar.style.backgroundColor = DialogRaisedSurface;
            shell.Content.style.backgroundColor = DialogBaseSurface;
            shell.StatusBar.style.backgroundColor = DialogRaisedSurface;
            shell.StatusBar.style.borderTopColor = DialogBorderColor;
        }

        private void BuildDialogIdentityStrip()
        {
            if (shell?.Header == null)
                return;

            Color accent = DialogIdentityAccent;
            var identity = new VisualElement { name = "ESDialogIdentityStrip" };
            identity.AddToClassList("es-dialog-identity-strip");
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.flexWrap = Wrap.Wrap;
            identity.style.alignItems = Align.Center;
            identity.style.minWidth = 0f;
            identity.style.marginBottom = 7f;
            identity.style.paddingLeft = 8f;
            identity.style.paddingRight = 8f;
            identity.style.paddingTop = 5f;
            identity.style.paddingBottom = 5f;
            identity.style.minHeight = 46f;
            identity.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.22f);
            identity.style.borderTopWidth = 1f;
            identity.style.borderTopColor = accent;
            identity.style.borderBottomWidth = 1f;
            identity.style.borderBottomColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            identity.style.borderLeftWidth = 4f;
            identity.style.borderLeftColor = accent;

            var brandMark = new Label("ES") { name = "ESDialogBrandMark" };
            brandMark.AddToClassList("es-dialog-brand-mark");
            brandMark.style.width = 30f;
            brandMark.style.height = 30f;
            brandMark.style.minWidth = 30f;
            brandMark.style.unityTextAlign = TextAnchor.MiddleCenter;
            brandMark.style.unityFontStyleAndWeight = FontStyle.Bold;
            brandMark.style.color = GetReadableActionTextColor(accent);
            brandMark.style.backgroundColor = accent;
            brandMark.style.marginRight = 8f;
            identity.Add(brandMark);

            var layer = new Label("对话交互层") { name = "ESDialogLayerLabel" };
            layer.AddToClassList("es-dialog-layer-label");
            layer.style.unityFontStyleAndWeight = FontStyle.Bold;
            layer.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
            layer.style.marginRight = 10f;
            identity.Add(layer);

            var badge = new Label("ES 对话框") { name = "ESDialogIdentityBadge" };
            badge.AddToClassList("es-dialog-identity-badge");
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = accent;
            badge.style.marginRight = 8f;
            identity.Add(badge);

            var mode = new Label(modalMode ? "模态" : "非模态") { name = "ESDialogModeBadge" };
            mode.AddToClassList("es-dialog-mode-badge");
            mode.style.color = GetReadableActionTextColor(accent);
            mode.style.backgroundColor = accent;
            mode.style.paddingLeft = 6f;
            mode.style.paddingRight = 6f;
            mode.style.paddingTop = 2f;
            mode.style.paddingBottom = 2f;
            mode.style.marginRight = 8f;
            identity.Add(mode);

            var tone = new Label(GetToneLabel(request.tone)) { name = "ESDialogToneBadge" };
            tone.AddToClassList("es-dialog-tone-badge");
            tone.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            tone.style.overflow = Overflow.Hidden;
            tone.style.textOverflow = TextOverflow.Ellipsis;
            identity.Add(tone);

            var policy = new Label("仅输入 / 确认") { name = "ESDialogPolicyBadge" };
            policy.AddToClassList("es-dialog-policy-badge");
            policy.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            policy.style.marginLeft = 8f;
            policy.text = request.allowMainWorkspaceFallback
                ? "仅输入 / 确认 · 主工作区"
                : "仅输入 / 确认 · 有 owner";
            policy.tooltip = "ES 对话框只收集输入并返回结果；写资产、发布、删除、保存等业务权限仍由调用方正式入口执行。";
            identity.Add(policy);

            var idLabel = new Label("ID: " + request.dialogId)
            {
                name = "ESDialogStableId",
                tooltip = request.dialogId,
            };
            idLabel.AddToClassList("es-dialog-stable-id");
            idLabel.style.minWidth = 0f;
            idLabel.style.maxWidth = Length.Percent(100f);
            idLabel.style.flexShrink = 1f;
            idLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            idLabel.style.overflow = Overflow.Hidden;
            idLabel.style.textOverflow = TextOverflow.Ellipsis;
            idLabel.style.marginLeft = 8f;
            identity.Add(idLabel);
            shell.Header.Insert(0, identity);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (initialPositionReappliedAfterAttach
                || !hasInitialScreenPosition
                || position.width <= 0f
                || position.height <= 0f)
                return;

            // The native utility host can overwrite the position between CreateGUI
            // and later layout passes. Restore the clamped screen-space target here;
            // the bounded Editor update loop continues to cover subsequent host writes.
            initialPositionReappliedAfterAttach = true;
            position = initialScreenPosition;
        }

        private bool BuildCustomContent(VisualElement parent)
        {
            if (request.createCustomContent == null)
                return true;
            try
            {
                customContent = request.createCustomContent(BuildValues());
                if (customContent == null)
                    return true;
                customContent.name = string.IsNullOrWhiteSpace(customContent.name)
                    ? "ESDialogCustomContent"
                    : customContent.name;
                customContent.style.minWidth = 0f;
                customContent.style.marginBottom = 11f;
                parent.Add(customContent);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CompleteWithFailureAndScheduleClose(exception);
                return false;
            }
        }

        private void CompleteWithFailureAndScheduleClose(Exception exception)
        {
            if (!completed && request != null)
            {
                completed = true;
                CancelAsyncValidation();
                operationCancellation?.Cancel();
                lastResult = new ESAdvancedDialogResult
                {
                    accepted = false,
                    cancelled = false,
                    actionId = string.Empty,
                    values = BuildValues(),
                    exception = exception,
                };
            }
            if (buildFailureCloseScheduled || this == null)
                return;
            buildFailureCloseScheduled = true;
            EditorApplication.delayCall -= CloseAfterBuildFailure;
            EditorApplication.delayCall += CloseAfterBuildFailure;
        }

        private void CloseAfterBuildFailure()
        {
            EditorApplication.delayCall -= CloseAfterBuildFailure;
            buildFailureCloseScheduled = false;
            if (this != null)
                ESDialogService.CloseCompletedWindow(this);
        }

        private void BuildBusyOverlay(VisualElement parent)
        {
            busyOverlay = new VisualElement { name = "ESDialogBusyOverlay" };
            busyOverlay.AddToClassList("es-dialog-busy-overlay");
            busyOverlay.style.position = Position.Absolute;
            busyOverlay.style.left = 0f;
            busyOverlay.style.right = 0f;
            busyOverlay.style.top = 0f;
            busyOverlay.style.bottom = 0f;
            busyOverlay.style.display = DisplayStyle.None;
            busyOverlay.style.justifyContent = Justify.Center;
            busyOverlay.style.alignItems = Align.Center;
            busyOverlay.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.IsProSkin
                ? new Color(0.025f, 0.075f, 0.048f, 0.92f)
                : new Color(0.255f, 0.420f, 0.300f, 0.72f);

            var panel = new VisualElement();
            panel.AddToClassList("es-dialog-busy-card");
            panel.style.width = 320f;
            panel.style.maxWidth = Length.Percent(86f);
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 16f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                panel,
                DialogRaisedSurface,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Overlay,
                DialogBorderColor);
            busyLabel = new Label("正在处理");
            busyLabel.style.whiteSpace = WhiteSpace.Normal;
            busyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(busyLabel);
            busyProgress = new ProgressBar { value = 0f, title = "正在准备" };
            busyProgress.style.marginTop = 8f;
            panel.Add(busyProgress);
            cancelBusyButton = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                "取消任务",
                "请求当前操作安全取消。",
                CancelBusyOperation);
            cancelBusyButton.style.marginTop = 9f;
            cancelBusyButton.style.alignSelf = Align.FlexEnd;
            panel.Add(cancelBusyButton);
            busyOverlay.Add(panel);
            parent.Add(busyOverlay);
        }

        private void RestoreBusyPresentation()
        {
            if (!busy || busyOverlay == null)
                return;

            bool cancellationRequested = operationCancellation?.IsCancellationRequested == true
                || activeProgress?.IsCancellationRequested == true;
            busyOverlay.style.display = DisplayStyle.Flex;
            if (busyLabel != null)
            {
                busyLabel.text = cancellationRequested
                    ? "正在取消"
                    : string.IsNullOrWhiteSpace(busyMessage) ? "正在处理" : busyMessage;
            }
            if (cancelBusyButton != null)
            {
                cancelBusyButton.style.display = request?.allowOperationCancellation == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                    cancelBusyButton,
                    request?.allowOperationCancellation == true && !cancellationRequested);
            }
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(decisionActions, false);
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(auxiliaryActions, false);
            RefreshBusyOverlay();
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = rootVisualElement.schedule
                .Execute(RefreshBusyOverlay)
                .Every(100);
        }

        private void ScheduleInitialFocus()
        {
            rootVisualElement.schedule.Execute(() =>
            {
                if (this == null || completed || busy)
                    return;
                VisualElement target = null;
                if (!string.IsNullOrWhiteSpace(request.initialFocusFieldId))
                    fieldControls.TryGetValue(request.initialFocusFieldId.Trim(), out target);
                target ??= fieldControls.Values.FirstOrDefault(element => element.enabledInHierarchy);
                target?.Focus();
            });
        }

        private void BuildSummary(VisualElement parent)
        {
            if (string.IsNullOrWhiteSpace(request.message) && string.IsNullOrWhiteSpace(request.detail))
                return;

            Color accent = GetToneAccent(request.tone);
            VisualElement summary = new VisualElement { name = "ESDialogSummary" };
            summary.AddToClassList("es-dialog-summary");
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.FlexStart;
            summary.style.minWidth = 0f;
            summary.style.marginBottom = request.fields.Count > 0 ? 14f : 4f;
            summary.style.paddingLeft = 12f;
            summary.style.paddingRight = 12f;
            summary.style.paddingTop = 10f;
            summary.style.paddingBottom = 10f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                summary,
                DialogRaisedSurface,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Section,
                DialogBorderColor);
            summary.style.borderLeftWidth = 3f;
            summary.style.borderLeftColor = accent;

            Texture toneIcon = ResolveToneIcon(request.tone);
            if (toneIcon != null)
            {
                VisualElement iconSurface = new VisualElement { name = "ESDialogToneIconSurface" };
                iconSurface.style.width = 34f;
                iconSurface.style.height = 34f;
                iconSurface.style.minWidth = 34f;
                iconSurface.style.marginRight = 10f;
                iconSurface.style.justifyContent = Justify.Center;
                iconSurface.style.alignItems = Align.Center;
                ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                    iconSurface,
                    new Color(accent.r, accent.g, accent.b, 0.14f),
                    ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                    new Color(accent.r, accent.g, accent.b, 0.48f));
                Image icon = new Image
                {
                    image = toneIcon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                };
                icon.style.width = 19f;
                icon.style.height = 19f;
                iconSurface.Add(icon);
                summary.Add(iconSurface);
            }

            VisualElement summaryText = new VisualElement { name = "ESDialogSummaryText" };
            summaryText.style.flexGrow = 1f;
            summaryText.style.minWidth = 0f;

            if (!string.IsNullOrWhiteSpace(request.message))
            {
                Label message = new Label(request.message.Trim()) { name = "ESDialogMessage" };
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.fontSize = 13f;
                message.style.unityFontStyleAndWeight = FontStyle.Bold;
                message.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
                summaryText.Add(message);
            }

            if (!string.IsNullOrWhiteSpace(request.detail))
            {
                Label detail = new Label(request.detail.Trim()) { name = "ESDialogDetail" };
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.marginTop = string.IsNullOrWhiteSpace(request.message) ? 0f : 6f;
                detail.style.fontSize = 11f;
                detail.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                summaryText.Add(detail);
            }

            summary.Add(summaryText);

            parent.Add(summary);
        }

        private void BuildField(VisualElement parent, ESAdvancedDialogField field)
        {
            VisualElement block = new VisualElement { name = "ESDialogField-" + field.id };
            block.AddToClassList("es-dialog-field");
            block.style.minWidth = 0f;
            block.style.marginBottom = 11f;
            block.style.paddingLeft = 10f;
            block.style.paddingRight = 10f;
            block.style.paddingTop = 8f;
            block.style.paddingBottom = 8f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                block,
                DialogInsetSurface,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                DialogBorderColor);

            Label label = new Label(field.label + (field.required ? " *" : string.Empty));
            label.style.marginBottom = 4f;
            label.style.fontSize = 11f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = field.required
                ? GetToneAccent(ESDialogTone.Info)
                : ES.EditorInternal.ESEditorPresentation.SectionTextColor;
            block.Add(label);

            VisualElement control = CreateFieldControl(field);
            control.style.minWidth = 0f;
            control.style.flexGrow = 1f;
            if (field.readOnly && !(control is TextField))
                control.SetEnabled(false);
            block.Add(control);
            fieldBlocks[field.id] = block;
            fieldControls[field.id] = control;

            if (!string.IsNullOrWhiteSpace(field.help))
            {
                Label help = new Label(field.help.Trim());
                help.style.whiteSpace = WhiteSpace.Normal;
                help.style.marginTop = 4f;
                help.style.fontSize = 10f;
                help.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                block.Add(help);
            }

            parent.Add(block);
        }

        private VisualElement CreateFieldControl(ESAdvancedDialogField field)
        {
            switch (field.kind)
            {
                case ESAdvancedDialogFieldKind.Text:
                case ESAdvancedDialogFieldKind.MultilineText:
                {
                    TextField text = new TextField
                    {
                        value = field.stringValue ?? string.Empty,
                        multiline = field.kind == ESAdvancedDialogFieldKind.MultilineText,
                        isReadOnly = field.readOnly,
                    };
                    if (text.multiline)
                        text.style.minHeight = 76f;
                    text.RegisterValueChangedCallback(evt =>
                    {
                        field.stringValue = evt.newValue ?? string.Empty;
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Type);
                        RefreshValidation();
                    });
                    return text;
                }
                case ESAdvancedDialogFieldKind.Toggle:
                {
                    Toggle toggle = new Toggle { value = field.boolValue };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        field.boolValue = evt.newValue;
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                        RefreshValidation();
                    });
                    return toggle;
                }
                case ESAdvancedDialogFieldKind.Choice:
                {
                    int selectedIndex = Mathf.Max(0, field.choiceValues.IndexOf(field.stringValue));
                    DropdownField dropdown = new DropdownField(field.choices, selectedIndex);
                    dropdown.RegisterValueChangedCallback(evt =>
                    {
                        int index = field.choices.IndexOf(evt.newValue);
                        if (index >= 0 && index < field.choiceValues.Count)
                            field.stringValue = field.choiceValues[index];
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                        RefreshValidation();
                    });
                    return dropdown;
                }
                case ESAdvancedDialogFieldKind.MultiChoice:
                    return CreateMultiChoiceField(field);
                case ESAdvancedDialogFieldKind.Recommendation:
                    return CreateRecommendationField(field);
                case ESAdvancedDialogFieldKind.FolderPath:
                    return CreatePathField(field, true);
                case ESAdvancedDialogFieldKind.FilePath:
                    return CreatePathField(field, false);
                case ESAdvancedDialogFieldKind.Object:
                {
                    ObjectField objectField = new ObjectField
                    {
                        objectType = field.objectType,
                        allowSceneObjects = field.allowSceneObjects,
                        value = field.objectValue,
                    };
                    objectField.RegisterValueChangedCallback(evt =>
                    {
                        field.objectValue = evt.newValue;
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                        RefreshValidation();
                    });
                    return objectField;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private VisualElement CreateMultiChoiceField(ESAdvancedDialogField field)
        {
            VisualElement group = new VisualElement { name = "ESDialogMultiChoice-" + field.id };
            group.AddToClassList("es-dialog-multichoice");
            group.style.minWidth = 0f;
            group.style.paddingLeft = 8f;
            group.style.paddingRight = 8f;
            group.style.paddingTop = 5f;
            group.style.paddingBottom = 5f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                group,
                DialogControlSurface,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                DialogBorderColor);

            Label selectionStatus = new Label();
            selectionStatus.style.marginBottom = 4f;
            selectionStatus.style.fontSize = 9f;
            group.Add(selectionStatus);

            var controls = new List<Toggle>(field.choices.Count);
            for (int i = 0; i < field.choices.Count; i++)
            {
                int optionIndex = i;
                string optionId = field.choiceValues[optionIndex];
                Toggle option = new Toggle(field.choices[optionIndex])
                {
                    value = field.selectedChoiceValues.Contains(optionId),
                };
                option.style.minHeight = 24f;
                option.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        if (!field.selectedChoiceValues.Contains(optionId))
                            field.selectedChoiceValues.Add(optionId);
                    }
                    else
                    {
                        field.selectedChoiceValues.Remove(optionId);
                    }
                    RefreshMultiChoiceAvailability(field, controls, selectionStatus);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    RefreshValidation();
                });
                controls.Add(option);
                group.Add(option);
            }
            RefreshMultiChoiceAvailability(field, controls, selectionStatus);
            return group;
        }

        private static void RefreshMultiChoiceAvailability(
            ESAdvancedDialogField field,
            List<Toggle> controls,
            Label selectionStatus)
        {
            bool limitReached = field.maximumSelections > 0
                && field.selectedChoiceValues.Count >= field.maximumSelections;
            for (int i = 0; i < controls.Count; i++)
            {
                Toggle option = controls[i];
                string optionId = field.choiceValues[i];
                option.SetEnabled(!field.readOnly
                    && (!limitReached || field.selectedChoiceValues.Contains(optionId)));
            }

            int selectedCount = field.selectedChoiceValues.Count;
            string maximum = field.maximumSelections > 0
                ? " / 最多 " + field.maximumSelections
                : string.Empty;
            string minimum = field.minimumSelections > 0
                ? " / 至少 " + field.minimumSelections
                : string.Empty;
            selectionStatus.text = "已选 " + selectedCount + maximum + minimum;
            selectionStatus.style.color = selectedCount < field.minimumSelections
                ? ES.EditorInternal.ESEditorPresentation.WarningTextColor
                : ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
        }

        private VisualElement CreateRecommendationField(ESAdvancedDialogField field)
        {
            VisualElement group = new VisualElement();
            group.AddToClassList("es-dialog-recommendation");
            group.style.minWidth = 0f;

            Label valueLabel = new Label();
            valueLabel.style.marginBottom = 4f;
            valueLabel.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
            UpdateRecommendationLabel(field, valueLabel);
            group.Add(valueLabel);

            int levelCount = field.maxIntValue - field.minIntValue + 1;
            if (levelCount <= 9)
                group.Add(CreateRecommendationButtons(field, valueLabel));
            else
                group.Add(CreateRecommendationSlider(field, valueLabel));

            VisualElement endpoints = new VisualElement();
            endpoints.style.flexDirection = FlexDirection.Row;
            endpoints.style.marginTop = 2f;
            Label low = new Label(field.lowValueLabel ?? string.Empty);
            low.style.flexGrow = 1f;
            low.style.fontSize = 9f;
            low.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            Label high = new Label(field.highValueLabel ?? string.Empty);
            high.style.fontSize = 9f;
            high.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
            endpoints.Add(low);
            endpoints.Add(high);
            group.Add(endpoints);
            return group;
        }

        private VisualElement CreateRecommendationButtons(
            ESAdvancedDialogField field,
            Label valueLabel)
        {
            var row = new VisualElement { name = "ESDialogRecommendationOptions" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0f;
            var buttons = new List<Button>(field.maxIntValue - field.minIntValue + 1);

            for (int level = field.minIntValue; level <= field.maxIntValue; level++)
            {
                int selectedLevel = level;
                var button = new Button(() =>
                {
                    if (field.readOnly || field.intValue == selectedLevel)
                        return;
                    field.intValue = selectedLevel;
                    UpdateRecommendationLabel(field, valueLabel);
                    RefreshRecommendationButtons(field, buttons);
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                    RefreshValidation();
                })
                {
                    name = "ESDialogRecommendationOption-" + level,
                    text = level.ToString(),
                    tooltip = BuildRecommendationTooltip(field, level),
                };
                button.style.flexGrow = 1f;
                button.style.flexBasis = 34f;
                button.style.minWidth = 34f;
                button.style.minHeight = 30f;
                button.style.marginRight = level < field.maxIntValue ? 4f : 0f;
                button.style.marginBottom = 3f;
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.SetEnabled(!field.readOnly);
                buttons.Add(button);
                row.Add(button);
            }

            RefreshRecommendationButtons(field, buttons);
            return row;
        }

        private VisualElement CreateRecommendationSlider(
            ESAdvancedDialogField field,
            Label valueLabel)
        {
            var slider = new SliderInt(field.minIntValue, field.maxIntValue)
            {
                value = field.intValue,
                showInputField = true,
            };
            slider.name = "ESDialogRecommendationSlider";
            slider.style.minWidth = 0f;
            slider.SetEnabled(!field.readOnly);
            slider.RegisterValueChangedCallback(evt =>
            {
                field.intValue = evt.newValue;
                UpdateRecommendationLabel(field, valueLabel);
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                RefreshValidation();
            });
            return slider;
        }

        private static void RefreshRecommendationButtons(
            ESAdvancedDialogField field,
            List<Button> buttons)
        {
            Color selectedBackground = Color.Lerp(
                DialogRaisedSurface,
                ES.EditorInternal.ESEditorPresentation.PrimaryActionColor,
                0.90f);
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                int level = field.minIntValue + i;
                bool selected = level == field.intValue;
                Color background = selected
                    ? selectedBackground
                    : DialogControlSurface;
                Color border = selected
                    ? ES.EditorInternal.ESEditorPresentation.GetSemanticAccent(2)
                    : DialogBorderColor;
                button.style.backgroundColor = background;
                ES.EditorInternal.ESEditorPresentation.ApplyCornerRadius(
                    button,
                    ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Control);
                button.style.color = selected
                    ? ES.EditorInternal.ESEditorPresentation.PrimaryActionTextColor
                    : ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
                button.style.borderLeftWidth = selected ? 2f : 1f;
                button.style.borderRightWidth = selected ? 2f : 1f;
                button.style.borderTopWidth = selected ? 2f : 1f;
                button.style.borderBottomWidth = selected ? 2f : 1f;
                button.style.borderLeftColor = border;
                button.style.borderRightColor = border;
                button.style.borderTopColor = border;
                button.style.borderBottomColor = border;
            }
        }

        private static string BuildRecommendationTooltip(ESAdvancedDialogField field, int level)
        {
            if (level == field.minIntValue && !string.IsNullOrWhiteSpace(field.lowValueLabel))
                return level + " - " + field.lowValueLabel.Trim();
            if (level == field.maxIntValue && !string.IsNullOrWhiteSpace(field.highValueLabel))
                return level + " - " + field.highValueLabel.Trim();
            return "推荐程度 " + level + " / " + field.maxIntValue;
        }

        private static void UpdateRecommendationLabel(
            ESAdvancedDialogField field,
            Label label)
        {
            label.text = "推荐程度 " + field.intValue + " / " + field.maxIntValue;
        }

        private VisualElement CreatePathField(ESAdvancedDialogField field, bool folder)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0f;

            TextField path = new TextField
            {
                value = field.stringValue ?? string.Empty,
                isReadOnly = field.readOnly,
            };
            path.style.flexGrow = 1f;
            path.style.minWidth = 0f;
            path.RegisterValueChangedCallback(evt =>
            {
                field.stringValue = evt.newValue ?? string.Empty;
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Type);
                RefreshValidation();
            });
            row.Add(path);

            Button browse = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                "选择",
                folder ? "选择文件夹" : "选择文件",
                () => BrowsePath(field, path, folder));
            browse.style.flexShrink = 0f;
            browse.style.marginLeft = 6f;
            browse.style.marginRight = 0f;
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(browse, !field.readOnly);
            row.Add(browse);
            return row;
        }

        private static void BrowsePath(ESAdvancedDialogField field, TextField path, bool folder)
        {
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
            string startDirectory = string.IsNullOrWhiteSpace(field.browseStartDirectory)
                ? Application.dataPath
                : field.browseStartDirectory;
            string selected = folder
                ? EditorUtility.OpenFolderPanel(field.label, startDirectory, string.Empty)
                : EditorUtility.OpenFilePanel(field.label, startDirectory, field.fileExtension ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(selected))
                path.value = selected;
        }

        private void BuildValidation(VisualElement parent)
        {
            validationPanel = new VisualElement { name = "ESDialogValidation" };
            validationPanel.AddToClassList("es-dialog-validation");
            validationPanel.style.display = DisplayStyle.None;
            validationPanel.style.marginTop = 2f;
            validationPanel.style.paddingLeft = 10f;
            validationPanel.style.paddingRight = 10f;
            validationPanel.style.paddingTop = 8f;
            validationPanel.style.paddingBottom = 8f;
            ES.EditorInternal.ESEditorPresentation.ApplyRoundedSurface(
                validationPanel,
                ES.EditorInternal.ESEditorPresentation.WarningBackground,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Card,
                ES.EditorInternal.ESEditorPresentation.WarningColor);
            validationPanel.style.borderLeftWidth = 3f;
            validationPanel.tooltip = "单击定位第一个未通过校验的字段。";
            validationPanel.RegisterCallback<PointerDownEvent>(_ => RevealInvalidField());

            validationLabel = new Label();
            validationLabel.style.whiteSpace = WhiteSpace.Normal;
            validationLabel.style.color = ES.EditorInternal.ESEditorPresentation.WarningTextColor;
            validationPanel.Add(validationLabel);
            parent.Add(validationPanel);
        }

        private void BuildFooter(VisualElement parent)
        {
            VisualElement footer = new VisualElement { name = "ESDialogFooter" };
            footer.AddToClassList("es-dialog-footer");
            footer.style.flexShrink = 0f;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.flexWrap = Wrap.Wrap;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 14f;
            footer.style.paddingRight = 14f;
            footer.style.paddingTop = 9f;
            footer.style.paddingBottom = 9f;
            footer.style.backgroundColor = DialogRaisedSurface;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = DialogBorderColor;
            ES.EditorInternal.ESEditorPresentation.ApplyCornerRadius(
                footer,
                ES.EditorInternal.ESEditorPresentation.ESCornerRadiusToken.Section,
                ES.EditorInternal.ESEditorPresentation.ESCornerMask.Top);

            auxiliaryActions = new VisualElement { name = "ESDialogAuxiliaryActions" };
            auxiliaryActions.AddToClassList("es-dialog-actions");
            auxiliaryActions.style.flexDirection = FlexDirection.Row;
            auxiliaryActions.style.flexWrap = Wrap.Wrap;
            auxiliaryActions.style.alignItems = Align.Center;
            auxiliaryActions.style.flexGrow = 1f;
            auxiliaryActions.style.minWidth = 0f;
            for (int i = 0; i < request.auxiliaryActions.Count; i++)
            {
                ESAdvancedDialogAction action = request.auxiliaryActions[i];
                Button button = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    action.text,
                    string.IsNullOrWhiteSpace(action.tooltip) ? action.text : action.tooltip,
                    () => ExecuteAuxiliaryAction(action),
                    action.role == ESAdvancedDialogActionRole.Primary);
                ApplyActionRole(button, action.role);
                auxiliaryActions.Add(button);
            }
            footer.Add(auxiliaryActions);

            decisionActions = new VisualElement { name = "ESDialogDecisionActions" };
            decisionActions.AddToClassList("es-dialog-decision-actions");
            decisionActions.style.flexDirection = FlexDirection.Row;
            decisionActions.style.alignItems = Align.Center;
            decisionActions.style.flexShrink = 0f;

            if (request.showCancel)
            {
                Button cancel = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                    request.cancelText,
                    request.cancelText,
                    () =>
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                        Complete(false);
                    });
                cancel.style.minWidth = 88f;
                decisionActions.Add(cancel);
            }

            confirmButton = ES.EditorInternal.ESWindowPresentation.CreateToolbarButton(
                request.confirmText,
                request.confirmText,
                BeginConfirm,
                true);
            confirmButton.style.minWidth = 96f;
            confirmButton.style.marginRight = 0f;
            ApplyActionRole(
                confirmButton,
                request.tone == ESDialogTone.Danger
                    ? ESAdvancedDialogActionRole.Danger
                    : ESAdvancedDialogActionRole.Primary);
            decisionActions.Add(confirmButton);
            footer.Add(decisionActions);
            parent.Add(footer);
        }

        private async void ExecuteAuxiliaryAction(ESAdvancedDialogAction action)
        {
            if (action == null || completed || busy)
                return;
            try
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Click);
                ESAdvancedDialogValues values = BuildValues();
                if (action.executeAsync != null)
                {
                    BeginBusy("正在执行：" + action.text, "dialog.aux." + action.id);
                    await action.executeAsync(values, activeProgress, operationCancellation.Token);
                    if (this == null || completed)
                        return;
                    activeProgress?.Complete(action.text + "已完成");
                    EndBusy();
                }
                else
                {
                    action.execute?.Invoke(values);
                }
                if (action.closeDialogAfterExecution)
                {
                    CompleteFromAction(action.id);
                    return;
                }
                RefreshValidation();
            }
            catch (OperationCanceledException)
            {
                if (this == null || completed)
                    return;
                activeProgress?.Cancel();
                EndBusy();
                shell?.SetStatus("操作已取消。", ES.EditorInternal.ESStatusKind.Warning);
            }
            catch (Exception exception)
            {
                if (this == null || completed)
                    return;
                activeProgress?.Fail(exception);
                EndBusy();
                Debug.LogException(exception);
                if (shell != null)
                    shell.SetStatus("辅助动作执行失败；请查看 Console。", ES.EditorInternal.ESStatusKind.Error);
            }
        }

        private async void BeginConfirm()
        {
            if (completed || busy || asyncValidationPending
                || !string.IsNullOrWhiteSpace(validationMessage))
            {
                RevealInvalidField();
                return;
            }
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
            if (request.confirmAsync == null)
            {
                Complete(true);
                return;
            }

            try
            {
                BeginBusy("正在执行：" + request.confirmText, "dialog.confirm");
                await request.confirmAsync(
                    BuildValues(),
                    activeProgress,
                    operationCancellation.Token);
                if (this == null || completed)
                    return;
                activeProgress?.Complete(request.confirmText + "已完成");
                EndBusy();
                Complete(true);
            }
            catch (OperationCanceledException)
            {
                if (this == null || completed)
                    return;
                activeProgress?.Cancel();
                EndBusy();
                shell?.SetStatus("操作已取消，可以修改输入后重试。", ES.EditorInternal.ESStatusKind.Warning);
            }
            catch (Exception exception)
            {
                if (this == null || completed)
                    return;
                activeProgress?.Fail(exception);
                EndBusy();
                Debug.LogException(exception);
                shell?.SetStatus(
                    "执行失败：" + exception.Message,
                    ES.EditorInternal.ESStatusKind.Error);
            }
        }

        private void BeginBusy(string message, string operationId)
        {
            if (busy)
                throw new InvalidOperationException("当前对话框已有操作正在执行。");
            busy = true;
            busyMessage = message ?? string.Empty;
            operationCancellation?.Dispose();
            string progressId = string.IsNullOrWhiteSpace(DialogId)
                ? "dialog." + GetInstanceID() + "." + operationId
                : "dialog." + DialogId + "." + operationId;
            activeProgress = ESProgressCenter.Begin(
                progressId,
                request.title,
                message,
                request.allowOperationCancellation);
            operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                activeProgress.CancellationToken);
            if (busyOverlay != null)
                busyOverlay.style.display = DisplayStyle.Flex;
            if (busyLabel != null)
                busyLabel.text = busyMessage;
            if (cancelBusyButton != null)
                cancelBusyButton.style.display = request.allowOperationCancellation
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(decisionActions, false);
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(auxiliaryActions, false);
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = rootVisualElement.schedule.Execute(RefreshBusyOverlay).Every(100);
        }

        private void RefreshBusyOverlay()
        {
            if (!busy || activeProgress == null)
                return;
            if (!ESProgressCenter.TryGetProgress(
                    activeProgress.Id,
                    out float progress,
                    out string summary)
                || busyProgress == null)
                return;
            busyProgress.value = Mathf.Clamp01(progress) * 100f;
            busyProgress.title = summary;
        }

        private void CancelBusyOperation()
        {
            if (!busy)
                return;
            if (request == null || !request.allowOperationCancellation)
            {
                if (busyLabel != null)
                    busyLabel.text = "正在执行，当前操作不可取消";
                shell?.SetStatus(
                    "当前操作不允许取消，请等待执行完成。",
                    ES.EditorInternal.ESStatusKind.Warning);
                return;
            }

            operationCancellation?.Cancel();
            activeProgress?.RequestCancel();
            busyMessage = "正在取消";
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(cancelBusyButton, false);
            if (busyLabel != null)
                busyLabel.text = "正在取消";
        }

        private void EndBusy()
        {
            busy = false;
            busyMessage = string.Empty;
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = null;
            if (busyOverlay != null)
                busyOverlay.style.display = DisplayStyle.None;
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(cancelBusyButton, true);
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(decisionActions, true);
            ES.EditorInternal.ESWindowPresentation.SetElementEnabled(auxiliaryActions, true);
            activeProgress = null;
            operationCancellation?.Dispose();
            operationCancellation = null;
            RefreshValidation();
        }

        private static void ApplyActionRole(Button button, ESAdvancedDialogActionRole role)
        {
            if (button == null)
                return;
            if (role == ESAdvancedDialogActionRole.Danger)
            {
                ES.EditorInternal.ESWindowPresentation.SetButtonPresentationState(
                    button,
                    ES.EditorInternal.ESEditorPresentation.ESPresentationState.Error);
            }
            else if (role == ESAdvancedDialogActionRole.Primary)
            {
                ES.EditorInternal.ESWindowPresentation.SetButtonPresentationState(
                    button,
                    ES.EditorInternal.ESEditorPresentation.ESPresentationState.Normal);
            }
            else
                return;

            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        internal static Color GetReadableActionTextColor(Color background)
        {
            float luminance = 0.2126f * ToLinear(background.r)
                + 0.7152f * ToLinear(background.g)
                + 0.0722f * ToLinear(background.b);
            float whiteContrast = 1.05f / (luminance + 0.05f);
            float blackContrast = (luminance + 0.05f) / 0.05f;
            return whiteContrast >= blackContrast
                ? new Color(0.98f, 0.99f, 1f, 1f)
                : new Color(0.055f, 0.065f, 0.07f, 1f);
        }

        private static float ToLinear(float channel)
        {
            channel = Mathf.Clamp01(channel);
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        private void BuildExpiredView()
        {
            Texture titleIcon = ES.EditorInternal.ESEditorPresentation.ResolveDefaultWindowIcon(
                this,
                "ES 对话框已失效",
                null);
            var expiredShell = new ES.EditorInternal.ESWindowShell(
                "ES 对话框已失效",
                "Domain Reload 已清除本次临时输入上下文",
                false,
                titleIcon);
            expiredShell.Toolbar.style.display = DisplayStyle.None;
            expiredShell.SetStatus("请关闭并从原功能重新打开", ES.EditorInternal.ESStatusKind.Warning);
            expiredShell.Content.Add(ES.EditorInternal.ESWindowPresentation.CreateErrorState(
                "无法恢复本次输入",
                "对话框请求只属于当前 Editor 域。",
                "未确认输入不会提交。",
                "关闭窗口并从原功能重新发起。",
                "关闭",
                Close));
            rootVisualElement.Add(expiredShell.Root);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                if (busy)
                {
                    CancelBusyOperation();
                    evt.StopImmediatePropagation();
                    return;
                }
                if (!request.closeOnEscape || !request.showCancel)
                {
                    evt.StopImmediatePropagation();
                    return;
                }
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                Complete(false);
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;
            if (evt.altKey || evt.ctrlKey || evt.commandKey || evt.shiftKey)
                return;

            VisualElement focused = rootVisualElement.focusController?.focusedElement as VisualElement;
            TextField focusedText = focused as TextField ?? focused?.GetFirstAncestorOfType<TextField>();
            if (focusedText != null && focusedText.multiline)
                return;
            if (!string.IsNullOrWhiteSpace(validationMessage) || asyncValidationPending)
            {
                RevealInvalidField();
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                evt.StopImmediatePropagation();
                return;
            }

            BeginConfirm();
            evt.StopImmediatePropagation();
        }

        private void ApplyInitialPosition()
        {
            Vector2 requestedMin = request.minSize;
            Rect ownerBounds = ESDialogService.ResolveOwnerBounds(request);
            Rect placementAnchor = request.positionMode == ESAdvancedDialogPositionMode.CustomScreenPosition
                ? new Rect(request.customScreenPosition, Vector2.one)
                : ownerBounds;
            Rect workArea = ESDialogService.ResolvePlacementWorkArea(placementAnchor);
            float estimatedHeight = 250f;
            for (int i = 0; i < request.fields.Count; i++)
            {
                ESAdvancedDialogField field = request.fields[i];
                if (field.kind == ESAdvancedDialogFieldKind.MultilineText)
                    estimatedHeight += 122f;
                else if (field.kind == ESAdvancedDialogFieldKind.MultiChoice)
                    estimatedHeight += 58f + Mathf.Min(field.choices.Count, 6) * 28f;
                else if (field.kind == ESAdvancedDialogFieldKind.Recommendation)
                    estimatedHeight += 104f;
                else
                    estimatedHeight += 74f;
            }

            Rect centered = CalculatePosition(
                ownerBounds,
                workArea,
                requestedMin,
                request.preferredSize,
                estimatedHeight,
                request.positionMode,
                request.customScreenPosition,
                request.positionOffset);
            minSize = new Vector2(
                Mathf.Min(Mathf.Max(360f, requestedMin.x), centered.width),
                Mathf.Min(Mathf.Max(240f, requestedMin.y), centered.height));
            maxSize = new Vector2(
                Mathf.Max(minSize.x, workArea.width),
                Mathf.Max(minSize.y, workArea.height));
            int ownerDepth = request.positionMode == ESAdvancedDialogPositionMode.CustomScreenPosition
                ? 0
                : ESDialogService.ResolveOwnerDepth(request);
            Rect offsetPosition = ESDialogService.OffsetChildDialog(centered, ownerDepth);
            initialScreenPosition = OffsetAndClamp(offsetPosition, workArea, Vector2.zero);
            hasInitialScreenPosition = true;
            position = initialScreenPosition;
        }

        internal static Rect CalculateCenteredPosition(
            Rect main,
            Vector2 requestedMin,
            Vector2 preferred,
            float estimatedHeight)
        {
            float availableWidth = Mathf.Max(1f, main.width - 48f);
            float availableHeight = Mathf.Max(1f, main.height - 64f);
            float minimumWidth = Mathf.Min(360f, availableWidth);
            float minimumHeight = Mathf.Min(260f, availableHeight);
            float width = Mathf.Clamp(
                Mathf.Max(Mathf.Max(360f, requestedMin.x), preferred.x),
                minimumWidth,
                availableWidth);
            float height = Mathf.Clamp(
                Mathf.Max(Mathf.Max(240f, requestedMin.y), Mathf.Min(preferred.y, estimatedHeight)),
                minimumHeight,
                availableHeight);
            return new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width,
                height);
        }

        internal static Rect CalculatePosition(
            Rect owner,
            Vector2 requestedMin,
            Vector2 preferred,
            float estimatedHeight,
            ESAdvancedDialogPositionMode mode,
            Vector2 customScreenPosition,
            Vector2 positionOffset)
        {
            return CalculatePosition(
                owner,
                owner,
                requestedMin,
                preferred,
                estimatedHeight,
                mode,
                customScreenPosition,
                positionOffset);
        }

        internal static Rect CalculatePosition(
            Rect owner,
            Rect workArea,
            Vector2 requestedMin,
            Vector2 preferred,
            float estimatedHeight,
            ESAdvancedDialogPositionMode mode,
            Vector2 customScreenPosition,
            Vector2 positionOffset)
        {
            // 这里的 position 语义是“最终窗口矩形”，不是按钮锚点矩形。
            // CustomScreenPosition 传入的是窗口左上角；OffsetAndClamp 只负责防止窗口越出
            // 当前显示器工作区，不能替代调用方的布局方向选择。右侧 Inspector 场景尤其不能
            // 先向屏幕右边放置、再指望钳制把窗口推回合理位置，否则会产生跳位和错误视觉归属。
            Rect sized = CalculateCenteredPosition(
                workArea,
                requestedMin,
                preferred,
                estimatedHeight);
            Rect centered = new Rect(
                owner.center - sized.size * 0.5f,
                sized.size);
            if (mode == ESAdvancedDialogPositionMode.CenterOwner)
                return OffsetAndClamp(centered, workArea, positionOffset);

            Vector2 point;
            switch (mode)
            {
                case ESAdvancedDialogPositionMode.OwnerTopLeft:
                    point = new Vector2(owner.xMin, owner.yMin);
                    break;
                case ESAdvancedDialogPositionMode.OwnerTopRight:
                    point = new Vector2(owner.xMax - centered.width, owner.yMin);
                    break;
                case ESAdvancedDialogPositionMode.OwnerBottomLeft:
                    point = new Vector2(owner.xMin, owner.yMax - centered.height);
                    break;
                case ESAdvancedDialogPositionMode.OwnerBottomRight:
                    point = new Vector2(owner.xMax - centered.width, owner.yMax - centered.height);
                    break;
                case ESAdvancedDialogPositionMode.CustomScreenPosition:
                    point = customScreenPosition;
                    break;
                default:
                    point = centered.position;
                    break;
            }
            return OffsetAndClamp(
                new Rect(point + positionOffset, centered.size),
                workArea,
                Vector2.zero);
        }

        private static Rect OffsetAndClamp(Rect position, Rect bounds, Vector2 offset)
        {
            // 这是最后一道安全边界，不是正常放置策略。调用方若从 Inspector 按钮触发大型
            // 独立对话框，应在进入这里之前把目标放到按钮左侧/左上方（右侧停靠时），
            // 而不是故意放到右侧后让这里整体反推。
            position.position += offset;
            float minX = bounds.xMin + 12f;
            float maxX = Mathf.Max(minX, bounds.xMax - position.width - 12f);
            float minY = bounds.yMin + 12f;
            float maxY = Mathf.Max(minY, bounds.yMax - position.height - 12f);
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            return position;
        }

        private static Texture ResolveToneIcon(ESDialogTone tone)
        {
            switch (tone)
            {
                case ESDialogTone.Success:
                    return LoadFirstUnityIcon("TestPassed", "d_TestPassed", "d_console.infoicon");
                case ESDialogTone.Warning:
                    return LoadFirstUnityIcon("d_console.warnicon", "console.warnicon");
                case ESDialogTone.Danger:
                    return LoadFirstUnityIcon("d_console.erroricon", "console.erroricon");
                default:
                    return LoadFirstUnityIcon("d_console.infoicon", "console.infoicon");
            }
        }

        private static Texture LoadFirstUnityIcon(params string[] iconNames)
        {
            for (int i = 0; i < iconNames.Length; i++)
            {
                Texture icon = ES.EditorInternal.ESEditorPresentation.LoadUnityIcon(iconNames[i]);
                if (icon != null)
                    return icon;
            }
            return null;
        }

        private Color GetToneAccent(ESDialogTone tone)
        {
            switch (tone)
            {
                case ESDialogTone.Success:
                    return ES.EditorInternal.ESEditorPresentation.ActiveColor;
                case ESDialogTone.Warning:
                    return ES.EditorInternal.ESEditorPresentation.WarningColor;
                case ESDialogTone.Danger:
                    return ES.EditorInternal.ESEditorPresentation.ErrorColor;
                default:
                    return ES.EditorInternal.ESEditorPresentation.SelectionColor;
            }
        }

        private ES.EditorInternal.ESStatusKind GetToneStatus()
        {
            switch (request.tone)
            {
                case ESDialogTone.Success:
                    return ES.EditorInternal.ESStatusKind.Ready;
                case ESDialogTone.Warning:
                    return ES.EditorInternal.ESStatusKind.Warning;
                case ESDialogTone.Danger:
                    return ES.EditorInternal.ESStatusKind.Error;
                default:
                    return ES.EditorInternal.ESStatusKind.Info;
            }
        }

        private void RefreshValidation()
        {
            if (!initialized || request == null)
                return;
            ESAdvancedDialogValidation validation = ValidateValuesDetailed(BuildValues());
            validationMessage = validation?.message ?? string.Empty;
            invalidFieldId = validation?.fieldId ?? string.Empty;
            bool valid = string.IsNullOrWhiteSpace(validationMessage);
            if (validationPanel != null)
                validationPanel.style.display = valid ? DisplayStyle.None : DisplayStyle.Flex;
            if (validationLabel != null)
                validationLabel.text = valid ? string.Empty : "无法继续：" + validationMessage;
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                confirmButton,
                valid && !busy && !asyncValidationPending);
            if (shell != null)
            {
                shell.SetStatus(
                    valid ? (request.fields.Count == 0 ? "等待确认" : "输入有效，可以继续") : validationMessage,
                    valid ? GetToneStatus() : ES.EditorInternal.ESStatusKind.Warning);
            }
            if (valid && request.validateAsync != null && !busy && confirmButton != null)
                ScheduleAsyncValidation();
            else if (!valid)
                CancelAsyncValidation();
        }

        private async void ScheduleAsyncValidation()
        {
            int generation = ++validationGeneration;
            validationCancellation?.Cancel();
            validationCancellation?.Dispose();
            validationCancellation = new CancellationTokenSource();
            CancellationToken token = validationCancellation.Token;
            asyncValidationPending = true;
            ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(confirmButton, false);
            shell?.SetStatus("正在校验输入...", ES.EditorInternal.ESStatusKind.Info);
            try
            {
                int delay = Mathf.Max(0, request.asyncValidationDelayMs);
                if (delay > 0)
                    await Task.Delay(delay, token);
                ESAdvancedDialogValidation result = await request.validateAsync(BuildValues(), token);
                EditorApplication.delayCall += () =>
                {
                    if (this == null || completed || token.IsCancellationRequested
                        || generation != validationGeneration)
                        return;
                    asyncValidationPending = false;
                    validationMessage = result?.message ?? string.Empty;
                    invalidFieldId = result?.fieldId ?? string.Empty;
                    bool valid = string.IsNullOrWhiteSpace(validationMessage);
                    if (validationPanel != null)
                        validationPanel.style.display = valid ? DisplayStyle.None : DisplayStyle.Flex;
                    if (validationLabel != null)
                        validationLabel.text = valid ? string.Empty : "无法继续：" + validationMessage;
                    ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(
                        confirmButton,
                        valid && !busy);
                    shell?.SetStatus(
                        valid ? "异步校验通过，可以继续" : validationMessage,
                        valid
                            ? GetToneStatus()
                            : ES.EditorInternal.ESStatusKind.Warning);
                };
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null || completed || generation != validationGeneration)
                        return;
                    asyncValidationPending = false;
                    validationMessage = "异步校验发生异常；请查看 Console。";
                    invalidFieldId = string.Empty;
                    validationPanel.style.display = DisplayStyle.Flex;
                    validationLabel.text = "无法继续：" + validationMessage;
                    ES.EditorInternal.ESWindowPresentation.SetButtonEnabled(confirmButton, false);
                    shell?.SetStatus(validationMessage, ES.EditorInternal.ESStatusKind.Error);
                    Debug.LogException(exception);
                };
            }
        }

        private void CancelAsyncValidation()
        {
            validationGeneration++;
            validationCancellation?.Cancel();
            validationCancellation?.Dispose();
            validationCancellation = null;
            asyncValidationPending = false;
        }

        private void RevealInvalidField()
        {
            if (string.IsNullOrWhiteSpace(invalidFieldId)
                || !fieldBlocks.TryGetValue(invalidFieldId, out VisualElement block))
                return;
            bodyScroll?.ScrollTo(block);
            if (fieldControls.TryGetValue(invalidFieldId, out VisualElement control))
                control.schedule.Execute(control.Focus);
        }

        private ESAdvancedDialogValidation ValidateValuesDetailed(ESAdvancedDialogValues values)
        {
            foreach (ESAdvancedDialogField field in request.fields)
            {
                if (field.required && field.kind == ESAdvancedDialogFieldKind.Object && field.objectValue == null)
                    return new ESAdvancedDialogValidation("“" + field.label + "”不能为空。", field.id);
                if (field.kind == ESAdvancedDialogFieldKind.MultiChoice
                    && field.selectedChoiceValues.Count < field.minimumSelections)
                    return new ESAdvancedDialogValidation(
                        "“" + field.label + "”至少需要选择 " + field.minimumSelections + " 项。",
                        field.id);
                if (field.kind == ESAdvancedDialogFieldKind.MultiChoice
                    && field.maximumSelections > 0
                    && field.selectedChoiceValues.Count > field.maximumSelections)
                    return new ESAdvancedDialogValidation(
                        "“" + field.label + "”最多只能选择 " + field.maximumSelections + " 项。",
                        field.id);
                if (field.required
                    && field.kind != ESAdvancedDialogFieldKind.Object
                    && field.kind != ESAdvancedDialogFieldKind.Toggle
                    && field.kind != ESAdvancedDialogFieldKind.MultiChoice
                    && field.kind != ESAdvancedDialogFieldKind.Recommendation
                    && string.IsNullOrWhiteSpace(field.stringValue))
                    return new ESAdvancedDialogValidation("“" + field.label + "”不能为空。", field.id);
            }

            try
            {
                ESAdvancedDialogValidation detailed = request.validateDetailed?.Invoke(values);
                if (detailed != null && !string.IsNullOrWhiteSpace(detailed.message))
                    return detailed;
                string legacyMessage = request.validate?.Invoke(values) ?? string.Empty;
                return new ESAdvancedDialogValidation(legacyMessage);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new ESAdvancedDialogValidation("输入校验发生异常；请查看 Console。");
            }
        }

        private ESAdvancedDialogValues BuildValues()
        {
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            var toggles = new Dictionary<string, bool>(StringComparer.Ordinal);
            var integers = new Dictionary<string, int>(StringComparer.Ordinal);
            var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
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
                    case ESAdvancedDialogFieldKind.Recommendation:
                        integers.Add(field.id, field.intValue);
                        break;
                    case ESAdvancedDialogFieldKind.MultiChoice:
                        var selectedIds = new HashSet<string>(
                            field.selectedChoiceValues,
                            StringComparer.Ordinal);
                        selections.Add(
                            field.id,
                            Array.AsReadOnly(field.choiceValues
                                .Where(selectedIds.Contains)
                                .ToArray()));
                        break;
                    default:
                        strings.Add(field.id, field.stringValue ?? string.Empty);
                        break;
                }
            }
            return new ESAdvancedDialogValues(strings, toggles, integers, selections, objects);
        }

        private void Complete(bool accepted)
        {
            if (!TryComplete(accepted, !accepted, null))
                return;
            if (this != null)
                ESDialogService.CloseCompletedWindow(this);
        }

        private void CompleteFromAction(string actionId)
        {
            if (!TryComplete(false, false, actionId))
                return;
            if (this != null)
                ESDialogService.CloseCompletedWindow(this);
        }

        private bool TryComplete(bool accepted, bool cancelled, string actionId)
        {
            if (completed || request == null)
                return false;
            completed = true;
            CancelAsyncValidation();
            operationCancellation?.Cancel();
            lastResult = new ESAdvancedDialogResult
            {
                accepted = accepted,
                cancelled = cancelled,
                actionId = actionId ?? string.Empty,
                values = BuildValues(),
            };
            return true;
        }

        private void PublishResult()
        {
            if (resultPublished || lastResult == null)
                return;
            resultPublished = true;
            try
            {
                request.completed?.Invoke(lastResult);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            for (int i = 0; i < completionObservers.Count; i++)
                completionObservers[i]?.TrySetResult(lastResult);
            completionObservers.Clear();
        }

        private void OnDisable()
        {
            ReleaseWindowResources();
            try
            {
                ESWindowFoundation.Suspend(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            CompleteCloseLifecycle();
        }

        private void OnDestroy()
        {
            ReleaseWindowResources();
            CompleteCloseLifecycle();
            try
            {
                ESWindowFoundation.Close(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ReleaseWindowResources()
        {
            StopInitialPositionReapplyLoop();
            EditorApplication.delayCall -= CloseAfterBuildFailure;
            buildFailureCloseScheduled = false;
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = null;
            CancelAndDispose(ref validationCancellation);
            CancelAndDispose(ref operationCancellation);
            ESProgressHandle progress = activeProgress;
            activeProgress = null;
            if (progress != null)
            {
                try
                {
                    progress.Cancel("对话框已关闭");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            ReleaseCustomContent();
            IDisposable interactionHold = ownerInteractionHold;
            ownerInteractionHold = null;
            if (interactionHold != null)
            {
                try
                {
                    interactionHold.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void CompleteCloseLifecycle()
        {
            if (initialized && !completed)
            {
                try
                {
                    TryComplete(false, true, null);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    completed = true;
                    lastResult = new ESAdvancedDialogResult
                    {
                        accepted = false,
                        cancelled = false,
                        actionId = string.Empty,
                        exception = exception,
                    };
                }
            }
            try
            {
                ESDialogService.NotifyClosed(this, lastResult);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            PublishResult();
        }

        private static void CancelAndDispose(ref CancellationTokenSource source)
        {
            CancellationTokenSource current = source;
            source = null;
            if (current == null)
                return;
            try
            {
                current.Cancel();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            try
            {
                current.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ReleaseCustomContent()
        {
            if (customContentReleased)
                return;
            customContentReleased = true;
            VisualElement content = customContent;
            customContent = null;
            if (content == null)
                return;
            try
            {
                if (request?.releaseCustomContent != null)
                    request.releaseCustomContent.Invoke(content);
                else if (content is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void ValidateRequest(ESAdvancedDialogRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            string dialogId = request.dialogId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dialogId))
                throw new ArgumentException("对话框必须提供稳定、非空的 dialogId。", nameof(request));
            if (dialogId.Length > 128 || !IsStableDialogId(dialogId))
                throw new ArgumentException(
                    "dialogId 只能包含字母、数字、点、短横线、下划线、冒号或斜线，长度不能超过 128。",
                    nameof(request));
            if (!string.Equals(request.dialogId, dialogId, StringComparison.Ordinal))
                request.dialogId = dialogId;
            if (string.IsNullOrWhiteSpace(request.title)) throw new ArgumentException("对话框标题不能为空。", nameof(request));
            if (request.title.Trim().Length > 160)
                throw new ArgumentException("对话框标题长度不能超过 160 个字符。", nameof(request));
            if (!Enum.IsDefined(typeof(ESDialogTone), request.tone))
                throw new ArgumentException("对话框语义类型无效。", nameof(request));
            if (!Enum.IsDefined(typeof(ESDialogDuplicatePolicy), request.duplicatePolicy))
                throw new ArgumentException("对话框去重策略无效。", nameof(request));
            if (request.queueBehindActiveDialog
                && request.duplicatePolicy == ESDialogDuplicatePolicy.AllowParallel)
                throw new ArgumentException("队列策略不能与允许并行同时启用。", nameof(request));
            if (string.IsNullOrWhiteSpace(request.confirmText)) throw new ArgumentException("确认按钮文本不能为空。", nameof(request));
            if (request.showCancel && string.IsNullOrWhiteSpace(request.cancelText)) throw new ArgumentException("取消按钮文本不能为空。", nameof(request));
            if (request.fields == null) throw new ArgumentException("高级对话框字段集合不能为空。", nameof(request));
            if (request.auxiliaryActions == null) throw new ArgumentException("高级对话框辅助动作集合不能为空。", nameof(request));
            if (request.asyncValidationDelayMs < 0)
                throw new ArgumentException("异步校验延迟不能小于 0。", nameof(request));
            if (!IsFinitePositiveSize(request.minSize)
                || !IsFinitePositiveSize(request.preferredSize)
                || request.minSize.x > 4096f
                || request.minSize.y > 4096f
                || request.preferredSize.x > 4096f
                || request.preferredSize.y > 4096f)
                throw new ArgumentException("对话框尺寸必须是有限、正数且不超过 4096。", nameof(request));
            if (request.preferredSize.x < request.minSize.x
                || request.preferredSize.y < request.minSize.y)
                throw new ArgumentException("preferredSize 不能小于 minSize。", nameof(request));
            if (!Enum.IsDefined(typeof(ESAdvancedDialogPositionMode), request.positionMode))
                throw new ArgumentException("对话框位置模式无效。", nameof(request));
            if (!IsFinite(request.positionOffset)
                || request.positionMode == ESAdvancedDialogPositionMode.CustomScreenPosition
                    && !IsFinite(request.customScreenPosition))
                throw new ArgumentException("对话框位置必须是有限坐标。", nameof(request));
            if (!ReferenceEquals(request.owner, null) && request.owner == null)
                throw new ArgumentException("对话框 owner 已关闭，不能继续提交请求。", nameof(request));
            if (request.owner == null && !request.allowMainWorkspaceFallback)
                throw new ArgumentException(
                    "Editor 对话框必须提供显式 owner；确实无法取得 owner 时，必须显式设置 allowMainWorkspaceFallback。",
                    nameof(request));

            string initialFocusFieldId = request.initialFocusFieldId?.Trim() ?? string.Empty;
            if (!string.Equals(request.initialFocusFieldId, initialFocusFieldId, StringComparison.Ordinal))
                request.initialFocusFieldId = initialFocusFieldId;
            if (initialFocusFieldId.Length > 128 || !string.IsNullOrEmpty(initialFocusFieldId)
                && !IsStableDialogId(initialFocusFieldId))
                throw new ArgumentException("初始焦点字段 ID 必须是稳定标识。", nameof(request));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAdvancedDialogField field in request.fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.id) || string.IsNullOrWhiteSpace(field.label))
                    throw new ArgumentException("每个输入字段都必须具备稳定 ID 和显示名称。", nameof(request));
                string fieldId = field.id.Trim();
                if (!string.Equals(field.id, fieldId, StringComparison.Ordinal)
                    || !IsStableDialogId(fieldId))
                    throw new ArgumentException("字段 ID 必须是无空白的稳定标识：" + field.id, nameof(request));
                if (!ids.Add(fieldId)) throw new ArgumentException("高级对话框存在重复字段 ID：" + field.id, nameof(request));
                if (!Enum.IsDefined(typeof(ESAdvancedDialogFieldKind), field.kind))
                    throw new ArgumentException("字段类型无效：" + field.id, nameof(request));
                if (field.label.Trim().Length > 160 || (field.help?.Length ?? 0) > 1000)
                    throw new ArgumentException("字段显示文本过长：" + field.id, nameof(request));
                if (field.kind == ESAdvancedDialogFieldKind.Choice
                    || field.kind == ESAdvancedDialogFieldKind.MultiChoice)
                {
                    if (field.choices.Count == 0) throw new ArgumentException("选择字段必须提供至少一个选项：" + field.id, nameof(request));
                    if (field.choiceValues.Count != field.choices.Count)
                        throw new ArgumentException("选择字段的显示项与稳定值数量不一致：" + field.id, nameof(request));
                    var choiceValueIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string value in field.choiceValues)
                    {
                        if (string.IsNullOrWhiteSpace(value)
                            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                            || !choiceValueIds.Add(value))
                            throw new ArgumentException("选择字段包含空或重复稳定值：" + field.id, nameof(request));
                    }
                    if (field.kind == ESAdvancedDialogFieldKind.Choice
                        && !field.choiceValues.Contains(field.stringValue))
                        field.stringValue = field.choiceValues[0];
                    if (field.kind == ESAdvancedDialogFieldKind.MultiChoice)
                    {
                        if (field.minimumSelections < 0)
                            throw new ArgumentException("多选字段的最少选择数不能小于 0：" + field.id, nameof(request));
                        if (field.maximumSelections < 0
                            || (field.maximumSelections > 0 && field.maximumSelections > field.choiceValues.Count))
                            throw new ArgumentException("多选字段的最多选择数超出选项范围：" + field.id, nameof(request));
                        int effectiveMaximum = field.maximumSelections <= 0
                            ? field.choiceValues.Count
                            : field.maximumSelections;
                        if (field.minimumSelections > effectiveMaximum)
                            throw new ArgumentException("多选字段的最少选择数不能大于最多选择数：" + field.id, nameof(request));
                        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
                        foreach (string selectedId in field.selectedChoiceValues)
                        {
                            if (!field.choiceValues.Contains(selectedId) || !selectedIds.Add(selectedId))
                                throw new ArgumentException("多选字段包含未注册或重复的默认稳定值：" + field.id, nameof(request));
                        }
                        if (field.selectedChoiceValues.Count > effectiveMaximum)
                            throw new ArgumentException("多选字段的默认选择超过最多选择数：" + field.id, nameof(request));
                    }
                }
                if (field.kind == ESAdvancedDialogFieldKind.Recommendation)
                {
                    if (field.minIntValue >= field.maxIntValue)
                        throw new ArgumentException("推荐程度字段必须具有有效的最小值和最大值：" + field.id, nameof(request));
                    if (field.intValue < field.minIntValue || field.intValue > field.maxIntValue)
                        throw new ArgumentException("推荐程度默认值超出范围：" + field.id, nameof(request));
                }
                if (field.kind == ESAdvancedDialogFieldKind.Object && (field.objectType == null || !typeof(UnityEngine.Object).IsAssignableFrom(field.objectType)))
                    throw new ArgumentException("Object 字段必须指定 UnityEngine.Object 类型：" + field.id, nameof(request));
            }

            if (!string.IsNullOrEmpty(initialFocusFieldId) && !ids.Contains(initialFocusFieldId))
                throw new ArgumentException(
                    "initialFocusFieldId 未对应任何已声明字段：" + initialFocusFieldId,
                    nameof(request));

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAdvancedDialogAction action in request.auxiliaryActions)
            {
                if (action == null
                    || string.IsNullOrWhiteSpace(action.id)
                    || string.IsNullOrWhiteSpace(action.text)
                    || action.execute == null && action.executeAsync == null)
                    throw new ArgumentException("每个辅助动作都必须具备稳定 ID、显示名称和回调。", nameof(request));
                if (!actionIds.Add(action.id))
                    throw new ArgumentException("高级对话框存在重复辅助动作 ID：" + action.id, nameof(request));
                if (!string.Equals(action.id, action.id.Trim(), StringComparison.Ordinal)
                    || !IsStableDialogId(action.id.Trim()))
                    throw new ArgumentException("辅助动作 ID 必须是无空白的稳定标识：" + action.id, nameof(request));
                if (!Enum.IsDefined(typeof(ESAdvancedDialogActionRole), action.role))
                    throw new ArgumentException("辅助动作语义角色无效：" + action.id, nameof(request));
                if (action.text.Trim().Length > 96)
                    throw new ArgumentException("辅助动作文本过长：" + action.id, nameof(request));
            }
        }

        private static bool IsStableDialogId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'
                    || c == ':' || c == '/')
                    continue;
                return false;
            }
            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsFinitePositiveSize(Vector2 value)
        {
            return IsFinite(value) && value.x > 0f && value.y > 0f;
        }

        private static string BuildNativeTitle(string title)
        {
            return BuildNativeTitle(title, false, ESDialogTone.Info);
        }

        private static string BuildNativeTitle(
            string title,
            bool modal,
            ESDialogTone tone)
        {
            string value = (title ?? string.Empty).Trim();
            const string existingPrefix = "ES 对话框 · ";
            if (value.StartsWith(existingPrefix, StringComparison.Ordinal))
                value = value.Substring(existingPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(value))
                value = "未命名请求";
            return "ES 对话框 · "
                + (modal ? "模态" : "非模态")
                + " · " + GetToneLabel(tone)
                + " · " + value;
        }

        private static string GetToneClass(ESDialogTone tone)
        {
            switch (tone)
            {
                case ESDialogTone.Success: return "es-dialog-tone-success";
                case ESDialogTone.Warning: return "es-dialog-tone-warning";
                case ESDialogTone.Danger: return "es-dialog-tone-danger";
                default: return "es-dialog-tone-info";
            }
        }

        private static string GetToneLabel(ESDialogTone tone)
        {
            switch (tone)
            {
                case ESDialogTone.Success: return "成功";
                case ESDialogTone.Warning: return "警告";
                case ESDialogTone.Danger: return "危险操作";
                default: return "信息确认";
            }
        }
    }
}
