using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ES
{
    public enum ESDialogHost : byte
    {
        Auto,
        Editor,
        Runtime,
    }

    public enum ESDialogTone : byte
    {
        Info,
        Success,
        Warning,
        Danger,
    }

    public enum ESDialogDuplicatePolicy : byte
    {
        FocusExisting,
        Queue,
        ReplaceExisting,
        AllowParallel,
    }

    public enum ESDialogChoice : byte
    {
        Primary,
        Secondary,
        Cancelled,
    }

    public enum ESDialogFieldKind : byte
    {
        Text,
        MultilineText,
        Toggle,
        Choice,
        MultiChoice,
        Recommendation,
    }

    public enum ESDialogCompletion : byte
    {
        Accepted,
        Cancelled,
        HostUnavailable,
        AmbiguousHost,
        PresenterStopped,
        CapabilityUnavailable,
        Failed,
    }

    public enum ESDialogPresenterStopReason : byte
    {
        RegistrationReleased,
        HostShutdown,
        DomainReload,
        ApplicationQuit,
    }

    [Flags]
    public enum ESDialogCapabilities
    {
        None = 0,
        Message = 1 << 0,
        TextInput = 1 << 1,
        Toggle = 1 << 2,
        Choice = 1 << 3,
        MultiChoice = 1 << 4,
        Recommendation = 1 << 5,
        AsyncValidation = 1 << 6,
        Progress = 1 << 7,
        AllCommon = Message | TextInput | Toggle | Choice | MultiChoice
            | Recommendation | AsyncValidation | Progress,
    }

    public sealed class ESDialogValidation
    {
        public string FieldId { get; }
        public string Message { get; }

        public ESDialogValidation(string message, string fieldId = null)
        {
            Message = message ?? string.Empty;
            FieldId = fieldId?.Trim() ?? string.Empty;
        }
    }

    public sealed class ESDialogOption
    {
        public string Id { get; }
        public string Label { get; }

        public ESDialogOption(string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Dialog option id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Dialog option label cannot be empty.", nameof(label));
            Id = id.Trim();
            Label = label;
        }
    }

    public sealed class ESDialogField
    {
        public string Id { get; }
        public string Label { get; }
        public ESDialogFieldKind Kind { get; }
        public string Help { get; set; } = string.Empty;
        public bool Required { get; set; }
        public bool ReadOnly { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public bool ToggleValue { get; set; }
        public int IntegerValue { get; set; }
        public int Minimum { get; set; }
        public int Maximum { get; set; }
        public string LowLabel { get; set; } = string.Empty;
        public string HighLabel { get; set; } = string.Empty;
        public int MinimumSelections { get; set; }
        public int MaximumSelections { get; set; }
        public List<ESDialogOption> Options { get; } = new List<ESDialogOption>();
        public List<string> SelectedOptionIds { get; } = new List<string>();

        public ESDialogField(string id, string label, ESDialogFieldKind kind)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Dialog field id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Dialog field label cannot be empty.", nameof(label));
            Id = id.Trim();
            Label = label;
            Kind = kind;
        }
    }

    public sealed class ESDialogValues
    {
        private readonly Dictionary<string, string> strings;
        private readonly Dictionary<string, bool> toggles;
        private readonly Dictionary<string, int> integers;
        private readonly Dictionary<string, IReadOnlyList<string>> selections;

        public static ESDialogValues Empty { get; } = new ESDialogValues(null, null, null, null);

        public ESDialogValues(
            IDictionary<string, string> strings,
            IDictionary<string, bool> toggles,
            IDictionary<string, int> integers,
            IDictionary<string, IReadOnlyList<string>> selections)
        {
            this.strings = Copy(strings);
            this.toggles = Copy(toggles);
            this.integers = Copy(integers);
            this.selections = CopySelections(selections);
        }

        public string GetString(string id, string fallback = "")
            => strings.TryGetValue(id ?? string.Empty, out string value) ? value : fallback;

        public bool GetToggle(string id, bool fallback = false)
            => toggles.TryGetValue(id ?? string.Empty, out bool value) ? value : fallback;

        public int GetInteger(string id, int fallback = 0)
            => integers.TryGetValue(id ?? string.Empty, out int value) ? value : fallback;

        public int GetRecommendation(string id, int fallback = 0) => GetInteger(id, fallback);

        public IReadOnlyList<string> GetSelections(string id)
            => selections.TryGetValue(id ?? string.Empty, out IReadOnlyList<string> value)
                ? value
                : Array.Empty<string>();

        public bool HasSelection(string id, string optionId)
        {
            IReadOnlyList<string> values = GetSelections(id);
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], optionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static Dictionary<string, TValue> Copy<TValue>(IDictionary<string, TValue> source)
            => source == null
                ? new Dictionary<string, TValue>(StringComparer.Ordinal)
                : new Dictionary<string, TValue>(source, StringComparer.Ordinal);

        private static Dictionary<string, IReadOnlyList<string>> CopySelections(
            IDictionary<string, IReadOnlyList<string>> source)
        {
            var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (source == null)
                return copy;
            foreach (KeyValuePair<string, IReadOnlyList<string>> pair in source)
            {
                IReadOnlyList<string> value = pair.Value ?? Array.Empty<string>();
                var items = new string[value.Count];
                for (int i = 0; i < value.Count; i++)
                    items[i] = value[i];
                copy[pair.Key] = Array.AsReadOnly(items);
            }
            return copy;
        }
    }

    public sealed class ESDialogResult
    {
        public ESDialogCompletion Completion { get; }
        public ESDialogHost Host { get; }
        public string ActionId { get; }
        public string Error { get; }
        public ESDialogValues Values { get; }
        public bool Accepted => Completion == ESDialogCompletion.Accepted;
        public bool Cancelled => Completion == ESDialogCompletion.Cancelled;

        public ESDialogResult(
            ESDialogCompletion completion,
            ESDialogHost host,
            ESDialogValues values = null,
            string actionId = null,
            string error = null)
        {
            Completion = completion;
            Host = host;
            Values = values ?? ESDialogValues.Empty;
            ActionId = actionId ?? string.Empty;
            Error = error ?? string.Empty;
        }
    }

    public sealed class ESDialogRequest
    {
        public string DialogId { get; set; } = string.Empty;
        public ESDialogHost Host { get; set; } = ESDialogHost.Auto;
        public ESDialogTone Tone { get; set; } = ESDialogTone.Info;
        public ESDialogDuplicatePolicy DuplicatePolicy { get; set; } = ESDialogDuplicatePolicy.FocusExisting;
        public string Title { get; set; } = "ES";
        public string Subtitle { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string ConfirmText { get; set; } = "确定";
        public string CancelText { get; set; } = "取消";
        public string SecondaryText { get; set; } = string.Empty;
        public bool ShowCancel { get; set; } = true;
        public bool QueueBehindActiveDialog { get; set; }
        public string InitialFocusFieldId { get; set; } = string.Empty;
        public int AsyncValidationDelayMs { get; set; } = 180;
        public List<ESDialogField> Fields { get; } = new List<ESDialogField>();
        public Func<ESDialogValues, ESDialogValidation> Validate { get; set; }
        public Func<ESDialogValues, CancellationToken, Task<ESDialogValidation>> ValidateAsync { get; set; }

        public static ESDialogRequest MessageRequest(
            string dialogId,
            string title,
            string message,
            string confirmText = "知道了")
            => new ESDialogRequest
            {
                DialogId = dialogId,
                Title = title,
                Message = message,
                ConfirmText = confirmText,
                ShowCancel = false,
            };

        public static ESDialogRequest ConfirmRequest(
            string dialogId,
            string title,
            string message,
            string confirmText = "确定",
            string cancelText = "取消")
            => new ESDialogRequest
            {
                DialogId = dialogId,
                Title = title,
                Message = message,
                ConfirmText = confirmText,
                CancelText = cancelText,
                ShowCancel = true,
            };

        public ESDialogField AddText(string id, string label, string value = "", bool required = false)
            => AddField(new ESDialogField(id, label, ESDialogFieldKind.Text)
            {
                StringValue = value ?? string.Empty,
                Required = required,
            });

        public ESDialogField AddMultilineText(string id, string label, string value = "", bool required = false)
            => AddField(new ESDialogField(id, label, ESDialogFieldKind.MultilineText)
            {
                StringValue = value ?? string.Empty,
                Required = required,
            });

        public ESDialogField AddToggle(string id, string label, bool value = false)
            => AddField(new ESDialogField(id, label, ESDialogFieldKind.Toggle) { ToggleValue = value });

        public ESDialogField AddChoice(
            string id,
            string label,
            IEnumerable<ESDialogOption> options,
            string selectedOptionId = "",
            bool required = true)
        {
            var field = new ESDialogField(id, label, ESDialogFieldKind.Choice)
            {
                StringValue = selectedOptionId ?? string.Empty,
                Required = required,
            };
            AddOptions(field, options);
            return AddField(field);
        }

        public ESDialogField AddMultiChoice(
            string id,
            string label,
            IEnumerable<ESDialogOption> options,
            IEnumerable<string> selectedOptionIds = null,
            int minimumSelections = 0,
            int maximumSelections = 0)
        {
            var field = new ESDialogField(id, label, ESDialogFieldKind.MultiChoice)
            {
                MinimumSelections = minimumSelections,
                MaximumSelections = maximumSelections,
                Required = minimumSelections > 0,
            };
            AddOptions(field, options);
            if (selectedOptionIds != null)
            {
                foreach (string optionId in selectedOptionIds)
                {
                    if (!string.IsNullOrWhiteSpace(optionId))
                        field.SelectedOptionIds.Add(optionId.Trim());
                }
            }
            return AddField(field);
        }

        public ESDialogField AddRecommendation(
            string id,
            string label,
            int value = 3,
            int minimum = 0,
            int maximum = 5,
            string lowLabel = "不推荐",
            string highLabel = "强烈推荐")
            => AddField(new ESDialogField(id, label, ESDialogFieldKind.Recommendation)
            {
                IntegerValue = value,
                Minimum = minimum,
                Maximum = maximum,
                LowLabel = lowLabel ?? string.Empty,
                HighLabel = highLabel ?? string.Empty,
            });

        public ESDialogRequest CreateSnapshot()
        {
            ValidateContract();
            return Snapshot();
        }

        private void ValidateContract()
        {
            if (string.IsNullOrWhiteSpace(DialogId))
                throw new ArgumentException("ESDialog requires a stable DialogId.", nameof(DialogId));
            if (string.IsNullOrWhiteSpace(Title))
                throw new ArgumentException("ESDialog title cannot be empty.", nameof(Title));
            if (string.IsNullOrWhiteSpace(ConfirmText))
                throw new ArgumentException("ESDialog confirm text cannot be empty.", nameof(ConfirmText));
            if (ShowCancel && string.IsNullOrWhiteSpace(CancelText))
                throw new ArgumentException("ESDialog cancel text cannot be empty.", nameof(CancelText));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Fields.Count; i++)
            {
                ESDialogField field = Fields[i]
                    ?? throw new ArgumentException("ESDialog field cannot be null.", nameof(Fields));
                if (!ids.Add(field.Id))
                    throw new ArgumentException("Duplicate ESDialog field id: " + field.Id, nameof(Fields));
                if (field.Kind == ESDialogFieldKind.Choice
                    || field.Kind == ESDialogFieldKind.MultiChoice)
                {
                    if (field.Options.Count == 0)
                        throw new ArgumentException(
                            "Choice field requires at least one option: " + field.Id,
                            nameof(Fields));
                    var optionIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int optionIndex = 0; optionIndex < field.Options.Count; optionIndex++)
                    {
                        if (!optionIds.Add(field.Options[optionIndex].Id))
                            throw new ArgumentException(
                                "Duplicate option id in field " + field.Id + ": "
                                + field.Options[optionIndex].Id,
                                nameof(Fields));
                    }
                    if (field.Kind == ESDialogFieldKind.Choice
                        && field.Required
                        && !optionIds.Contains(field.StringValue ?? string.Empty))
                        throw new ArgumentException(
                            "Required choice field has no valid selected option: " + field.Id,
                            nameof(Fields));
                    if (field.Kind == ESDialogFieldKind.MultiChoice)
                    {
                        int effectiveMaximum = field.MaximumSelections <= 0
                            ? field.Options.Count
                            : field.MaximumSelections;
                        if (field.MinimumSelections < 0
                            || effectiveMaximum > field.Options.Count
                            || field.MinimumSelections > effectiveMaximum)
                            throw new ArgumentException(
                                "Multi-choice selection bounds are invalid: " + field.Id,
                                nameof(Fields));
                        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
                        for (int selectedIndex = 0;
                             selectedIndex < field.SelectedOptionIds.Count;
                             selectedIndex++)
                        {
                            string selectedId = field.SelectedOptionIds[selectedIndex];
                            if (!optionIds.Contains(selectedId) || !selectedIds.Add(selectedId))
                                throw new ArgumentException(
                                    "Multi-choice field contains an unknown or duplicate selection: "
                                    + field.Id,
                                    nameof(Fields));
                        }
                        if (selectedIds.Count > effectiveMaximum)
                            throw new ArgumentException(
                                "Multi-choice default selection exceeds its maximum: " + field.Id,
                                nameof(Fields));
                    }
                }
                if (field.Kind == ESDialogFieldKind.Recommendation
                    && (field.Minimum >= field.Maximum
                        || field.IntegerValue < field.Minimum
                        || field.IntegerValue > field.Maximum))
                    throw new ArgumentException(
                        "Recommendation range or value is invalid: " + field.Id,
                        nameof(Fields));
            }
        }

        internal ESDialogCapabilities GetRequiredCapabilities()
        {
            ESDialogCapabilities required = ESDialogCapabilities.Message;
            if (ValidateAsync != null)
                required |= ESDialogCapabilities.AsyncValidation;
            for (int i = 0; i < Fields.Count; i++)
            {
                switch (Fields[i].Kind)
                {
                    case ESDialogFieldKind.Text:
                    case ESDialogFieldKind.MultilineText:
                        required |= ESDialogCapabilities.TextInput;
                        break;
                    case ESDialogFieldKind.Toggle:
                        required |= ESDialogCapabilities.Toggle;
                        break;
                    case ESDialogFieldKind.Choice:
                        required |= ESDialogCapabilities.Choice;
                        break;
                    case ESDialogFieldKind.MultiChoice:
                        required |= ESDialogCapabilities.MultiChoice;
                        break;
                    case ESDialogFieldKind.Recommendation:
                        required |= ESDialogCapabilities.Recommendation;
                        break;
                }
            }
            return required;
        }

        private ESDialogRequest Snapshot()
        {
            var snapshot = new ESDialogRequest
            {
                DialogId = DialogId?.Trim() ?? string.Empty,
                Host = Host,
                Tone = Tone,
                DuplicatePolicy = DuplicatePolicy,
                Title = Title,
                Subtitle = Subtitle,
                Message = Message,
                Detail = Detail,
                ConfirmText = ConfirmText,
                CancelText = CancelText,
                SecondaryText = SecondaryText,
                ShowCancel = ShowCancel,
                QueueBehindActiveDialog = QueueBehindActiveDialog,
                InitialFocusFieldId = InitialFocusFieldId,
                AsyncValidationDelayMs = AsyncValidationDelayMs,
                Validate = Validate,
                ValidateAsync = ValidateAsync,
            };
            for (int i = 0; i < Fields.Count; i++)
                snapshot.Fields.Add(CloneField(Fields[i]));
            return snapshot;
        }

        private ESDialogField AddField(ESDialogField field)
        {
            Fields.Add(field);
            return field;
        }

        private static void AddOptions(ESDialogField field, IEnumerable<ESDialogOption> options)
        {
            if (options == null)
                return;
            foreach (ESDialogOption option in options)
            {
                if (option != null)
                    field.Options.Add(option);
            }
        }

        private static ESDialogField CloneField(ESDialogField source)
        {
            var clone = new ESDialogField(source.Id, source.Label, source.Kind)
            {
                Help = source.Help,
                Required = source.Required,
                ReadOnly = source.ReadOnly,
                StringValue = source.StringValue,
                ToggleValue = source.ToggleValue,
                IntegerValue = source.IntegerValue,
                Minimum = source.Minimum,
                Maximum = source.Maximum,
                LowLabel = source.LowLabel,
                HighLabel = source.HighLabel,
                MinimumSelections = source.MinimumSelections,
                MaximumSelections = source.MaximumSelections,
            };
            for (int i = 0; i < source.Options.Count; i++)
            {
                ESDialogOption option = source.Options[i];
                clone.Options.Add(new ESDialogOption(option.Id, option.Label));
            }
            clone.SelectedOptionIds.AddRange(source.SelectedOptionIds);
            return clone;
        }
    }

    public sealed class ESDialogPresenterLease : IDisposable
    {
        private readonly ESDialogHost host;
        private readonly long generation;
        private bool disposed;

        internal ESDialogPresenterLease(ESDialogHost host, long generation)
        {
            this.host = host;
            this.generation = generation;
        }

        public ESDialogHost Host => host;
        public long Generation => generation;
        public bool IsActive => !disposed && ESDialog.IsPresenterRegistrationActive(host, generation);

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            ESDialog.ReleasePresenter(host, generation);
        }
    }

    public static class ESDialog
    {
        private sealed class Registration
        {
            internal long Generation;
            internal IESDialogPresenter Presenter;
            internal int ActiveDispatches;
            internal bool ReleasePending;
        }

        private static readonly object gate = new object();
        private static readonly Dictionary<ESDialogHost, Registration> presenters =
            new Dictionary<ESDialogHost, Registration>();
        private static long nextGeneration;

        public static ESDialogPresenterLease RegisterPresenter(IESDialogPresenter presenter)
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter));
            if (presenter.Host == ESDialogHost.Auto)
                throw new ArgumentException("A presenter must register an explicit host.", nameof(presenter));
            lock (gate)
            {
                if (presenters.ContainsKey(presenter.Host))
                    throw new InvalidOperationException(
                        "An ESDialog presenter is already registered for host " + presenter.Host + ".");
                long generation = ++nextGeneration;
                presenters.Add(presenter.Host, new Registration
                {
                    Generation = generation,
                    Presenter = presenter,
                });
                return new ESDialogPresenterLease(presenter.Host, generation);
            }
        }

        public static Task<ESDialogResult> ShowAsync(
            ESDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            request = request.CreateSnapshot();
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<ESDialogResult>(cancellationToken);

            Registration registration;
            lock (gate)
            {
                registration = ResolveRegistration(request.Host, out ESDialogResult routingFailure);
                if (registration == null)
                    return Task.FromResult(routingFailure);
                IESDialogPresenter presenter = registration.Presenter;
                ESDialogCapabilities required = request.GetRequiredCapabilities();
                ESDialogCapabilities missing = required & ~presenter.Capabilities;
                if (missing != ESDialogCapabilities.None)
                {
                    return Task.FromResult(new ESDialogResult(
                        ESDialogCompletion.CapabilityUnavailable,
                        presenter.Host,
                        error: "ESDialog presenter does not support: " + missing + "."));
                }
                registration.ActiveDispatches++;
            }
            try
            {
                return registration.Presenter.ShowAsync(request, cancellationToken)
                    ?? Task.FromResult(new ESDialogResult(
                        ESDialogCompletion.Failed,
                        registration.Presenter.Host,
                        error: "ESDialog presenter returned no task."));
            }
            catch (Exception exception)
            {
                return Task.FromException<ESDialogResult>(exception);
            }
            finally
            {
                CompleteDispatch(registration);
            }
        }

        public static async Task InfoAsync(
            string dialogId,
            string title,
            string message,
            string confirmText = "知道了",
            string detail = "",
            ESDialogHost host = ESDialogHost.Auto,
            CancellationToken cancellationToken = default)
        {
            ESDialogRequest request = ESDialogRequest.MessageRequest(dialogId, title, message, confirmText);
            request.Detail = detail ?? string.Empty;
            request.Host = host;
            ESDialogResult result = await ShowAsync(request, cancellationToken);
            EnsureCompletedByUser(result);
        }

        public static async Task<bool> ConfirmAsync(
            string dialogId,
            string title,
            string message,
            string confirmText = "确定",
            string cancelText = "取消",
            string detail = "",
            ESDialogTone tone = ESDialogTone.Info,
            ESDialogHost host = ESDialogHost.Auto,
            CancellationToken cancellationToken = default)
        {
            ESDialogRequest request = ESDialogRequest.ConfirmRequest(
                dialogId, title, message, confirmText, cancelText);
            request.Detail = detail ?? string.Empty;
            request.Tone = tone;
            request.Host = host;
            ESDialogResult result = await ShowAsync(request, cancellationToken);
            EnsureCompletedByUser(result);
            return result.Accepted;
        }

        public static Task<bool> DangerAsync(
            string dialogId,
            string title,
            string message,
            string confirmText = "确认执行",
            string cancelText = "取消",
            string detail = "",
            ESDialogHost host = ESDialogHost.Auto,
            CancellationToken cancellationToken = default)
            => ConfirmAsync(
                dialogId,
                title,
                message,
                confirmText,
                cancelText,
                detail,
                ESDialogTone.Danger,
                host,
                cancellationToken);

        public static async Task<ESDialogChoice> ChooseAsync(
            string dialogId,
            string title,
            string message,
            string primaryText,
            string secondaryText,
            string cancelText = "取消",
            string detail = "",
            ESDialogTone tone = ESDialogTone.Info,
            ESDialogHost host = ESDialogHost.Auto,
            CancellationToken cancellationToken = default)
        {
            ESDialogRequest request = ESDialogRequest.ConfirmRequest(
                dialogId, title, message, primaryText, cancelText);
            request.SecondaryText = secondaryText ?? string.Empty;
            request.Detail = detail ?? string.Empty;
            request.Tone = tone;
            request.Host = host;
            ESDialogResult result = await ShowAsync(request, cancellationToken);
            EnsureCompletedByUser(result);
            if (result.Completion != ESDialogCompletion.Accepted)
                return ESDialogChoice.Cancelled;
            return string.Equals(result.ActionId, "dialog.secondary", StringComparison.Ordinal)
                ? ESDialogChoice.Secondary
                : ESDialogChoice.Primary;
        }

        internal static bool IsPresenterRegistrationActive(ESDialogHost host, long generation)
        {
            lock (gate)
                return presenters.TryGetValue(host, out Registration registration)
                    && registration.Generation == generation;
        }

        internal static void ReleasePresenter(ESDialogHost host, long generation)
        {
            IESDialogPresenter presenter = null;
            lock (gate)
            {
                if (!presenters.TryGetValue(host, out Registration registration)
                    || registration.Generation != generation)
                    return;
                presenters.Remove(host);
                registration.ReleasePending = true;
                if (registration.ActiveDispatches == 0)
                    presenter = registration.Presenter;
            }
            presenter?.Stop(ESDialogPresenterStopReason.RegistrationReleased);
        }

        private static Registration ResolveRegistration(
            ESDialogHost requestedHost,
            out ESDialogResult failure)
        {
            failure = null;
            if (requestedHost != ESDialogHost.Auto)
            {
                if (presenters.TryGetValue(requestedHost, out Registration registration))
                    return registration;
                failure = new ESDialogResult(
                    ESDialogCompletion.HostUnavailable,
                    requestedHost,
                    error: "No ESDialog presenter is registered for host " + requestedHost + ".");
                return null;
            }

            if (presenters.Count == 1)
            {
                foreach (Registration registration in presenters.Values)
                    return registration;
            }
            if (presenters.Count == 0)
            {
                failure = new ESDialogResult(
                    ESDialogCompletion.HostUnavailable,
                    ESDialogHost.Auto,
                    error: "No ESDialog presenter is registered.");
                return null;
            }
            failure = new ESDialogResult(
                ESDialogCompletion.AmbiguousHost,
                ESDialogHost.Auto,
                error: "Multiple ESDialog presenters are active; select Editor or Runtime explicitly.");
            return null;
        }

        private static void CompleteDispatch(Registration registration)
        {
            IESDialogPresenter presenter = null;
            lock (gate)
            {
                registration.ActiveDispatches--;
                if (registration.ActiveDispatches < 0)
                    throw new InvalidOperationException("ESDialog dispatch count became negative.");
                if (registration.ReleasePending && registration.ActiveDispatches == 0)
                    presenter = registration.Presenter;
            }
            presenter?.Stop(ESDialogPresenterStopReason.RegistrationReleased);
        }

        private static void EnsureCompletedByUser(ESDialogResult result)
        {
            if (result == null)
                throw new InvalidOperationException("ESDialog returned no result.");
            if (result.Completion == ESDialogCompletion.Accepted
                || result.Completion == ESDialogCompletion.Cancelled)
                return;
            throw new InvalidOperationException(
                "ESDialog failed before user completion: " + result.Completion + ". " + result.Error);
        }
    }
}
