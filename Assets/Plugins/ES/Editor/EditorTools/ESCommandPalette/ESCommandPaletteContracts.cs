using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ES_Design.ConfigKey.Tests")]

namespace ES
{
    public enum ESCommandPaletteActionKind
    {
        OpenWindow,
        OpenMenu,
        OpenFile,
        OpenAsset,
        CopyText,
        CopyPath,
        Select
    }

    public enum ESCommandPaletteRegistrationCode
    {
        Accepted,
        NullProvider,
        EmptyProviderId,
        DuplicateProviderId,
        EmptyProviderPrefix,
        ProviderBuildFailed,
        NullItem,
        EmptyItemId,
        EmptyTitle,
        EmptyCategory,
        EmptyTargetId,
        DuplicateItemId,
        PrefixMismatch,
        CategoryMismatch,
        MutatingItemRejected,
        ConfirmationItemRejected,
        UnsupportedAction,
        MenuNotWhitelisted,
        WindowNotRegistered,
        FileNotAllowed,
        SceneNotRegistered
    }

    public sealed class ESCommandPaletteItem
    {
        public ESCommandPaletteItem(
            string itemId,
            string title,
            string description,
            string category,
            string keywords,
            string prefix,
            string targetId,
            ESCommandPaletteActionKind actionKind,
            bool isMutating = false,
            bool requiresConfirmation = false)
        {
            ItemId = itemId;
            Title = title;
            Description = description ?? string.Empty;
            Category = category;
            Keywords = keywords ?? string.Empty;
            Prefix = prefix;
            TargetId = targetId;
            ActionKind = actionKind;
            IsMutating = isMutating;
            RequiresConfirmation = requiresConfirmation;
            SearchText = string.Concat(Title, " ", Category, " ", Keywords, " ", Description);
        }

        public string ItemId { get; }
        public string Title { get; }
        public string Description { get; }
        public string Category { get; }
        public string Keywords { get; }
        public string Prefix { get; }
        public string TargetId { get; }
        public ESCommandPaletteActionKind ActionKind { get; }
        public bool IsMutating { get; }
        public bool RequiresConfirmation { get; }
        public string SearchText { get; }
        private string providerId;
        private string stableId;

        internal string ProviderId
        {
            get => providerId;
            set
            {
                providerId = value;
                stableId = string.Concat(providerId ?? string.Empty, ":", ItemId ?? string.Empty);
            }
        }

        public string StableId => stableId ?? string.Concat(string.Empty, ":", ItemId ?? string.Empty);
    }

    public interface IESCommandPaletteProvider
    {
        string ProviderId { get; }
        string DisplayName { get; }
        string Prefix { get; }
        IReadOnlyList<ESCommandPaletteItem> BuildItems();
    }

    public sealed class ESCommandPaletteRegistrationDiagnostic
    {
        public ESCommandPaletteRegistrationDiagnostic(
            ESCommandPaletteRegistrationCode code,
            string providerId,
            string itemId,
            string message)
        {
            Code = code;
            ProviderId = providerId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ESCommandPaletteRegistrationCode Code { get; }
        public string ProviderId { get; }
        public string ItemId { get; }
        public string Message { get; }
        public bool IsAccepted => Code == ESCommandPaletteRegistrationCode.Accepted;
    }

    public sealed class ESCommandPaletteRegistrationResult
    {
        private readonly List<ESCommandPaletteRegistrationDiagnostic> diagnostics =
            new List<ESCommandPaletteRegistrationDiagnostic>();

        public bool ProviderAccepted { get; internal set; }
        public int AcceptedItemCount { get; internal set; }
        public IReadOnlyList<ESCommandPaletteRegistrationDiagnostic> Diagnostics => diagnostics;

        internal void Add(ESCommandPaletteRegistrationDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
        }
    }

    public sealed class ESCommandPaletteResult
    {
        private ESCommandPaletteResult(bool success, string message, string recoveryAction)
        {
            Success = success;
            Message = message ?? string.Empty;
            RecoveryAction = recoveryAction ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
        public string RecoveryAction { get; }

        public static ESCommandPaletteResult Ok(string message)
        {
            return new ESCommandPaletteResult(true, message, string.Empty);
        }

        public static ESCommandPaletteResult Fail(string message, string recoveryAction = null)
        {
            return new ESCommandPaletteResult(false, message, recoveryAction);
        }
    }

    public readonly struct ESCommandPaletteSearchMetrics
    {
        public ESCommandPaletteSearchMetrics(
            double durationMilliseconds,
            long allocatedBytes,
            int candidateCount,
            int resultCount,
            long allocationBudgetBytes)
        {
            DurationMilliseconds = durationMilliseconds;
            AllocatedBytes = allocatedBytes;
            CandidateCount = candidateCount;
            ResultCount = resultCount;
            AllocationBudgetBytes = allocationBudgetBytes;
        }

        public double DurationMilliseconds { get; }
        public long AllocatedBytes { get; }
        public int CandidateCount { get; }
        public int ResultCount { get; }
        public long AllocationBudgetBytes { get; }
        public bool IsWithinAllocationBudget => AllocatedBytes < 0 || AllocatedBytes <= AllocationBudgetBytes;
    }
}
