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
        public ESDialogTone tone = ESDialogTone.Info;
        public bool showCancel = true;
        public bool animateOpening = true;
        public bool closeOnEscape = true;
        public bool allowOperationCancellation = true;
        public bool queueBehindActiveDialog;
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
                RemoveRecordsLocked(item =>
                    item.state != ESProgressState.Running
                    && item.finishedAtUtc < (item.state == ESProgressState.Failed
                        ? failedCutoff
                        : cutoff));
                hasRunning = records.Any(item => item.state == ESProgressState.Running);
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

    public sealed class ESProgressCenterWindow : EditorWindow
    {
        private VisualElement content;
        private readonly HashSet<string> expandedIds = new HashSet<string>(StringComparer.Ordinal);

        internal static ESProgressCenterWindow OpenAtBottomRight()
        {
            EditorWindow previous = focusedWindow;
            var result = CreateInstance<ESProgressCenterWindow>();
            result.titleContent = new GUIContent("ES 任务进度");
            result.minSize = new Vector2(320f, 120f);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
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
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor =
                ES.EditorInternal.ESEditorPresentation.WindowSurfaceColor;
            var header = new VisualElement();
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
            content.style.flexGrow = 1f;
            content.style.minWidth = 0f;
            rootVisualElement.Add(content);
            RefreshNow();
            ES.EditorInternal.ESEditorPresentation.BindWindow(this, allowSemiSleep: false);
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
            block.style.marginLeft = 8f;
            block.style.marginRight = 8f;
            block.style.marginBottom = 7f;
            block.style.paddingLeft = 9f;
            block.style.paddingRight = 9f;
            block.style.paddingTop = 7f;
            block.style.paddingBottom = 7f;
            block.style.backgroundColor =
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor;
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
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
        }
    }

    public static class ESDialogService
    {
        private const int MaximumActiveDialogs = 8;
        private const int MaximumPendingDialogs = 64;
        private sealed class PendingDialog
        {
            internal ESAdvancedDialogRequest request;
            internal TaskCompletionSource<ESAdvancedDialogResult> completion;
            internal readonly List<TaskCompletionSource<ESAdvancedDialogResult>> observers =
                new List<TaskCompletionSource<ESAdvancedDialogResult>>();

            internal void Complete(ESAdvancedDialogResult result)
            {
                completion?.TrySetResult(result);
                for (int i = 0; i < observers.Count; i++)
                    observers[i]?.TrySetResult(result);
                observers.Clear();
            }
        }

        private static readonly List<ESAdvancedDialogWindow> activeWindows =
            new List<ESAdvancedDialogWindow>();
        private static readonly List<PendingDialog> pendingDialogs =
            new List<PendingDialog>();
        private static bool openingReplacement;
        private static bool shuttingDown;

        internal static void InitializeLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        public static ESAdvancedDialogWindow Show(ESAdvancedDialogRequest request)
        {
            ValidateServiceRequest(request);
            request = SnapshotRequest(request);
            PendingDialog pendingDuplicate = FindPendingDuplicate(request.dialogId);
            if (pendingDuplicate != null)
            {
                if (request.duplicatePolicy == ESDialogDuplicatePolicy.ReplaceExisting)
                {
                    pendingDialogs.Remove(pendingDuplicate);
                    pendingDuplicate.Complete(
                        new ESAdvancedDialogResult { accepted = false, cancelled = true });
                }
                else if (request.duplicatePolicy != ESDialogDuplicatePolicy.AllowParallel)
                {
                    return null;
                }
            }
            ESAdvancedDialogWindow duplicate = FindDuplicate(request.dialogId);
            if (duplicate != null)
            {
                switch (request.duplicatePolicy)
                {
                    case ESDialogDuplicatePolicy.FocusExisting:
                        duplicate.Focus();
                        return duplicate;
                    case ESDialogDuplicatePolicy.ReplaceExisting:
                        openingReplacement = true;
                        duplicate.CancelAndClose();
                        break;
                    case ESDialogDuplicatePolicy.Queue:
                        Enqueue(new PendingDialog { request = request });
                        return null;
                }
            }
            if (request.queueBehindActiveDialog && activeWindows.Any(IsLive))
            {
                Enqueue(new PendingDialog { request = request });
                return null;
            }
            return OpenNow(request, null, false);
        }

        public static Task<ESAdvancedDialogResult> ShowAsync(
            ESAdvancedDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateServiceRequest(request);
            request = SnapshotRequest(request);
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SynchronizationContext editorContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "ESDialogService.ShowAsync 必须从 Unity Editor 主线程调用。");
            ESAdvancedDialogWindow observedWindow = null;
            bool ownsWindow = false;
            CancellationTokenRegistration cancellationRegistration = default;
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return completion.Task;
            }
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(() =>
                {
                    editorContext.Post(_ =>
                    {
                        if (ownsWindow && observedWindow != null)
                            observedWindow.CancelAndClose();
                        else if (observedWindow != null)
                            observedWindow.RemoveCompletionObserver(completion);
                        PendingDialog queued = pendingDialogs.FirstOrDefault(item =>
                            item?.completion == completion || item?.observers.Contains(completion) == true);
                        if (queued?.completion == completion)
                        {
                            if (queued.observers.Count > 0)
                            {
                                queued.completion = queued.observers[0];
                                queued.observers.RemoveAt(0);
                            }
                            else
                            {
                                pendingDialogs.Remove(queued);
                            }
                        }
                        else
                            queued?.observers.Remove(completion);
                        completion.TrySetCanceled(cancellationToken);
                    }, null);
                });
                completion.Task.ContinueWith(_ => cancellationRegistration.Dispose());
            }
            PendingDialog pendingDuplicate = FindPendingDuplicate(request.dialogId);
            if (pendingDuplicate != null
                && request.duplicatePolicy == ESDialogDuplicatePolicy.FocusExisting)
            {
                pendingDuplicate.observers.Add(completion);
                return completion.Task;
            }
            if (pendingDuplicate != null
                && request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
            {
                Enqueue(new PendingDialog
                {
                    request = request,
                    completion = completion,
                });
                return completion.Task;
            }
            if (pendingDuplicate != null
                && request.duplicatePolicy == ESDialogDuplicatePolicy.ReplaceExisting)
            {
                pendingDialogs.Remove(pendingDuplicate);
                pendingDuplicate.Complete(
                    new ESAdvancedDialogResult { accepted = false, cancelled = true });
            }
            ESAdvancedDialogWindow duplicate = FindDuplicate(request.dialogId);
            if (duplicate != null && request.duplicatePolicy == ESDialogDuplicatePolicy.FocusExisting)
            {
                observedWindow = duplicate;
                duplicate.Focus();
                duplicate.AddCompletionObserver(completion);
                return completion.Task;
            }
            if (duplicate != null && request.duplicatePolicy == ESDialogDuplicatePolicy.ReplaceExisting)
            {
                openingReplacement = true;
                duplicate.CancelAndClose();
            }
            if ((duplicate != null && request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
                || request.queueBehindActiveDialog && activeWindows.Any(IsLive))
            {
                Enqueue(new PendingDialog
                {
                    request = request,
                    completion = completion,
                });
                return completion.Task;
            }

            observedWindow = OpenNow(request, completion, false);
            ownsWindow = true;
            return completion.Task;
        }

        /// <summary>
        /// 原生同步兼容入口。只适合短确认；耗时工作必须使用 ShowAsync，避免阻塞 Editor 主线程。
        /// </summary>
        public static ESAdvancedDialogResult ShowModal(ESAdvancedDialogRequest request)
        {
            ValidateServiceRequest(request);
            request = SnapshotRequest(request);
            if (request.queueBehindActiveDialog
                || request.duplicatePolicy == ESDialogDuplicatePolicy.Queue)
                throw new InvalidOperationException("ShowModal 不支持队列策略；请使用 ShowAsync。");
            if (FindDuplicate(request.dialogId) != null)
                throw new InvalidOperationException("同 ID 对话框已经打开；ShowModal 不会阻塞等待已有窗口。");
            if (FindPendingDuplicate(request.dialogId) != null)
                throw new InvalidOperationException("同 ID 对话框已经排队；ShowModal 不会越过队列。");
            if (request.confirmAsync != null || request.validateAsync != null
                || request.auxiliaryActions.Any(action => action?.executeAsync != null))
                throw new InvalidOperationException("ShowModal 不接受异步校验或异步动作；请使用 ShowAsync。");
            var completion = new TaskCompletionSource<ESAdvancedDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            OpenNow(request, completion, true);
            return completion.Task.GetAwaiter().GetResult();
        }

        internal static void NotifyClosed(
            ESAdvancedDialogWindow window,
            ESAdvancedDialogResult result)
        {
            ESAdvancedDialogWindow[] children = activeWindows
                .Where(item => IsLive(item) && item != window && item.Owner == window)
                .ToArray();
            for (int i = 0; i < children.Length; i++)
                children[i].CancelAndClose();
            activeWindows.RemoveAll(item => !IsLive(item) || item == window);
            if (shuttingDown)
                return;
            if (openingReplacement)
                return;
            if (activeWindows.Any(IsLive) || pendingDialogs.Count == 0)
                return;
            PendingDialog next = TakeNextPending();
            if (next == null)
                return;
            EditorApplication.delayCall += () =>
            {
                if (next?.completion?.Task.IsCanceled == true)
                {
                    OpenNextQueued();
                    return;
                }
                OpenNow(next.request, next.completion, false);
                AddPendingObserversToWindow(next);
            };
        }

        internal static Rect ResolveOwnerBounds(ESAdvancedDialogRequest request)
        {
            EditorWindow owner = request?.owner;
            if (IsLive(owner))
                return owner.position;
            EditorWindow focused = EditorWindow.focusedWindow;
            if (IsLive(focused) && !(focused is ESAdvancedDialogWindow))
                return focused.position;
            return EditorGUIUtility.GetMainWindowPosition();
        }

        internal static int ResolveOwnerDepth(ESAdvancedDialogRequest request)
        {
            int depth = 0;
            EditorWindow current = request?.owner;
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
            ESAdvancedDialogRequest request,
            TaskCompletionSource<ESAdvancedDialogResult> completion,
            bool modal)
        {
            activeWindows.RemoveAll(item => !IsLive(item));
            if (activeWindows.Count >= MaximumActiveDialogs)
                throw new InvalidOperationException(
                    "ES 对话框活动窗口已达到上限 " + MaximumActiveDialogs
                    + "；请复用稳定 dialogId 或启用队列，而不是继续并行打开。");
            try
            {
                ESAdvancedDialogWindow window = ESAdvancedDialogWindow.Create(request, completion);
                activeWindows.Add(window);
                window.Open(modal);
                return window;
            }
            finally
            {
                openingReplacement = false;
            }
        }

        private static void OpenNextQueued()
        {
            if (activeWindows.Any(IsLive) || pendingDialogs.Count == 0)
                return;
            PendingDialog next = TakeNextPending();
            if (next == null)
                return;
            OpenNow(next.request, next.completion, false);
            AddPendingObserversToWindow(next);
        }

        private static ESAdvancedDialogWindow FindDuplicate(string dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
                return null;
            activeWindows.RemoveAll(item => !IsLive(item));
            return activeWindows.FirstOrDefault(window =>
                string.Equals(window.DialogId, dialogId.Trim(), StringComparison.Ordinal));
        }

        private static PendingDialog FindPendingDuplicate(string dialogId)
        {
            if (string.IsNullOrWhiteSpace(dialogId))
                return null;
            string normalized = dialogId.Trim();
            return pendingDialogs.FirstOrDefault(item =>
                item?.request != null
                && string.Equals(item.request.dialogId?.Trim(), normalized, StringComparison.Ordinal));
        }

        private static PendingDialog TakeNextPending()
        {
            while (pendingDialogs.Count > 0)
            {
                PendingDialog next = pendingDialogs[0];
                pendingDialogs.RemoveAt(0);
                if (next?.completion?.Task.IsCanceled == true)
                    continue;
                return next;
            }
            return null;
        }

        private static void AddPendingObserversToWindow(PendingDialog pending)
        {
            if (pending == null || pending.observers.Count == 0)
                return;
            ESAdvancedDialogWindow window = FindDuplicate(pending.request.dialogId);
            if (window == null)
                return;
            for (int i = 0; i < pending.observers.Count; i++)
                window.AddCompletionObserver(pending.observers[i]);
            pending.observers.Clear();
        }

        private static bool IsLive(EditorWindow window) => window != null;

        private static ESAdvancedDialogResult CancelledResult()
        {
            return new ESAdvancedDialogResult { accepted = false, cancelled = true };
        }

        private static void ValidateServiceRequest(ESAdvancedDialogRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ESAdvancedDialogWindow.ValidateRequest(request);
        }

        private static void Enqueue(PendingDialog pending)
        {
            if (pendingDialogs.Count >= MaximumPendingDialogs)
                throw new InvalidOperationException(
                    "ES 对话框等待队列已达到上限 " + MaximumPendingDialogs
                    + "；请检查是否在循环或高频回调中重复提交对话框。");
            pendingDialogs.Add(pending);
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
                tone = source.tone,
                showCancel = source.showCancel,
                animateOpening = source.animateOpening,
                closeOnEscape = source.closeOnEscape,
                allowOperationCancellation = source.allowOperationCancellation,
                queueBehindActiveDialog = source.queueBehindActiveDialog,
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
            var cancelled = new ESAdvancedDialogResult
            {
                accepted = false,
                cancelled = true,
                actionId = string.Empty,
            };
            for (int i = 0; i < pendingDialogs.Count; i++)
                pendingDialogs[i]?.Complete(cancelled);
            pendingDialogs.Clear();
            ESAdvancedDialogWindow[] windows = activeWindows.Where(IsLive).ToArray();
            for (int i = 0; i < windows.Length; i++)
                windows[i].CancelAndClose();
            activeWindows.Clear();
        }

        internal static void RestartAfterPresenterRegistration()
        {
            shuttingDown = false;
        }
    }

    public sealed class ESAdvancedDialogWindow : EditorWindow
    {
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
        private IVisualElementScheduledItem busyRefreshSchedule;
        private int validationGeneration;
        private bool busy;
        private bool asyncValidationPending;
        private bool customContentReleased;
        private bool resultPublished;
        private ESAdvancedDialogResult lastResult;
        private readonly List<TaskCompletionSource<ESAdvancedDialogResult>> completionObservers =
            new List<TaskCompletionSource<ESAdvancedDialogResult>>();

        internal string DialogId => request?.dialogId?.Trim() ?? string.Empty;
        internal EditorWindow Owner => request?.owner;
        internal ESAdvancedDialogResult LastResult => lastResult;

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
        public static ESAdvancedDialogWindow Show(ESAdvancedDialogRequest request)
        {
            return ESDialogService.Show(request);
        }

        public static Task<ESAdvancedDialogResult> ShowAsync(
            ESAdvancedDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            return ESDialogService.ShowAsync(request, cancellationToken);
        }

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
            if (completion != null)
                window.completionObservers.Add(completion);
            window.Initialize(request);
            window.titleContent = new GUIContent(request.title);
            window.ApplyInitialPosition();
            return window;
        }

        internal void Open(bool modal)
        {
            if (modal)
            {
                if (request.animateOpening)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (this == null)
                            return;
                        Focus();
                        ES.EditorInternal.ESWindowFrameActivation.Play(this, position);
                    };
                }
                ShowModalUtility();
                return;
            }
            ShowUtility();
            Focus();
            if (request.animateOpening)
                ES.EditorInternal.ESWindowFrameActivation.Play(this, position);
        }

        private void Initialize(ESAdvancedDialogRequest value)
        {
            request = value;
            initialized = true;
            RefreshValidation();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            fieldBlocks.Clear();
            fieldControls.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.minWidth = 0f;
            rootVisualElement.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WindowSurfaceColor;

            if (!initialized || request == null)
            {
                BuildExpiredView();
                ES.EditorInternal.ESEditorPresentation.BindWindow(this, allowSemiSleep: false);
                return;
            }

            shell = new ES.EditorInternal.ESWindowShell(
                request.title,
                request.subtitle,
                false);
            shell.Toolbar.style.display = DisplayStyle.None;
            shell.Content.style.flexDirection = FlexDirection.Column;
            shell.HeaderToolbar.Add(ES.EditorInternal.ESWindowPresentation.CreateHeaderIconButton(
                "×",
                "关闭对话框",
                () =>
                {
                    ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
                    Complete(false);
                }));
            rootVisualElement.Add(shell.Root);

            dialogContent = new VisualElement { name = "ESDialogContent" };
            dialogContent.style.flexGrow = 1f;
            dialogContent.style.flexShrink = 1f;
            dialogContent.style.minWidth = 0f;
            dialogContent.style.minHeight = 0f;
            dialogContent.style.flexDirection = FlexDirection.Column;
            shell.Content.Add(dialogContent);

            bodyScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ESDialogBodyScroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            bodyScroll.style.flexGrow = 1f;
            bodyScroll.style.flexShrink = 1f;
            bodyScroll.style.minWidth = 0f;
            bodyScroll.style.minHeight = 0f;
            bodyScroll.contentContainer.style.paddingLeft = 18f;
            bodyScroll.contentContainer.style.paddingRight = 18f;
            bodyScroll.contentContainer.style.paddingTop = 16f;
            bodyScroll.contentContainer.style.paddingBottom = 14f;
            dialogContent.Add(bodyScroll);

            BuildSummary(bodyScroll);
            for (int i = 0; i < request.fields.Count; i++)
                BuildField(bodyScroll, request.fields[i]);
            BuildCustomContent(bodyScroll);
            BuildValidation(bodyScroll);
            BuildFooter(dialogContent);
            BuildBusyOverlay(shell.Content);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            ES.EditorInternal.ESEditorPresentation.BindWindow(this, allowSemiSleep: false);
            RefreshValidation();
            ScheduleInitialFocus();
        }

        private void BuildCustomContent(VisualElement parent)
        {
            if (request.createCustomContent == null)
                return;
            customContent = request.createCustomContent(BuildValues());
            if (customContent == null)
                return;
            customContent.name = string.IsNullOrWhiteSpace(customContent.name)
                ? "ESDialogCustomContent"
                : customContent.name;
            customContent.style.minWidth = 0f;
            customContent.style.marginBottom = 11f;
            parent.Add(customContent);
        }

        private void BuildBusyOverlay(VisualElement parent)
        {
            busyOverlay = new VisualElement { name = "ESDialogBusyOverlay" };
            busyOverlay.style.position = Position.Absolute;
            busyOverlay.style.left = 0f;
            busyOverlay.style.right = 0f;
            busyOverlay.style.top = 0f;
            busyOverlay.style.bottom = 0f;
            busyOverlay.style.display = DisplayStyle.None;
            busyOverlay.style.justifyContent = Justify.Center;
            busyOverlay.style.alignItems = Align.Center;
            busyOverlay.style.backgroundColor = new Color(0.035f, 0.045f, 0.055f, 0.90f);

            var panel = new VisualElement();
            panel.style.width = 320f;
            panel.style.maxWidth = Length.Percent(86f);
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 16f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor;
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

        private void ScheduleInitialFocus()
        {
            rootVisualElement.schedule.Execute(() =>
            {
                if (this == null || completed)
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
            summary.style.minWidth = 0f;
            summary.style.marginBottom = request.fields.Count > 0 ? 14f : 4f;
            summary.style.paddingLeft = 12f;
            summary.style.paddingRight = 12f;
            summary.style.paddingTop = 10f;
            summary.style.paddingBottom = 10f;
            summary.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor;
            summary.style.borderLeftWidth = 3f;
            summary.style.borderLeftColor = accent;

            if (!string.IsNullOrWhiteSpace(request.message))
            {
                Label message = new Label(request.message.Trim()) { name = "ESDialogMessage" };
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.fontSize = 13f;
                message.style.unityFontStyleAndWeight = FontStyle.Bold;
                message.style.color = ES.EditorInternal.ESEditorPresentation.SectionSelectedTextColor;
                summary.Add(message);
            }

            if (!string.IsNullOrWhiteSpace(request.detail))
            {
                Label detail = new Label(request.detail.Trim()) { name = "ESDialogDetail" };
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.marginTop = string.IsNullOrWhiteSpace(request.message) ? 0f : 6f;
                detail.style.fontSize = 11f;
                detail.style.color = ES.EditorInternal.ESEditorPresentation.SectionMutedTextColor;
                summary.Add(detail);
            }

            parent.Add(summary);
        }

        private void BuildField(VisualElement parent, ESAdvancedDialogField field)
        {
            VisualElement block = new VisualElement { name = "ESDialogField-" + field.id };
            block.style.minWidth = 0f;
            block.style.marginBottom = 11f;

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
            group.style.minWidth = 0f;
            group.style.paddingLeft = 8f;
            group.style.paddingRight = 8f;
            group.style.paddingTop = 5f;
            group.style.paddingBottom = 5f;
            group.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.ControlSurfaceColor;
            group.style.borderLeftWidth = 1f;
            group.style.borderRightWidth = 1f;
            group.style.borderTopWidth = 1f;
            group.style.borderBottomWidth = 1f;
            group.style.borderLeftColor = ES.EditorInternal.ESEditorPresentation.DividerColor;
            group.style.borderRightColor = ES.EditorInternal.ESEditorPresentation.DividerColor;
            group.style.borderTopColor = ES.EditorInternal.ESEditorPresentation.DividerColor;
            group.style.borderBottomColor = ES.EditorInternal.ESEditorPresentation.DividerColor;

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
                ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor,
                ES.EditorInternal.ESEditorPresentation.GetSemanticAccent(2),
                0.82f);
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                int level = field.minIntValue + i;
                bool selected = level == field.intValue;
                Color background = selected
                    ? selectedBackground
                    : ES.EditorInternal.ESEditorPresentation.ControlSurfaceColor;
                Color border = selected
                    ? ES.EditorInternal.ESEditorPresentation.GetSemanticAccent(2)
                    : ES.EditorInternal.ESEditorPresentation.DividerColor;
                button.style.backgroundColor = background;
                button.style.color = selected
                    ? GetReadableActionTextColor(background)
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
            browse.SetEnabled(!field.readOnly);
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
            validationPanel.style.display = DisplayStyle.None;
            validationPanel.style.marginTop = 2f;
            validationPanel.style.paddingLeft = 10f;
            validationPanel.style.paddingRight = 10f;
            validationPanel.style.paddingTop = 8f;
            validationPanel.style.paddingBottom = 8f;
            validationPanel.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WarningBackground;
            validationPanel.style.borderLeftWidth = 3f;
            validationPanel.style.borderLeftColor = ES.EditorInternal.ESEditorPresentation.WarningColor;
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
            footer.style.flexShrink = 0f;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.flexWrap = Wrap.Wrap;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 14f;
            footer.style.paddingRight = 14f;
            footer.style.paddingTop = 9f;
            footer.style.paddingBottom = 9f;
            footer.style.backgroundColor = ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = ES.EditorInternal.ESEditorPresentation.DividerColor;

            auxiliaryActions = new VisualElement { name = "ESDialogAuxiliaryActions" };
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
                    () => ExecuteAuxiliaryAction(action));
                ApplyActionRole(button, action.role);
                auxiliaryActions.Add(button);
            }
            footer.Add(auxiliaryActions);

            decisionActions = new VisualElement { name = "ESDialogDecisionActions" };
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
            busyOverlay.style.display = DisplayStyle.Flex;
            busyLabel.text = message;
            cancelBusyButton.style.display = request.allowOperationCancellation
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            decisionActions?.SetEnabled(false);
            auxiliaryActions?.SetEnabled(false);
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = rootVisualElement.schedule.Execute(RefreshBusyOverlay).Every(100);
        }

        private void RefreshBusyOverlay()
        {
            if (!busy || activeProgress == null)
                return;
            ESProgressSnapshot snapshot = ESProgressCenter.GetSnapshot()
                .LastOrDefault(item => string.Equals(item.id, activeProgress.Id, StringComparison.Ordinal));
            if (snapshot == null)
                return;
            busyProgress.value = Mathf.Clamp01(snapshot.progress) * 100f;
            busyProgress.title = snapshot.summary ?? string.Empty;
        }

        private void CancelBusyOperation()
        {
            operationCancellation?.Cancel();
            activeProgress?.RequestCancel();
            cancelBusyButton?.SetEnabled(false);
            if (busyLabel != null)
                busyLabel.text = "正在取消";
        }

        private void EndBusy()
        {
            busy = false;
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = null;
            busyOverlay.style.display = DisplayStyle.None;
            cancelBusyButton?.SetEnabled(true);
            decisionActions?.SetEnabled(true);
            auxiliaryActions?.SetEnabled(true);
            activeProgress = null;
            operationCancellation?.Dispose();
            operationCancellation = null;
            RefreshValidation();
        }

        private static void ApplyActionRole(Button button, ESAdvancedDialogActionRole role)
        {
            if (button == null)
                return;
            Color background;
            if (role == ESAdvancedDialogActionRole.Danger)
                background = ES.EditorInternal.ESEditorPresentation.ErrorColor;
            else if (role == ESAdvancedDialogActionRole.Primary)
                background = Color.Lerp(
                    ES.EditorInternal.ESEditorPresentation.WindowRaisedSurfaceColor,
                    ES.EditorInternal.ESEditorPresentation.GetSemanticAccent(2),
                    0.82f);
            else
                return;

            button.style.backgroundColor = background;
            button.style.color = GetReadableActionTextColor(background);
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
            var expiredShell = new ES.EditorInternal.ESWindowShell(
                "ES 对话框已失效",
                "Domain Reload 已清除本次临时输入上下文");
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
            Rect main = ESDialogService.ResolveOwnerBounds(request);
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

            Rect centered = CalculateCenteredPosition(
                main,
                requestedMin,
                request.preferredSize,
                estimatedHeight);
            minSize = new Vector2(
                Mathf.Min(Mathf.Max(360f, requestedMin.x), centered.width),
                Mathf.Min(Mathf.Max(240f, requestedMin.y), centered.height));
            position = ESDialogService.OffsetChildDialog(
                centered,
                ESDialogService.ResolveOwnerDepth(request));
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
            confirmButton?.SetEnabled(valid && !busy && !asyncValidationPending);
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
            confirmButton?.SetEnabled(false);
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
                    confirmButton?.SetEnabled(valid && !busy);
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
                    confirmButton?.SetEnabled(false);
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
                Close();
        }

        private void CompleteFromAction(string actionId)
        {
            if (!TryComplete(false, false, actionId))
                return;
            if (this != null)
                Close();
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
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            busyRefreshSchedule?.Pause();
            busyRefreshSchedule = null;
            validationCancellation?.Cancel();
            validationCancellation?.Dispose();
            validationCancellation = null;
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            operationCancellation = null;
            if (activeProgress != null)
            {
                activeProgress.Cancel("对话框已关闭");
                activeProgress = null;
            }
            ReleaseCustomContent();
            ES.EditorInternal.ESEditorPresentation.UnbindWindow(this, true);
            if (initialized && !completed)
                TryComplete(false, true, null);
            ESDialogService.NotifyClosed(this, lastResult);
            PublishResult();
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
            if (string.IsNullOrWhiteSpace(request.title)) throw new ArgumentException("对话框标题不能为空。", nameof(request));
            if (string.IsNullOrWhiteSpace(request.confirmText)) throw new ArgumentException("确认按钮文本不能为空。", nameof(request));
            if (request.showCancel && string.IsNullOrWhiteSpace(request.cancelText)) throw new ArgumentException("取消按钮文本不能为空。", nameof(request));
            if (request.fields == null) throw new ArgumentException("高级对话框字段集合不能为空。", nameof(request));
            if (request.auxiliaryActions == null) throw new ArgumentException("高级对话框辅助动作集合不能为空。", nameof(request));
            if (request.asyncValidationDelayMs < 0)
                throw new ArgumentException("异步校验延迟不能小于 0。", nameof(request));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAdvancedDialogField field in request.fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.id) || string.IsNullOrWhiteSpace(field.label))
                    throw new ArgumentException("每个输入字段都必须具备稳定 ID 和显示名称。", nameof(request));
                if (!ids.Add(field.id)) throw new ArgumentException("高级对话框存在重复字段 ID：" + field.id, nameof(request));
                if (field.kind == ESAdvancedDialogFieldKind.Choice
                    || field.kind == ESAdvancedDialogFieldKind.MultiChoice)
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
            }
        }
    }
}
