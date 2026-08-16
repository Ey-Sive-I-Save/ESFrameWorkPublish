using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESEditorDialogOptions
    {
        public EditorWindow Owner { get; set; }
        public Vector2 PreferredSize { get; set; } = new Vector2(560f, 440f);
    }

    public sealed class ESEditorDialogPresenter : IESDialogPresenter, IESDialogModalPresenter
    {
        public ESDialogHost Host => ESDialogHost.Editor;
        public ESDialogCapabilities Capabilities => ESDialogCapabilities.AllCommon;

        public async Task<ESDialogResult> ShowAsync(
            ESDialogRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ESAdvancedDialogRequest editorRequest = MapRequest(request, null);
            ESAdvancedDialogResult result = await ESDialogService.ShowAsync(
                editorRequest,
                cancellationToken);
            return MapResult(result, request);
        }

        public ESDialogResult ShowModal(ESDialogRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ESAdvancedDialogResult result = ESDialogService.ShowModal(MapRequest(request, null));
            return MapResult(result, request);
        }

        public void Stop(ESDialogPresenterStopReason reason)
        {
            ESDialogService.Shutdown();
        }

        internal static ESAdvancedDialogRequest MapRequest(
            ESDialogRequest request,
            ESEditorDialogOptions options)
        {
            var mapped = new ESAdvancedDialogRequest
            {
                dialogId = request.DialogId,
                title = request.Title,
                subtitle = request.Subtitle,
                message = request.Message,
                detail = request.Detail,
                confirmText = request.ConfirmText,
                cancelText = request.CancelText,
                tone = request.Tone,
                showCancel = request.ShowCancel,
                duplicatePolicy = request.DuplicatePolicy,
                queueBehindActiveDialog = request.QueueBehindActiveDialog,
                initialFocusFieldId = request.InitialFocusFieldId,
                asyncValidationDelayMs = request.AsyncValidationDelayMs,
                owner = options?.Owner,
                preferredSize = options?.PreferredSize ?? new Vector2(560f, 440f),
            };
            for (int i = 0; i < request.Fields.Count; i++)
                mapped.fields.Add(MapField(request.Fields[i]));
            if (!string.IsNullOrWhiteSpace(request.SecondaryText))
            {
                mapped.AddAuxiliaryAction(
                    "dialog.secondary",
                    request.SecondaryText,
                    _ => { },
                    request.SecondaryText,
                    ESAdvancedDialogActionRole.Secondary,
                    true);
            }
            if (request.Validate != null)
            {
                mapped.validateDetailed = values =>
                    MapValidation(request.Validate(MapValues(values, request.Fields)));
            }
            if (request.ValidateAsync != null)
            {
                mapped.validateAsync = async (values, token) =>
                    MapValidation(await request.ValidateAsync(
                        MapValues(values, request.Fields),
                        token));
            }
            return mapped;
        }

        internal static ESDialogResult MapResult(
            ESAdvancedDialogResult result,
            ESDialogRequest request)
        {
            if (result == null)
            {
                return new ESDialogResult(
                    ESDialogCompletion.Failed,
                    ESDialogHost.Editor,
                    error: "Editor dialog returned no result.");
            }
            ESDialogCompletion completion = result.accepted || !string.IsNullOrWhiteSpace(result.actionId)
                ? ESDialogCompletion.Accepted
                : result.cancelled
                    ? ESDialogCompletion.Cancelled
                    : result.exception != null
                        ? ESDialogCompletion.Failed
                        : ESDialogCompletion.Cancelled;
            return new ESDialogResult(
                completion,
                ESDialogHost.Editor,
                MapValues(result.values, request.Fields),
                result.actionId,
                result.exception?.Message);
        }

        private static ESAdvancedDialogField MapField(ESDialogField field)
        {
            ESAdvancedDialogFieldKind kind;
            switch (field.Kind)
            {
                case ESDialogFieldKind.Text:
                    kind = ESAdvancedDialogFieldKind.Text;
                    break;
                case ESDialogFieldKind.MultilineText:
                    kind = ESAdvancedDialogFieldKind.MultilineText;
                    break;
                case ESDialogFieldKind.Toggle:
                    kind = ESAdvancedDialogFieldKind.Toggle;
                    break;
                case ESDialogFieldKind.Choice:
                    kind = ESAdvancedDialogFieldKind.Choice;
                    break;
                case ESDialogFieldKind.MultiChoice:
                    kind = ESAdvancedDialogFieldKind.MultiChoice;
                    break;
                case ESDialogFieldKind.Recommendation:
                    kind = ESAdvancedDialogFieldKind.Recommendation;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field.Kind), field.Kind, null);
            }
            var mapped = new ESAdvancedDialogField(field.Id, field.Label, kind)
            {
                help = field.Help,
                required = field.Required,
                readOnly = field.ReadOnly,
                stringValue = field.StringValue,
                boolValue = field.ToggleValue,
                intValue = field.IntegerValue,
                minIntValue = field.Minimum,
                maxIntValue = field.Maximum,
                lowValueLabel = field.LowLabel,
                highValueLabel = field.HighLabel,
                minimumSelections = field.MinimumSelections,
                maximumSelections = field.MaximumSelections,
            };
            for (int i = 0; i < field.Options.Count; i++)
            {
                mapped.choiceValues.Add(field.Options[i].Id);
                mapped.choices.Add(field.Options[i].Label);
            }
            mapped.selectedChoiceValues.AddRange(field.SelectedOptionIds);
            return mapped;
        }

        private static ESAdvancedDialogValidation MapValidation(ESDialogValidation validation)
            => validation == null || string.IsNullOrWhiteSpace(validation.Message)
                ? null
                : new ESAdvancedDialogValidation(validation.Message, validation.FieldId);

        private static ESDialogValues MapValues(
            ESAdvancedDialogValues values,
            IReadOnlyList<ESDialogField> fields)
        {
            if (values == null)
                return ESDialogValues.Empty;
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            var toggles = new Dictionary<string, bool>(StringComparer.Ordinal);
            var integers = new Dictionary<string, int>(StringComparer.Ordinal);
            var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Count; i++)
            {
                ESDialogField field = fields[i];
                switch (field.Kind)
                {
                    case ESDialogFieldKind.Toggle:
                        toggles.Add(field.Id, values.GetToggle(field.Id));
                        break;
                    case ESDialogFieldKind.Recommendation:
                        integers.Add(field.Id, values.GetRecommendation(field.Id));
                        break;
                    case ESDialogFieldKind.MultiChoice:
                        selections.Add(field.Id, values.GetSelections(field.Id));
                        break;
                    default:
                        strings.Add(field.Id, values.GetString(field.Id));
                        break;
                }
            }
            return new ESDialogValues(strings, toggles, integers, selections);
        }
    }

    public static class ESEditorDialog
    {
        public static async Task<ESDialogResult> ShowAsync(
            ESDialogRequest request,
            ESEditorDialogOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            request = request.CreateSnapshot();
            ESAdvancedDialogResult result = await ESDialogService.ShowAsync(
                ESEditorDialogPresenter.MapRequest(request, options),
                cancellationToken);
            return ESEditorDialogPresenter.MapResult(result, request);
        }

        public static ESAdvancedDialogWindow ShowAdvanced(ESAdvancedDialogRequest request)
            => ESDialogService.Show(request);

        public static ESAdvancedDialogResult ShowAdvancedModal(ESAdvancedDialogRequest request)
            => ESDialogService.ShowModal(request);
    }

    public sealed class ESEditorDialogPresenterInitializer : EditorInvoker_Level0
    {
        private static ESDialogPresenterLease lease;

        public override void InitInvoke()
        {
            if (lease != null && lease.IsActive)
                return;
            ESDialogService.RestartAfterPresenterRegistration();
            ESDialogService.InitializeLifecycle();
            lease = ESDialog.RegisterPresenter(new ESEditorDialogPresenter());
        }
    }
}
