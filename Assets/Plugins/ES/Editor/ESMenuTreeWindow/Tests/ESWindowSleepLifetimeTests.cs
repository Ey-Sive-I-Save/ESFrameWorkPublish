using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ES.EditorInternal;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ESWindowRootAlias = UnityEditor.EditorWindow;

namespace ES.Tests
{
    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test full lifecycle probe")]
    public sealed class ESWindowSleepLifetimeProbeWindow : EditorWindow,
        IESWindowMultiInstanceContract
    {
        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => nameof(ESWindowSleepLifetimeTests);
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test full contract")]
    public sealed class ESWindowFullContractProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Utility,
        "test transient contract")]
    public sealed class ESWindowTransientContractProbeWindow : EditorWindow
    {
    }

    public sealed class ESWindowUnmarkedContractProbeWindow : EditorWindow
    {
    }

    public abstract class ESWindowInheritedDiscoveryProbeBase : ESWindowRootAlias
    {
    }

    public sealed class ESWindowInheritedDiscoveryProbeWindow
        : ESWindowInheritedDiscoveryProbeBase
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test coordinator probe")]
    public sealed class ESWindowCoordinatorIdentityProbeWindow : EditorWindow,
        IESWindowMultiInstanceContract
    {
        public string CoordinatorId;

        string IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId
            => CoordinatorId;
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test duplicate probe")]
    public sealed class ESWindowDuplicateHealthProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test short-title probe")]
    public sealed class ESWindowShortTitleProbeWindow : EditorWindow,
        IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "契约";
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test attribute-title probe")]
    [ESWindowPresentationShortTitle("标记")]
    public sealed class ESWindowAttributeShortTitleProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Workspace,
        "test tab-label probe")]
    public sealed class ESWindowTabLabelProbeWindow : EditorWindow,
        IESWindowPresentationTabLabel,
        IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationTabLabel => "世界";
        public string ESWindow_PresentationShortTitle => "旧标题";
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Dialog,
        "test unauthorized dialog surface")]
    public sealed class ESConfirmPromptWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Dialog,
        "test unauthorized dialog surface without dialog suffix")]
    public sealed class ESQuestionSheet : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Popup,
        "test invalid mode-kind pair")]
    public sealed class ESWindowInvalidSurfaceModeProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Transient,
        ESWindowSurfaceKind.Preview,
        "test inverse invalid mode-kind pair")]
    public sealed class ESWindowInvalidInverseSurfaceModeProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        ESWindowSurfaceKind.Unknown,
        "test unknown surface kind")]
    public sealed class ESWindowUnknownSurfaceKindProbeWindow : EditorWindow
    {
    }

    [ESWindowSleepContract(
        ESWindowSleepMode.Full,
        (ESWindowSurfaceKind)byte.MaxValue,
        "test out-of-range surface kind")]
    public sealed class ESWindowOutOfRangeSurfaceKindProbeWindow : EditorWindow
    {
    }

    internal sealed class ESThrowingScheduledItem : IVisualElementScheduledItem
    {
        public VisualElement element => null;
        public bool isActive => true;

        public void Resume() { }
        public void Pause() => throw new InvalidOperationException("ES teardown schedule failure");
        public void ExecuteLater(long delayMs) { }
        public IVisualElementScheduledItem StartingIn(long delayMs) => this;
        public IVisualElementScheduledItem Every(long intervalMs) => this;
        public IVisualElementScheduledItem Every(Func<long> intervalMs) => this;
        public IVisualElementScheduledItem Until(Func<bool> stopCondition) => this;
        public IVisualElementScheduledItem ForDuration(long durationMs) => this;
    }

    public sealed class ESEditorSectionIdentityProbeAsset : ScriptableObject
    {
        [ESEditorBeginSection("tests.identity", "summary", "摘要", 0f)]
        public int summary;

        [ESEditorBeginSection("tests.identity", "advanced", "高级", 10f)]
        public int advanced;
    }

    [Serializable]
    public sealed class ESEditorSectionIdentityManagedProbe
    {
        public int value;
    }

    public sealed class ESWindowSleepLifetimeTests
    {
        private const string WindowSourceTypePattern =
            @"(?:global\s*::\s*)?@?[A-Za-z_]\w*(?:\s*\.\s*@?[A-Za-z_]\w*)*";

        private static readonly Regex LegacyAdvancedDialogMemberReferencePattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*\.\s*Show(?:Modal|Async)?\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex LegacyAdvancedDialogAliasPattern = new Regex(
            @"\busing\s+[A-Za-z_]\w*\s*=\s*(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*;",
            RegexOptions.CultureInvariant);

        private static readonly Regex LegacyAdvancedDialogStaticImportPattern = new Regex(
            @"\busing\s+static\s+(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*;",
            RegexOptions.CultureInvariant);

        private static readonly Regex AdvancedDialogCreateCallPattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*\.\s*Create\s*\(",
            RegexOptions.CultureInvariant);

        private static readonly Regex AdvancedDialogCreateMethodGroupPattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*\.\s*Create\b(?!\s*\()",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeDialogCallPattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:(?:global\s*::\s*)?UnityEditor\s*\.\s*)?EditorUtility\s*\.\s*DisplayDialog(?:Complex)?\s*\(",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeDialogMethodGroupPattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:(?:global\s*::\s*)?UnityEditor\s*\.\s*)?EditorUtility\s*\.\s*DisplayDialog(?:Complex)?\b(?!\s*\()",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeEditorWindowModalReferencePattern = new Regex(
            @"(?<![A-Za-z0-9_])@?ShowModalUtility\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeEditorWindowModalCallPattern = new Regex(
            @"(?<![A-Za-z0-9_])@?ShowModalUtility\s*\(",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeEditorWindowModalReflectionPattern = new Regex(
            @"\b(?:GetMethod|GetRuntimeMethod|GetMember)\s*\(\s*@?""ShowModalUtility""",
            RegexOptions.CultureInvariant);

        private static readonly Regex AdvancedDialogServiceModalEntryReferencePattern = new Regex(
            @"(?<![A-Za-z0-9_])@?Internal_OpenFromDialogService\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex AdvancedDialogServiceModalEntryCallPattern = new Regex(
            @"\.\s*@?Internal_OpenFromDialogService\s*\(",
            RegexOptions.CultureInvariant);

        private static readonly Regex WindowFactoryTokenPattern = new Regex(
            @"(?<![A-Za-z0-9_])(?:CreateInstance|CreateWindow|GetWindow)\b|"
            + @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?(?:[A-Za-z_]\w*\s*\.\s*)*ESAdvancedDialogWindow\s*\.\s*Create\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex GenericDirectWindowCreationPattern = new Regex(
            @"(?:CreateInstance|CreateWindow)\s*<\s*(?<type>"
            + WindowSourceTypePattern + @")\s*>",
            RegexOptions.CultureInvariant);

        private static readonly Regex DynamicGetWindowPattern = new Regex(
            @"(?<receiver>" + WindowSourceTypePattern + @")\s*\.\s*GetWindow\s*\(",
            RegexOptions.CultureInvariant);

        private static readonly Regex GenericWindowConstraintPattern = new Regex(
            @"\bwhere\s+(?<parameter>@?[A-Za-z_]\w*)\s*:\s*(?<constraints>[^\r\n\{;]+)",
            RegexOptions.CultureInvariant);

        private static readonly Regex WindowSourceTypeTokenPattern = new Regex(
            WindowSourceTypePattern,
            RegexOptions.CultureInvariant);

        private static readonly Regex DirectTypeofWindowCreationPattern = new Regex(
            @"(?:CreateInstance|CreateWindow|Activator\s*\.\s*CreateInstance)\s*\(\s*typeof\s*\(\s*(?<type>"
            + WindowSourceTypePattern + @")\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex DynamicCreateAssignmentPattern = new Regex(
            @"(?m)^\s*(?<declared>" + WindowSourceTypePattern
            + @")\s+@?[A-Za-z_]\w*\s*=\s*(?:" + WindowSourceTypePattern
            + @"\s*\.\s*)?CreateInstance\s*\(\s*@?[A-Za-z_]\w*\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex InlineAsWindowCastPattern = new Regex(
            @"(?:" + WindowSourceTypePattern
            + @"\s*\.\s*)?CreateInstance\s*\(\s*@?[A-Za-z_]\w*\s*\)\s+as\s+(?<cast>"
            + WindowSourceTypePattern + @")",
            RegexOptions.CultureInvariant);

        private static readonly Regex InlineExplicitWindowCastPattern = new Regex(
            @"\(\s*(?<cast>" + WindowSourceTypePattern + @")\s*\)\s*(?:"
            + WindowSourceTypePattern
            + @"\s*\.\s*)?CreateInstance\s*\(\s*@?[A-Za-z_]\w*\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex[] InlineWindowCastPatterns =
        {
            InlineAsWindowCastPattern,
            InlineExplicitWindowCastPattern,
        };

        private static readonly Regex RuntimeCreateWindowPattern = new Regex(
            @"(?<receiver>" + WindowSourceTypePattern
            + @")\s*\.\s*CreateWindow\s*\(\s*(?!typeof\s*\()",
            RegexOptions.CultureInvariant);

        private static readonly Regex RuntimeTypeValuePattern = new Regex(
            @"\b(?:(?:var|(?:System\s*\.\s*)?Type)\s+)?(?<variable>@?[A-Za-z_]\w*)\s*=\s*typeof\s*\(\s*(?<type>"
            + WindowSourceTypePattern + @")\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex RuntimeTypeCreationPattern = new Regex(
            @"(?:CreateInstance|CreateWindow|Activator\s*\.\s*CreateInstance)\s*\(\s*(?<variable>@?[A-Za-z_]\w*)\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex DynamicCreateResultPattern = new Regex(
            @"\b(?:(?<declared>" + WindowSourceTypePattern
            + @")\s+)?(?<result>@?[A-Za-z_]\w*)\s*=\s*(?:" + WindowSourceTypePattern
            + @"\s*\.\s*)?CreateInstance\s*\(\s*(?<argument>@?[A-Za-z_]\w*)\s*\)",
            RegexOptions.CultureInvariant);

        private static readonly Regex RuntimeResultCastPattern = new Regex(
            @"(?:\(\s*(?<cast>" + WindowSourceTypePattern
            + @")\s*\)\s*(?<result>@?[A-Za-z_]\w*)\b|\b(?<result>@?[A-Za-z_]\w*)\s+(?:as|is)\s+(?<cast>"
            + WindowSourceTypePattern + @"))",
            RegexOptions.CultureInvariant);

        private static readonly Regex SourceTypeAliasPattern = new Regex(
            @"\busing\s+(?<alias>@?[A-Za-z_]\w*)\s*=\s*(?<target>"
            + WindowSourceTypePattern + @")\s*;",
            RegexOptions.CultureInvariant);

        private const string InternalLifecycleMemberPattern =
            "BindWindow|UnbindWindow|SetWindowSleepOwner|RegisterPendingSleepOwner|"
            + "ResolvePendingSleepOwners|ClearWindowSleepOwner|ClearPendingSleepOwner|"
            + "ClearPendingSleepOwners";

        private const string LifecycleQualifiedSourceNamePattern =
            @"(?:global\s*::\s*)?@?[A-Za-z_]\w*"
            + @"(?:(?:\s*::\s*|\s*\.\s*)@?[A-Za-z_]\w*)*";

        private static readonly Regex InternalLifecycleMemberReferencePattern = new Regex(
            @"(?<![A-Za-z0-9_])(?<receiver>"
            + LifecycleQualifiedSourceNamePattern
            + @")\s*\.\s*(?<member>"
            + InternalLifecycleMemberPattern
            + @")\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex StaticSourceTypeImportPattern = new Regex(
            @"\busing\s+static\s+(?<target>"
            + LifecycleQualifiedSourceNamePattern
            + @")\s*;",
            RegexOptions.CultureInvariant);

        private static readonly Regex NativeDialogMemberDeclarationPattern = new Regex(
            @"(?s)(?<name>[A-Za-z_]\w*)\s*(?:<[^>{};()]+>)?\s*\([^{};]*\)\s*(?:where\s+[^{};]+)?$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> NativeDialogControlKeywords =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "if", "for", "foreach", "while", "switch", "catch", "lock", "using", "fixed"
            };

        [Test]
        public void DeclaredSleepContractRejectsMismatchedBindingMode()
        {
            ESWindowFullContractProbeWindow full =
                ScriptableObject.CreateInstance<ESWindowFullContractProbeWindow>();
            ESWindowTransientContractProbeWindow transient =
                ScriptableObject.CreateInstance<ESWindowTransientContractProbeWindow>();
            try
            {
                Assert.AreEqual(
                    ESWindowSleepMode.Full,
                    ESWindowFoundation.GetDeclaredSleepMode(full));
                Assert.AreEqual(
                    ESWindowSleepMode.Transient,
                    ESWindowFoundation.GetDeclaredSleepMode(transient));
                Assert.AreEqual(
                    ESWindowSurfaceKind.Workspace,
                    ESWindowFoundation.GetDeclaredSurfaceKind(full));
                Assert.AreEqual(
                    ESWindowSurfaceKind.Utility,
                    ESWindowFoundation.GetDeclaredSurfaceKind(transient));
                Assert.DoesNotThrow(() => ESWindowFoundation.BindFullSleep(full));
                Assert.DoesNotThrow(() => ESWindowFoundation.BindTransient(transient));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(transient));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.Bind(full, allowSemiSleep: false));

            }
            finally
            {
                ESWindowFoundation.Close(transient);
                ESWindowFoundation.Close(full);
                UnityEngine.Object.DestroyImmediate(transient);
                UnityEngine.Object.DestroyImmediate(full);
            }
        }

        [Test]
        public void DirectProductionEditorWindowsDeclareExplicitLifecycleContracts()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string[] sourceFiles = EnumerateESProductionEditorSources(projectRoot);
            int directWindowFileCount = 0;
            foreach (string path in sourceFiles)
            {
                string normalized = NormalizeProjectPath(path);
                if ((!normalized.Contains("/Plugins/ES/Editor/", StringComparison.OrdinalIgnoreCase)
                        && !normalized.Contains("/Scripts/ESLogic/Editor/", StringComparison.OrdinalIgnoreCase))
                    || normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/-Templates/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (!Regex.IsMatch(
                        source,
                        @"class\s+[^\r\n]*:\s*[^\r\n]*\b(?:EditorWindow|OdinEditorWindow)\b",
                        RegexOptions.CultureInvariant))
                    continue;

                directWindowFileCount++;
                bool hasContract = source.Contains(
                    "ESWindowSleepContract(",
                    StringComparison.Ordinal);
                bool hasSurfaceKind = source.Contains(
                    "ESWindowSurfaceKind.",
                    StringComparison.Ordinal);
                bool hasBinding = source.Contains(
                        "ESWindowFoundation.BindFullSleep(",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.BindTransient(",
                        StringComparison.Ordinal)
                    || source.Contains(
                        "ESWindowFoundation.BindWithStandardSystemHost(",
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    hasContract,
                    "直接生产窗口必须显式声明 ESWindowSleepContract：" + normalized);
                Assert.IsTrue(
                    hasSurfaceKind,
                    "直接生产窗口必须显式声明 ESWindowSurfaceKind：" + normalized);
                Assert.IsTrue(
                    hasBinding,
                    "直接生产窗口必须显式声明 ES 生命周期绑定：" + normalized);

                bool bindsTransient = source.Contains(
                    "ESWindowFoundation.BindTransient(",
                    StringComparison.Ordinal);
                if (bindsTransient)
                {
                    Assert.IsTrue(
                        hasContract,
                        "关闭休眠的直接窗口必须显式声明 Transient 及原因：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepMode.Transient",
                        source,
                        "Transient 窗口合同模式无法解析：" + normalized);
                }
                else if (hasContract)
                {
                    Assert.IsFalse(
                        source.Contains(
                            "ESWindowSleepMode.Transient",
                            StringComparison.Ordinal),
                        "未关闭休眠的窗口不应声明 Transient：" + normalized);
                }
            }

            Assert.GreaterOrEqual(
                directWindowFileCount,
                12,
                "直接 EditorWindow 扫描结果异常，可能漏掉了生产窗口。");
        }

        [Test]
        public void UnmarkedWindowCannotEnterESFoundation()
        {
            ESWindowUnmarkedContractProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowUnmarkedContractProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);
            int rootChildCount = window.rootVisualElement.childCount;
            int actionBarChildCount = actionBar.childCount;
            try
            {
                Assert.IsNull(ESWindowFoundation.GetDeclaredSleepMode(window));
                Assert.IsNull(ESWindowFoundation.GetDeclaredSurfaceKind(window));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.BindFullSleep(window));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.BindTransient(window));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.BindWithStandardSystemHost(window, actionBar));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.EnsureStandardSystemActionBar(window));
                Assert.IsFalse(ESWindowFoundation.IsBound(window));
                Assert.AreEqual(rootChildCount, window.rootVisualElement.childCount);
                Assert.AreEqual(actionBarChildCount, actionBar.childCount);
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DialogSurfaceContractRejectsNonServiceWindowsRegardlessOfTypeName()
        {
            EditorWindow[] windows =
            {
                ScriptableObject.CreateInstance<ESConfirmPromptWindow>(),
                ScriptableObject.CreateInstance<ESQuestionSheet>(),
            };
            try
            {
                foreach (EditorWindow window in windows)
                {
                    Assert.AreEqual(
                        ESWindowSurfaceKind.Dialog,
                        ESWindowFoundation.GetDeclaredSurfaceKind(window));
                    int rootChildCount = window.rootVisualElement.childCount;
                    Assert.Throws<InvalidOperationException>(
                        () => ESWindowFoundation.EnsureStandardSystemActionBar(window));
                    Assert.AreEqual(rootChildCount, window.rootVisualElement.childCount);
                    Assert.Throws<InvalidOperationException>(() =>
                        ESWindowFoundation.RegisterPendingSleepOwner(
                            window,
                            "ES.Tests.InvalidDialogOwner"));
                    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                        () => ESWindowFoundation.BindTransient(window));
                    StringAssert.Contains("ESAdvancedDialogWindow", exception.Message);
                    Assert.IsFalse(ESWindowFoundation.IsBound(window));
                }
            }
            finally
            {
                foreach (EditorWindow window in windows)
                {
                    ESWindowFoundation.Close(window);
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [Test]
        public void SurfaceContractRejectsUnknownAndModeKindMismatchBeforeBinding()
        {
            EditorWindow[] windows =
            {
                ScriptableObject.CreateInstance<ESWindowInvalidSurfaceModeProbeWindow>(),
                ScriptableObject.CreateInstance<ESWindowInvalidInverseSurfaceModeProbeWindow>(),
                ScriptableObject.CreateInstance<ESWindowUnknownSurfaceKindProbeWindow>(),
                ScriptableObject.CreateInstance<ESWindowOutOfRangeSurfaceKindProbeWindow>(),
            };
            ESWindowFullContractProbeWindow validChild =
                ScriptableObject.CreateInstance<ESWindowFullContractProbeWindow>();
            try
            {
                foreach (EditorWindow window in windows)
                {
                    int rootChildCount = window.rootVisualElement.childCount;
                    Assert.Throws<InvalidOperationException>(
                        () => ESWindowFoundation.EnsureStandardSystemActionBar(window));
                    Assert.AreEqual(rootChildCount, window.rootVisualElement.childCount);
                    Assert.Throws<InvalidOperationException>(() =>
                        ESWindowFoundation.RegisterPendingSleepOwner(
                            window,
                            "ES.Tests.InvalidSurfaceOwner"));
                    Assert.Throws<InvalidOperationException>(() =>
                        ESWindowFoundation.SetSleepOwner(
                            window,
                            null,
                            ESWindowSleepLinkMode.Independent));
                    Assert.Throws<InvalidOperationException>(() =>
                        ESWindowFoundation.SetSleepOwner(
                            validChild,
                            window,
                            ESWindowSleepLinkMode.FollowOwner));
                    Assert.Throws<InvalidOperationException>(() =>
                        ESWindowFoundation.ResolvePendingSleepOwners(
                            "ES.Tests.InvalidSurfaceOwner",
                            window));
                    Assert.Throws<InvalidOperationException>(
                        () => ESWindowFoundation.BindFullSleep(window));
                    Assert.IsFalse(ESWindowFoundation.IsBound(window));
                }
            }
            finally
            {
                foreach (EditorWindow window in windows)
                {
                    ESWindowFoundation.Close(window);
                    UnityEngine.Object.DestroyImmediate(window);
                }
                ESWindowFoundation.Close(validChild);
                UnityEngine.Object.DestroyImmediate(validChild);
            }
        }

        [Test]
        public void FullLifecycleCapabilitiesRejectTransientSurfacesWithoutMutation()
        {
            const string ownerKey = "ES.Tests.TransientCapabilityOwner";
            ESWindowTransientContractProbeWindow transient =
                ScriptableObject.CreateInstance<ESWindowTransientContractProbeWindow>();
            ESWindowFullContractProbeWindow owner =
                ScriptableObject.CreateInstance<ESWindowFullContractProbeWindow>();
            var actionBar = new VisualElement();
            transient.rootVisualElement.Add(actionBar);
            int rootChildCount = transient.rootVisualElement.childCount;
            int actionBarChildCount = actionBar.childCount;

            try
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.EnsureStandardSystemActionBar(transient));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.BindWithStandardSystemHost(
                        transient,
                        actionBar,
                        allowSemiSleep: false));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.SetSleepOwner(
                        transient,
                        owner,
                        ESWindowSleepLinkMode.FollowOwner));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.SetSleepOwner(
                        transient,
                        owner,
                        ESWindowSleepLinkMode.OwnedSurface));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.RegisterPendingSleepOwner(transient, ownerKey));
                Assert.Throws<InvalidOperationException>(() =>
                    ESEditorPresentation.SetWindowSleepOwner(
                        transient,
                        owner,
                        ESWindowSleepLinkMode.FollowOwner));
                Assert.Throws<InvalidOperationException>(() =>
                    ESEditorPresentation.RegisterPendingSleepOwner(
                        transient,
                        ownerKey,
                        ESWindowSleepLinkMode.FollowOwner));
                Assert.Throws<InvalidOperationException>(() =>
                    ESEditorPresentation.ResolvePendingSleepOwners(ownerKey, transient));

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    transient,
                    null,
                    ESWindowSleepLinkMode.Independent));
                Assert.IsTrue(ESEditorPresentation.SetWindowSleepOwner(
                    transient,
                    null,
                    ESWindowSleepLinkMode.Independent));
                Assert.DoesNotThrow(() => ESWindowFoundation.ClearSleepOwner(transient));
                Assert.DoesNotThrow(() => ESWindowFoundation.ClearPendingSleepOwner(transient));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(transient));
                Assert.IsFalse(ESWindowFoundation.IsBound(transient));
                Assert.IsFalse(PendingOwnerKeyExists(ownerKey));
                Assert.AreEqual(rootChildCount, transient.rootVisualElement.childCount);
                Assert.AreEqual(actionBarChildCount, actionBar.childCount);
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                ESWindowFoundation.Close(transient);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(transient);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EdgeTabShortTitleSupportsSemanticContractAndPersistentOverride()
        {
            Assert.AreEqual(
                "世界构建",
                ESEditorPresentation.BuildDefaultPresentationShortTitle(
                    "环境/世界构建工作台"));

            ESWindowShortTitleProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowShortTitleProbeWindow>();
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);

            try
            {
                ESWindowFoundation.Bind(window);
                Assert.IsTrue(
                    ESWindowFoundation.IsWindowSleepSupported(window),
                    "首次绑定必须立即建立休眠能力，不能依赖窗口进行第二次绑定。");
                Assert.AreEqual("契约", ESWindowFoundation.GetPresentationShortTitle(window));

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, "世界"));
                Assert.AreEqual("世界", ESWindowFoundation.GetPresentationShortTitle(window));
                StringAssert.Contains(
                    "\"presentationShortTitle\":\"世界\"",
                    EditorPrefs.GetString(preferenceKey));

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(
                    window,
                    "这是一个远超页签宽度的短标题"));
                Assert.AreEqual(
                    "这是一个远超页签",
                    ESWindowFoundation.GetPresentationShortTitle(window),
                    "用户覆盖仍须保持极限页签的紧凑边界。");

                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, null));
                Assert.AreEqual("契约", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TabLabelContractHasPriorityWithoutChangingWindowIdentity()
        {
            ESWindowTabLabelProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowTabLabelProbeWindow>();
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);
            try
            {
                ESWindowFoundation.Bind(window);
                Assert.AreEqual("世界", ESWindowFoundation.GetPresentationShortTitle(window));
                Assert.AreEqual(typeof(ESWindowTabLabelProbeWindow), window.GetType());
                Assert.IsTrue(ESWindowFoundation.TrySetPresentationShortTitle(window, "自定义"));
                Assert.AreEqual("自定义", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DefaultShortTitlePrefersSemanticDomainWords()
        {
            Assert.AreEqual(
                "资源",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES 资源诊断"));
            Assert.AreEqual(
                "Agent",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Codex 会话控制台"));
            Assert.AreEqual(
                "世界构建",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("环境/世界构建工作台"));
            Assert.AreEqual(
                "材质",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("Composite Material Bake"));
            Assert.AreEqual(
                "诊断",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Runtime Diagnostic Monitor"));
            Assert.AreEqual(
                "层级",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("Hierarchy Object Tools"));
            Assert.AreEqual(
                "运行时",
                ESEditorPresentation.BuildDefaultPresentationShortTitle("ES Runtime Watch"));
        }

        [Test]
        public void AttributeShortTitleIsUsedWhenWindowDoesNotImplementInterface()
        {
            ESWindowAttributeShortTitleProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowAttributeShortTitleProbeWindow>();
            try
            {
                ESWindowFoundation.Bind(window);
                Assert.AreEqual("标记", ESWindowFoundation.GetPresentationShortTitle(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UngovernedSecondWindowInstanceCannotJoinSleepPersistence()
        {
            ESWindowDuplicateHealthProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            ESWindowDuplicateHealthProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            try
            {
                ESWindowFoundation.Bind(first);
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("同一 EditorWindow 具体类型出现多个实例：.*额外实例"));
                ESWindowFoundation.Bind(second);

                bool firstViolation = ESWindowFoundation.IsWindowSingleInstanceViolation(first);
                bool secondViolation = ESWindowFoundation.IsWindowSingleInstanceViolation(second);
                Assert.AreNotEqual(firstViolation, secondViolation,
                    "同一无协调契约类型只能有一个实例拥有休眠与持久化所有权。");

                ESWindowDuplicateHealthProbeWindow primary = firstViolation ? second : first;
                ESWindowDuplicateHealthProbeWindow duplicate = firstViolation ? first : second;
                Assert.IsFalse(ESWindowFoundation.CanWindowSleep(duplicate));
                StringAssert.Contains(
                    "额外实例",
                    ESWindowFoundation.GetWindowSleepBlockReason(duplicate));

                ESWindowFoundation.Close(primary);
                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(duplicate),
                    "首实例退出后，剩余实例应确定性接管唯一实例所有权。");
            }
            finally
            {
                ESWindowFoundation.Close(second);
                ESWindowFoundation.Close(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void GovernedMultiInstanceRequiresOneStableCoordinatorIdentity()
        {
            ESWindowCoordinatorIdentityProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowCoordinatorIdentityProbeWindow>();
            ESWindowCoordinatorIdentityProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowCoordinatorIdentityProbeWindow>();
            try
            {
                first.CoordinatorId = " ES.Tests.Coordinator ";
                second.CoordinatorId = "ES.Tests.Coordinator";
                ESWindowFoundation.Bind(first);
                ESWindowFoundation.Bind(second);

                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(first));
                Assert.IsFalse(ESWindowFoundation.IsWindowSingleInstanceViolation(second));

                ESWindowFoundation.Close(second);
                second.CoordinatorId = "ES.Tests.OtherCoordinator";
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("同一 EditorWindow 具体类型出现多个实例：.*额外实例"));
                ESWindowFoundation.Bind(second);

                Assert.AreNotEqual(
                    ESWindowFoundation.IsWindowSingleInstanceViolation(first),
                    ESWindowFoundation.IsWindowSingleInstanceViolation(second),
                    "同类型实例的协调器 ID 不一致时，只能保留一个单实例所有者。");
            }
            finally
            {
                ESWindowFoundation.Close(first);
                ESWindowFoundation.Close(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void StandardSystemHostBindingIsExplicitAndIdempotentWhileDetached()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);

            try
            {
                ESWindowActionHosts first = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    actionBar);
                ESWindowActionHosts second = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    actionBar);

                Assert.AreSame(first.System, second.System);
                Assert.AreSame(actionBar, first.System.parent);
                Assert.IsNull(
                    first.System.Q<VisualElement>("ESWindowSystemActions"),
                    "detached root 只能建立逻辑绑定，panel attach 前不得创建系统控件。");
                Assert.IsNull(window.rootVisualElement.Q<VisualElement>(
                    "ESWindowSystemActionsFallback"));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void StandardSystemActionBarFactoryUsesNormalFlowAndExplicitOwnership()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                VisualElement bar = ESWindowFoundation.EnsureStandardSystemActionBar(window);
                Assert.AreSame(window.rootVisualElement, bar.parent);
                Assert.AreEqual(Position.Relative, bar.style.position.value);
                Assert.AreEqual(0f, bar.style.top.value.value, 0.001f);
                ESWindowActionHosts hosts = ESWindowFoundation.BindWithStandardSystemHost(
                    window,
                    bar);
                Assert.AreSame(bar, hosts.System.parent);
                Assert.IsNotNull(bar.Q<VisualElement>("ESWindowStandardSystemActionHost"));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RejectedActionHostsDoNotMutateTreeBindingOrLifecycleHookState()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            var localSystemHost = new VisualElement();
            var foreignRoot = new VisualElement();
            var foreignGlobalHost = new VisualElement();
            window.rootVisualElement.Add(actionBar);
            window.rootVisualElement.Add(localSystemHost);
            foreignRoot.Add(foreignGlobalHost);
            int rootChildCount = window.rootVisualElement.childCount;
            int actionBarChildCount = actionBar.childCount;
            FieldInfo hooksField = typeof(ESEditorPresentation).GetField(
                "windowLifecycleHooksInstalled",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(hooksField);
            bool previousHooksInstalled = (bool)hooksField.GetValue(null);

            try
            {
                hooksField.SetValue(null, false);
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.BindWithStandardSystemHost(
                        window,
                        actionBar,
                        foreignGlobalHost));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.BindFullSleep(
                        window,
                        new ESWindowActionHosts(localSystemHost, foreignGlobalHost)));

                Assert.IsFalse((bool)hooksField.GetValue(null));
                Assert.IsFalse(ESWindowFoundation.IsBound(window));
                Assert.AreEqual(rootChildCount, window.rootVisualElement.childCount);
                Assert.AreEqual(actionBarChildCount, actionBar.childCount);
                Assert.IsNull(actionBar.Q<VisualElement>(
                    ESWindowFoundation.StandardSystemActionHostName));
            }
            finally
            {
                hooksField.SetValue(null, previousHooksInstalled);
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DirectESBindingDefersVisualsUntilPanelAttach()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNull(window.rootVisualElement.panel);
            int initialChildCount = window.rootVisualElement.childCount;
            try
            {
                ESWindowFoundation.Bind(window);

                Assert.IsTrue(ESWindowFoundation.IsBound(window));
                Assert.AreEqual(initialChildCount, window.rootVisualElement.childCount);
                IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
                object binding = bindings[window.GetInstanceID()];
                Assert.IsNotNull(binding);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "lifecycleSuspended"));
                Assert.AreSame(
                    window.rootVisualElement,
                    GetFieldValue<VisualElement>(bindingType, binding, "pendingPanelRoot"));
                AssertFieldIsNull(bindingType, binding, "root");
                AssertFieldIsNull(bindingType, binding, "host");
                AssertFieldIsNull(bindingType, binding, "semiSleepOverlay");

                ESWindowFoundation.Close(window);
                Assert.IsFalse(ESWindowFoundation.IsBound(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TransientContractCannotBePromotedAtRuntime()
        {
            ESWindowTransientContractProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowTransientContractProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);

            try
            {
                ESWindowFoundation.BindTransient(window);
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsFalse(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.Throws<InvalidOperationException>(() =>
                    ESWindowFoundation.BindWithStandardSystemHost(
                        window,
                        actionBar,
                        allowSemiSleep: true));
                Assert.IsFalse(ESWindowFoundation.IsWindowSleepSupported(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PublicSleepControlsAreSymmetricAndExplainUnavailableState()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var actionBar = new VisualElement();
            window.rootVisualElement.Add(actionBar);
            string preferenceKey = InvokePrivate<string>(
                typeof(ESEditorPresentation),
                "GetSemiSleepPreferenceKey",
                window);
            EditorPrefs.DeleteKey(preferenceKey);

            try
            {
                Assert.IsFalse(ESWindowFoundation.TrySetWindowSleepAllowed(window, false));
                Assert.AreEqual(
                    "窗口尚未接入 ES Presentation。",
                    ESWindowFoundation.GetWindowSleepBlockReason(window));

                ESWindowFoundation.BindWithStandardSystemHost(window, actionBar);
                Assert.IsTrue(ESWindowFoundation.IsWindowSleepSupported(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsTrue(ESWindowFoundation.IsWindowAutoSleepEnabled(window));

                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(window, false));
                Assert.IsFalse(ESWindowFoundation.IsWindowSemiSleepAllowed(window));
                Assert.IsFalse(ESWindowFoundation.CanWindowSleep(window));
                Assert.AreEqual(
                    "当前窗口已关闭半休眠。",
                    ESWindowFoundation.GetWindowSleepBlockReason(window));

                Assert.IsTrue(ESWindowFoundation.TrySetWindowSleepAllowed(window, true));
                Assert.IsTrue(ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, false));
                Assert.IsFalse(ESWindowFoundation.IsWindowAutoSleepEnabled(window));
                Assert.IsTrue(ESWindowFoundation.TrySetWindowAutoSleepEnabled(window, true));
                Assert.IsTrue(ESWindowFoundation.IsWindowAutoSleepEnabled(window));
            }
            finally
            {
                ESWindowFoundation.Close(window);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SystemActionsUseExpandedIntermediateAndMinimalLayouts()
        {
            Assert.IsFalse(ESEditorPresentation.ShouldCompactSystemActions(1174f));
            Assert.IsTrue(ESEditorPresentation.ShouldCompactSystemActions(800f));
            Assert.IsTrue(ESEditorPresentation.ShouldShowPrimarySystemAction(800f));
            Assert.IsFalse(ESEditorPresentation.ShouldShowPrimarySystemAction(480f));
        }

        [Test]
        public void AwakeGeometryRecoveryRequiresAFullPanelSizedFrame()
        {
            Rect sleep = new Rect(1800f, 900f, 100f, 100f);
            Rect awake = new Rect(320f, 180f, 920f, 620f);

            Assert.IsTrue(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(340f, 200f, 900f, 600f),
                sleep,
                awake));
            Assert.IsFalse(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(1800f, 900f, 180f, 140f),
                sleep,
                awake));
            Assert.IsFalse(ESEditorPresentation.IsClearlyAwakeGeometry(
                new Rect(340f, 200f, 520f, 300f),
                sleep,
                awake));
        }

        [Test]
        public void SemiSleepSlotSelectionReusesItsMainThreadScratch()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            object requested = Activator.CreateInstance(bindingType, true);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            FieldInfo scratchField = presentationType.GetField(
                "semiSleepUsedSlotScratch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(scratchField);
            object scratch = scratchField.GetValue(null);
            Assert.IsNotNull(scratch);

            int key = int.MinValue + 647;
            while (bindings.Contains(key))
                key++;
            bindings[key] = requested;

            try
            {
                int first = InvokePrivate<int>(
                    presentationType,
                    "AcquireSemiSleepSlot",
                    requested);
                int second = InvokePrivate<int>(
                    presentationType,
                    "AcquireSemiSleepSlot",
                    requested);

                Assert.GreaterOrEqual(first, 0);
                Assert.AreEqual(first, second);
                Assert.AreSame(scratch, scratchField.GetValue(null));
                AssertSlotIsUnusedByOtherSleepingWindows(
                    bindings,
                    bindingType,
                    requested,
                    first);
            }
            finally
            {
                bindings.Remove(key);
            }
        }

        [Test]
        public void FollowOwnerSyncNeverAddsBindingsDuringTheUpdatePass()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow owner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", child);
            SetField(
                bindingType,
                binding,
                "sleepLinkMode",
                ESWindowSleepLinkMode.FollowOwner);
            SetField(bindingType, binding, "sleepOwner", owner);

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int childId = child.GetInstanceID();
            int ownerId = owner.GetInstanceID();
            Assert.IsFalse(bindings.Contains(ownerId));
            bindings[childId] = binding;
            int countBefore = bindings.Count;

            try
            {
                InvokePrivate<object>(presentationType, "SyncSleepOwnerState", binding);

                Assert.AreEqual(countBefore, bindings.Count);
                Assert.IsFalse(bindings.Contains(ownerId));
            }
            finally
            {
                bindings.Remove(childId);
                bindings.Remove(ownerId);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SwitchingToAwakeOwnerReleasesSleepForcedByPreviousOwnerImmediately()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow sleepingOwner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow awakeOwner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.BindFullSleep(child);
                ESWindowFoundation.BindFullSleep(sleepingOwner);
                ESWindowFoundation.BindFullSleep(awakeOwner);
                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    sleepingOwner,
                    ESWindowSleepLinkMode.FollowOwner));

                IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
                object childBinding = bindings[child.GetInstanceID()];
                Assert.IsNotNull(childBinding);
                SetField(bindingType, childBinding, "visualState", ESWindowVisualState.SleepTile);
                SetField(bindingType, childBinding, "transitionTargetState", ESWindowVisualState.SleepTile);
                SetField(bindingType, childBinding, "semiSleeping", true);
                SetField(bindingType, childBinding, "semiSleepTarget", true);
                SetField(bindingType, childBinding, "semiSleepAnimating", false);
                SetField(bindingType, childBinding, "sleepOwnerForcedSleep", true);
                SetField(bindingType, childBinding, "awakeBounds", new Rect(20f, 20f, 640f, 480f));

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    child,
                    awakeOwner,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.AreSame(
                    awakeOwner,
                    GetFieldValue<EditorWindow>(bindingType, childBinding, "sleepOwner"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    childBinding,
                    "sleepOwnerForcedSleep"));
                Assert.IsFalse(
                    GetFieldValue<bool>(bindingType, childBinding, "semiSleepTarget"),
                    "新 owner 已唤醒时必须立即释放旧 owner 强制的休眠目标。");
            }
            finally
            {
                ESWindowFoundation.Close(child);
                ESWindowFoundation.Close(sleepingOwner);
                ESWindowFoundation.Close(awakeOwner);
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(sleepingOwner);
                UnityEngine.Object.DestroyImmediate(awakeOwner);
            }
        }

        [Test]
        public void OwnerKeyRecoveryIsOrderIndependentUniqueAndReleasedOnClose()
        {
            const string ownerKey = "ES.Tests.OwnerKeyOrdering";
            ESWindowSleepLifetimeProbeWindow owner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow replacementOwner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow lateChild =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow cycleChild =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                ESWindowFoundation.BindFullSleep(owner);
                ESWindowFoundation.BindFullSleep(replacementOwner);
                ESWindowFoundation.BindFullSleep(lateChild);
                ESWindowFoundation.BindFullSleep(cycleChild);

                Assert.AreEqual(0, ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, owner));
                Assert.Throws<InvalidOperationException>(
                    () => ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, replacementOwner),
                    "同一活动 ownerKey 不得被第二个 Full 窗口接管。");

                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(lateChild, ownerKey));
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(lateChild),
                    "父窗口先恢复时，后登记的子窗口也必须立即解析 owner。");
                AssertPendingOwnerKeyAbsent(ownerKey);

                Assert.IsTrue(ESWindowFoundation.SetSleepOwner(
                    owner,
                    cycleChild,
                    ESWindowSleepLinkMode.FollowOwner));
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(cycleChild, ownerKey));
                Assert.IsTrue(
                    PendingOwnerKeyExists(ownerKey),
                    "会形成 owner 环的即时解析必须保留 Pending 恢复意图。");

                ESWindowFoundation.Close(owner);
                AssertPendingOwnerKeyAbsent(ownerKey);
                Assert.AreEqual(
                    ESWindowSleepLinkMode.Independent,
                    ESWindowFoundation.GetSleepLinkMode(lateChild));
                Assert.DoesNotThrow(
                    () => ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, replacementOwner),
                    "旧 owner 真实关闭后必须释放稳定 key。");
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                ESWindowFoundation.Close(cycleChild);
                ESWindowFoundation.Close(lateChild);
                ESWindowFoundation.Close(replacementOwner);
                ESWindowFoundation.Close(owner);
                UnityEngine.Object.DestroyImmediate(cycleChild);
                UnityEngine.Object.DestroyImmediate(lateChild);
                UnityEngine.Object.DestroyImmediate(replacementOwner);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RegularProductionWindowOpenersUseSingleInstancePaths()
        {
            Assert.IsTrue(typeof(IESWindowMultiInstanceContract).IsAssignableFrom(
                typeof(ESAdvancedDialogWindow)));
            Assert.IsTrue(typeof(IESWindowMultiInstanceContract).IsAssignableFrom(
                typeof(global::ES.ESWindowSleepBenchmarkProbeWindow)));
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs",
                "static new void OpenWindow");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESEditorFeedbackSound/ESEditorFeedbackSound.cs",
                "ESWindow_SupportsSemiSleep => false");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs",
                "private static void OpenFromMenu()",
                "OpenWindow();");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESCmdAgent/ESCmdAgentWindow.cs",
                "CreateInstance<ESCmdAgentWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESIndependentInspectorWindow.cs",
                "CreateInstance<TWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs",
                "CreateInstance<ESInputActionImportWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputBindingDefineDrawer.cs",
                "CreateInstance<ESInputActionBindingImportWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "CreateInstance<ESProgressCenterWindow>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESAssetConfigKeyDrawer.cs",
                "CreateInstance<ESAssetReferKeyPickerWindow>");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESAssetConfigKeyDrawer.cs",
                "GetWindow<ESAssetReferKeyPickerWindow>(");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "IsSemiSleepWindowTypeExcluded");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "!ReferenceEquals(binding.root, window.rootVisualElement)",
                "AttachWindowOverlay(binding);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;",
                "private static void EnsurePlayModeLifecycleHook()");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void UninstallGlobalEditorAdapters()",
                "AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;",
                "EditorApplication.quitting -= HandleEditorQuitting;",
                "UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;",
                "windowLifecycleHooksInstalled = false;",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "UnbindAllWindowBindings();");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void EnsurePlayModeLifecycleHook()",
                "if (globalEditorAdapterLifecycleInstalled)");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "bool playModeSuspended = playModeBindingsSuspended",
                "if (playModeSuspended)");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "if (playModeSuspended)",
                "bool resumed = ResumeWindowBindings();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "if (binding.window.rootVisualElement.panel == null)",
                "QueueResumeOnPanelAttach(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "BindWindow",
                "catch (InvalidOperationException) when (lifecycleSuspended)",
                "actionHosts = null;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "actionHosts.ValidateOwnership(window.rootVisualElement);",
                "EnsureWindowLifecycleHooks();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void UnbindAllWindowBindings()",
                "for (int i = 0; i < bindings.Count; i++)\n                {\n                    try",
                "UnbindWindowBinding(",
                "catch (Exception exception)",
                "finally",
                "windowBindings.Clear();",
                "windowBindingsByRoot.Clear();",
                "sleepOwnerBindingsByKey.Clear();",
                "pendingSleepOwners.Clear();",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "RefreshSemiSleepUpdateSubscription();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void CloseWindowBinding(",
                "try",
                "RunWindowTeardownStep(() => RestoreSemiSleep",
                "RunWindowTeardownStep(() => DetachOwnedSleepRelationships",
                "finally",
                "RemoveSleepOwnerBindingReferences(binding)",
                "windowBindingsByRoot.Remove(boundRoot);",
                "windowBindings.Remove(id);",
                "resumeBindingsRetryExhaustedWindowIds.Remove(id);",
                "ReleaseWindowBindingReferences(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "internal static ESWindowPresentationHealthSnapshot CaptureWindowHealthSnapshot()",
                "if (!binding.lifecycleSuspended\n                        && FindDeclaredSystemActionHost(binding) == null)",
                "if (!binding.lifecycleSuspended\n                    && HasSettledSemiSleepGeometryMismatch(binding))");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "EditorApplication.delayCall -= ResumeWindowBindingsRetry;");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private const int ResumeBindingsRetryBurstLimit = 4;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "if (binding.window.rootVisualElement == null)",
                "waitingForPanel = true",
                "QueueResumeOnPanelAttach(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "bool overlayNeedsRebuild =",
                "if (overlayNeedsRebuild || binding.lifecycleSuspended)",
                "LoadSemiSleepPreferences(binding);",
                "if (overlayNeedsRebuild)",
                "if (!IsWindowOverlayAttached(binding))",
                "SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);",
                "MarkWindowBindingResumed(pair.Key, binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "if (window.rootVisualElement.panel == null)",
                "SuspendWindowBindingForPanelRetry(binding, actionHosts);",
                "EnsureStandardSystemActionBar(window);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool IsWindowOverlayAttached(WindowBinding binding)",
                "ReferenceEquals(binding.root, binding.window.rootVisualElement)",
                "binding.semiSleepOverlay.panel != null",
                "VisualElement systemHost = FindDeclaredSystemActionHost(binding);",
                "binding.semiSleepControls.panel != null");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "resumeBindingsRetryAttempt = 0;",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "binding.actionHostsWereExplicit",
                "awaitingExplicitHosts = true",
                "resumeBindingsRetryRequested = true;",
                "MarkWindowBindingResumed(pair.Key, binding);",
                "return !needsPanelRetry && !waitingForPanel && !awaitingExplicitHosts;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "if (callerProvidedActionHosts)",
                "binding.actionHostsWereExplicit = true;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void QueueResumeWindowBindingsRetry(bool resetRetryBudget = false)",
                "resumeBindingsRetryRequested = true;",
                "if (resetRetryBudget)",
                "resumeBindingsRetryAttempt = 0;",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "resumeBindingsRetryAttempt >= ResumeBindingsRetryBurstLimit",
                "RecordExhaustedResumeWindowBindings();",
                "resumeBindingsRetryAttempt++;",
                "EditorApplication.delayCall += ResumeWindowBindingsRetry;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void RecordExhaustedResumeWindowBindings()",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "pair.Value.lifecycleSuspended",
                "resumeBindingsRetryExhaustedWindowIds.Add(pair.Key);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void MarkWindowBindingResumed(int id, WindowBinding binding)",
                "binding.lifecycleSuspended = false;",
                "resumeBindingsRetryExhaustedWindowIds.Remove(id);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void ResumeWindowBindingsRetry()",
                "if (domainReloadInProgress || EditorApplication.isCompiling)\n                return;",
                "if (!resumeBindingsRetryRequested)",
                "ResumeWindowBindings(false);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void CompleteResumeWindowBindingsRetry()",
                "resumeBindingsRetryRequested = false;",
                "resumeBindingsRetryAttempt = 0;",
                "resumeBindingsRetryExhaustedWindowIds.Clear();",
                "EditorApplication.delayCall -= ResumeWindowBindingsRetry;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void OnWindowRootAttached(AttachToPanelEvent evt)",
                "binding.pendingPanelRoot = null;",
                "QueueResumeWindowBindingsRetry(true);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void OnWindowRootDetached(DetachFromPanelEvent evt)",
                "SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);",
                "QueueResumeWindowBindingsRetry();",
                "RefreshSemiSleepUpdateSubscription();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "public static void BindWindow(",
                "MarkWindowBindingResumed(id, binding);",
                "if (playModeBindingsSuspended || resumeBindingsRetryRequested)",
                "ResumeWindowBindings();");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void SaveSemiSleepPreferences(WindowBinding binding)",
                "if (binding.lifecycleSuspended)",
                "SaveSuspendedStablePreferences(binding);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "private static void UpdateSemiSleepWindowsCore()",
                "if (!CanEnterSemiSleep(binding, false))",
                "HasSemiSleepStateToNormalize(binding)",
                "RestoreSemiSleep(binding, true);");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs",
                "EditorApplication.delayCall -= RecoverSemiSleepAfterFailedCompilation;");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.cs",
                "activePopup.Close();",
                "CreateInstance<ESCompactChoicePopup>");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId",
                "=> nameof(ESDialogService);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "IESWindowMultiInstanceContract.ESWindow_MultiInstanceCoordinatorId",
                "=> nameof(ESWindowSemiSleepStressTest);");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ScriptableObject.CreateInstance(windowType)");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ESEditorPresentation.BindWindow(window, true)");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "ESEditorPresentation.SetSemiSleepEnabled(true)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "if (HasOpenWindowInstance(windowType))",
                "EditorWindow window = EditorWindow.GetWindow(windowType);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs",
                "!ESWindowFoundation.IsBound(window)",
                "!ESWindowFoundation.IsWindowSleepSupported(window)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "private const int MaximumActiveDialogs = 8;",
                "if (activeWindows.Count >= MaximumActiveDialogs)");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "if (activeWindows.Count >= MaximumActiveDialogs)",
                "CreateInstance<ESAdvancedDialogWindow>()");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "DialogOperation pendingDuplicate = FindPendingDuplicate(request.dialogId);",
                "DialogOperation activeDuplicate = FindActiveDuplicateOperation(request.dialogId);");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "DialogOperation activeDuplicate = FindActiveDuplicateOperation(request.dialogId);",
                "if (request.duplicatePolicy == ESDialogDuplicatePolicy.ReplaceExisting");
            AssertSourceContainsInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "if (request.queueBehindActiveDialog && HasLiveActiveWindow())",
                "return OpenNow(operation, false);");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "private static void TryDrainQueue()",
                "next.state = DialogOperationState.Scheduled;",
                "scheduledOperation = next;",
                "EditorApplication.delayCall += next.scheduledCallback;");
            AssertSourceContainsInMethodInOrder(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "private static void OpenScheduled(DialogOperation operation)",
                "RemoveScheduledCallback(operation);",
                "|| !pendingDialogs.Contains(operation)",
                "if (shuttingDown)",
                "PruneDeadActiveOperations();",
                "if (HasLiveActiveWindow())",
                "if (IsOwnerInvalid(operation.request))",
                "OpenNow(operation, false);");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "TakeNextPending(");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "AddPendingObserversToWindow(");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "observedWindow");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs",
                "ownsWindow");
            AssertSourceContainsInOrder(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "activeWindow.Close();",
                "CreateInstance<ESWorkbenchPopupWindow>");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "GetWindow<ESInstaller>(");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "CreateInstance<ESInstaller>");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "temporaryCheckInstance");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "DependencyCheckWindowLease");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "Resources.FindObjectsOfTypeAll<");
            AssertSourceExcludes(
                "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs",
                "public static ESInstaller installer;");
        }

        [Test]
        public void InstallerUsesCanonicalProfileAndSingleUpmSnapshotForRequiredDependencies()
        {
            const string path = "Assets/Plugins/ES/Editor/Installer/ESInstaller.cs";
            AssertSourceContainsInMethodInOrder(
                path,
                "private static async Task CheckAndShowInstallerIfNeededAsync()",
                "LoadCanonicalInstallationProfile();",
                "CheckRequiredDependenciesAsync(profile.mainPackage);");
            AssertSourceContainsInMethodInOrder(
                path,
                "private static async Task QuickCheckAndShowResultAsync()",
                "LoadCanonicalInstallationProfile();",
                "CheckRequiredDependenciesAsync(profile.mainPackage);");
            AssertSourceContainsInMethodInOrder(
                path,
                "private void LoadConfiguration()",
                "LoadCanonicalInstallationProfile();",
                "RebuildPackageUiIndex(currentProfile);");
            AssertSourceContainsInMethodInOrder(
                path,
                "private static async Task<DependencyCheckResult> CheckRequiredDependenciesAsync(",
                "CaptureInstalledPackageSnapshotAsync();",
                "package.unityDependencies",
                "package.gitDependencies",
                "package.userDependencies",
                "package.assetFileDependencies",
                "return result;");
            AssertSourceExcludes(path, "ScanAndLoadAllPackages();");
            AssertSourceExcludes(path, "CheckForUninstalledRequiredDependenciesAsync");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string source = ReadSourceText(Path.Combine(
                projectRoot,
                path.Replace('/', Path.DirectorySeparatorChar)));
            Assert.AreEqual(
                1,
                Regex.Matches(source, Regex.Escape("Client.List(false, false)")).Count,
                "每次依赖检查必须只从统一 helper 获取一次 UPM 包快照。");
        }

        [Test]
        public void DirectTransientWindowsDeclareExplicitNoSleepContracts()
        {
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESTreeCollector/ESTree_Center/ESTreeMenuBuilder.cs",
                "ESWindowFoundation.BindTransient(this)");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESTreeCollector/ESTree_Center/ESTreeMenuBuilder.cs",
                "ESWindowFoundation.Suspend(this)");
            AssertSourceContains(
                "Assets/Plugins/ES/Editor/ESTreeCollector/ESTree_Center/ESTreeMenuBuilder.cs",
                "ESWindowFoundation.Close(this)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "ESWindowFoundation.BindTransient(this)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "ESWindowFoundation.Suspend(this)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "ESWindowFoundation.Close(this)");
            AssertSourceContains(
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs",
                "if (!ESWindowFoundation.IsBound(owner))");
        }

        [Test]
        public void ProgressCenterUsesExplicitTransientSleepContract()
        {
            string path =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            AssertSourceContains(path, "public sealed class ESProgressCenterWindow");
            AssertSourceContains(path, "ESWindowFoundation.BindTransient(this)");
            AssertSourceContains(path, "跨任务全局进度聚合面不参与自动半休眠");
        }

        [Test]
        public void AssetReferKeyPickerUsesEditorBridgeAndTransientLifecycle()
        {
            const string standPath = "Assets/Plugins/ES/0_Stand/_Res/ResUse/ESAssetRefer.cs";
            const string editorPath =
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESAssetConfigKeyDrawer.cs";
            AssertSourceContains(
                standPath,
                "Action<ESAssetReferKind, ESAssetPage, Action<ESAssetPage>> OpenAssetKeyPicker");
            AssertSourceContains(standPath, "ESAssetReferEditorBridge.OpenAssetKeyPicker");
            AssertSourceExcludes(standPath, "class ESAssetReferKeyPickerWindow : EditorWindow");
            AssertSourceContains(
                editorPath,
                "ESWindowSurfaceKind.Popup");
            AssertSourceContains(editorPath, "class ESAssetReferKeyPickerWindow : EditorWindow");
            AssertSourceContains(editorPath, "ESAssetReferKeyPickerWindow : EditorWindow, IESWindowPresentationShortTitle");
            AssertSourceContains(editorPath, "ESAssetReferEditorBridge.OpenAssetKeyPicker = ESAssetReferKeyPickerWindow.Open;");
            AssertSourceContains(editorPath, "class ESAssetCatalogKeyPickerInitializer : EditorInvoker_Level2");
            AssertSourceContains(editorPath, "GetWindow<ESAssetReferKeyPickerWindow>(");
            AssertSourceContains(editorPath, "ShowAuxWindow();");
            AssertSourceContains(editorPath, "ESWindowFoundation.BindTransient(this);");
            AssertSourceContains(editorPath, "ESWindowFoundation.Suspend(this);");
            AssertSourceContains(editorPath, "ESWindowFoundation.Close(this);");
            AssertSourceExcludes(editorPath, "InitializeOnLoad");
        }

        [Test]
        public void ProductionBaseWindowsInheritExplicitFullSleepContract()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            const string templatePath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs";
            string templateSource = ReadSourceText(Path.Combine(
                projectRoot,
                templatePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(Regex.IsMatch(
                templateSource,
                @"\[ESWindowSleepContract\(ESWindowSleepMode\.Full,\s*ESWindowSurfaceKind\.Workspace\)\]\s*public abstract class ESMenuTreeWindow<This>",
                RegexOptions.CultureInvariant));
            Assert.IsTrue(Regex.IsMatch(
                templateSource,
                @"\[ESWindowSleepContract\(ESWindowSleepMode\.Full,\s*ESWindowSurfaceKind\.Workspace\)\]\s*public abstract class ESOdinMenuTreeWindow<This>",
                RegexOptions.CultureInvariant));
            string[] sourceFiles = EnumerateESProductionEditorSources(projectRoot);
            var shortExceptionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeProjectPath(
                    Path.Combine(
                        projectRoot,
                        "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs")),
                NormalizeProjectPath(
                    Path.Combine(
                        projectRoot,
                        "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputBindingDefineDrawer.cs")),
            };
            int productionBaseWindowCount = 0;
            foreach (string path in sourceFiles)
            {
                string normalized = NormalizeProjectPath(path);
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase))
                    continue;
                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (!Regex.IsMatch(
                        source,
                        @"class\s+[^\r\n]*:\s*[^\r\n]*(?:ESSinglePage(?:IMGUI)?Window|ESMenuTreeWindow|ESOdinMenuTreeWindow)",
                        RegexOptions.CultureInvariant))
                    continue;

                productionBaseWindowCount++;
                if (Regex.IsMatch(
                        source,
                        @"ESWindow_SupportsSemiSleep\s*=>\s*false",
                        RegexOptions.CultureInvariant))
                {
                    Assert.IsTrue(
                        shortExceptionPaths.Contains(normalized),
                        "生产基类窗口不能静默关闭完整休眠：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepMode.Transient",
                        source,
                        "基类休眠例外必须声明 Transient 合同：" + normalized);
                    StringAssert.Contains(
                        "ESWindowSleepContract",
                        source,
                        "基类休眠例外必须登记原因：" + normalized);
                }
            }

            Assert.GreaterOrEqual(
                productionBaseWindowCount,
                20,
                "生产基类窗口扫描结果异常，可能是扫描规则或工程路径失效。");
        }

        [Test]
        public void ProductionWindowInventoryHasExplicitOrInheritedContracts()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            Dictionary<string, (string Path, string Reason)> explicitExceptions =
                CreateExplicitNonProductionWindowExceptions();
            IReadOnlyList<ESOwnedWindowSource> allDiscovered =
                DiscoverESOwnedConcreteEditorWindows(projectRoot);
            Assert.IsTrue(
                allDiscovered.Any(item =>
                    item.WindowType == typeof(ESWindowInheritedDiscoveryProbeWindow)),
                "TypeCache + MonoScript 映射必须发现别名根和中间基类后的具体 EditorWindow。");
            ESOwnedWindowSource[] declarations = allDiscovered
                .Where(item => !IsExcludedProductionSourcePath(item.ProjectRelativePath)
                    && !explicitExceptions.ContainsKey(item.WindowType.Name))
                .ToArray();

            Assert.AreEqual(
                49,
                declarations.Length,
                "生产 ES 窗口库存发生漂移；新增窗口必须重新判定 Full/Transient 并更新覆盖表。");
            Dictionary<string, ESWindowSurfaceKind> expectedSurfaceKinds =
                CreateExpectedProductionWindowSurfaceKinds();
            Assert.AreEqual(49, expectedSurfaceKinds.Count);
            Assert.AreEqual(
                32,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Workspace));
            Assert.AreEqual(
                4,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Inspector));
            Assert.AreEqual(
                5,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Popup));
            Assert.AreEqual(
                1,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Dialog));
            Assert.AreEqual(
                3,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Preview));
            Assert.AreEqual(
                4,
                expectedSurfaceKinds.Values.Count(kind => kind == ESWindowSurfaceKind.Utility));
            HashSet<string> expectedProductionNames = new HashSet<string>(
                expectedSurfaceKinds.Keys,
                StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(
                expectedProductionNames,
                declarations.Select(item => item.WindowType.Name).ToArray(),
                "生产 ES 窗口类型集合发生漂移。");
            Assert.AreEqual(
                16,
                declarations.Count(item => IsDirectEditorWindowType(item.WindowType)),
                "直接 EditorWindow/OdinEditorWindow 窗口数量发生漂移。");
            Assert.AreEqual(
                33,
                declarations.Count(item => !IsDirectEditorWindowType(item.WindowType)),
                "基类派生窗口数量发生漂移。");

            HashSet<string> expectedTransient = CreateExpectedTransientWindowNames();

            foreach (string name in expectedTransient)
            {
                Assert.IsTrue(
                    declarations.Any(item => item.WindowType.Name == name),
                    "Transient 窗口不在生产库存中：" + name);
            }

            Assert.AreEqual(
                expectedTransient.Count,
                declarations.Count(item => expectedTransient.Contains(item.WindowType.Name)),
                "Transient 例外集合与生产窗口库存不一致。");
            Assert.AreEqual(
                39,
                declarations.Length - expectedTransient.Count,
                "未声明 Transient 的生产窗口必须继承或声明 Full。");

            foreach (ESOwnedWindowSource declaration in declarations)
            {
                Type windowType = declaration.WindowType;
                var contract = (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                    windowType,
                    typeof(ESWindowSleepContractAttribute),
                    true);
                Assert.IsNotNull(contract, "生产 ES 窗口缺少显式或继承合同：" + windowType.FullName);
                Assert.AreEqual(
                    expectedTransient.Contains(windowType.Name)
                        ? ESWindowSleepMode.Transient
                        : ESWindowSleepMode.Full,
                    contract.Mode,
                    "生产 ES 窗口合同模式漂移：" + windowType.FullName);
                Assert.AreEqual(
                    expectedSurfaceKinds[windowType.Name],
                    contract.SurfaceKind,
                    "生产 ES 窗口 SurfaceKind 漂移；分类不得由类型名推断："
                    + windowType.FullName);
            }
        }

        [Test]
        public void AllESOwnedProductionWindowsStayInApprovedRootsAndInventory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            HashSet<string> expectedProductionNames = CreateExpectedProductionWindowNames();
            Dictionary<string, (string Path, string Reason)> explicitExceptions =
                CreateExplicitNonProductionWindowExceptions();
            var discoveredProductionNames = new HashSet<string>(StringComparer.Ordinal);
            var discoveredExceptions = new HashSet<string>(StringComparer.Ordinal);

            foreach (ESOwnedWindowSource window in DiscoverESOwnedConcreteEditorWindows(projectRoot))
            {
                string projectRelativePath = window.ProjectRelativePath;
                if (IsExcludedProductionSourcePath(projectRelativePath))
                    continue;

                string windowName = window.WindowType.Name;
                if (explicitExceptions.TryGetValue(
                        windowName,
                        out (string Path, string Reason) exception))
                {
                    Assert.AreEqual(
                        exception.Path,
                        projectRelativePath,
                        "非生产窗口例外只能存在于登记路径："
                        + windowName
                        + "，原因="
                        + exception.Reason);
                    Assert.IsTrue(
                        discoveredExceptions.Add(windowName),
                        "非生产窗口例外重复声明：" + windowName);
                    continue;
                }

                Assert.IsTrue(
                    IsApprovedProductionEditorPath(projectRelativePath),
                    "ES-owned 生产 EditorWindow 必须位于批准的 Editor 根；Editor-only asmdef 也不能逃逸库存："
                    + projectRelativePath
                    + "|"
                    + window.WindowType.FullName);
                Assert.IsTrue(
                    expectedProductionNames.Contains(windowName),
                    "ES-owned 生产 EditorWindow 必须进入 49 窗口库存并判定 Full/Transient："
                    + projectRelativePath
                    + "|"
                    + window.WindowType.FullName);
                Assert.IsTrue(
                    discoveredProductionNames.Add(windowName),
                    "生产窗口简单类型名或声明重复：" + window.WindowType.FullName);
            }

            CollectionAssert.AreEquivalent(
                explicitExceptions.Keys,
                discoveredExceptions,
                "显式 benchmark/test probe 例外集合发生漂移；例外不得按目录或名称模式自动扩大。");
            CollectionAssert.AreEquivalent(
                expectedProductionNames,
                discoveredProductionNames,
                "全 ES-owned 源码扫描必须与现有 49 窗口库存完全一致。");
            Assert.AreEqual(49, discoveredProductionNames.Count);
        }

        [Test]
        public void LifecycleSourceScannerKeepsTypeMethodAndHelperBoundaries()
        {
            const string source = @"
namespace Scanner.Probes
{
    using WindowRoot = UnityEditor.EditorWindow;

    // public sealed class CommentDecoy : EditorWindow { }
    public sealed class HealthyWindow : EditorWindow
    {
        private void ReleaseFoundation()
        {
            ESWindowFoundation.Unbind(this);
        }

        private void RestoreFoundation()
            => ESWindowFoundation.BindFullSleep(this);

        private void CreateGUI()
        {
            string stringDecoy = ""rootVisualElement.Clear();"";
            ReleaseFoundation();
            rootVisualElement.Clear();
            RestoreFoundation();
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

    public sealed class LocalFunctionLeakWindow : EditorWindow
    {
        private void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            void Rebuild()
            {
                rootVisualElement.Clear();
            }
            ESWindowFoundation.BindTransient(this);
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

    public sealed class MissingShutdownWindow : EditorWindow
    {
        private void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            rootVisualElement.Clear();
            ESWindowFoundation.BindTransient(this);
        }
    }

    public sealed class RepeatedClearWindow : EditorWindow
    {
        private void CreateGUI()
        {
            ESWindowFoundation.Unbind(this);
            rootVisualElement.Clear();
            ESWindowFoundation.BindTransient(this);
            rootVisualElement.Clear();
            ESWindowFoundation.BindTransient(this);
        }
    }

    public sealed class UncalledHelperDecoyWindow : EditorWindow
    {
        private void ReleaseFoundation() => ESWindowFoundation.Unbind(this);
        private void RestoreFoundation() => ESWindowFoundation.BindTransient(this);
        private void CreateGUI()
        {
            rootVisualElement.Clear();
        }
    }

    public sealed class ForeignReceiverDecoyWindow : EditorWindow
    {
        private ForeignReceiverDecoyWindow other;
        private void ReleaseFoundation() => ESWindowFoundation.Unbind(this);
        private void RestoreFoundation() => ESWindowFoundation.BindTransient(this);
        private void CreateGUI()
        {
            other.ReleaseFoundation();
            rootVisualElement.Clear();
            other.RestoreFoundation();
        }
    }

    public sealed class DeadLifecycleCallWindow : EditorWindow
    {
        private void OnDestroy()
        {
            void NeverCalled() => ESWindowFoundation.Close(this);
        }
    }

    public sealed class FalseBranchLifecycleCallWindow : EditorWindow
    {
        private void OnDestroy()
        {
            if (false)
            {
                ESWindowFoundation.Close(this);
            }
        }
    }

    public abstract class CustomBase : WindowRoot { }
    public sealed class HiddenInheritedWindow : CustomBase { }
}";
            SourceWindowTypeDeclaration[] declarations =
                ExtractWindowTypeDeclarations(source, "synthetic.cs").ToArray();
            Assert.AreEqual(10, declarations.Length, "注释中的伪窗口不得进入类型扫描。");
            Assert.AreEqual(
                "CustomBase",
                declarations.Single(item => item.Name == "HiddenInheritedWindow").BaseType.Trim(),
                "库存发现不得依赖固定直系基类名；别名和中间基类由 TypeCache 判定。");

            SourceWindowTypeDeclaration healthy = declarations.Single(
                declaration => declaration.Name == "HealthyWindow");
            Assert.DoesNotThrow(
                () => AssertRootVisualElementClearHasFoundationRebindBoundary(
                    healthy,
                    ESWindowSleepMode.Full));
            Assert.DoesNotThrow(
                () => AssertMethodContainsFoundationCall(healthy, "OnDisable", "Suspend"));
            Assert.DoesNotThrow(
                () => AssertMethodContainsFoundationCall(healthy, "OnDestroy", "Close"));

            SourceWindowTypeDeclaration localLeak = declarations.Single(
                declaration => declaration.Name == "LocalFunctionLeakWindow");
            Assert.Throws<AssertionException>(
                () => AssertRootVisualElementClearHasFoundationRebindBoundary(
                    localLeak,
                    ESWindowSleepMode.Transient),
                "局部函数 Clear 不得借用外层方法的 Unbind/Bind 形成伪闭环。");

            foreach (string name in new[]
                     {
                         "RepeatedClearWindow",
                         "UncalledHelperDecoyWindow",
                         "ForeignReceiverDecoyWindow",
                     })
            {
                SourceWindowTypeDeclaration invalid = declarations.Single(
                    declaration => declaration.Name == name);
                Assert.Throws<AssertionException>(
                    () => AssertRootVisualElementClearHasFoundationRebindBoundary(
                        invalid,
                        ESWindowSleepMode.Transient),
                    name + " 不得用历史转换、未调用 helper 或其他接收者伪造 Clear 闭环。");
            }

            SourceWindowTypeDeclaration missingShutdown = declarations.Single(
                declaration => declaration.Name == "MissingShutdownWindow");
            Assert.Throws<AssertionException>(
                () => AssertMethodContainsFoundationCall(
                    missingShutdown,
                    "OnDestroy",
                    "Close"),
                "同文件其他窗口的 OnDestroy 不得替当前类型满足关闭合同。");
            SourceWindowTypeDeclaration deadLifecycle = declarations.Single(
                declaration => declaration.Name == "DeadLifecycleCallWindow");
            Assert.Throws<AssertionException>(
                () => AssertMethodContainsFoundationCall(
                    deadLifecycle,
                    "OnDestroy",
                    "Close"),
                "未调用局部函数中的 Close 不得满足 OnDestroy 入口合同。");
            SourceWindowTypeDeclaration falseBranchLifecycle = declarations.Single(
                declaration => declaration.Name == "FalseBranchLifecycleCallWindow");
            Assert.Throws<AssertionException>(
                () => AssertMethodContainsFoundationCall(
                    falseBranchLifecycle,
                    "OnDestroy",
                    "Close"),
                "if (false) 死分支中的 Close 不得满足 OnDestroy 入口合同。");
        }

        [Test]
        public void SourceContractReaderRejectsInvalidUtf8WithoutCodePageFallback()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "ESWindowSleepLifetimeTests-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x80 });
                Assert.Throws<DecoderFallbackException>(() => ReadSourceText(path));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void DirectProductionEditorWindowsDeclareExplicitESLifecycleBinding()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            Dictionary<string, (string Path, string Reason)> explicitExceptions =
                CreateExplicitNonProductionWindowExceptions();
            ESOwnedWindowSource[] productionWindows = DiscoverESOwnedConcreteEditorWindows(projectRoot)
                .Where(item => !IsExcludedProductionSourcePath(item.ProjectRelativePath)
                    && !explicitExceptions.ContainsKey(item.WindowType.Name))
                .ToArray();
            Assert.AreEqual(49, productionWindows.Length);

            foreach (ESOwnedWindowSource window in productionWindows)
            {
                var contract = (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                    window.WindowType,
                    typeof(ESWindowSleepContractAttribute),
                    true);
                Assert.IsNotNull(contract, window.WindowType.FullName);
                AssertRootVisualElementClearHasFoundationRebindBoundary(
                    window.Declaration,
                    contract.Mode);
                AssertDeclaredLifecycleMethodPreservesFoundation(
                    window,
                    "OnEnable",
                    "BindFullSleep",
                    "BindTransient",
                    "BindWithStandardSystemHost");
                AssertDeclaredLifecycleMethodPreservesFoundation(
                    window,
                    "OnDisable",
                    "Suspend");
                AssertDeclaredLifecycleMethodPreservesFoundation(
                    window,
                    "OnDestroy",
                    "Close");
                Assert.IsFalse(
                    Regex.IsMatch(
                        window.Declaration.SearchableBody,
                        @"\bESEditorPresentation\s*\.\s*(?:BindWindow|UnbindWindow|SetWindowSleepOwner|RegisterPendingSleepOwner|ResolvePendingSleepOwners|ClearWindowSleepOwner|ClearPendingSleepOwner|ClearPendingSleepOwners)\s*\(",
                        RegexOptions.CultureInvariant),
                    "生产窗口不得在自身类体中绕过 ESWindowFoundation："
                    + window.Declaration.DiagnosticIdentity);
            }
        }

        [Test]
        public void ProductionLifecycleCallsCannotBypassWindowFoundation()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            foreach (string path in EnumerateESProductionEditorSources(projectRoot))
            {
                string normalized = NormalizeProjectPath(path);
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Obsolete/", StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith(
                        "/ESPresentation/Core/ESEditorPresentationCore.cs",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = ReadSourceText(path);
                string searchableCode = GetSearchableCSharpCode(source);
                AssertNoInternalLifecycleBypass(searchableCode, normalized);
                StringAssert.DoesNotContain(
                    "ESWindowFoundation.Unbind(this, true)",
                    source,
                    "OnDisable/OnDestroy 不得再用布尔参数猜测关闭语义：" + normalized);
            }
        }

        [Test]
        public void LifecycleBypassScannerRejectsMethodGroupsAndNamespaceAliases()
        {
            string[] lifecycleMembers =
            {
                "BindWindow",
                "UnbindWindow",
                "SetWindowSleepOwner",
                "RegisterPendingSleepOwner",
                "ResolvePendingSleepOwners",
                "ClearWindowSleepOwner",
                "ClearPendingSleepOwner",
                "ClearPendingSleepOwners",
            };
            for (int i = 0; i < lifecycleMembers.Length; i++)
            {
                string source = "class Probe { object Capture() => "
                    + "ESEditorPresentation."
                    + lifecycleMembers[i]
                    + "; }";
                int index = i;
                Assert.Throws<AssertionException>(() => AssertNoInternalLifecycleBypass(
                    GetSearchableCSharpCode(source),
                    "synthetic-lifecycle-method-group-" + index));
            }

            string[] aliasBypasses =
            {
                "using Lifecycle = global::ES.EditorInternal.ESEditorPresentation; "
                + "class Probe { object Capture() => Lifecycle.SetWindowSleepOwner; }",
                "using Internal = ES.EditorInternal; class Probe { void Restore() { "
                + "Internal.ESEditorPresentation.RegisterPendingSleepOwner(null, null); } }",
                "using Internal = ES.EditorInternal; class Probe { object Capture() => "
                + "Internal::ESEditorPresentation.ResolvePendingSleepOwners; }",
                "using static ES.EditorInternal.ESEditorPresentation; "
                + "class Probe { object Capture() => ClearPendingSleepOwner; }",
            };
            for (int i = 0; i < aliasBypasses.Length; i++)
            {
                int index = i;
                Assert.Throws<AssertionException>(() => AssertNoInternalLifecycleBypass(
                    GetSearchableCSharpCode(aliasBypasses[index]),
                    "synthetic-lifecycle-alias-" + index));
            }

            const string decoySource =
                "// ESEditorPresentation.SetWindowSleepOwner;\n"
                + "class Probe { string text = \"ESEditorPresentation.BindWindow\"; "
                + "void Close() { ESWindowFoundation.ClearSleepOwner(null); } }";
            Assert.DoesNotThrow(() => AssertNoInternalLifecycleBypass(
                GetSearchableCSharpCode(decoySource),
                "synthetic-lifecycle-decoy"));
        }

        [Test]
        public void ProductionDialogsUseServiceAndNativeLegacyBaselineCannotGrow()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            const string servicePath =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            const string baselinePath =
                "Documentation/ES_EDITOR_NATIVE_DIALOG_BASELINE.txt";
            var nativeBaseline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string baselineFullPath = Path.Combine(
                projectRoot,
                baselinePath.Replace('/', Path.DirectorySeparatorChar));
            foreach (string rawLine in File.ReadAllLines(
                baselineFullPath,
                new UTF8Encoding(false, true)))
            {
                string line = rawLine?.Trim() ?? string.Empty;
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                int separator = line.LastIndexOf('|');
                Assert.Greater(separator, 0, "原生对话框基线格式错误：" + line);
                Assert.IsTrue(
                    int.TryParse(line.Substring(separator + 1), out int maximumCalls)
                    && maximumCalls > 0,
                    "原生对话框基线计数无效：" + line);
                string baselineEntryPath = line.Substring(0, separator);
                Assert.IsFalse(
                    nativeBaseline.ContainsKey(baselineEntryPath),
                    "原生对话框基线路径重复：" + line);
                nativeBaseline.Add(baselineEntryPath, maximumCalls);
            }
            Assert.AreEqual(82, nativeBaseline.Count, "原生对话框逐文件基线发生未审查漂移。");
            Assert.AreEqual(462, nativeBaseline.Values.Sum(), "原生对话框逐文件基线总量异常。");
            ESOwnedWindowSource[] customDialogs = DiscoverESOwnedConcreteEditorWindows(projectRoot)
                .Where(item => !IsExcludedProductionSourcePath(item.ProjectRelativePath)
                    && GetRequiredWindowContract(item.WindowType).SurfaceKind
                    == ESWindowSurfaceKind.Dialog)
                .ToArray();
            var actualNativeCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nativeCallsiteSignatures = new List<string>();
            var advancedDialogCreateCallsites = new List<string>();
            int nativeCallCount = 0;
            int nativeFileCount = 0;
            var nativeDialogStaticUsing = new Regex(
                @"\busing\s+static\s+(?:(?:global\s*::\s*)?UnityEditor\s*\.\s*)?EditorUtility\s*;",
                RegexOptions.CultureInvariant);
            var nativeDialogAlias = new Regex(
                @"\busing\s+[A-Za-z_]\w*\s*=\s*(?:(?:global\s*::\s*)?UnityEditor(?:\s*\.\s*EditorUtility)?|EditorUtility)\s*;",
                RegexOptions.CultureInvariant);

            foreach (string path in EnumerateESOwnedEditorSources(projectRoot))
            {
                string normalized = NormalizeProjectPath(path);
                if (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Test/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Examples/", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("/Obsolete/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = ReadSourceText(path);
                string commentFreeSource = StripCSharpComments(source);
                string code = MaskCSharpStringAndCharacterLiterals(commentFreeSource);
                AssertNoWindowFactoryTokensHiddenInLiterals(
                    commentFreeSource,
                    code,
                    normalized);
                Assert.IsFalse(
                    nativeDialogStaticUsing.IsMatch(code),
                    "禁止 using static 绕过原生对话框基线：" + normalized);
                Assert.IsFalse(
                    nativeDialogAlias.IsMatch(code),
                    "禁止 using alias 绕过原生对话框基线：" + normalized);
                Assert.IsFalse(
                    NativeDialogMethodGroupPattern.IsMatch(code),
                    "禁止通过 EditorUtility.DisplayDialog* 方法组绕过原生对话框基线："
                    + normalized);
                AssertNoLegacyAdvancedDialogCompatibilityBypass(code, normalized);
                Assert.IsFalse(
                    AdvancedDialogCreateMethodGroupPattern.IsMatch(commentFreeSource),
                    "禁止通过 ESAdvancedDialogWindow.Create 方法组绕过 ESDialogService："
                    + normalized);
                foreach (Match match in AdvancedDialogCreateCallPattern.Matches(code))
                {
                    advancedDialogCreateCallsites.Add(
                        GetProjectRelativePath(projectRoot, path)
                        + "|" + FindContainingMemberIdentity(code, match.Index));
                }

                Assert.AreEqual(
                    NativeDialogCallPattern.Matches(commentFreeSource).Count,
                    NativeDialogCallPattern.Matches(code).Count,
                    "禁止在字符串或字符字面量中隐藏/伪造原生对话框调用：" + normalized);
                MatchCollection nativeMatches = NativeDialogCallPattern.Matches(code);
                int calls = nativeMatches.Count;
                nativeCallCount += calls;
                if (calls > 0)
                {
                    nativeFileCount++;
                    string projectRelative = normalized
                        .Substring(NormalizeProjectPath(projectRoot).Length)
                        .TrimStart('/');
                    actualNativeCalls.Add(projectRelative, calls);
                    Assert.IsTrue(
                        nativeBaseline.TryGetValue(projectRelative, out int maximumCalls),
                        "禁止在新的生产文件中使用 EditorUtility 对话框：" + projectRelative);
                    Assert.AreEqual(
                        maximumCalls,
                        calls,
                        "原生对话框债务变化后必须在同一审查中下调基线，禁止回涨："
                        + projectRelative);
                    foreach (Match match in nativeMatches)
                    {
                        nativeCallsiteSignatures.Add(
                            BuildNativeDialogCallsiteSignature(
                                projectRelative,
                                commentFreeSource,
                                code,
                                match.Index));
                    }
                }
            }

            Assert.AreEqual(1, customDialogs.Length, "禁止新增自建 EditorWindow 对话框实现。");
            Assert.AreEqual(
                typeof(ESAdvancedDialogWindow),
                customDialogs[0].WindowType,
                "Dialog 必须通过显式 SurfaceKind 和真实 EditorWindow 继承关系识别；"
                + "非 Dialog 后缀及间接基类均不得绕过 ESDialogService。");
            Assert.AreEqual(servicePath, customDialogs[0].ProjectRelativePath);
            CollectionAssert.AreEqual(
                new[] { servicePath + "|OpenNow" },
                advancedDialogCreateCallsites,
                "ESAdvancedDialogWindow.Create 必须且只能由 ESDialogService.OpenNow 调用一次。");
            AssertAdvancedDialogCreateCallIsInsideServiceOpenNow(
                ReadSourceText(Path.Combine(
                    projectRoot,
                    servicePath.Replace('/', Path.DirectorySeparatorChar))));
            AssertAdvancedDialogCreateSurfaceIsClosed(
                ReadSourceText(Path.Combine(
                    projectRoot,
                    servicePath.Replace('/', Path.DirectorySeparatorChar))));
            CollectionAssert.AreEquivalent(
                nativeBaseline.Keys,
                actualNativeCalls.Keys,
                "原生对话框基线路径必须与当前债务精确对应；迁移后应同步删除旧基线项。");
            Assert.AreEqual(462, nativeCallCount, "原生 EditorUtility 对话框债务与基线不一致。");
            Assert.AreEqual(82, nativeFileCount, "原生 EditorUtility 对话框文件数与基线不一致。");
            // V1 binds path, containing member, one-based line/column and the full
            // comment-free invocation. Moving or replacing a call changes the hash.
            Assert.AreEqual(
                "74ea05bf1d3c9f0f75e50c541946ef46a4ce9d63c1ea147552d5df43b743ed54",
                ComputeStableSha256(nativeCallsiteSignatures),
                "原生对话框调用点发生替换或语义改写；即使同文件计数未变，也必须迁移到 ESDialogService 或显式审查并更新指纹。");
        }

        [Test]
        public void NativeEditorWindowModalPresentationIsServiceInternalOnly()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            const string servicePath =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            bool serviceScanned = false;

            foreach (string path in EnumerateESOwnedEditorSources(projectRoot)
                         .OrderBy(item => NormalizeProjectPath(item), StringComparer.OrdinalIgnoreCase))
            {
                string projectRelativePath = GetProjectRelativePath(projectRoot, path);
                if (IsExcludedProductionSourcePath(projectRelativePath))
                    continue;

                string source = ReadSourceText(path);
                if (string.Equals(
                        projectRelativePath,
                        servicePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    serviceScanned = true;
                    AssertAdvancedDialogOwnsOnlyNativeModalPresentationCall(
                        source,
                        projectRelativePath);
                    AssertAdvancedDialogCreateCallIsInsideServiceOpenNow(source);
                    continue;
                }

                AssertNoNativeEditorWindowModalPresentationReferences(
                    source,
                    projectRelativePath);
            }

            Assert.IsTrue(serviceScanned, "ESDialogService 生产源码未进入原生模态门禁扫描。");
            ESOwnedWindowSource[] dialogWindows = DiscoverESOwnedConcreteEditorWindows(projectRoot)
                .Where(item => !IsExcludedProductionSourcePath(item.ProjectRelativePath)
                    && GetRequiredWindowContract(item.WindowType).SurfaceKind
                    == ESWindowSurfaceKind.Dialog)
                .ToArray();
            Assert.AreEqual(1, dialogWindows.Length, "原生模态白名单必须只对应一个 Dialog surface。");
            Assert.AreEqual(typeof(ESAdvancedDialogWindow), dialogWindows[0].WindowType);
            Assert.AreEqual(servicePath, dialogWindows[0].ProjectRelativePath);
        }

        [Test]
        public void NativeDialogScannerIgnoresCommentsAndLiteralsButFindsQualifiedSpacedCalls()
        {
            const string source =
                "// EditorUtility.DisplayDialog(\"comment\", \"body\", \"ok\");\n"
                + "/* UnityEditor.EditorUtility.DisplayDialogComplex(\"comment\", \"body\", \"a\", \"b\", \"c\"); */\n"
                + "string regular = \"EditorUtility.DisplayDialog(\\\"literal\\\", \\\"body\\\", \\\"ok\\\")\";\n"
                + "string verbatim = @\"EditorUtility.DisplayDialog(\"\"literal\"\", \"\"body\"\", \"\"ok\"\")\";\n"
                + "string raw = \"\"\"EditorUtility.DisplayDialog(\"literal\", \"body\", \"ok\")\"\"\";\n"
                + "char marker = '/';\n"
                + "global::UnityEditor . EditorUtility . DisplayDialog (\"real\", \"body\", \"ok\");\n";
            string commentFree = StripCSharpComments(source);
            string code = MaskCSharpStringAndCharacterLiterals(commentFree);
            MatchCollection matches = NativeDialogCallPattern.Matches(code);

            Assert.AreEqual(1, matches.Count);
            StringAssert.StartsWith(
                "global::UnityEditor . EditorUtility . DisplayDialog",
                ExtractCSharpInvocation(commentFree, code, matches[0].Index));
            string firstHash = ComputeStableSha256(new[] { "b", "a" });
            Assert.AreEqual(firstHash, ComputeStableSha256(new[] { "a", "b" }));
        }

        [Test]
        public void NativeDialogScannerRejectsMethodGroupsWithoutChangingInvocationCount()
        {
            const string source =
                "// var ignored = EditorUtility.DisplayDialog;\n"
                + "string text = \"EditorUtility.DisplayDialogComplex\";\n"
                + "var show = EditorUtility . DisplayDialog;\n"
                + "var showComplex = global::UnityEditor.EditorUtility.DisplayDialogComplex;\n"
                + "EditorUtility.DisplayDialog(\"title\", \"body\", \"ok\");\n";

            string code = GetSearchableCSharpCode(source);

            Assert.AreEqual(1, NativeDialogCallPattern.Matches(code).Count);
            Assert.AreEqual(2, NativeDialogMethodGroupPattern.Matches(code).Count);
        }

        [Test]
        public void NativeEditorWindowModalScannerIgnoresCommentsAndLiteralsButRejectsBypasses()
        {
            const string decoySource =
                "// ShowModalUtility();\n"
                + "/* var modal = window.ShowModalUtility; */\n"
                + "string regular = \"ShowModalUtility()\";\n"
                + "string verbatim = @\"window.ShowModalUtility\";\n"
                + "string raw = \"\"\"ShowModalUtility()\"\"\";\n"
                + "string internalEntry = \"Internal_OpenFromDialogService\";\n"
                + "ESDialogService.ShowModal(request);\n";
            Assert.DoesNotThrow(() =>
                AssertNoNativeEditorWindowModalPresentationReferences(
                    decoySource,
                    "synthetic-native-modal-decoys"));

            string[] invalidSources =
            {
                "using ModalHost = global::UnityEditor.EditorWindow; "
                + "class Probe : ModalHost { void Open() { base . ShowModalUtility (); } }",
                "using ModalHost = UnityEditor.EditorWindow; "
                + "class Probe { void Bind(ModalHost host) { var show = host . ShowModalUtility; } }",
                "class Probe : UnityEditor.EditorWindow { "
                + "void Open() { this . @ShowModalUtility /* gap */ (); } }",
                "class Probe : UnityEditor.EditorWindow { "
                + "string Name() => nameof(ShowModalUtility); }",
                "class Probe { void Open(ESAdvancedDialogWindow window) { "
                + "window.Internal_OpenFromDialogService(true); } }",
                "class Probe { void Bind(ESAdvancedDialogWindow window) { "
                + "var open = window.Internal_OpenFromDialogService; } }",
                "using ModalHost = UnityEditor.EditorWindow; class Probe { "
                + "void Open() { typeof(ModalHost).GetMethod(\"ShowModalUtility\").Invoke(this, null); } }",
            };
            for (int i = 0; i < invalidSources.Length; i++)
            {
                int index = i;
                Assert.Throws<AssertionException>(() =>
                    AssertNoNativeEditorWindowModalPresentationReferences(
                        invalidSources[index],
                        "synthetic-native-modal-bypass-" + index));
            }
        }

        [Test]
        public void DialogCompatibilityScannerRejectsWhitespaceAliasesStaticImportsAndMethodGroups()
        {
            string[] invalidSources =
            {
                "class Probe { void Open(Request request) { ESAdvancedDialogWindow . Show(request); } }",
                "using Dialog = global::ES.ESAdvancedDialogWindow; class Probe { void Open(Request request) { Dialog.Show(request); } }",
                "using static ES.ESAdvancedDialogWindow; class Probe { void Open(Request request) { Show(request); } }",
                "class Probe { void Bind() { var show = ESAdvancedDialogWindow.ShowAsync; } }",
            };
            for (int i = 0; i < invalidSources.Length; i++)
            {
                int index = i;
                Assert.Throws<AssertionException>(
                    () => AssertNoLegacyAdvancedDialogCompatibilityBypass(
                        GetSearchableCSharpCode(invalidSources[index]),
                        "synthetic-dialog-" + index));
            }

            const string decoySource =
                "// ESAdvancedDialogWindow.Show(request);\n"
                + "class Probe { string text = \"using Dialog = ESAdvancedDialogWindow;\"; }";
            Assert.DoesNotThrow(
                () => AssertNoLegacyAdvancedDialogCompatibilityBypass(
                    GetSearchableCSharpCode(decoySource),
                    "synthetic-dialog-decoy"));
        }

        [Test]
        public void SectionIdentityUsesDurableGlobalIdsAndExplicitTransientFallback()
        {
            const string drawerPath =
                "Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionNavigatorDrawer.cs";
            UnityEngine.Object persistent = AssetDatabase.LoadMainAssetAtPath(drawerPath);
            Assert.IsNotNull(persistent, "Section drawer script asset must be loadable.");

            Type navigationContext = typeof(ESEditorSectionNavigatorDrawer).GetNestedType(
                "NavigationContext",
                BindingFlags.NonPublic);
            MethodInfo buildIdentity = navigationContext?.GetMethod(
                "BuildUnityTargetIdentity",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo buildManagedIdentity = navigationContext?.GetMethod(
                "BuildManagedTargetIdentity",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo buildDurableSelectionKey = navigationContext?.GetMethod(
                "BuildDurableSelectionKey",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo computeIdentityDigest = navigationContext?.GetMethod(
                "ComputeIdentityDigest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(buildIdentity);
            Assert.IsNotNull(buildManagedIdentity);
            Assert.IsNotNull(buildDurableSelectionKey);
            Assert.IsNotNull(computeIdentityDigest);

            string fixedDigest = (string)computeIdentityDigest.Invoke(
                null,
                new object[] { "abc" });
            Assert.AreEqual(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                fixedDigest,
                "section identity digest 必须是 UTF-8 输入的标准 SHA-256，始终输出 64 位小写 hex。");
            StringAssert.IsMatch(
                "^[0-9a-f]{64}$",
                fixedDigest,
                "section identity digest 不得输出大写、分隔符或截断值。");

            string persistentIdentity = (string)buildIdentity.Invoke(
                null,
                new object[] { persistent });
            Assert.AreEqual(
                "Global" + GlobalObjectId.GetGlobalObjectIdSlow(persistent),
                persistentIdentity,
                "持久资产必须使用跨 Reload/重启稳定的 GlobalObjectId。");

            ESWindowSleepLifetimeProbeWindow transient =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                Assert.AreEqual(
                    "Transient" + transient.GetInstanceID(),
                    (string)buildIdentity.Invoke(null, new object[] { transient }),
                    "未保存对象必须明确标记为仅当前域有效的 transient identity。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transient);
            }

            object firstManaged = new object();
            object secondManaged = new object();
            string firstManagedIdentity = (string)buildManagedIdentity.Invoke(
                null,
                new[] { firstManaged });
            Assert.AreEqual(
                firstManagedIdentity,
                (string)buildManagedIdentity.Invoke(null, new[] { firstManaged }),
                "同一 managed target 在当前域内必须保持稳定 identity。");
            Assert.AreNotEqual(
                firstManagedIdentity,
                (string)buildManagedIdentity.Invoke(null, new[] { secondManaged }),
                "不同 managed target 不得依赖可能碰撞的 RuntimeHelpers hash。");

            const string navigatorId = "tests.identity";
            string persistentSelectionKey = (string)buildDurableSelectionKey.Invoke(
                null,
                new object[] { typeof(ESEditorSectionNavigatorDrawer).FullName, persistentIdentity, navigatorId });
            Assert.IsFalse(string.IsNullOrEmpty(persistentSelectionKey));
            Assert.AreEqual(
                persistentSelectionKey,
                (string)buildDurableSelectionKey.Invoke(
                    null,
                    new object[]
                    {
                        typeof(ESEditorSectionNavigatorDrawer).FullName,
                        persistentIdentity,
                        navigatorId,
                    }),
                "持久对象必须得到可跨 PropertyTree 重建复用的稳定 SessionState key。");
            Assert.IsNull(buildDurableSelectionKey.Invoke(
                null,
                new object[] { "TransientType", "Transient123", navigatorId }));
            Assert.IsNull(buildDurableSelectionKey.Invoke(
                null,
                new object[] { "ManagedType", firstManagedIdentity, navigatorId }));
            Assert.IsNull(buildDurableSelectionKey.Invoke(
                null,
                new object[] { "MixedType", "MultiTransient:deadbeef", navigatorId }));

            AssertSourceContains(drawerPath, "identities.Sort(StringComparer.Ordinal)");
            AssertSourceContains(drawerPath, "GlobalObjectId.GetGlobalObjectIdSlow(target)");
            AssertSourceContains(drawerPath, "ConditionalWeakTable<object, ManagedIdentityToken>");
            AssertSourceContains(drawerPath, "identities[i].Length.ToString(CultureInfo.InvariantCulture)");
            AssertSourceContains(drawerPath, "TransientManaged");
            AssertSourceContains(drawerPath, "BuildDurableSelectionKey(");
            AssertSourceContains(drawerPath, "if (!string.IsNullOrEmpty(selectionKey))");
            AssertSourceContains(drawerPath, "if (!string.IsNullOrEmpty(visibilityKey))");
            AssertSourceExcludes(drawerPath, "TryGetGlobalObjectIdSlow");
            AssertSourceExcludes(drawerPath, "return \"Object\" + target.GetInstanceID()");
            AssertSourceExcludes(drawerPath, "RuntimeHelpers.GetHashCode(target)");
        }

        [Test]
        public void SectionIdentityRestoresAcrossPropertyTreeRebuildsWithoutTransientLeakage()
        {
            const string navigatorId = "tests.identity";
            string suffix = Guid.NewGuid().ToString("N");
            string firstPath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/__SectionIdentity_"
                + suffix
                + "_A.asset";
            string secondPath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/__SectionIdentity_"
                + suffix
                + "_B.asset";
            string thirdPath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/Tests/__SectionIdentity_"
                + suffix
                + "_C.asset";
            ESEditorSectionIdentityProbeAsset first =
                ScriptableObject.CreateInstance<ESEditorSectionIdentityProbeAsset>();
            ESEditorSectionIdentityProbeAsset second =
                ScriptableObject.CreateInstance<ESEditorSectionIdentityProbeAsset>();
            ESEditorSectionIdentityProbeAsset third =
                ScriptableObject.CreateInstance<ESEditorSectionIdentityProbeAsset>();
            ESEditorSectionIdentityProbeAsset transient =
                ScriptableObject.CreateInstance<ESEditorSectionIdentityProbeAsset>();
            object firstTree = null;
            object rebuiltTree = null;
            object firstOrderTree = null;
            object reverseOrderTree = null;
            object differentSelectionTree = null;
            object mixedTree = null;
            object transientTree = null;
            object managedTree = null;
            string persistentSelectionKey = null;
            string firstSelectionKey = null;
            string differentSelectionKey = null;

            try
            {
                AssetDatabase.CreateAsset(first, firstPath);
                AssetDatabase.CreateAsset(second, secondPath);
                AssetDatabase.CreateAsset(third, thirdPath);

                Type navigationContext = typeof(ESEditorSectionNavigatorDrawer).GetNestedType(
                    "NavigationContext",
                    BindingFlags.NonPublic);
                MethodInfo ensureInitialized = navigationContext?.GetMethod(
                    "EnsureInitialized",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo select = navigationContext?.GetMethod(
                    "Select",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo buildTargetIdentity = navigationContext?.GetMethod(
                    "BuildTargetIdentity",
                    BindingFlags.Static | BindingFlags.NonPublic);
                FieldInfo selectedId = navigationContext?.GetField(
                    "selectedId",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo selectionKey = navigationContext?.GetField(
                    "selectionKey",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(navigationContext);
                Assert.IsNotNull(ensureInitialized);
                Assert.IsNotNull(select);
                Assert.IsNotNull(buildTargetIdentity);
                Assert.IsNotNull(selectedId);
                Assert.IsNotNull(selectionKey);

                Type propertyTreeType = ensureInitialized.GetParameters()[0].ParameterType;
                MethodInfo[] propertyTreeCreateMethods = propertyTreeType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public);
                MethodInfo createSingleTree = propertyTreeCreateMethods.FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "Create", StringComparison.Ordinal)
                        || method.IsGenericMethodDefinition)
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(object)
                           && parameters[1].ParameterType.IsEnum;
                });
                MethodInfo createMultipleTree = propertyTreeCreateMethods.FirstOrDefault(method =>
                {
                    if (!string.Equals(method.Name, "Create", StringComparison.Ordinal)
                        || method.IsGenericMethodDefinition)
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(IList)
                           && parameters[1].ParameterType.IsEnum;
                });
                MethodInfo disposeTree = propertyTreeType.GetMethod(
                    "Dispose",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(createSingleTree);
                Assert.IsNotNull(createMultipleTree);
                Assert.IsNotNull(disposeTree);
                object serializationBackend = Enum.Parse(
                    createSingleTree.GetParameters()[1].ParameterType,
                    "Odin");

                object CreateSingleTree(object target)
                    => createSingleTree.Invoke(null, new[] { target, serializationBackend });

                object CreateMultipleTree(IList targets)
                    => createMultipleTree.Invoke(null, new object[] { targets, serializationBackend });

                object CreateContext(object tree)
                {
                    object context = Activator.CreateInstance(
                        navigationContext,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new object[] { navigatorId },
                        null);
                    Assert.IsNotNull(context);
                    ensureInitialized.Invoke(context, new object[] { tree });
                    return context;
                }

                firstTree = CreateSingleTree(first);
                object firstContext = CreateContext(firstTree);
                persistentSelectionKey = (string)selectionKey.GetValue(firstContext);
                Assert.IsFalse(string.IsNullOrEmpty(persistentSelectionKey));
                SessionState.EraseString(persistentSelectionKey);
                select.Invoke(firstContext, new object[] { "advanced" });
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(persistentSelectionKey, null));

                disposeTree.Invoke(firstTree, null);
                firstTree = null;
                rebuiltTree = CreateSingleTree(
                    AssetDatabase.LoadAssetAtPath<ESEditorSectionIdentityProbeAsset>(firstPath));
                object rebuiltContext = CreateContext(rebuiltTree);
                Assert.AreEqual(
                    "advanced",
                    selectedId.GetValue(rebuiltContext),
                    "重建 PropertyTree 必须从同一持久对象的 SessionState 恢复分区。");

                firstOrderTree = CreateMultipleTree(
                    new ESEditorSectionIdentityProbeAsset[] { first, second });
                reverseOrderTree = CreateMultipleTree(
                    new ESEditorSectionIdentityProbeAsset[] { second, first });
                string firstOrderIdentity = (string)buildTargetIdentity.Invoke(
                    null,
                    new object[] { firstOrderTree });
                string reverseOrderIdentity = (string)buildTargetIdentity.Invoke(
                    null,
                    new object[] { reverseOrderTree });
                StringAssert.StartsWith("MultiGlobal:", firstOrderIdentity);
                Assert.AreEqual(
                    firstOrderIdentity,
                    reverseOrderIdentity,
                    "同一持久多选集合的身份必须与 Unity 返回顺序无关。");

                object firstOrderContext = CreateContext(firstOrderTree);
                firstSelectionKey = (string)selectionKey.GetValue(firstOrderContext);
                Assert.IsFalse(string.IsNullOrEmpty(firstSelectionKey));
                SessionState.EraseString(firstSelectionKey);
                select.Invoke(firstOrderContext, new object[] { "advanced" });
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(firstSelectionKey, null));

                object reverseOrderContext = CreateContext(reverseOrderTree);
                Assert.AreEqual(
                    firstSelectionKey,
                    selectionKey.GetValue(reverseOrderContext),
                    "反序多选必须落到与正序相同的 SessionState key。");
                Assert.AreEqual(
                    "advanced",
                    selectedId.GetValue(reverseOrderContext),
                    "反序多选 PropertyTree 必须读回正序多选写入的 section。");

                differentSelectionTree = CreateMultipleTree(
                    new ESEditorSectionIdentityProbeAsset[] { first, third });
                string differentSelectionIdentity = (string)buildTargetIdentity.Invoke(
                    null,
                    new object[] { differentSelectionTree });
                Assert.AreNotEqual(
                    firstOrderIdentity,
                    differentSelectionIdentity,
                    "包含第三资产的不同多选集合必须得到隔离身份。");
                object differentSelectionContext = CreateContext(differentSelectionTree);
                differentSelectionKey = (string)selectionKey.GetValue(differentSelectionContext);
                Assert.IsFalse(string.IsNullOrEmpty(differentSelectionKey));
                Assert.AreNotEqual(
                    firstSelectionKey,
                    differentSelectionKey,
                    "不同持久多选集合不得共享 SessionState key。");
                Assert.AreEqual(
                    "summary",
                    selectedId.GetValue(differentSelectionContext),
                    "第一组选择 advanced 后，包含第三资产的另一组必须保持默认 section。");
                Assert.IsNull(
                    SessionState.GetString(differentSelectionKey, null),
                    "不同多选集合不得串读第一组的持久选择。");

                mixedTree = CreateMultipleTree(
                    new ESEditorSectionIdentityProbeAsset[] { first, transient });
                object mixedContext = CreateContext(mixedTree);
                Assert.IsNull(
                    selectionKey.GetValue(mixedContext),
                    "持久对象与未保存对象混选时不得生成 SessionState key。");
                select.Invoke(mixedContext, new object[] { "summary" });
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(persistentSelectionKey, null),
                    "mixed selection 不得覆盖持久对象的 SessionState。");

                transientTree = CreateSingleTree(transient);
                object transientContext = CreateContext(transientTree);
                Assert.IsNull(
                    selectionKey.GetValue(transientContext),
                    "单个未保存 Unity 对象不得生成 SessionState key。");
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(persistentSelectionKey, null),
                    "transient 选择前持久对象 canary 必须保持有效。");
                select.Invoke(transientContext, new object[] { "summary" });
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(persistentSelectionKey, null),
                    "单个未保存 Unity 对象不得覆盖持久对象的 SessionState。");

                managedTree = CreateSingleTree(new ESEditorSectionIdentityManagedProbe());
                object managedContext = CreateContext(managedTree);
                Assert.IsNull(
                    selectionKey.GetValue(managedContext),
                    "managed target 只能保留当前 PropertyTree 内的选择。");
                select.Invoke(managedContext, new object[] { "summary" });
                Assert.AreEqual(
                    "advanced",
                    SessionState.GetString(persistentSelectionKey, null),
                    "managed target 不得写入持久对象的 SessionState。");
            }
            finally
            {
                MethodInfo dispose = firstTree?.GetType().GetMethod("Dispose")
                    ?? rebuiltTree?.GetType().GetMethod("Dispose")
                    ?? firstOrderTree?.GetType().GetMethod("Dispose")
                    ?? reverseOrderTree?.GetType().GetMethod("Dispose")
                    ?? differentSelectionTree?.GetType().GetMethod("Dispose")
                    ?? mixedTree?.GetType().GetMethod("Dispose")
                    ?? transientTree?.GetType().GetMethod("Dispose")
                    ?? managedTree?.GetType().GetMethod("Dispose");
                if (managedTree != null)
                    dispose?.Invoke(managedTree, null);
                if (transientTree != null)
                    dispose?.Invoke(transientTree, null);
                if (mixedTree != null)
                    dispose?.Invoke(mixedTree, null);
                if (reverseOrderTree != null)
                    dispose?.Invoke(reverseOrderTree, null);
                if (differentSelectionTree != null)
                    dispose?.Invoke(differentSelectionTree, null);
                if (firstOrderTree != null)
                    dispose?.Invoke(firstOrderTree, null);
                if (rebuiltTree != null)
                    dispose?.Invoke(rebuiltTree, null);
                if (firstTree != null)
                    dispose?.Invoke(firstTree, null);
                if (!string.IsNullOrEmpty(persistentSelectionKey))
                {
                    SessionState.EraseString(persistentSelectionKey);
                    SessionState.EraseBool(persistentSelectionKey + ".directoryVisible");
                }
                if (!string.IsNullOrEmpty(firstSelectionKey))
                {
                    SessionState.EraseString(firstSelectionKey);
                    SessionState.EraseBool(firstSelectionKey + ".directoryVisible");
                }
                if (!string.IsNullOrEmpty(differentSelectionKey))
                {
                    SessionState.EraseString(differentSelectionKey);
                    SessionState.EraseBool(differentSelectionKey + ".directoryVisible");
                }
                AssetDatabase.DeleteAsset(thirdPath);
                AssetDatabase.DeleteAsset(secondPath);
                AssetDatabase.DeleteAsset(firstPath);
                if (transient != null)
                    UnityEngine.Object.DestroyImmediate(transient);
                if (first != null && !EditorUtility.IsPersistent(first))
                    UnityEngine.Object.DestroyImmediate(first);
                if (second != null && !EditorUtility.IsPersistent(second))
                    UnityEngine.Object.DestroyImmediate(second);
                if (third != null && !EditorUtility.IsPersistent(third))
                    UnityEngine.Object.DestroyImmediate(third);
            }
        }

        [Test]
        public void OwnerScopedPreviewOpenersCommitAndRestoreStableRelationships()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            const string cameraPath =
                "Assets/Scripts/ESLogic/Editor/Camera/ESCameraTrackPreviewWindow.cs";
            const string assetPreviewPath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs";
            const string trackInspectorPath =
                "Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTemporaryInspectorWindow.cs";
            const string trackToolbarPath =
                "Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTimerToolbar.cs";
            const string dialoguePath =
                "Assets/Scripts/ESLogic/Editor/World/ESWorldDialogueWorkbenchWindow.cs";
            const string worldBuilderPath =
                "Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs";
            AssertSourceContainsInMethodInOrder(
                cameraPath,
                "public static void Open(ESTrackViewWindow owner)",
                "if (owner == null)",
                "throw new ArgumentNullException(nameof(owner));",
                "GetWindow<ESCameraTrackPreviewWindow>()",
                "window.ESWindow_SetSleepOwnerOverride(owner);",
                "ESWindowFoundation.SetSleepOwner(");
            AssertSourceContains(cameraPath, "Open(owner);");
            AssertSourceExcludes(cameraPath, "Open();");
            AssertSourceExcludes(cameraPath, "ESTrackViewWindow owner = null");
            AssertSourceContains(
                cameraPath,
                "protected override ESWindowSleepLinkMode ESWindow_SleepLinkMode");
            AssertSourceContains(
                cameraPath,
                "ESWindow_SleepOwnerKey => ESTrackViewWindow.SleepOwnerKey;");
            AssertSourceExcludes(
                cameraPath,
                "public static void Open(EditorWindow owner = null)");
            AssertSourceContains(
                assetPreviewPath,
                "ESAssetPackageRecordPreviewWindow");
            AssertSourceContains(
                assetPreviewPath,
                "ESAssetPackageBakeWindow owner)");
            AssertSourceContainsInMethodInOrder(
                assetPreviewPath,
                "public static void Open(",
                "ESAssetPackageBakeWindow owner)",
                "if (owner == null)",
                "throw new ArgumentNullException(nameof(owner));",
                "GetWindow<ESAssetPackageRecordPreviewWindow>",
                "window.ESWindow_SetSleepOwnerOverride(owner);",
                "ESWindowFoundation.SetSleepOwner(");
            AssertSourceExcludes(
                assetPreviewPath,
                "EditorWindow owner)");
            AssertSourceContainsInMethodInOrder(
                assetPreviewPath,
                "protected override void ESWindow_OnFoundationBound()",
                "base.ESWindow_OnFoundationBound();",
                "ESWindowFoundation.ResolvePendingSleepOwners(SleepOwnerKey, this);");
            string assetPreviewSource = ReadSourceText(Path.Combine(
                projectRoot,
                assetPreviewPath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsFalse(
                Regex.IsMatch(
                    assetPreviewSource,
                    @"protected override void ESWindow_OnHostEnable\(\)\s*\{[^}]*ResolvePendingSleepOwners",
                    RegexOptions.CultureInvariant | RegexOptions.Singleline),
                "AssetPackage 只能在 Foundation 完成绑定后解析 Pending owner。");
            int assetOpenIndex = assetPreviewSource.IndexOf(
                "public static void Open(",
                StringComparison.Ordinal);
            int assetOpenEnd = assetPreviewSource.IndexOf(
                "public override GUIContent ESWindow_GetWindowGUIContent()",
                assetOpenIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(assetOpenIndex, 0);
            Assert.Greater(assetOpenEnd, assetOpenIndex);
            StringAssert.DoesNotContain(
                "RegisterPendingSleepOwner",
                assetPreviewSource.Substring(assetOpenIndex, assetOpenEnd - assetOpenIndex),
                "Asset Preview 普通 Open 不得伪装成 ReloadDomain Pending 恢复。");

            string trackInspectorSource = ReadSourceText(Path.Combine(
                projectRoot,
                trackInspectorPath.Replace('/', Path.DirectorySeparatorChar)));
            MatchCollection trackInspectorOpeners = Regex.Matches(
                trackInspectorSource,
                @"public\s+static\s+\w+\s+OpenFor\s*\((?<parameters>[^)]*ESTrackViewWindow\s+owner[^)]*)\)\s*\{(?<body>.*?return\s+OpenIndependent\([^;]+;\s*\})",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
            Assert.AreEqual(
                3,
                trackInspectorOpeners.Count,
                "三类 Track 临时 Inspector 都必须只接受具体 ESTrackViewWindow owner。");
            foreach (Match trackInspectorOpener in trackInspectorOpeners)
            {
                StringAssert.DoesNotContain(
                    "= null",
                    trackInspectorOpener.Groups["parameters"].Value);
                string body = trackInspectorOpener.Groups["body"].Value;
                int nullGuard = body.IndexOf("if (owner == null)", StringComparison.Ordinal);
                int rejection = body.IndexOf(
                    "throw new ArgumentNullException(nameof(owner));",
                    StringComparison.Ordinal);
                int openIndependent = body.IndexOf("OpenIndependent(", StringComparison.Ordinal);
                Assert.GreaterOrEqual(nullGuard, 0);
                Assert.Greater(rejection, nullGuard);
                Assert.Greater(openIndependent, rejection);
            }
            AssertSourceExcludes(trackInspectorPath, "EditorWindow owner = null");
            AssertSourceExcludes(trackInspectorPath, "ESTrackViewWindow owner = null");
            AssertSourceContainsInMethodInOrder(
                trackToolbarPath,
                "public static void OpenCurrentSkillDataInfoEditor(ESTrackViewWindow trackWindow)",
                "trackWindow = ESTrackViewWindow.window;",
                "if (trackWindow == null)",
                "Debug.LogWarning(",
                "return;",
                "ESTrackSkillDataTemporaryInspectorWindow.CloseCurrentWindow();",
                "ESTrackSkillDataTemporaryInspectorWindow.OpenFor(");
            AssertSourceContains(dialoguePath, "IESWindowSleepRelationshipState");
            AssertSourceContains(dialoguePath, "[SerializeField] private string serializedSleepOwnerKey");
            AssertSourceExcludes(dialoguePath, "ESWorldBuilderWorkbenchWindow owner = null");
            AssertSourceExcludes(dialoguePath, "ConfigureSleepOwner(");
            AssertSourceContains(dialoguePath, "private void ConfigureIndependentSleep()");
            AssertSourceContains(dialoguePath,
                "private void ConfigureFollowOwner(ESWorldBuilderWorkbenchWindow owner)");
            string dialogueSource = ReadSourceText(Path.Combine(
                projectRoot,
                dialoguePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.AreEqual(
                4,
                Regex.Matches(dialogueSource, @"public\s+static\s+void\s+OpenFor\s*\(")
                    .Count);
            MatchCollection ownerDialogueOpeners = Regex.Matches(
                dialogueSource,
                @"public\s+static\s+void\s+OpenFor\s*\((?<parameters>[^)]*ESWorldBuilderWorkbenchWindow\s+owner[^)]*)\)\s*\{(?<body>.*?window\.Focus\(\);\s*\})",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
            Assert.AreEqual(2, ownerDialogueOpeners.Count);
            foreach (Match ownerOpener in ownerDialogueOpeners)
            {
                StringAssert.DoesNotContain("= null", ownerOpener.Groups["parameters"].Value);
                string body = ownerOpener.Groups["body"].Value;
                int nullGuard = body.IndexOf("if (owner == null)", StringComparison.Ordinal);
                int rejection = body.IndexOf(
                    "throw new ArgumentNullException(nameof(owner));",
                    StringComparison.Ordinal);
                int create = body.IndexOf(
                    "GetWindow<ESWorldDialogueEditorWindow>",
                    StringComparison.Ordinal);
                int followOwner = body.IndexOf(
                    "window.ConfigureFollowOwner(owner);",
                    StringComparison.Ordinal);
                Assert.GreaterOrEqual(nullGuard, 0);
                Assert.Greater(rejection, nullGuard);
                Assert.Greater(create, rejection);
                Assert.Greater(followOwner, create);
            }
            AssertSourceContains(
                dialoguePath,
                "serializedSleepOwnerKey = ESWorldBuilderWorkbenchWindow.SleepOwnerKey;");
            AssertSourceContains(dialoguePath, "ESWindowFoundation.RegisterPendingSleepOwner(");
            AssertSourceContains(worldBuilderPath, "internal const string SleepOwnerKey");
            AssertSourceContainsInOrder(
                worldBuilderPath,
                "protected override void ESWindow_OnFoundationBound()",
                "ESWindowFoundation.ResolvePendingSleepOwners(");
        }

        [Test]
        public void AIBrainWindowPreservesBaseLifecycleContract()
        {
            const string path =
                "Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs";
            AssertSourceContainsInOrder(
                path,
                "protected override void OnEnable()",
                "base.OnEnable();");
            AssertSourceContainsInOrder(
                path,
                "ESAIBrainCoordinator.CapabilityDriftDetected -= OnCapabilityDriftDetected;",
                "base.OnDisable();");
            AssertSourceExcludes(path, "private void OnEnable()");
            AssertSourceExcludes(path, "private void OnDisable()");
        }

        [Test]
        public void AdvancedDialogRequiresExplicitOwnerOrDeclaredMainWorkspaceFallback()
        {
            Type dialogWindowType = typeof(ESAdvancedDialogWindow);
            MethodInfo validate = dialogWindowType.GetMethod(
                "ValidateRequest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(validate);

            var ownerless = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.owner.required",
                title = "Owner contract",
            };
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => validate.Invoke(null, new object[] { ownerless }));
            Assert.IsInstanceOf<ArgumentException>(exception.InnerException);
            StringAssert.Contains("显式 owner", exception.InnerException.Message);

            var fallback = new ESAdvancedDialogRequest
            {
                dialogId = "tests.dialog.owner.fallback",
                title = "Fallback contract",
                allowMainWorkspaceFallback = true,
            };
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { fallback }));
        }

        [Test]
        public void AdvancedDialogRejectsDestroyedExplicitOwner()
        {
            Type dialogWindowType = typeof(ESAdvancedDialogWindow);
            MethodInfo validate = dialogWindowType.GetMethod(
                "ValidateRequest",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(validate);

            EditorWindow owner = ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                UnityEngine.Object.DestroyImmediate(owner);
                var request = new ESAdvancedDialogRequest
                {
                    dialogId = "tests.dialog.owner.destroyed",
                    title = "Destroyed owner",
                    owner = owner,
                };
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => validate.Invoke(null, new object[] { request }));
                Assert.IsInstanceOf<ArgumentException>(exception.InnerException);
                StringAssert.Contains("已关闭", exception.InnerException.Message);
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AdvancedDialogServiceTreatsDestroyedOwnerAsInvalidUntilFallbackIsExplicit()
        {
            Type serviceType = typeof(ESAdvancedDialogWindow).Assembly
                .GetType("ES.ESDialogService");
            MethodInfo isOwnerInvalid = serviceType?.GetMethod(
                "IsOwnerInvalid",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(isOwnerInvalid);

            EditorWindow owner = ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                UnityEngine.Object.DestroyImmediate(owner);
                var request = new ESAdvancedDialogRequest
                {
                    dialogId = "tests.dialog.owner.monitor",
                    title = "Owner monitor",
                    owner = owner,
                };
                Assert.IsTrue((bool)isOwnerInvalid.Invoke(null, new object[] { request }));
                request.allowMainWorkspaceFallback = true;
                Assert.IsFalse((bool)isOwnerInvalid.Invoke(null, new object[] { request }));
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AdvancedDialogFirstScreenBrandingAndLifecycleGuardsRemainSourceLocked()
        {
            const string dialogPath =
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs";
            AssertSourceContains(dialogPath, "es-dialog-branded");
            AssertSourceContains(dialogPath, "ESDialogBrandMark");
            AssertSourceContains(dialogPath, "对话交互层");
            AssertSourceContains(dialogPath, "仅输入 / 确认");
            AssertSourceContainsInOrder(
                dialogPath,
                "if (state == PlayModeStateChange.ExitingEditMode",
                "Shutdown();");
            AssertSourceContainsInOrder(
                dialogPath,
                "beforeAssemblyReload -= Shutdown",
                "CompilationPipeline.compilationFinished -= OnCompilationFinished");
            AssertSourceContainsInMethodInOrder(
                dialogPath,
                "private void ScheduleInitialFocus()",
                "initialFocusSchedule?.Pause();",
                "initialFocusSchedule = rootVisualElement.schedule.Execute");
            AssertSourceContainsInMethodInOrder(
                dialogPath,
                "private void ReleaseWindowResources()",
                "initialFocusSchedule?.Pause();",
                "initialFocusSchedule = null;");
        }

        [Test]
        public void PresentationLifecycleCapturesEachBoundaryOnlyOnceAndResumesAfterFailure()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void CapturePlayModePreferences()",
                "CapturePlayModePreferences",
                "if (playModeBindingsSuspended)",
                "return;");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void CaptureAssemblyReloadPreferences()",
                "CaptureAssemblyReloadPreferences",
                "if (assemblyReloadPreferencesCaptured)",
                "return;");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void RecoverSemiSleepAfterFailedCompilation()",
                "RecoverSemiSleepAfterFailedCompilation",
                "bool resumed = ResumeWindowBindings();",
                "LoadSemiSleepPreferences(binding);");
            AssertSourceContains(presentationPath, "SuspendWindowBinding(binding);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void SuspendWindowBindings()",
                "SuspendWindowBinding(binding, true);",
                "PlayMode/reload suspension preserves the user's actual sleep");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "bool preserveSleepGeometry)",
                "StopTransientWindowVisuals(binding, !preserveSleepGeometry);",
                "if (!preserveSleepGeometry)\n                RestoreSemiSleep(binding, true, true);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void MarkWindowBindingResumed(WindowBinding binding)",
                "binding.lifecycleSuspended = false;",
                "binding.semiSleepOverlay.pickingMode = PickingMode.Position;");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "MarkWindowBindingResumed(pair.Key, binding);",
                "EnsureWindowOverlayScheduledVisuals(binding);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void EnsureWindowOverlayScheduledVisuals(WindowBinding binding)",
                "binding.lifecycleSuspended",
                "binding.host.schedule",
                "UpdateWindowOverlay(binding)");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "public static void SuspendWindow(EditorWindow window)",
                "OnDisable is not a user wake or a close confirmation.",
                "SuspendWindowBinding(binding, true);",
                "RefreshSemiSleepUpdateSubscription();");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void RestoreSemiSleep(",
                "if (binding.lifecycleSuspended && !forceLifecycleReset)",
                "if (!forceLifecycleReset\n                && (binding.semiSleepDragging");
            AssertSourceContains(
                presentationPath,
                "RunWindowTeardownStep(() => RestoreSemiSleep(binding, true, true));");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "public static bool RequestWindowWake(EditorWindow window)",
                "binding.lifecycleSuspended",
                "EditorApplication.isPlayingOrWillChangePlaymode",
                "BeginSemiSleepTransition(binding, false);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void BeginWindowActivation(WindowBinding binding)",
                "binding.lifecycleSuspended",
                "EditorApplication.isPlayingOrWillChangePlaymode",
                "ESWindowOpeningSweep.Play(binding.root);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void BeginSemiSleepTransition(WindowBinding binding, bool sleep)",
                "binding.lifecycleSuspended",
                "EditorApplication.isPlayingOrWillChangePlaymode",
                "binding.semiSleepDragging");
            AssertSourceContains(presentationPath, "ResumeWindowBindings();");
            AssertSourceExcludes(
                presentationPath,
                "AssemblyReloadEvents.beforeAssemblyReload += RestoreAllSemiSleepWindows");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void OnGlobalPlayModeStateChanged(PlayModeStateChange state)",
                "OnGlobalPlayModeStateChanged",
                "InstallGlobalEditorAdapters();",
                "EditorApplication.RepaintHierarchyWindow();");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static bool ResumeWindowBindings(bool resetRetryBudget = true)",
                "bool completedPlayModeRestore = playModeBindingsSuspended;",
                "playModeBindingsSuspended = false;",
                "if (completedPlayModeRestore)",
                "assemblyReloadPreferencesCaptured = false;");
        }

        [Test]
        [Test]
        public void DuplicateInstanceViolationWakesOnlyAfterLifecycleResume()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void MarkWindowBindingResumed(int id, WindowBinding binding)",
                "binding.lifecycleSuspended = false;",
                "if (binding.singleInstanceViolation && IsSleepingOrTargetingSleep(binding))",
                "RestoreSemiSleep(binding, true);");
        }

        public void DetachedWindowRootsUseTheSameDeterministicSuspendTeardown()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void OnWindowRootDetached(DetachFromPanelEvent evt)",
                "OnWindowRootDetached",
                "SuspendWindowBindingForPanelRetry(binding, binding.actionHosts);",
                "QueueResumeWindowBindingsRetry();");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void SuspendWindowBinding(WindowBinding binding)",
                "SuspendWindowBinding",
                "if (!binding.lifecycleSuspended)",
                "CaptureWindowPreferencesForSuspend(binding);",
                "binding.lifecycleSuspended = true;",
                "UnregisterWindowCallbacks(binding);");
            AssertSourceContains(
                presentationPath,
                "windowBindingsByRoot.Remove(binding.root)");
        }

        [Test]
        public void SleepOwnerTransitionsUseStrictTuplesAndFailureIsolatedCloseSnapshot()
        {
            const string presentationPath =
                "Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs";
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "public static bool SetWindowSleepOwner(",
                "switch (mode)",
                "case ES.ESWindowSleepLinkMode.Independent:",
                "if (owner != null)",
                "ClearPendingSleepOwner(child);",
                "ClearWindowSleepOwner(child);",
                "case ES.ESWindowSleepLinkMode.FollowOwner:",
                "case ES.ESWindowSleepLinkMode.OwnedSurface:",
                "if (owner == null || child == owner)",
                "default:");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void DetachWindowSleepOwnerCore(WindowBinding binding)",
                "bool ownerForcedSleep = binding.sleepOwnerForcedSleep;",
                "bool wasOwnedSurface = binding.sleepLinkMode == ES.ESWindowSleepLinkMode.OwnedSurface;",
                "binding.sleepLinkMode = ES.ESWindowSleepLinkMode.Independent;",
                "RestoreOwnedSurfaceSleepCapability(binding);",
                "RestoreSemiSleep(binding, true);",
                "RefreshSemiSleepControls(binding);");
            AssertSourceContainsInMethodInOrder(
                presentationPath,
                "private static void DetachOwnedSleepRelationships(",
                "List<WindowBinding> ownedChildren = null;",
                "ownedChildren.Add(child);",
                "for (int i = 0; i < ownedChildren.Count; i++)",
                "DetachWindowSleepOwnerCore(child);",
                "try",
                "relationshipState.DetachSleepOwnerAfterOwnerClose();",
                "catch (Exception exception)");
        }

        [Test]
        public void PresentationStateApisDoNotImplicitlyBindUnknownWindows()
        {
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "测试前窗口必须保持未显式接入状态。");

                using (IDisposable lease = ESEditorPresentation.BeginWindowBusy(
                           window,
                           "不应隐式接入"))
                {
                    Assert.IsFalse(
                        ESWindowFoundation.IsBound(window),
                        "BeginWindowBusy 不得为未知窗口创建 Presentation binding。");
                }

                ESEditorPresentation.NotifyWindow(
                    window,
                    "不应隐式接入",
                    ESStatusKind.Info,
                    focus: false);
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "NotifyWindow 不得为未知窗口创建 Presentation binding。");

                Assert.IsFalse(
                    ESWindowFoundation.TrySetPresentationShortTitle(window, "未知"),
                    "未绑定窗口不能写入 Presentation 页签标签。");
                Assert.IsFalse(
                    ESEditorPresentation.SetWindowSemiSleepDockBounds(
                        window,
                        new Rect(20f, 20f, 100f, 100f)),
                    "未绑定窗口不能写入半休眠落点。");
                ESEditorPresentation.PulseWindow(window, ESStatusKind.Modified);
                Assert.IsFalse(
                    ESWindowFoundation.IsBound(window),
                    "Presentation 状态 API 不得为未知窗口创建 binding。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DirectProductionWindowCreationIsRestrictedToGovernedExceptions()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs|ESAdvancedDialogWindow",
                "Assets/Plugins/ES/Editor/EditorTools/ESCompactChoicePopup.cs|ESCompactChoicePopup",
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs|ESWindowSleepBenchmarkProbeWindow",
                "Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs|ESWorkbenchPopupWindow"
            };
            var allowedDynamicTypeOpeners = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs|EditorWindow.GetWindow(Type)"
            };
            var editorWindowTypeNames = new HashSet<string>(
                TypeCache.GetTypesDerivedFrom<EditorWindow>().Select(type => type.Name),
                StringComparer.Ordinal);
            editorWindowTypeNames.Add(nameof(EditorWindow));
            editorWindowTypeNames.Add("OdinEditorWindow");
            foreach (string path in EnumerateESOwnedEditorSources(projectRoot))
            {
                string relative = path.Substring(projectRoot.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (relative.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0
                    || relative.IndexOf("/Obsolete/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string source = ReadSourceText(path);
                string commentFreeSource = StripCSharpComments(source);
                string searchableCode =
                    MaskCSharpStringAndCharacterLiterals(commentFreeSource);
                AssertNoWindowFactoryTokensHiddenInLiterals(
                    commentFreeSource,
                    searchableCode,
                    relative);
                Dictionary<string, string> aliases =
                    ExtractSourceTypeAliases(searchableCode);
                foreach (string typeName in FindGenericDirectWindowCreations(
                             searchableCode,
                             editorWindowTypeNames,
                             aliases))
                {
                    string key = relative + "|" + typeName;
                    Assert.IsTrue(
                        allowed.Remove(key),
                        "发现未受治理的 EditorWindow 直接创建入口：" + key);
                }
                AssertNoDynamicWindowCreationBypass(
                    searchableCode,
                    relative,
                    editorWindowTypeNames,
                    aliases);

                foreach (string receiver in FindDynamicGetWindowReceivers(
                             searchableCode,
                             editorWindowTypeNames,
                             aliases))
                {
                    string key = relative + "|" + receiver + ".GetWindow(Type)";
                    Assert.IsTrue(
                        allowedDynamicTypeOpeners.Remove(key),
                        "禁止在生产代码中通过运行时 Type 打开 EditorWindow；请走具体类型的单实例入口："
                        + key);
                }
            }

            Assert.AreEqual(
                0,
                allowed.Count + allowedDynamicTypeOpeners.Count,
                "受治理例外清单已经漂移："
                + string.Join(", ", allowed.Concat(allowedDynamicTypeOpeners)));
        }

        [Test]
        public void DirectWindowCreationScannerRejectsAliasesRuntimeFactoriesAndSplitCasts()
        {
            var windowTypeNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "EditorWindow",
                "ESCmdAgentWindow",
            };
            const string aliasSource =
                "using Tool = ES.ESCmdAgentWindow;\n"
                + "class Probe { void Open() { CreateInstance<Tool>(); } }\n";
            CollectionAssert.AreEqual(
                new[] { "ESCmdAgentWindow" },
                FindGenericDirectWindowCreations(
                    GetSearchableCSharpCode(aliasSource),
                    windowTypeNames));

            string[] invalidSources =
            {
                "class Probe { void Open(Type windowType) { EditorWindow.CreateWindow(windowType); } }",
                "class Probe { void Open(Type windowType) { ESCmdAgentWindow.CreateWindow(windowType); } }",
                "using @Tool = ES.@ESCmdAgentWindow; class Probe { void Open(Type windowType) { @Tool.CreateWindow(windowType); } }",
                "class Probe { void Open() { Type target = typeof(ESCmdAgentWindow); object raw = ScriptableObject.CreateInstance(target); } }",
                "class Probe { void Open(Type target) { object raw = ScriptableObject.CreateInstance(target); var window = raw as ESCmdAgentWindow; } }",
                "using Tool = ES.ESCmdAgentWindow; class Probe { void Open(Type target) { var raw = ScriptableObject.CreateInstance(target); if (raw is Tool window) window.Show(); } }",
            };
            for (int i = 0; i < invalidSources.Length; i++)
            {
                int index = i;
                Assert.Throws<AssertionException>(
                    () => AssertNoDynamicWindowCreationBypass(
                        GetSearchableCSharpCode(invalidSources[index]),
                        "synthetic-" + index,
                        windowTypeNames));
            }

            const string decoySource =
                "// CreateInstance<ESCmdAgentWindow>();\n"
                + "class Probe { string text = \"EditorWindow.CreateWindow(windowType)\"; }";
            Assert.IsEmpty(FindGenericDirectWindowCreations(
                GetSearchableCSharpCode(decoySource),
                windowTypeNames));
            Assert.DoesNotThrow(
                () => AssertNoDynamicWindowCreationBypass(
                    GetSearchableCSharpCode(decoySource),
                    "synthetic-decoy",
                    windowTypeNames));

            string[] invalidGetWindowSources =
            {
                "class Probe { void Open(Type target) { EditorWindow . GetWindow ( target ); } }",
                "using WindowBase = UnityEditor.EditorWindow; class Probe { void Open(Type target) { WindowBase.GetWindow(target); } }",
                "class Probe { void Open(Type target) { ESCmdAgentWindow.GetWindow(target, true, \"Probe\"); } }",
            };
            for (int i = 0; i < invalidGetWindowSources.Length; i++)
            {
                CollectionAssert.IsNotEmpty(
                    FindDynamicGetWindowReceivers(
                        GetSearchableCSharpCode(invalidGetWindowSources[i]),
                        windowTypeNames),
                    "动态 GetWindow(Type) 扫描器不得被空白、别名或派生 receiver 绕过："
                    + i);
            }
        }

        [Test]
        public void WindowFactoryScannerRejectsInterpolatedExecutionAndDialogMethodGroups()
        {
            string[] hiddenFactorySources =
            {
                "class Probe { string Open() => $\"{CreateInstance<ESCmdAgentWindow>()}\"; }",
                "class Probe { string Open() => $\"{ESAdvancedDialogWindow.Create(null, null)}\"; }",
            };
            for (int i = 0; i < hiddenFactorySources.Length; i++)
            {
                string commentFree = StripCSharpComments(hiddenFactorySources[i]);
                string searchable = MaskCSharpStringAndCharacterLiterals(commentFree);
                Assert.Throws<AssertionException>(() =>
                    AssertNoWindowFactoryTokensHiddenInLiterals(
                        commentFree,
                        searchable,
                        "synthetic-interpolation-" + i));
            }

            Assert.IsTrue(AdvancedDialogCreateMethodGroupPattern.IsMatch(
                "class Probe { object factory = ESAdvancedDialogWindow.Create; }"));
            Assert.Throws<AssertionException>(() => AssertAdvancedDialogCreateSurfaceIsClosed(
                "sealed class ESAdvancedDialogWindow { "
                + "internal static ESAdvancedDialogWindow Create() => null; "
                + "object Capture() { return Create; } }"));
        }

        [Test]
        public void UnbindWindowBindingReleasesWindowAndVisualTreeReferences()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var root = new VisualElement();
            var host = new VisualElement();
            var overlay = new VisualElement();
            root.Add(host);
            root.Add(overlay);

            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", root);
            SetField(bindingType, binding, "host", host);
            SetField(bindingType, binding, "accentLine", new VisualElement());
            SetField(bindingType, binding, "sweep", new VisualElement());
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            SetField(bindingType, binding, "animation", new ESThrowingScheduledItem());
            overlay.userData = binding;

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            int id = window.GetInstanceID();
            bindings[id] = binding;
            bindingsByRoot[root] = binding;

            try
            {
                MethodInfo unbind = presentationType.GetMethod(
                    "UnbindWindowBinding",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbind);
                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("ES teardown schedule failure"));
                unbind.Invoke(null, new[] { (object)id, binding, true, false });

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
                Assert.IsNull(host.parent);
                Assert.IsNull(overlay.parent);
                Assert.IsNull(overlay.userData);
                AssertFieldIsNull(bindingType, binding, "window");
                AssertFieldIsNull(bindingType, binding, "root");
                AssertFieldIsNull(bindingType, binding, "host");
                AssertFieldIsNull(bindingType, binding, "semiSleepOverlay");
                AssertFieldIsNull(bindingType, binding, "animation");
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnbindAllContinuesAfterMidTeardownExceptionAndClearsGlobalState()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(
                presentationType,
                "windowBindingsByRoot");
            IDictionary ownerBindings = GetStaticDictionary(
                presentationType,
                "sleepOwnerBindingsByKey");
            IList pendingOwners = presentationType.GetField(
                    "pendingSleepOwners",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IList;
            var exhaustedIds = presentationType.GetField(
                    "resumeBindingsRetryExhaustedWindowIds",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as HashSet<int>;
            Assert.IsNotNull(pendingOwners);
            Assert.IsNotNull(exhaustedIds);

            DictionaryEntry[] previousBindings = bindings.Keys.Cast<object>()
                .Select(key => new DictionaryEntry(key, bindings[key]))
                .ToArray();
            DictionaryEntry[] previousRoots = bindingsByRoot.Keys.Cast<object>()
                .Select(key => new DictionaryEntry(key, bindingsByRoot[key]))
                .ToArray();
            DictionaryEntry[] previousOwners = ownerBindings.Keys.Cast<object>()
                .Select(key => new DictionaryEntry(key, ownerBindings[key]))
                .ToArray();
            object[] previousPending = pendingOwners.Cast<object>().ToArray();
            int[] previousExhausted = exhaustedIds.ToArray();
            ESWindowSleepLifetimeProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var firstRoot = new VisualElement();
            var secondRoot = new VisualElement();
            object firstBinding = Activator.CreateInstance(bindingType, true);
            object secondBinding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, firstBinding, "window", first);
            SetField(bindingType, firstBinding, "root", firstRoot);
            SetField(bindingType, firstBinding, "animation", new ESThrowingScheduledItem());
            SetField(bindingType, secondBinding, "window", second);
            SetField(bindingType, secondBinding, "root", secondRoot);
            int firstId = first.GetInstanceID();
            int secondId = second.GetInstanceID();

            try
            {
                bindings.Clear();
                bindingsByRoot.Clear();
                ownerBindings.Clear();
                pendingOwners.Clear();
                exhaustedIds.Clear();
                bindings[firstId] = firstBinding;
                bindings[secondId] = secondBinding;
                bindingsByRoot[firstRoot] = firstBinding;
                bindingsByRoot[secondRoot] = secondBinding;
                ownerBindings["ES.Tests.UnbindAllFailure"] = firstBinding;
                pendingOwners.Add(null);
                exhaustedIds.Add(firstId);
                exhaustedIds.Add(secondId);

                MethodInfo unbindAll = presentationType.GetMethod(
                    "UnbindAllWindowBindings",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbindAll);
                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("ES teardown schedule failure"));
                Assert.DoesNotThrow(() => unbindAll.Invoke(null, null));

                Assert.AreEqual(0, bindings.Count);
                Assert.AreEqual(0, bindingsByRoot.Count);
                Assert.AreEqual(0, ownerBindings.Count);
                Assert.AreEqual(0, pendingOwners.Count);
                Assert.AreEqual(0, exhaustedIds.Count);
                AssertFieldIsNull(bindingType, secondBinding, "window");
            }
            finally
            {
                bindings.Clear();
                bindingsByRoot.Clear();
                ownerBindings.Clear();
                pendingOwners.Clear();
                exhaustedIds.Clear();
                foreach (DictionaryEntry pair in previousBindings)
                    bindings[pair.Key] = pair.Value;
                foreach (DictionaryEntry pair in previousRoots)
                    bindingsByRoot[pair.Key] = pair.Value;
                foreach (DictionaryEntry pair in previousOwners)
                    ownerBindings[pair.Key] = pair.Value;
                foreach (object pending in previousPending)
                    pendingOwners.Add(pending);
                exhaustedIds.UnionWith(previousExhausted);
                InvokePrivate<object>(presentationType, "RefreshSemiSleepUpdateSubscription");
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ClearPendingSleepOwnerRemovesDestroyedWindowReference()
        {
            const string ownerKey = "ES.Tests.WindowSleepLifetime";
            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(child, ownerKey));
                UnityEngine.Object.DestroyImmediate(child);

                ESWindowFoundation.ClearPendingSleepOwner(child);
                AssertPendingOwnerKeyAbsent(ownerKey);
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                if (child != null)
                    UnityEngine.Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void RebuildUnbindPreservesPendingOwnerUntilPermanentClose()
        {
            const string ownerKey = "ES.Tests.PendingOwnerRebuild";
            ESWindowSleepLifetimeProbeWindow child =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            ESWindowSleepLifetimeProbeWindow owner =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            try
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(child, ownerKey));

                ESWindowFoundation.Unbind(child);
                Assert.IsTrue(
                    PendingOwnerKeyExists(ownerKey),
                    "VisualTree 重建解绑不得丢失 Pending FollowOwner 恢复意图。");
                Assert.AreEqual(
                    1,
                    ESWindowFoundation.ResolvePendingSleepOwners(ownerKey, owner),
                    "Pending 子窗口内容重建后仍必须能按稳定 ownerKey 恢复。");
                Assert.AreEqual(
                    ESWindowSleepLinkMode.FollowOwner,
                    ESWindowFoundation.GetSleepLinkMode(child));

                ESWindowFoundation.Close(child);
                AssertPendingOwnerKeyAbsent(ownerKey);
            }
            finally
            {
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                ESWindowFoundation.Close(owner);
                if (child != null)
                    UnityEngine.Object.DestroyImmediate(child);
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void UnbindWindowAcceptsDestroyedUnityObjectReference()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            var root = new VisualElement();
            var overlay = new VisualElement();
            root.Add(overlay);

            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", root);
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            overlay.userData = binding;

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            int id = window.GetInstanceID();
            bindings[id] = binding;
            bindingsByRoot[root] = binding;

            try
            {
                UnityEngine.Object.DestroyImmediate(window);
                Assert.IsFalse(ReferenceEquals(window, null));
                Assert.IsTrue(window == null);

                ESWindowFoundation.Close(window);

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
                Assert.IsNull(overlay.parent);
                Assert.IsNull(overlay.userData);
                AssertFieldIsNull(bindingType, binding, "window");
                AssertFieldIsNull(bindingType, binding, "root");
                AssertFieldIsNull(bindingType, binding, "semiSleepOverlay");
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnbindNullBindingClearsOrphanedRootEntries()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary bindingsByRoot = GetStaticDictionary(presentationType, "windowBindingsByRoot");
            var root = new VisualElement();
            const int id = int.MinValue + 173;
            bindings[id] = null;
            bindingsByRoot[root] = null;

            try
            {
                MethodInfo unbind = presentationType.GetMethod(
                    "UnbindWindowBinding",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbind);
                unbind.Invoke(null, new object[] { id, null, true, false });

                Assert.IsFalse(bindings.Contains(id));
                Assert.IsFalse(bindingsByRoot.Contains(root));
            }
            finally
            {
                bindings.Remove(id);
                bindingsByRoot.Remove(root);
            }
        }

        [Test]
        public void EdgeTabTitleUsesAvailableLengthForBothOrientations()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType(
                "WindowBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            object binding = Activator.CreateInstance(bindingType, true);
            var overlay = new VisualElement();
            var title = new Label("轨道编辑器完整标题");
            var icon = new Image();
            SetField(bindingType, binding, "semiSleepOverlay", overlay);
            SetField(bindingType, binding, "semiSleepTitleLabel", title);
            SetField(bindingType, binding, "semiSleepIcon", icon);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.EdgeTab);

            MethodInfo apply = presentationType.GetMethod(
                "ApplySemiSleepOverlayState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(apply);

            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Top);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(FlexDirection.Column, overlay.style.flexDirection.value);
            Assert.AreEqual(WhiteSpace.Normal, title.style.whiteSpace.value);
            Assert.AreEqual(TextOverflow.Clip, title.style.textOverflow.value);
            Assert.AreEqual(36f, title.style.maxWidth.value.value, 0.01f);
            Assert.AreEqual(30f, title.style.maxHeight.value.value, 0.01f);

            SetField(bindingType, binding, "visualState", ESWindowVisualState.EdgeTabHover);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(156f, title.style.maxHeight.value.value, 0.01f);

            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Left);
            apply.Invoke(null, new object[] { binding, null });
            Assert.AreEqual(FlexDirection.Row, overlay.style.flexDirection.value);
            Assert.AreEqual(WhiteSpace.NoWrap, title.style.whiteSpace.value);
            Assert.AreEqual(TextOverflow.Ellipsis, title.style.textOverflow.value);
            Assert.AreEqual(164f, title.style.maxWidth.value.value, 0.01f);
        }

        [Test]
        public void PersistedSleepRestoreRemainsPendingWhileBusy()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", new VisualElement());
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "restorePersistedSleepScheduled", true);
            SetField(bindingType, binding, "busyCount", 1);

            try
            {
                bool restored = InvokePrivate<bool>(
                    presentationType,
                    "TryRestorePersistedSemiSleepGeometry",
                    binding);

                Assert.IsFalse(restored);
                Assert.IsTrue(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.IsTrue(InvokePrivate<bool>(
                    presentationType,
                    "HasPersistedSleepRuntimeState",
                    binding));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PermanentlyDisallowedPersistedSleepNormalizesToActivePanel()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", false);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                bool restored = InvokePrivate<bool>(
                    presentationType,
                    "TryRestorePersistedSemiSleepGeometry",
                    binding);

                Assert.IsFalse(restored);
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.IsFalse(GetFieldValue<bool>(bindingType, binding, "semiSleepTarget"));
                Assert.AreEqual(
                    ESWindowVisualState.ActivePanel,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "visualState"));
                Assert.IsFalse(InvokePrivate<bool>(
                    presentationType,
                    "HasPersistedSleepRuntimeState",
                    binding));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PersistedSleepTileRestoreSynchronizesNativeGeometryAndRestartsPromotion()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            window.minSize = new Vector2(320f, 220f);
            window.maxSize = new Vector2(1600f, 1200f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "restorePersistedSleepScheduled", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(bindingType, binding, "semiSleepDockBounds", new Rect(250f, 190f, 100f, 100f));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            double startedAt = EditorApplication.timeSinceStartup;

            try
            {
                InvokePrivate<object>(
                    presentationType,
                    "RestorePersistedSemiSleepGeometry",
                    binding);

                Assert.AreEqual(
                    ESWindowVisualState.SleepTile,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "visualState"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "semiSleeping"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepScheduled"));
                Assert.AreEqual(100f, window.position.width, 0.01f);
                Assert.AreEqual(100f, window.position.height, 0.01f);
                Assert.GreaterOrEqual(
                    GetFieldValue<double>(bindingType, binding, "sleepTileIdleStartedAt"),
                    startedAt);
                Assert.Greater(
                    GetFieldValue<double>(bindingType, binding, "persistedSleepGeometryVerifyUntil"),
                    EditorApplication.timeSinceStartup);
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PersistedEdgeTabRestoreRepairsLaterNativeFrameWriteback()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect awake = new Rect(320f, 220f, 960f, 640f);
            window.position = awake;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "awakeBounds", awake);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.EdgeTab);
            SetField(bindingType, binding, "edge", ESEditorPresentation.ESWindowEdge.Right);
            SetField(bindingType, binding, "edgeOffset", 120f);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(
                    presentationType,
                    "RestorePersistedSemiSleepGeometry",
                    binding);
                Rect expected = window.position;
                Assert.AreEqual(56f, expected.width, 0.01f);
                Assert.AreEqual(44f, expected.height, 0.01f);

                window.position = awake;
                InvokePrivate<object>(
                    presentationType,
                    "RepairSettledSemiSleepGeometry",
                    binding);

                Assert.AreEqual(expected.x, window.position.x, 0.01f);
                Assert.AreEqual(expected.y, window.position.y, 0.01f);
                Assert.AreEqual(expected.width, window.position.width, 0.01f);
                Assert.AreEqual(expected.height, window.position.height, 0.01f);
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LegacySemiSleepPreferencesRemainCompatible()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(180f, 140f, 840f, 560f);
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(preferenceType, saved, "schemaVersion", 0);
            SetField(preferenceType, saved, "allowSemiSleep", true);
            SetField(preferenceType, saved, "pinned", true);
            SetField(preferenceType, saved, "sleeping", true);
            SetField(preferenceType, saved, "visualState", (int)ESWindowVisualState.EdgeTab);
            SetField(preferenceType, saved, "edge", (int)ESEditorPresentation.ESWindowEdge.Right);
            SetField(preferenceType, saved, "edgeOffset", 96f);
            SetField(preferenceType, saved, "awakeBounds", new Rect(200f, 160f, 900f, 620f));
            SetField(preferenceType, saved, "dockBounds", new Rect(260f, 220f, 100f, 100f));
            SetField(preferenceType, saved, "hasDockBounds", true);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsTrue(applied);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "allowSemiSleep"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "pinned"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "restorePersistedSleepOnBind"));
                Assert.AreEqual(
                    ESWindowVisualState.EdgeTab,
                    GetFieldValue<ESWindowVisualState>(bindingType, binding, "transitionTargetState"));
                Assert.AreEqual(96f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "hasSemiSleepDockBounds"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowHealthSnapshotUsesExistingBindingsAndReportsContractDrift()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            ESWindowPresentationHealthSnapshot before =
                ESEditorPresentation.CaptureWindowHealthSnapshot();

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(280f, 200f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", window.rootVisualElement);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(
                bindingType,
                binding,
                "semiSleepDockBounds",
                new Rect(320f, 240f, 100f, 100f));
            int windowId = window.GetInstanceID();
            bindings[windowId] = binding;

            try
            {
                ESWindowPresentationHealthSnapshot after =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();

                Assert.AreEqual(before.BindingSlotCount + 1, after.BindingSlotCount);
                Assert.AreEqual(before.LiveWindowCount + 1, after.LiveWindowCount);
                Assert.AreEqual(before.SleepSupportedCount + 1, after.SleepSupportedCount);
                Assert.AreEqual(before.SleepingCount + 1, after.SleepingCount);
                Assert.AreEqual(
                    before.MissingSystemHostCount + 1,
                    after.MissingSystemHostCount);
                Assert.AreEqual(
                    before.GeometryMismatchCount + 1,
                    after.GeometryMismatchCount);
                Assert.AreEqual(
                    before.FirstIssueWindowType ?? window.GetType().FullName,
                    after.FirstIssueWindowType);

                SetField(bindingType, binding, "lifecycleSuspended", true);
                ESWindowPresentationHealthSnapshot suspended =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();
                Assert.AreEqual(
                    before.MissingSystemHostCount,
                    suspended.MissingSystemHostCount,
                    "Suspend 会主动移除 System host，不得被健康快照误报为合同漂移。");
                Assert.AreEqual(
                    before.GeometryMismatchCount,
                    suspended.GeometryMismatchCount,
                    "Suspend 期间的临时原生几何不得被报告为稳定几何漂移。");
            }
            finally
            {
                bindings.Remove(windowId);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowHealthSnapshotReportsDuplicateConcreteTypesFromExistingBindings()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            ESWindowPresentationHealthSnapshot before =
                ESEditorPresentation.CaptureWindowHealthSnapshot();

            ESWindowDuplicateHealthProbeWindow first =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            ESWindowDuplicateHealthProbeWindow second =
                ScriptableObject.CreateInstance<ESWindowDuplicateHealthProbeWindow>();
            object firstBinding = Activator.CreateInstance(bindingType, true);
            object secondBinding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, firstBinding, "window", first);
            SetField(bindingType, firstBinding, "root", first.rootVisualElement);
            SetField(bindingType, secondBinding, "window", second);
            SetField(bindingType, secondBinding, "root", second.rootVisualElement);
            int firstId = first.GetInstanceID();
            int secondId = second.GetInstanceID();
            bindings[firstId] = firstBinding;
            bindings[secondId] = secondBinding;

            try
            {
                ESWindowPresentationHealthSnapshot after =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();

                Assert.AreEqual(before.LiveWindowCount + 2, after.LiveWindowCount);
                Assert.AreEqual(
                    before.DuplicateWindowInstanceCount + 1,
                    after.DuplicateWindowInstanceCount);
                Assert.IsTrue(after.HasIssues);
            }
            finally
            {
                bindings.Remove(firstId);
                bindings.Remove(secondId);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ExhaustedResumeHealthRecoversWhenOnlyFailedWindowCloses()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            FieldInfo exhaustedField = presentationType.GetField(
                "resumeBindingsRetryExhaustedWindowIds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(exhaustedField);
            var exhaustedWindowIds = exhaustedField.GetValue(null) as HashSet<int>;
            Assert.IsNotNull(exhaustedWindowIds);
            int[] previousExhaustedWindowIds = exhaustedWindowIds.ToArray();

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "root", window.rootVisualElement);
            int windowId = window.GetInstanceID();
            bindings[windowId] = binding;

            try
            {
                exhaustedWindowIds.Clear();
                exhaustedWindowIds.Add(windowId);
                Assert.IsFalse(
                    ESEditorPresentation.CaptureWindowHealthSnapshot().ResumeRetryExhausted,
                    "仍在运行的绑定不得因旧耗尽记录被报告为 suspended 恢复失败。");
                exhaustedWindowIds.Clear();
                InvokePrivate<object>(
                    presentationType,
                    "RecordExhaustedResumeWindowBindings");
                Assert.IsFalse(exhaustedWindowIds.Contains(windowId));
                exhaustedWindowIds.Clear();
                Assert.IsFalse(
                    ESEditorPresentation.CaptureWindowHealthSnapshot().ResumeRetryExhausted);

                SetField(bindingType, binding, "lifecycleSuspended", true);
                InvokePrivate<object>(
                    presentationType,
                    "RecordExhaustedResumeWindowBindings");
                Assert.IsTrue(exhaustedWindowIds.Contains(windowId));
                exhaustedWindowIds.RemoveWhere(id => id != windowId);
                ESWindowPresentationHealthSnapshot snapshot =
                    ESEditorPresentation.CaptureWindowHealthSnapshot();

                Assert.IsTrue(snapshot.ResumeRetryExhausted);
                Assert.IsTrue(snapshot.HasIssues);
                Assert.AreEqual(
                    "Presentation 恢复重试耗尽",
                    snapshot.FirstIssueWindowType);

                InvokePrivate<object>(
                    presentationType,
                    "MarkWindowBindingResumed",
                    windowId,
                    binding);
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "lifecycleSuspended"));
                Assert.IsFalse(exhaustedWindowIds.Contains(windowId));
                Assert.IsFalse(
                    ESEditorPresentation.CaptureWindowHealthSnapshot().ResumeRetryExhausted);

                SetField(bindingType, binding, "lifecycleSuspended", true);
                InvokePrivate<object>(
                    presentationType,
                    "RecordExhaustedResumeWindowBindings");
                exhaustedWindowIds.RemoveWhere(id => id != windowId);

                MethodInfo unbind = presentationType.GetMethod(
                    "UnbindWindowBinding",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(unbind);
                unbind.Invoke(null, new[] { (object)windowId, binding, true, false });

                Assert.IsFalse(exhaustedWindowIds.Contains(windowId));
                Assert.IsFalse(
                    ESEditorPresentation.CaptureWindowHealthSnapshot().ResumeRetryExhausted);
                exhaustedWindowIds.Add(windowId);
                Assert.IsFalse(
                    ESEditorPresentation.CaptureWindowHealthSnapshot().ResumeRetryExhausted,
                    "已解绑窗口的陈旧 ID 不得污染健康快照。");
                ESEditorPresentation.UnbindWindow(window, true);
                Assert.IsFalse(
                    exhaustedWindowIds.Contains(windowId),
                    "无 binding 的 Close 仍必须清理该窗口的恢复耗尽 ID。");
            }
            finally
            {
                bindings.Remove(windowId);
                exhaustedWindowIds.Clear();
                exhaustedWindowIds.UnionWith(previousExhaustedWindowIds);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FutureSemiSleepPreferenceSchemaIsRejectedWithoutMutatingBinding()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "pinned", false);
            SetField(bindingType, binding, "edgeOffset", 33f);
            SetField(preferenceType, saved, "schemaVersion", int.MaxValue);
            SetField(preferenceType, saved, "allowSemiSleep", false);
            SetField(preferenceType, saved, "pinned", true);
            SetField(preferenceType, saved, "edgeOffset", 900f);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsFalse(applied);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "allowSemiSleep"));
                Assert.IsFalse(GetFieldValue<bool>(bindingType, binding, "pinned"));
                Assert.AreEqual(33f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NonFiniteSemiSleepPreferenceGeometryFallsBackWithoutPoisoningState()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect currentBounds = new Rect(320f, 240f, 960f, 640f);
            window.position = currentBounds;
            object binding = Activator.CreateInstance(bindingType, true);
            object saved = Activator.CreateInstance(preferenceType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(preferenceType, saved, "schemaVersion", 1);
            SetField(preferenceType, saved, "allowSemiSleep", true);
            SetField(preferenceType, saved, "sleeping", true);
            SetField(preferenceType, saved, "edgeOffset", float.NaN);
            SetField(
                preferenceType,
                saved,
                "awakeBounds",
                new Rect(float.NaN, 20f, 900f, 600f));
            SetField(
                preferenceType,
                saved,
                "dockBounds",
                new Rect(20f, float.PositiveInfinity, 100f, 100f));
            SetField(preferenceType, saved, "hasDockBounds", true);

            try
            {
                bool applied = InvokePrivate<bool>(
                    presentationType,
                    "TryApplySemiSleepPreferences",
                    binding,
                    saved);

                Assert.IsTrue(applied);
                Assert.AreEqual(0f, GetFieldValue<float>(bindingType, binding, "edgeOffset"));
                Assert.AreEqual(
                    currentBounds,
                    GetFieldValue<Rect>(bindingType, binding, "awakeBounds"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "hasSemiSleepDockBounds"));
                Assert.IsFalse(GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "restorePersistedSleepOnBind"));
                Assert.AreEqual(
                    ESWindowVisualState.ActivePanel,
                    GetFieldValue<ESWindowVisualState>(
                        bindingType,
                        binding,
                        "transitionTargetState"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(bindingType, binding, "semiSleepDockBounds"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SavingSemiSleepPreferencesWritesCurrentSchemaAndSanitizesDockBounds()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            SetField(bindingType, binding, "edgeOffset", float.NegativeInfinity);
            SetField(bindingType, binding, "hasSemiSleepDockBounds", true);
            SetField(
                bindingType,
                binding,
                "semiSleepDockBounds",
                new Rect(float.NaN, 20f, 100f, 100f));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                string json = EditorPrefs.GetString(preferenceKey, string.Empty);
                Assert.IsNotEmpty(json);
                object saved = JsonUtility.FromJson(json, preferenceType);
                Assert.IsNotNull(saved);
                Assert.AreEqual(1, GetFieldValue<int>(preferenceType, saved, "schemaVersion"));
                Assert.AreEqual(0f, GetFieldValue<float>(preferenceType, saved, "edgeOffset"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "hasDockBounds"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(preferenceType, saved, "dockBounds"));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SavingSemiSleepPolicyDoesNotDependOnValidWindowGeometry()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = default;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", false);
            SetField(bindingType, binding, "pinned", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", default(Rect));
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);

            try
            {
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                string json = EditorPrefs.GetString(preferenceKey, string.Empty);
                Assert.IsNotEmpty(json);
                object saved = JsonUtility.FromJson(json, preferenceType);
                Assert.IsNotNull(saved);
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "allowSemiSleep"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, saved, "pinned"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "sleeping"));
                Assert.AreEqual(
                    (int)ESWindowVisualState.ActivePanel,
                    GetFieldValue<int>(preferenceType, saved, "visualState"));
                Assert.AreEqual(
                    default(Rect),
                    GetFieldValue<Rect>(preferenceType, saved, "awakeBounds"));
            }
            finally
            {
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AssemblyReloadPreferenceCaptureIsOneShotAcrossRootDetach()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo capturedField = presentationType.GetField(
                "assemblyReloadPreferencesCaptured",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(capturedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(240f, 180f, 900f, 600f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4101;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousCaptured = (bool)capturedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                capturedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");
                SetField(bindingType, binding, "semiSleeping", false);
                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "Detach 后的临时 Awake 状态不能覆盖 Reload 前保存的休眠状态。");
            }
            finally
            {
                capturedField.SetValue(null, previousCaptured);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PlayModePreferenceCaptureIsOneShotAcrossDuplicateNotifications()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo suspendedField = presentationType.GetField(
                "playModeBindingsSuspended",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(suspendedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(260f, 200f, 880f, 580f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4102;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousSuspended = (bool)suspendedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                suspendedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CapturePlayModePreferences");
                SetField(bindingType, binding, "semiSleeping", false);
                InvokePrivate<object>(presentationType, "CapturePlayModePreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "EnteredPlayMode 的重复通知不能覆盖 ExitingEditMode 保存的休眠状态。");
            }
            finally
            {
                suspendedField.SetValue(null, previousSuspended);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DuplicateSuspendKeepsTheFirstSleepSnapshot()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo domainReloadField = presentationType.GetField(
                "domainReloadInProgress",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(domainReloadField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(230f, 170f, 920f, 620f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "awakeBounds", window.position);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousDomainReload = (bool)domainReloadField.GetValue(null);

            try
            {
                domainReloadField.SetValue(null, false);
                EditorPrefs.DeleteKey(preferenceKey);

                InvokePrivate<object>(presentationType, "SuspendWindowBinding", binding);
                string firstSnapshot = EditorPrefs.GetString(preferenceKey, string.Empty);
                Assert.IsNotEmpty(firstSnapshot);
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "lifecycleSuspended"));

                SetField(bindingType, binding, "allowSemiSleep", false);
                SetField(bindingType, binding, "pinned", true);
                SetField(bindingType, binding, "awakeBounds", default(Rect));
                InvokePrivate<object>(presentationType, "SuspendWindowBinding", binding);

                Assert.AreEqual(
                    firstSnapshot,
                    EditorPrefs.GetString(preferenceKey, string.Empty),
                    "重复 OnDisable/Detach 不得用首次 teardown 后的 Awake 状态覆盖休眠快照。");
                object saved = JsonUtility.FromJson(firstSnapshot, preferenceType);
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, saved, "sleeping"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, saved, "allowSemiSleep"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, saved, "pinned"));
            }
            finally
            {
                domainReloadField.SetValue(null, previousDomainReload);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LifecycleSuspensionBlocksImplicitWakeButCloseCanForceIt()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect sleepingBounds = new Rect(420f, 260f, 180f, 120f);
            Rect awakeBounds = new Rect(120f, 90f, 920f, 620f);
            window.position = sleepingBounds;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "semiSleepAnimating", false);
            SetField(bindingType, binding, "semiSleepTarget", true);
            SetField(bindingType, binding, "awakeBounds", awakeBounds);
            SetField(bindingType, binding, "lifecycleSuspended", true);

            try
            {
                InvokePrivate<object>(presentationType, "RestoreSemiSleep", binding, true);
                Assert.AreEqual(
                    sleepingBounds,
                    window.position,
                    "生命周期暂停不是隐式唤醒；暂停期间不得写回 awakeBounds。");
                Assert.IsTrue(GetFieldValue<bool>(bindingType, binding, "semiSleeping"));

                InvokePrivate<object>(presentationType, "RestoreSemiSleep", binding, true, true);
                Assert.AreEqual(
                    awakeBounds,
                    window.position,
                    "真实关闭/强制 teardown 才允许恢复 awakeBounds。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SuspendedStableSettingsMergeWithoutReplacingSleepSnapshot()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect firstAwakeBounds = new Rect(230f, 170f, 920f, 620f);
            window.position = firstAwakeBounds;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "awakeBounds", firstAwakeBounds);
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int id = window.GetInstanceID();

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[id] = binding;
                InvokePrivate<object>(presentationType, "SuspendWindowBinding", binding);

                ESEditorPresentation.SetWindowSemiSleepAllowed(window, false);
                ESEditorPresentation.SetWindowPinned(window, true);
                Assert.IsTrue(ESEditorPresentation.TrySetWindowPresentationShortTitle(
                    window,
                    "Dormant"));
                Assert.IsTrue(ESEditorPresentation.SetWindowSemiSleepDockBounds(
                    window,
                    new Rect(280f, 210f, 100f, 100f)));

                object merged = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, merged, "sleeping"));
                Assert.AreEqual(
                    (int)ESWindowVisualState.SleepTile,
                    GetFieldValue<int>(preferenceType, merged, "visualState"));
                Assert.AreEqual(
                    firstAwakeBounds,
                    GetFieldValue<Rect>(preferenceType, merged, "awakeBounds"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, merged, "allowSemiSleep"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, merged, "pinned"));
                Assert.AreEqual(
                    "Dormant",
                    GetFieldValue<string>(preferenceType, merged, "presentationShortTitle"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, merged, "hasDockBounds"));

                Rect nextAwakeBounds = new Rect(310f, 220f, 760f, 520f);
                SetField(bindingType, binding, "lifecycleSuspended", false);
                SetField(bindingType, binding, "allowSemiSleep", true);
                SetField(bindingType, binding, "pinned", false);
                SetField(bindingType, binding, "semiSleeping", false);
                SetField(bindingType, binding, "semiSleepTarget", false);
                SetField(bindingType, binding, "visualState", ESWindowVisualState.ActivePanel);
                SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.ActivePanel);
                SetField(bindingType, binding, "awakeBounds", nextAwakeBounds);
                SetField(bindingType, binding, "semiSleepManualHold", false);
                window.position = nextAwakeBounds;
                InvokePrivate<object>(presentationType, "SuspendWindowBinding", binding);

                object nextSnapshot = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, nextSnapshot, "sleeping"));
                Assert.AreEqual(
                    nextAwakeBounds,
                    GetFieldValue<Rect>(preferenceType, nextSnapshot, "awakeBounds"));
                Assert.IsTrue(GetFieldValue<bool>(preferenceType, nextSnapshot, "allowSemiSleep"));
                Assert.IsFalse(GetFieldValue<bool>(preferenceType, nextSnapshot, "pinned"));
            }
            finally
            {
                bindings.Remove(id);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BlockedBindingNormalizationClearsAllTransientSleepState()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            Rect originalBounds = new Rect(180f, 140f, 720f, 500f);
            window.position = originalBounds;
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "restorePersistedSleepOnBind", true);
            SetField(bindingType, binding, "restorePersistedSleepScheduled", true);
            SetField(bindingType, binding, "persistedSleepGeometryVerifyUntil", 12d);
            SetField(bindingType, binding, "persistedSleepGeometryRepairScheduled", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "semiSleepManualHold", true);
            SetField(bindingType, binding, "semiSleepSlot", 3);
            SetField(bindingType, binding, "focusLostAt", 5d);
            SetField(bindingType, binding, "sleepTileIdleStartedAt", 6d);
            SetField(bindingType, binding, "edgeTabFullyExpandedAt", 7d);
            SetField(bindingType, binding, "edgeTabHoverIntentStartedAt", 8d);
            SetField(bindingType, binding, "edgeTabHoverExitGraceUntil", 9d);
            SetField(bindingType, binding, "pointerInside", true);
            SetField(bindingType, binding, "hasEdgeTabPointerPosition", true);
            SetField(bindingType, binding, "semiSleepDragging", true);
            SetField(bindingType, binding, "semiSleepDragPointerId", 4);
            SetField(bindingType, binding, "hasSemiSleepDragPendingBounds", true);
            SetField(bindingType, binding, "semiSleepRecaptureScheduled", true);
            SetField(bindingType, binding, "semiSleepDragStartState", ESWindowVisualState.SleepTile);
            SetField(bindingType, binding, "awakeBounds", new Rect(20f, 20f, 900f, 640f));

            try
            {
                Assert.IsTrue(InvokePrivate<bool>(
                    presentationType,
                    "HasSemiSleepStateToNormalize",
                    binding));
                InvokePrivate<object>(presentationType, "RestoreSemiSleep", binding, true, true);
                Assert.IsFalse(InvokePrivate<bool>(
                    presentationType,
                    "HasSemiSleepStateToNormalize",
                    binding));
                Assert.AreEqual(
                    -1d,
                    GetFieldValue<double>(
                        bindingType,
                        binding,
                        "edgeTabHoverExitGraceUntil"));
                Assert.AreEqual(
                    originalBounds,
                    window.position,
                    "仅有瞬时状态时不得误写原生窗口几何。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BlockedSettledEdgeTabStaysEvaluableUntilItsStateIsNormalized()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "visualState", ESWindowVisualState.EdgeTab);
            SetField(bindingType, binding, "transitionTargetState", ESWindowVisualState.EdgeTab);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "semiSleepTarget", true);
            SetField(bindingType, binding, "pointerInside", false);

            try
            {
                Assert.IsTrue(InvokePrivate<bool>(
                    presentationType,
                    "HasBlockedSemiSleepStateToNormalize",
                    binding));
                Assert.IsTrue(InvokePrivate<bool>(
                    presentationType,
                    "ShouldEvaluateSemiSleepBinding",
                    binding));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void AssemblyReloadDuringPlayModeCannotOverwritePlayModeSnapshot()
        {
            Type presentationType = typeof(ESEditorPresentation);
            Type bindingType = presentationType.GetNestedType("WindowBinding", BindingFlags.NonPublic);
            Type preferenceType = presentationType.GetNestedType(
                "SemiSleepWindowPreferences",
                BindingFlags.NonPublic);
            FieldInfo suspendedField = presentationType.GetField(
                "playModeBindingsSuspended",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo capturedField = presentationType.GetField(
                "assemblyReloadPreferencesCaptured",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            Assert.IsNotNull(preferenceType);
            Assert.IsNotNull(suspendedField);
            Assert.IsNotNull(capturedField);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            window.position = new Rect(280f, 190f, 860f, 560f);
            object binding = Activator.CreateInstance(bindingType, true);
            SetField(bindingType, binding, "window", window);
            SetField(bindingType, binding, "supportsSemiSleep", true);
            SetField(bindingType, binding, "allowSemiSleep", true);
            SetField(bindingType, binding, "semiSleeping", true);
            SetField(bindingType, binding, "awakeBounds", window.position);
            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            int key = int.MinValue + 4103;
            while (bindings.Contains(key))
                key++;
            string preferenceKey = InvokePrivate<string>(
                presentationType,
                "GetSemiSleepPreferenceKey",
                window);
            bool previousSuspended = (bool)suspendedField.GetValue(null);
            bool previousCaptured = (bool)capturedField.GetValue(null);

            try
            {
                EditorPrefs.DeleteKey(preferenceKey);
                bindings[key] = binding;
                InvokePrivate<object>(presentationType, "SaveSemiSleepPreferences", binding);
                SetField(bindingType, binding, "semiSleeping", false);
                suspendedField.SetValue(null, true);
                capturedField.SetValue(null, false);

                InvokePrivate<object>(presentationType, "CaptureAssemblyReloadPreferences");

                object saved = JsonUtility.FromJson(
                    EditorPrefs.GetString(preferenceKey),
                    preferenceType);
                Assert.IsTrue(
                    GetFieldValue<bool>(preferenceType, saved, "sleeping"),
                    "PlayMode 中 Reload 不能把已捕获的休眠状态覆盖成 Awake。");
            }
            finally
            {
                suspendedField.SetValue(null, previousSuspended);
                capturedField.SetValue(null, previousCaptured);
                bindings.Remove(key);
                EditorPrefs.DeleteKey(preferenceKey);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnboundCloseCleanupSurvivesFrameActivationFailure()
        {
            const string ownerKey = "ES.Tests.UnboundCloseFailure";
            Type presentationType = typeof(ESEditorPresentation);
            Type activationType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowFrameActivation",
                true);
            Type runningType = activationType.GetNestedType(
                "RunningAnimation",
                BindingFlags.NonPublic);
            Assert.IsNotNull(runningType);

            IDictionary bindings = GetStaticDictionary(presentationType, "windowBindings");
            IDictionary runningByWindow = GetStaticDictionary(activationType, "Running");
            IDictionary runningByRoot = GetStaticDictionary(activationType, "RunningByRoot");
            var exhaustedWindowIds = presentationType.GetField(
                    "resumeBindingsRetryExhaustedWindowIds",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as HashSet<int>;
            Assert.IsNotNull(exhaustedWindowIds);

            ESWindowSleepLifetimeProbeWindow window =
                ScriptableObject.CreateInstance<ESWindowSleepLifetimeProbeWindow>();
            int windowId = window.GetInstanceID();
            VisualElement root = window.rootVisualElement;
            object previousRunning = runningByWindow.Contains(windowId)
                ? runningByWindow[windowId]
                : null;
            bool wasExhausted = exhaustedWindowIds.Contains(windowId);
            object running = Activator.CreateInstance(runningType, true);
            SetField(runningType, running, "WindowId", windowId);
            SetField(runningType, running, "Window", window);
            SetField(runningType, running, "Root", root);
            SetField(runningType, running, "Schedule", new ESThrowingScheduledItem());

            try
            {
                bindings[windowId] = null;
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                Assert.IsTrue(ESWindowFoundation.RegisterPendingSleepOwner(window, ownerKey));
                exhaustedWindowIds.Add(windowId);
                runningByWindow[windowId] = running;
                runningByRoot[root] = running;

                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("ES teardown schedule failure"));
                Assert.DoesNotThrow(() => ESEditorPresentation.UnbindWindow(window, true));

                Assert.IsFalse(PendingOwnerKeyExists(ownerKey));
                Assert.IsFalse(bindings.Contains(windowId));
                Assert.IsFalse(exhaustedWindowIds.Contains(windowId));
                Assert.IsFalse(runningByWindow.Contains(windowId));
                Assert.IsFalse(runningByRoot.Contains(root));
                AssertFieldIsNull(runningType, running, "Window");
                AssertFieldIsNull(runningType, running, "Root");
                AssertFieldIsNull(runningType, running, "Schedule");
            }
            finally
            {
                bindings.Remove(windowId);
                ESWindowFoundation.ClearPendingSleepOwners(ownerKey);
                runningByWindow.Remove(windowId);
                runningByRoot.Remove(root);
                if (previousRunning != null)
                    runningByWindow[windowId] = previousRunning;
                if (wasExhausted)
                    exhaustedWindowIds.Add(windowId);
                else
                    exhaustedWindowIds.Remove(windowId);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FrameActivationCanReleaseStaticEntryByCachedWindowId()
        {
            Type activationType = typeof(ESWindowActivationMotion).Assembly.GetType(
                "ES.EditorInternal.ESWindowFrameActivation",
                true);
            Type runningType = activationType.GetNestedType(
                "RunningAnimation",
                BindingFlags.NonPublic);
            Assert.IsNotNull(runningType);

            object running = Activator.CreateInstance(runningType, true);
            var root = new VisualElement();
            const int windowId = int.MinValue + 149;
            SetField(runningType, running, "WindowId", windowId);
            SetField(runningType, running, "Root", root);

            IDictionary runningByWindow = GetStaticDictionary(activationType, "Running");
            IDictionary runningByRoot = GetStaticDictionary(activationType, "RunningByRoot");
            runningByWindow[windowId] = running;
            runningByRoot[root] = running;

            MethodInfo stop = activationType.GetMethod(
                "Stop",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(bool) },
                null);
            Assert.IsNotNull(stop);
            stop.Invoke(null, new object[] { windowId, false });

            Assert.IsFalse(runningByWindow.Contains(windowId));
            Assert.IsFalse(runningByRoot.Contains(root));
            AssertFieldIsNull(runningType, running, "Root");
            AssertFieldIsNull(runningType, running, "Window");
            AssertFieldIsNull(runningType, running, "Schedule");
        }

        private static IDictionary GetStaticDictionary(Type ownerType, string fieldName)
        {
            var dictionary = ownerType
                .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IDictionary;
            Assert.IsNotNull(dictionary, ownerType.FullName + "." + fieldName);
            return dictionary;
        }

        private static void AssertSourceExcludes(string projectRelativePath, string forbiddenText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            StringAssert.DoesNotContain(forbiddenText, source, projectRelativePath);
        }

        private static void AssertSourceContains(string projectRelativePath, string requiredText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            StringAssert.Contains(requiredText, source, projectRelativePath);
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string[] EnumerateESProductionEditorSources(string projectRoot)
        {
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets/Plugins/ES/Editor"),
                Path.Combine(projectRoot, "Assets/Scripts/ESLogic/Editor"),
            };
            return roots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(
                    root,
                    "*.cs",
                    SearchOption.AllDirectories))
                .ToArray();
        }

        private static string[] EnumerateESOwnedEditorSources(string projectRoot)
        {
            string[] roots =
            {
                Path.Combine(projectRoot, "Assets/Plugins/ES"),
                Path.Combine(projectRoot, "Assets/Scripts/ESLogic"),
            };
            return roots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(
                    root,
                    "*.cs",
                    SearchOption.AllDirectories))
                .Where(path =>
                {
                    string normalized = NormalizeProjectPath(path);
                    return normalized.Contains("/Editor/", StringComparison.OrdinalIgnoreCase)
                        || ContainsUnityEditorDirective(path);
                })
                .ToArray();
        }

        private static bool ContainsUnityEditorDirective(string path)
        {
            // Discovery must survive unrelated legacy encodings long enough to
            // decide whether a file belongs to the Editor scan. Once selected,
            // ReadSourceText performs the strict UTF-8 validation. Replacement
            // fallback cannot forge the ASCII preprocessor token, and unlike the
            // old ASCII decode it handles a UTF-8 BOM without a "???" prefix.
            string source = File.ReadAllText(path, new UTF8Encoding(false, false));
            return Regex.IsMatch(
                source,
                @"^(?:\uFEFF)?[ \t]*#[ \t]*if[ \t]+[^\r\n]*\bUNITY_EDITOR\b",
                RegexOptions.CultureInvariant | RegexOptions.Multiline);
        }

        private static IReadOnlyList<ESOwnedWindowSource> DiscoverESOwnedConcreteEditorWindows(
            string projectRoot)
        {
            string[] assetRoots =
            {
                "Assets/Plugins/ES",
                "Assets/Scripts/ESLogic",
            };
            var sourceRecords = new List<MonoScriptSourceRecord>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", assetRoots))
            {
                string assetPath = NormalizeProjectPath(AssetDatabase.GUIDToAssetPath(guid));
                if (!assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                string fullPath = Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                string source = ReadSourceText(fullPath);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                sourceRecords.Add(new MonoScriptSourceRecord(
                    assetPath,
                    script != null ? script.GetClass() : null,
                    ExtractWindowTypeDeclarations(source, assetPath)));
            }

            var discovered = new List<ESOwnedWindowSource>();
            foreach (Type windowType in TypeCache.GetTypesDerivedFrom<EditorWindow>()
                         .Where(type => type != null && !type.IsAbstract))
            {
                MonoScriptSourceRecord[] directMatches = sourceRecords
                    .Where(record => record.PrimaryType == windowType)
                    .ToArray();
                MonoScriptSourceRecord[] matches = directMatches.Length > 0
                    ? directMatches
                    : sourceRecords.Where(record =>
                            (record.PrimaryType == null
                             || record.PrimaryType.Assembly == windowType.Assembly)
                            && record.Declarations.Any(declaration =>
                                declaration.Name == windowType.Name))
                        .ToArray();
                if (matches.Length == 0)
                    continue;

                Assert.AreEqual(
                    1,
                    matches.Length,
                    "TypeCache 中的 ES-owned EditorWindow 必须唯一映射到 MonoScript："
                    + windowType.FullName
                    + "|"
                    + string.Join(",", matches.Select(item => item.ProjectRelativePath)));
                MonoScriptSourceRecord match = matches[0];
                SourceWindowTypeDeclaration[] declarations = match.Declarations
                    .Where(declaration => declaration.Name == windowType.Name)
                    .ToArray();
                Assert.AreEqual(
                    1,
                    declarations.Length,
                    "MonoScript 必须包含唯一的具体窗口源码声明；别名和中间基类也不得绕过："
                    + match.ProjectRelativePath
                    + "|"
                    + windowType.FullName);
                discovered.Add(new ESOwnedWindowSource(
                    windowType,
                    match.ProjectRelativePath,
                    declarations[0]));
            }

            Assert.AreEqual(
                discovered.Count,
                discovered.Select(item => item.WindowType).Distinct().Count(),
                "同一 TypeCache 窗口不得映射到多个 ES MonoScript。");
            return discovered;
        }

        private static bool IsDirectEditorWindowType(Type windowType)
        {
            Type baseType = windowType?.BaseType;
            return baseType == typeof(EditorWindow)
                || string.Equals(baseType?.Name, "OdinEditorWindow", StringComparison.Ordinal);
        }

        private static ESWindowSleepContractAttribute GetRequiredWindowContract(Type windowType)
        {
            var contract = (ESWindowSleepContractAttribute)Attribute.GetCustomAttribute(
                windowType,
                typeof(ESWindowSleepContractAttribute),
                true);
            Assert.IsNotNull(contract, "ES-owned EditorWindow 缺少生命周期与 SurfaceKind 合同："
                + windowType?.FullName);
            return contract;
        }

        private static HashSet<string> CreateExpectedProductionWindowNames()
        {
            return new HashSet<string>(
                CreateExpectedProductionWindowSurfaceKinds().Keys,
                StringComparer.Ordinal);
        }

        private static Dictionary<string, ESWindowSurfaceKind>
            CreateExpectedProductionWindowSurfaceKinds()
        {
            return new Dictionary<string, ESWindowSurfaceKind>(StringComparer.Ordinal)
            {
                ["EntityBasicInteractionDebugWindow"] = ESWindowSurfaceKind.Workspace,
                ["EntityStatDebugWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAIBrainWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAgentArtifactCandidateReviewWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAssetPackageBakeWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAssetPackageRecordPreviewWindow"] = ESWindowSurfaceKind.Preview,
                ["ESAssetReferKeyPickerWindow"] = ESWindowSurfaceKind.Popup,
                ["ESAssetReleaseUploadWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAudioCueTrimPreviewWindow"] = ESWindowSurfaceKind.Preview,
                ["ESAutomationCenterWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESCameraTrackPreviewWindow"] = ESWindowSurfaceKind.Preview,
                ["ESCmdAgentWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESCommandPaletteWindow"] = ESWindowSurfaceKind.Popup,
                ["ESCompactChoicePopup"] = ESWindowSurfaceKind.Popup,
                ["ESCompositeShaderBakeWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESCreateSkillWindow"] = ESWindowSurfaceKind.Utility,
                ["ESDeveloperCockpitWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESDynamicAtlasMonitorWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESEditorFeedbackSoundSchemeWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESEditorHealthWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESEditorThemeWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESFontToolsWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESGameCoreDefinitionEditorWindow"] = ESWindowSurfaceKind.Inspector,
                ["ESInputActionBindingImportWindow"] = ESWindowSurfaceKind.Utility,
                ["ESInputActionImportWindow"] = ESWindowSurfaceKind.Utility,
                ["ESInstaller"] = ESWindowSurfaceKind.Workspace,
                ["ESLocalizationToolsWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESProgressCenterWindow"] = ESWindowSurfaceKind.Utility,
                ["ESResourceCollectionWorkflowWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESResourceRuntimeMonitorWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESResWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESSODataInfoWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESStableGraphViewWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESTrackClipTemporaryInspectorWindow"] = ESWindowSurfaceKind.Inspector,
                ["ESTrackItemTemporaryInspectorWindow"] = ESWindowSurfaceKind.Inspector,
                ["ESTrackSkillDataTemporaryInspectorWindow"] = ESWindowSurfaceKind.Inspector,
                ["ESTrackViewWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESTreeMenuShower"] = ESWindowSurfaceKind.Popup,
                ["ESUIRiskAuditWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESWindowLauncher"] = ESWindowSurfaceKind.Workspace,
                ["ESWorkbenchCaseStudyWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESWorkbenchIntegrationTestWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESWorkbenchPopupWindow"] = ESWindowSurfaceKind.Popup,
                ["ESWorldBuilderWorkbenchWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESWorldDialogueEditorWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESWorldMapSpaceEditorWindow"] = ESWindowSurfaceKind.Workspace,
                ["SimpleToolsWindow"] = ESWindowSurfaceKind.Workspace,
                ["ESAdvancedDialogWindow"] = ESWindowSurfaceKind.Dialog,
            };
        }

        private static HashSet<string> CreateExpectedTransientWindowNames()
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                "ESAdvancedDialogWindow",
                "ESAssetReferKeyPickerWindow",
                "ESProgressCenterWindow",
                "ESCommandPaletteWindow",
                "ESCompactChoicePopup",
                "ESCreateSkillWindow",
                "ESTreeMenuShower",
                "ESWorkbenchPopupWindow",
                "ESInputActionImportWindow",
                "ESInputActionBindingImportWindow",
            };
        }

        private static Dictionary<string, (string Path, string Reason)>
            CreateExplicitNonProductionWindowExceptions()
        {
            const string toolkitProbePath =
                "Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESMenuTreeToolkitTestWindow.cs";
            return new Dictionary<string, (string Path, string Reason)>(StringComparer.Ordinal)
            {
                ["ESMenuTreeToolkitTestWindow"] = (
                    toolkitProbePath,
                    "显式 UI Toolkit 菜单树测试窗，不属于生产工具库存。"),
                ["ESSinglePageToolkitTestWindow"] = (
                    toolkitProbePath,
                    "显式 SinglePage Toolkit 测试窗，不属于生产工具库存。"),
                ["ESWindowSleepBenchmarkProbeWindow"] = (
                    toolkitProbePath,
                    "显式 20/50/100 窗口半休眠性能探针，由压力测试协调器创建和清理。"),
            };
        }

        private static bool IsExcludedProductionSourcePath(string path)
        {
            string[] segments = NormalizeProjectPath(path)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string segment = segments[i];
                if (Regex.IsMatch(
                        segment,
                        @"(?:^|[_-])Tests?(?:$|[_-])|(?:^|[_-])Examples?(?:$|[_-])|^(?:Obsolete|Obsolute)$",
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetProjectRelativePath(string projectRoot, string path)
        {
            string normalizedRoot = NormalizeProjectPath(projectRoot).TrimEnd('/');
            string normalizedPath = NormalizeProjectPath(path);
            string prefix = normalizedRoot + "/";
            if (!normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("源码路径不在项目根内：" + normalizedPath);
            return normalizedPath.Substring(prefix.Length);
        }

        private static bool IsApprovedProductionEditorPath(string projectRelativePath)
        {
            string normalized = NormalizeProjectPath(projectRelativePath);
            return normalized.StartsWith(
                    "Assets/Plugins/ES/Editor/",
                    StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(
                    "Assets/Scripts/ESLogic/Editor/",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrackedProductionWindowBase(string baseType)
        {
            return Regex.IsMatch(
                baseType ?? string.Empty,
                @"\b(?:EditorWindow|OdinEditorWindow|ESMenuTreeWindow|ESOdinMenuTreeWindow|ESSinglePage(?:IMGUI)?Window|ESWorkbenchWindowBase|ESIndependentInspectorWindow|ESTrackTemporaryInspectorWindow)\b",
                RegexOptions.CultureInvariant);
        }

        private static bool IsDirectEditorWindowBase(string baseType)
        {
            return Regex.IsMatch(
                baseType ?? string.Empty,
                @"\b(?:EditorWindow|OdinEditorWindow)\b",
                RegexOptions.CultureInvariant);
        }

        private static IReadOnlyList<SourceWindowTypeDeclaration> ExtractWindowTypeDeclarations(
            string source,
            string path)
        {
            string commentFreeSource = StripCSharpComments(source ?? string.Empty);
            string searchableCode = MaskCSharpStringAndCharacterLiterals(commentFreeSource);
            var declarationPattern = new Regex(
                @"(?<modifiers>(?:(?:public|internal|protected|private|abstract|sealed|static|partial|new|unsafe)\s+)*)\bclass\s+(?<name>@?[A-Za-z_]\w*)\s*(?:<[^{};]+>)?\s*:\s*(?<baseTypes>[^{};]+?)\s*(?<body>\{)",
                RegexOptions.CultureInvariant);
            var declarations = new List<SourceWindowTypeDeclaration>();
            foreach (Match match in declarationPattern.Matches(searchableCode))
            {
                int bodyOpenIndex = match.Groups["body"].Index;
                int bodyCloseIndex = FindMatchingDelimiter(
                    searchableCode,
                    bodyOpenIndex,
                    '{',
                    '}',
                    searchableCode.Length);
                Assert.GreaterOrEqual(
                    bodyCloseIndex,
                    0,
                    "窗口类型声明缺少闭合大括号：" + path + "|" + match.Groups["name"].Value);
                declarations.Add(new SourceWindowTypeDeclaration(
                    match.Groups["name"].Value,
                    match.Groups["baseTypes"].Value,
                    path,
                    source,
                    searchableCode,
                    bodyOpenIndex,
                    bodyCloseIndex,
                    Regex.IsMatch(
                        match.Groups["modifiers"].Value,
                        @"\babstract\b",
                        RegexOptions.CultureInvariant)));
            }

            return declarations;
        }

        private static IReadOnlyList<SourceMethodDeclaration> ExtractTopLevelMethods(
            SourceWindowTypeDeclaration declaration)
        {
            var methods = new List<SourceMethodDeclaration>();
            string searchableCode = declaration.SearchableCode;
            int scopeEnd = declaration.BodyCloseIndex;
            int memberStart = declaration.BodyOpenIndex + 1;
            int cursor = memberStart;
            while (cursor < scopeEnd)
            {
                if (searchableCode[cursor] == '{')
                {
                    int bodyEnd = FindMatchingDelimiter(
                        searchableCode,
                        cursor,
                        '{',
                        '}',
                        scopeEnd);
                    Assert.GreaterOrEqual(
                        bodyEnd,
                        0,
                        "类成员缺少闭合大括号：" + declaration.DiagnosticIdentity);
                    if (TryParseMethodHeader(
                            searchableCode.Substring(memberStart, cursor - memberStart),
                            out string name,
                            out string parameters))
                    {
                        methods.Add(new SourceMethodDeclaration(
                            name,
                            parameters,
                            searchableCode,
                            memberStart,
                            cursor + 1,
                            bodyEnd));
                    }

                    cursor = bodyEnd + 1;
                    memberStart = cursor;
                    continue;
                }

                if (searchableCode[cursor] == '='
                    && cursor + 1 < scopeEnd
                    && searchableCode[cursor + 1] == '>')
                {
                    int bodyEnd = FindExpressionBodyTerminator(
                        searchableCode,
                        cursor + 2,
                        scopeEnd);
                    Assert.GreaterOrEqual(
                        bodyEnd,
                        0,
                        "表达式成员缺少结束分号：" + declaration.DiagnosticIdentity);
                    if (TryParseMethodHeader(
                            searchableCode.Substring(memberStart, cursor - memberStart),
                            out string name,
                            out string parameters))
                    {
                        methods.Add(new SourceMethodDeclaration(
                            name,
                            parameters,
                            searchableCode,
                            memberStart,
                            cursor + 2,
                            bodyEnd));
                    }

                    cursor = bodyEnd + 1;
                    memberStart = cursor;
                    continue;
                }

                if (searchableCode[cursor] == ';')
                    memberStart = cursor + 1;
                cursor++;
            }

            return methods;
        }

        private static void AssertMethodContainsFoundationCall(
            SourceWindowTypeDeclaration declaration,
            string methodName,
            string foundationMethodName)
        {
            SourceMethodDeclaration[] methods = ExtractTopLevelMethods(declaration)
                .Where(method => string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(method.Parameters))
                .ToArray();
            Assert.AreEqual(
                1,
                methods.Length,
                "直接生产窗口必须在自身类体中声明唯一无参 "
                + methodName
                + "："
                + declaration.DiagnosticIdentity);
            Assert.IsTrue(
                ContainsDirectExecutableCall(
                    methods[0],
                    BuildFoundationCallPattern(foundationMethodName),
                    declaration.DiagnosticIdentity),
                methodName
                + " 必须显式调用 ESWindowFoundation."
                + foundationMethodName
                + "(this)："
                    + declaration.DiagnosticIdentity);
        }

        private static void AssertDeclaredLifecycleMethodPreservesFoundation(
            ESOwnedWindowSource window,
            string methodName,
            params string[] foundationMethodNames)
        {
            SourceMethodDeclaration[] declarations = ExtractTopLevelMethods(window.Declaration)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(method.Parameters))
                .ToArray();
            Assert.LessOrEqual(
                declarations.Length,
                1,
                "具体窗口不得重复声明无参生命周期入口："
                + window.Declaration.DiagnosticIdentity
                + "|"
                + methodName);
            if (declarations.Length == 0)
                return;

            SourceMethodDeclaration lifecycle = declarations[0];
            bool callsFoundation = foundationMethodNames.Any(name =>
                ContainsDirectExecutableCall(
                    lifecycle,
                    BuildFoundationCallPattern(name),
                    window.Declaration.DiagnosticIdentity));
            bool callsBase = ContainsDirectExecutableCall(
                lifecycle,
                @"\bbase\s*\.\s*" + Regex.Escape(methodName) + @"\s*\(\s*\)",
                window.Declaration.DiagnosticIdentity);
            if (callsFoundation || callsBase)
                return;

            bool shadowsManagedBase = FindDeclaredLifecycleMethodInBaseHierarchy(
                window.WindowType.BaseType,
                methodName) != null;
            if (methodName == "OnEnable" && !shadowsManagedBase)
            {
                bool bindsFromAnotherEntry = ExtractTopLevelMethods(window.Declaration).Any(method =>
                    foundationMethodNames.Any(name => ContainsDirectExecutableCall(
                        method,
                        BuildFoundationCallPattern(name),
                        window.Declaration.DiagnosticIdentity)));
                if (bindsFromAnotherEntry)
                    return;
            }

            Assert.Fail(
                "具体窗口声明 "
                + methodName
                + " 时必须直接调用 base."
                + methodName
                + "() 或对应 ESWindowFoundation 入口，局部函数/lambda/死分支不能代替："
                + window.Declaration.DiagnosticIdentity);
        }

        private static MethodInfo FindDeclaredLifecycleMethodInBaseHierarchy(
            Type baseType,
            string methodName)
        {
            for (Type current = baseType;
                 current != null && current != typeof(EditorWindow);
                 current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(
                    methodName,
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method != null)
                    return method;
            }
            return null;
        }

        private static bool ContainsDirectExecutableCall(
            SourceMethodDeclaration method,
            string callPattern,
            string diagnosticIdentity)
        {
            IReadOnlyList<SourceRange> excludedRanges = ExtractNestedExecutableRanges(
                method,
                diagnosticIdentity);
            foreach (Match match in Regex.Matches(
                         method.SearchableBody,
                         callPattern,
                         RegexOptions.CultureInvariant))
            {
                int absoluteIndex = method.BodyStartIndex + match.Index;
                if (!excludedRanges.Any(range => range.Contains(absoluteIndex)))
                    return true;
            }
            return false;
        }

        private static void AssertRootVisualElementClearHasFoundationRebindBoundary(
            SourceWindowTypeDeclaration declaration,
            ESWindowSleepMode expectedMode)
        {
            IReadOnlyList<SourceMethodDeclaration> methods = ExtractTopLevelMethods(declaration);
            var validatedClearIndexes = new HashSet<int>();
            foreach (SourceMethodDeclaration method in methods)
            {
                IReadOnlyList<SourceTransitionEvent> events = BuildReachableTransitionEvents(
                    method,
                    methods,
                    declaration.DiagnosticIdentity,
                    new HashSet<int>());
                for (int index = 0; index < events.Count; index++)
                {
                    SourceTransitionEvent current = events[index];
                    if (current.Kind != SourceTransitionKind.Clear)
                        continue;
                    int line = GetSourceLineNumber(declaration.Source, current.SourceIndex);
                    Assert.Greater(
                        index,
                        0,
                        "每次 rootVisualElement.Clear() 的最近 Foundation 转换必须是 Unbind："
                        + declaration.DiagnosticIdentity
                        + ":"
                        + line);
                    Assert.AreEqual(
                        SourceTransitionKind.Unbind,
                        events[index - 1].Kind,
                        "每次 Clear 都必须拥有自己的 Unbind，不能借用更早闭环："
                        + declaration.DiagnosticIdentity
                        + ":"
                        + line);
                    Assert.Less(
                        index + 1,
                        events.Count,
                        "Clear 后必须出现首个正确的 Foundation Bind："
                        + declaration.DiagnosticIdentity
                        + ":"
                        + line);
                    SourceTransitionKind expectedBind = expectedMode == ESWindowSleepMode.Transient
                        ? SourceTransitionKind.BindTransient
                        : SourceTransitionKind.BindFull;
                    SourceTransitionKind actualBind = events[index + 1].Kind;
                    Assert.IsTrue(
                        actualBind == expectedBind
                        || (expectedMode == ESWindowSleepMode.Full
                            && actualBind == SourceTransitionKind.BindStandardHost),
                        "Clear 后的首次 Foundation 转换必须匹配窗口 Full/Transient 合同："
                        + declaration.DiagnosticIdentity
                        + ":"
                        + line);
                    validatedClearIndexes.Add(current.SourceIndex);
                }
            }

            const string clearPattern =
                @"(?:\bthis\s*\.\s*)?\brootVisualElement\s*\.\s*Clear\s*\(\s*\)";
            int[] allClearIndexes = Regex.Matches(
                    declaration.SearchableBody,
                    clearPattern,
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => declaration.BodyOpenIndex + 1 + match.Index)
                .ToArray();
            CollectionAssert.IsSubsetOf(
                allClearIndexes,
                validatedClearIndexes,
                "未调用局部函数、lambda 或不可达 helper 中的转换不得替 Clear 伪造闭环："
                + declaration.DiagnosticIdentity);
        }

        private static IReadOnlyList<SourceTransitionEvent> BuildReachableTransitionEvents(
            SourceMethodDeclaration method,
            IReadOnlyList<SourceMethodDeclaration> topLevelMethods,
            string diagnosticIdentity,
            HashSet<int> callStack)
        {
            if (!callStack.Add(method.DeclarationStartIndex))
                return Array.Empty<SourceTransitionEvent>();
            try
            {
                var occurrences = new List<SourceEventOccurrence>();
                IReadOnlyList<SourceRange> excluded = ExtractNestedExecutableRanges(
                    method,
                    diagnosticIdentity);
                const string eventPattern =
                    @"(?:(?:\bthis\s*\.\s*)?\brootVisualElement\s*\.\s*(?<clear>Clear)\s*\(\s*\))|(?:\bESWindowFoundation\s*\.\s*(?<foundation>Unbind|BindFullSleep|BindTransient|BindWithStandardSystemHost)\s*\(\s*this\b)";
                foreach (Match match in Regex.Matches(
                             method.SearchableBody,
                             eventPattern,
                             RegexOptions.CultureInvariant))
                {
                    int absoluteIndex = method.BodyStartIndex + match.Index;
                    if (excluded.Any(range => range.Contains(absoluteIndex)))
                        continue;
                    SourceTransitionKind kind;
                    if (match.Groups["clear"].Success)
                        kind = SourceTransitionKind.Clear;
                    else
                    {
                        switch (match.Groups["foundation"].Value)
                        {
                            case "Unbind":
                                kind = SourceTransitionKind.Unbind;
                                break;
                            case "BindTransient":
                                kind = SourceTransitionKind.BindTransient;
                                break;
                            case "BindWithStandardSystemHost":
                                kind = SourceTransitionKind.BindStandardHost;
                                break;
                            default:
                                kind = SourceTransitionKind.BindFull;
                                break;
                        }
                    }
                    occurrences.Add(new SourceEventOccurrence(
                        absoluteIndex,
                        new[] { new SourceTransitionEvent(kind, absoluteIndex) }));
                }

                var helpers = topLevelMethods
                    .Concat(ExtractLocalMethods(method, diagnosticIdentity))
                    .Where(helper => string.IsNullOrWhiteSpace(helper.Parameters)
                        && helper.DeclarationStartIndex != method.DeclarationStartIndex)
                    .GroupBy(helper => helper.Name, StringComparer.Ordinal)
                    .Where(group => group.Count() == 1)
                    .Select(group => group.Single())
                    .ToArray();
                foreach (SourceMethodDeclaration helper in helpers)
                {
                    string callPattern = @"(?<![A-Za-z0-9_@.])(?:this\s*\.\s*)?"
                        + Regex.Escape(helper.Name)
                        + @"\s*\(\s*\)";
                    foreach (Match call in Regex.Matches(
                                 method.SearchableBody,
                                 callPattern,
                                 RegexOptions.CultureInvariant))
                    {
                        int absoluteIndex = method.BodyStartIndex + call.Index;
                        if (excluded.Any(range => range.Contains(absoluteIndex)))
                            continue;
                        IReadOnlyList<SourceTransitionEvent> expanded =
                            BuildReachableTransitionEvents(
                                helper,
                                topLevelMethods,
                                diagnosticIdentity,
                                callStack);
                        if (expanded.Count > 0)
                            occurrences.Add(new SourceEventOccurrence(absoluteIndex, expanded));
                    }
                }

                return occurrences
                    .OrderBy(item => item.SourceIndex)
                    .SelectMany(item => item.Events)
                    .ToArray();
            }
            finally
            {
                callStack.Remove(method.DeclarationStartIndex);
            }
        }

        private static IReadOnlyList<SourceMethodDeclaration> ExtractLocalMethods(
            SourceMethodDeclaration containingMethod,
            string diagnosticIdentity)
        {
            var methods = new List<SourceMethodDeclaration>();
            string searchableCode = containingMethod.SearchableCode;
            int cursor = containingMethod.BodyStartIndex;
            while (cursor < containingMethod.BodyEndIndex)
            {
                bool blockBody = searchableCode[cursor] == '{';
                bool expressionBody = searchableCode[cursor] == '='
                    && cursor + 1 < containingMethod.BodyEndIndex
                    && searchableCode[cursor + 1] == '>';
                if (!blockBody && !expressionBody)
                {
                    cursor++;
                    continue;
                }

                int headerStart = FindPreviousMemberBoundary(
                    searchableCode,
                    cursor,
                    containingMethod.BodyStartIndex);
                if (!TryParseMethodHeader(
                        searchableCode.Substring(headerStart, cursor - headerStart),
                        out string name,
                        out string parameters))
                {
                    cursor++;
                    continue;
                }

                int bodyStart = blockBody ? cursor + 1 : cursor + 2;
                int bodyEnd = blockBody
                    ? FindMatchingDelimiter(
                        searchableCode,
                        cursor,
                        '{',
                        '}',
                        containingMethod.BodyEndIndex)
                    : FindExpressionBodyTerminator(
                        searchableCode,
                        cursor + 2,
                        containingMethod.BodyEndIndex);
                Assert.GreaterOrEqual(
                    bodyEnd,
                    0,
                    "局部方法体未闭合：" + diagnosticIdentity + "|" + name);
                methods.Add(new SourceMethodDeclaration(
                    name,
                    parameters,
                    searchableCode,
                    headerStart,
                    bodyStart,
                    bodyEnd));
                cursor++;
            }

            return methods;
        }

        private static IReadOnlyList<SourceRange> ExtractNestedExecutableRanges(
            SourceMethodDeclaration method,
            string diagnosticIdentity)
        {
            var ranges = ExtractLocalMethods(method, diagnosticIdentity)
                .Select(local => new SourceRange(
                    local.DeclarationStartIndex,
                    Math.Min(local.BodyEndIndex + 1, method.BodyEndIndex)))
                .ToList();
            foreach (Match arrow in Regex.Matches(
                         method.SearchableBody,
                         @"=>\s*",
                         RegexOptions.CultureInvariant))
            {
                int arrowIndex = method.BodyStartIndex + arrow.Index;
                if (ranges.Any(range => range.Contains(arrowIndex)))
                    continue;
                int bodyStart = method.BodyStartIndex + arrow.Index + arrow.Length;
                while (bodyStart < method.BodyEndIndex
                       && char.IsWhiteSpace(method.SearchableCode[bodyStart]))
                    bodyStart++;
                int bodyEnd;
                if (bodyStart < method.BodyEndIndex
                    && method.SearchableCode[bodyStart] == '{')
                {
                    bodyEnd = FindMatchingDelimiter(
                        method.SearchableCode,
                        bodyStart,
                        '{',
                        '}',
                        method.BodyEndIndex);
                }
                else
                {
                    bodyEnd = FindLambdaExpressionEnd(
                        method.SearchableCode,
                        bodyStart,
                        method.BodyEndIndex);
                }
                ranges.Add(new SourceRange(arrowIndex, bodyEnd + 1));
            }

            foreach (Match deadBranch in Regex.Matches(
                         method.SearchableBody,
                         @"\bif\s*\(\s*false\s*\)\s*\{",
                         RegexOptions.CultureInvariant))
            {
                int openingBrace = method.BodyStartIndex
                    + deadBranch.Index
                    + deadBranch.Value.LastIndexOf('{');
                int closingBrace = FindMatchingDelimiter(
                    method.SearchableCode,
                    openingBrace,
                    '{',
                    '}',
                    method.BodyEndIndex);
                Assert.GreaterOrEqual(closingBrace, 0, diagnosticIdentity);
                ranges.Add(new SourceRange(
                    method.BodyStartIndex + deadBranch.Index,
                    closingBrace + 1));
            }
            return ranges;
        }

        private static int FindLambdaExpressionEnd(
            string source,
            int start,
            int exclusiveEnd)
        {
            int parentheses = 0;
            int brackets = 0;
            int braces = 0;
            for (int cursor = start; cursor < exclusiveEnd; cursor++)
            {
                switch (source[cursor])
                {
                    case '(':
                        parentheses++;
                        break;
                    case ')':
                        if (parentheses == 0 && brackets == 0 && braces == 0)
                            return cursor;
                        parentheses--;
                        break;
                    case '[':
                        brackets++;
                        break;
                    case ']':
                        if (brackets == 0 && parentheses == 0 && braces == 0)
                            return cursor;
                        brackets--;
                        break;
                    case '{':
                        braces++;
                        break;
                    case '}':
                        if (braces == 0 && parentheses == 0 && brackets == 0)
                            return cursor;
                        braces--;
                        break;
                    case ',':
                    case ';':
                        if (parentheses == 0 && brackets == 0 && braces == 0)
                            return cursor;
                        break;
                }
            }
            return Math.Max(start, exclusiveEnd - 1);
        }

        private static bool TryParseMethodHeader(
            string header,
            out string name,
            out string parameters)
        {
            name = string.Empty;
            parameters = string.Empty;
            if (string.IsNullOrWhiteSpace(header))
                return false;

            int closeParenthesis = header.Length - 1;
            while (closeParenthesis >= 0 && char.IsWhiteSpace(header[closeParenthesis]))
                closeParenthesis--;
            if (closeParenthesis < 0 || header[closeParenthesis] != ')')
                return false;

            int openParenthesis = FindMatchingOpeningDelimiter(
                header,
                closeParenthesis,
                '(',
                ')');
            if (openParenthesis < 0)
                return false;
            string suffix = header.Substring(closeParenthesis + 1).Trim();
            if (suffix.Length > 0
                && !Regex.IsMatch(
                    suffix,
                    @"^where\b",
                    RegexOptions.CultureInvariant))
                return false;

            int nameEnd = openParenthesis - 1;
            while (nameEnd >= 0 && char.IsWhiteSpace(header[nameEnd]))
                nameEnd--;
            if (nameEnd >= 0 && header[nameEnd] == '>')
            {
                int genericOpen = FindMatchingOpeningDelimiter(header, nameEnd, '<', '>');
                if (genericOpen < 0)
                    return false;
                nameEnd = genericOpen - 1;
                while (nameEnd >= 0 && char.IsWhiteSpace(header[nameEnd]))
                    nameEnd--;
            }

            int nameStart = nameEnd;
            while (nameStart >= 0 && IsCSharpIdentifierCharacter(header[nameStart]))
                nameStart--;
            nameStart++;
            if (nameStart > nameEnd)
                return false;
            name = header.Substring(nameStart, nameEnd - nameStart + 1).TrimStart('@');
            var nonMethodNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "if", "for", "foreach", "while", "switch", "catch", "lock", "using",
                "fixed", "checked", "unchecked", "nameof", "typeof", "default", "new",
            };
            if (nonMethodNames.Contains(name))
                return false;

            string declarationPrefix = header.Substring(0, nameStart);
            int lastAttributeEnd = declarationPrefix.LastIndexOf(']');
            if (lastAttributeEnd >= 0)
                declarationPrefix = declarationPrefix.Substring(lastAttributeEnd + 1);
            if (declarationPrefix.Contains("=>", StringComparison.Ordinal)
                || declarationPrefix.Contains("=", StringComparison.Ordinal)
                || Regex.IsMatch(
                    declarationPrefix,
                    @"\bnew\s*$",
                    RegexOptions.CultureInvariant))
                return false;
            if (!Regex.IsMatch(
                    declarationPrefix,
                    @"[A-Za-z_]\w*",
                    RegexOptions.CultureInvariant))
                return false;

            parameters = header.Substring(
                openParenthesis + 1,
                closeParenthesis - openParenthesis - 1);
            return true;
        }

        private static int FindPreviousMemberBoundary(string source, int start, int minimum)
        {
            for (int cursor = start - 1; cursor >= minimum; cursor--)
            {
                char current = source[cursor];
                if (current == ';' || current == '{' || current == '}')
                    return cursor + 1;
            }
            return minimum;
        }

        private static int FindMatchingDelimiter(
            string source,
            int openingIndex,
            char opening,
            char closing,
            int exclusiveEnd)
        {
            int depth = 0;
            for (int cursor = openingIndex; cursor < exclusiveEnd; cursor++)
            {
                if (source[cursor] == opening)
                    depth++;
                else if (source[cursor] == closing && --depth == 0)
                    return cursor;
            }
            return -1;
        }

        private static int FindMatchingOpeningDelimiter(
            string source,
            int closingIndex,
            char opening,
            char closing)
        {
            int depth = 0;
            for (int cursor = closingIndex; cursor >= 0; cursor--)
            {
                if (source[cursor] == closing)
                    depth++;
                else if (source[cursor] == opening && --depth == 0)
                    return cursor;
            }
            return -1;
        }

        private static int FindExpressionBodyTerminator(
            string source,
            int start,
            int exclusiveEnd)
        {
            int parentheses = 0;
            int brackets = 0;
            int braces = 0;
            for (int cursor = start; cursor < exclusiveEnd; cursor++)
            {
                switch (source[cursor])
                {
                    case '(':
                        parentheses++;
                        break;
                    case ')':
                        parentheses--;
                        break;
                    case '[':
                        brackets++;
                        break;
                    case ']':
                        brackets--;
                        break;
                    case '{':
                        braces++;
                        break;
                    case '}':
                        if (braces == 0)
                            return -1;
                        braces--;
                        break;
                    case ';':
                        if (parentheses == 0 && brackets == 0 && braces == 0)
                            return cursor;
                        break;
                }
            }
            return -1;
        }

        private static string BuildFoundationCallPattern(string foundationMethodName)
        {
            return @"\bESWindowFoundation\s*\.\s*"
                + Regex.Escape(foundationMethodName)
                + @"\s*\(\s*this\s*\)";
        }

        private static bool IsCSharpIdentifierCharacter(char value)
        {
            return value == '@' || value == '_' || char.IsLetterOrDigit(value);
        }

        private static int GetSourceLineNumber(string source, int index)
        {
            int line = 1;
            for (int cursor = 0; cursor < index && cursor < source.Length; cursor++)
                if (source[cursor] == '\n')
                    line++;
            return line;
        }

        private enum SourceTransitionKind : byte
        {
            Unbind,
            Clear,
            BindFull,
            BindTransient,
            BindStandardHost,
        }

        private sealed class SourceTransitionEvent
        {
            internal SourceTransitionEvent(SourceTransitionKind kind, int sourceIndex)
            {
                Kind = kind;
                SourceIndex = sourceIndex;
            }

            internal SourceTransitionKind Kind { get; }
            internal int SourceIndex { get; }
        }

        private sealed class SourceEventOccurrence
        {
            internal SourceEventOccurrence(
                int sourceIndex,
                IReadOnlyList<SourceTransitionEvent> events)
            {
                SourceIndex = sourceIndex;
                Events = events;
            }

            internal int SourceIndex { get; }
            internal IReadOnlyList<SourceTransitionEvent> Events { get; }
        }

        private sealed class SourceRange
        {
            internal SourceRange(int startIndex, int exclusiveEndIndex)
            {
                StartIndex = startIndex;
                ExclusiveEndIndex = exclusiveEndIndex;
            }

            internal int StartIndex { get; }
            internal int ExclusiveEndIndex { get; }
            internal bool Contains(int index)
                => StartIndex <= index && index < ExclusiveEndIndex;
        }

        private sealed class MonoScriptSourceRecord
        {
            internal MonoScriptSourceRecord(
                string projectRelativePath,
                Type primaryType,
                IReadOnlyList<SourceWindowTypeDeclaration> declarations)
            {
                ProjectRelativePath = projectRelativePath;
                PrimaryType = primaryType;
                Declarations = declarations;
            }

            internal string ProjectRelativePath { get; }
            internal Type PrimaryType { get; }
            internal IReadOnlyList<SourceWindowTypeDeclaration> Declarations { get; }
        }

        private sealed class ESOwnedWindowSource
        {
            internal ESOwnedWindowSource(
                Type windowType,
                string projectRelativePath,
                SourceWindowTypeDeclaration declaration)
            {
                WindowType = windowType;
                ProjectRelativePath = projectRelativePath;
                Declaration = declaration;
            }

            internal Type WindowType { get; }
            internal string ProjectRelativePath { get; }
            internal SourceWindowTypeDeclaration Declaration { get; }
        }

        private sealed class SourceWindowTypeDeclaration
        {
            internal SourceWindowTypeDeclaration(
                string name,
                string baseType,
                string path,
                string source,
                string searchableCode,
                int bodyOpenIndex,
                int bodyCloseIndex,
                bool isAbstract)
            {
                Name = name;
                BaseType = baseType;
                Path = path;
                Source = source;
                SearchableCode = searchableCode;
                BodyOpenIndex = bodyOpenIndex;
                BodyCloseIndex = bodyCloseIndex;
                IsAbstract = isAbstract;
            }

            internal string Name { get; }
            internal string BaseType { get; }
            internal string Path { get; }
            internal string Source { get; }
            internal string SearchableCode { get; }
            internal int BodyOpenIndex { get; }
            internal int BodyCloseIndex { get; }
            internal bool IsAbstract { get; }
            internal string DiagnosticIdentity => Path + "|" + Name;
            internal string SearchableBody => SearchableCode.Substring(
                BodyOpenIndex + 1,
                BodyCloseIndex - BodyOpenIndex - 1);
        }

        private sealed class SourceMethodDeclaration
        {
            internal SourceMethodDeclaration(
                string name,
                string parameters,
                string searchableCode,
                int declarationStartIndex,
                int bodyStartIndex,
                int bodyEndIndex)
            {
                Name = name;
                Parameters = parameters;
                SearchableCode = searchableCode;
                DeclarationStartIndex = declarationStartIndex;
                BodyStartIndex = bodyStartIndex;
                BodyEndIndex = bodyEndIndex;
            }

            internal string Name { get; }
            internal string Parameters { get; }
            internal string SearchableCode { get; }
            internal int DeclarationStartIndex { get; }
            internal int BodyStartIndex { get; }
            internal int BodyEndIndex { get; }
            internal string SearchableBody => SearchableCode.Substring(
                BodyStartIndex,
                BodyEndIndex - BodyStartIndex);
        }

        private static string GetSimpleTypeName(string typeName)
        {
            string normalized = (typeName ?? string.Empty).Replace("global::", string.Empty);
            int separator = normalized.LastIndexOf('.');
            return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
        }

        private static string GetSearchableCSharpCode(string source)
        {
            return MaskCSharpStringAndCharacterLiterals(
                StripCSharpComments(source ?? string.Empty));
        }

        private static void AssertNoLegacyAdvancedDialogCompatibilityBypass(
            string searchableCode,
            string diagnosticIdentity)
        {
            Assert.IsFalse(
                LegacyAdvancedDialogMemberReferencePattern.IsMatch(searchableCode),
                "生产调用必须直接进入 ESDialogService，禁止使用 ESAdvancedDialogWindow.Show* 兼容入口："
                + diagnosticIdentity);
            Assert.IsFalse(
                LegacyAdvancedDialogAliasPattern.IsMatch(searchableCode),
                "禁止通过类型别名隐藏 ESAdvancedDialogWindow 兼容入口："
                + diagnosticIdentity);
            Assert.IsFalse(
                LegacyAdvancedDialogStaticImportPattern.IsMatch(searchableCode),
                "禁止通过 using static 隐藏 ESAdvancedDialogWindow 兼容入口："
                + diagnosticIdentity);
        }

        private static void AssertNoNativeEditorWindowModalPresentationReferences(
            string source,
            string diagnosticIdentity)
        {
            string commentFreeSource = StripCSharpComments(source ?? string.Empty);
            string searchableCode = MaskCSharpStringAndCharacterLiterals(commentFreeSource);
            MatchCollection references = NativeEditorWindowModalReferencePattern.Matches(
                searchableCode);
            MatchCollection serviceEntryReferences =
                AdvancedDialogServiceModalEntryReferencePattern.Matches(searchableCode);
            MatchCollection reflectionReferences =
                NativeEditorWindowModalReflectionPattern.Matches(commentFreeSource);
            Assert.AreEqual(
                0,
                references.Count,
                "生产 ES Editor 代码不得直接调用、缓存或转发 EditorWindow.ShowModalUtility；"
                + "SurfaceKind.Popup/Utility 也不能绕过 ESDialogService："
                + diagnosticIdentity);
            Assert.AreEqual(
                0,
                serviceEntryReferences.Count,
                "ESAdvancedDialogWindow 原生模态内部入口只能由同文件 ESDialogService.OpenNow 调用："
                + diagnosticIdentity);
            Assert.AreEqual(
                0,
                reflectionReferences.Count,
                "生产 ES Editor 代码不得通过反射解析 EditorWindow.ShowModalUtility："
                + diagnosticIdentity);
        }

        private static void AssertAdvancedDialogOwnsOnlyNativeModalPresentationCall(
            string source,
            string projectRelativePath)
        {
            string commentFreeSource = StripCSharpComments(source ?? string.Empty);
            string searchableCode = MaskCSharpStringAndCharacterLiterals(commentFreeSource);
            MatchCollection references = NativeEditorWindowModalReferencePattern.Matches(
                searchableCode);
            MatchCollection calls = NativeEditorWindowModalCallPattern.Matches(searchableCode);
            MatchCollection serviceEntryReferences =
                AdvancedDialogServiceModalEntryReferencePattern.Matches(searchableCode);
            MatchCollection serviceEntryCalls =
                AdvancedDialogServiceModalEntryCallPattern.Matches(searchableCode);
            MatchCollection reflectionReferences =
                NativeEditorWindowModalReflectionPattern.Matches(commentFreeSource);
            Assert.AreEqual(
                1,
                references.Count,
                "ESAdvancedDialogWindow 必须只保留一个原生模态引用；方法组和额外转发均被禁止："
                + projectRelativePath);
            Assert.AreEqual(
                1,
                calls.Count,
                "唯一原生模态引用必须是 ShowModalUtility 的直接调用："
                + projectRelativePath);
            Assert.AreEqual(
                references[0].Index,
                calls[0].Index,
                "原生模态白名单不得由方法组或声明占用：" + projectRelativePath);
            Assert.AreEqual(
                2,
                serviceEntryReferences.Count,
                "原生模态内部入口必须且只能保留一个真实声明与一个服务调用："
                + projectRelativePath);
            Assert.AreEqual(
                1,
                serviceEntryCalls.Count,
                "原生模态内部入口不得形成额外调用或方法组：" + projectRelativePath);
            Assert.AreEqual(
                0,
                reflectionReferences.Count,
                "ESAdvancedDialog 实现不得通过反射取得原生模态入口："
                + projectRelativePath);

            SourceWindowTypeDeclaration[] declarations = ExtractWindowTypeDeclarations(
                    source,
                    projectRelativePath)
                .Where(item => string.Equals(
                    item.Name,
                    nameof(ESAdvancedDialogWindow),
                    StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(
                1,
                declarations.Length,
                "原生模态白名单必须唯一绑定 ESAdvancedDialogWindow 类型："
                + projectRelativePath);
            SourceWindowTypeDeclaration declaration = declarations[0];
            Assert.Greater(references[0].Index, declaration.BodyOpenIndex);
            Assert.Less(references[0].Index, declaration.BodyCloseIndex);

            SourceMethodDeclaration[] openMethods = ExtractTopLevelMethods(declaration)
                .Where(method => string.Equals(
                        method.Name,
                        "Internal_OpenFromDialogService",
                        StringComparison.Ordinal)
                    && Regex.IsMatch(
                        method.Parameters,
                        @"^\s*bool\s+@?modal\s*$",
                        RegexOptions.CultureInvariant))
                .ToArray();
            Assert.AreEqual(
                1,
                openMethods.Length,
                "ESAdvancedDialogWindow 必须保留唯一 Internal_OpenFromDialogService(bool modal) 真实入口："
                + projectRelativePath);
            SourceMethodDeclaration open = openMethods[0];
            Assert.GreaterOrEqual(references[0].Index, open.BodyStartIndex);
            Assert.Less(references[0].Index, open.BodyEndIndex);
            Assert.IsTrue(
                ContainsDirectExecutableCall(
                    open,
                    NativeEditorWindowModalCallPattern.ToString(),
                    declaration.DiagnosticIdentity),
                "ShowModalUtility 必须由 ESAdvancedDialogWindow.Internal_OpenFromDialogService(bool modal) 直接执行，"
                + "不得藏入局部函数或 lambda："
                + projectRelativePath);
        }

        private static void AssertAdvancedDialogCreateCallIsInsideServiceOpenNow(string source)
        {
            string searchableCode = GetSearchableCSharpCode(source);
            MatchCollection calls = AdvancedDialogCreateCallPattern.Matches(searchableCode);
            Assert.AreEqual(
                1,
                calls.Count,
                "ESAdvancedDialogWindow.Create 必须只保留一个集中创建调用点。");

            Match service = Regex.Match(
                searchableCode,
                @"\bpublic\s+static\s+class\s+ESDialogService\b[^\{]*(?<body>\{)",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(service.Success, "缺少 ESDialogService 类型声明。");
            int serviceOpen = service.Groups["body"].Index;
            int serviceClose = FindMatchingDelimiter(
                searchableCode,
                serviceOpen,
                '{',
                '}',
                searchableCode.Length);
            Assert.Greater(serviceClose, serviceOpen, "ESDialogService 类型体不完整。");

            Match openNow = Regex.Match(
                searchableCode,
                @"\bprivate\s+static\s+ESAdvancedDialogWindow\s+OpenNow\s*\([^\{;]*(?<body>\{)",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(openNow.Success, "缺少 ESDialogService.OpenNow 集中创建入口。");
            int openNowOpen = openNow.Groups["body"].Index;
            int openNowClose = FindMatchingDelimiter(
                searchableCode,
                openNowOpen,
                '{',
                '}',
                searchableCode.Length);
            Assert.Greater(openNowClose, openNowOpen, "ESDialogService.OpenNow 方法体不完整。");
            Assert.Greater(openNowOpen, serviceOpen, "OpenNow 必须属于 ESDialogService。");
            Assert.Less(openNowClose, serviceClose, "OpenNow 必须属于 ESDialogService。");
            Assert.Greater(calls[0].Index, openNowOpen, "Create 调用必须位于 OpenNow 方法体内。");
            Assert.Less(calls[0].Index, openNowClose, "Create 调用必须位于 OpenNow 方法体内。");
            MatchCollection modalEntryCalls =
                AdvancedDialogServiceModalEntryCallPattern.Matches(searchableCode);
            Assert.AreEqual(
                1,
                modalEntryCalls.Count,
                "ESDialogService.OpenNow 必须唯一调用原生模态内部入口。");
            Assert.Greater(
                modalEntryCalls[0].Index,
                openNowOpen,
                "原生模态内部入口调用必须位于 ESDialogService.OpenNow 方法体内。");
            Assert.Less(
                modalEntryCalls[0].Index,
                openNowClose,
                "原生模态内部入口调用必须位于 ESDialogService.OpenNow 方法体内。");
        }

        private static void AssertNoWindowFactoryTokensHiddenInLiterals(
            string commentFreeSource,
            string searchableCode,
            string diagnosticIdentity)
        {
            Assert.AreEqual(
                WindowFactoryTokenPattern.Matches(commentFreeSource).Count,
                WindowFactoryTokenPattern.Matches(searchableCode).Count,
                "禁止在字符串或字符字面量中隐藏/伪造 EditorWindow 创建入口："
                + diagnosticIdentity);
        }

        private static void AssertAdvancedDialogCreateSurfaceIsClosed(string source)
        {
            string commentFreeSource = StripCSharpComments(source);
            string searchableCode = MaskCSharpStringAndCharacterLiterals(commentFreeSource);
            Match windowType = Regex.Match(
                searchableCode,
                @"\bclass\s+ESAdvancedDialogWindow\b[^\{;]*(?<body>\{)",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(windowType.Success, "缺少 ESAdvancedDialogWindow 类型定义。");
            int bodyOpen = windowType.Groups["body"].Index;
            int bodyClose = FindMatchingDelimiter(
                searchableCode,
                bodyOpen,
                '{',
                '}',
                searchableCode.Length);
            Assert.Greater(bodyClose, bodyOpen, "ESAdvancedDialogWindow 类型体不完整。");

            string windowTypeSource = commentFreeSource.Substring(
                bodyOpen,
                bodyClose - bodyOpen + 1);
            MatchCollection unqualifiedCreateReferences = Regex.Matches(
                windowTypeSource,
                @"(?<![.A-Za-z0-9_])Create\b",
                RegexOptions.CultureInvariant);
            Assert.AreEqual(
                1,
                unqualifiedCreateReferences.Count,
                "ESAdvancedDialogWindow.Create 在窗口类型内只能出现一次方法定义；"
                + "无前缀调用或方法组必须改走 ESDialogService。");
        }

        private static IReadOnlyList<string> FindGenericDirectWindowCreations(
            string searchableCode,
            ISet<string> editorWindowTypeNames,
            IReadOnlyDictionary<string, string> sourceAliases = null)
        {
            IReadOnlyDictionary<string, string> aliases =
                sourceAliases ?? ExtractSourceTypeAliases(searchableCode);
            var result = new List<string>();
            foreach (Match match in GenericDirectWindowCreationPattern.Matches(searchableCode))
            {
                string typeName = ResolveSourceTypeName(match.Groups["type"].Value, aliases);
                if (editorWindowTypeNames.Contains(typeName))
                    result.Add(typeName);
            }
            return result;
        }

        private static IReadOnlyList<string> FindDynamicGetWindowReceivers(
            string searchableCode,
            ISet<string> editorWindowTypeNames,
            IReadOnlyDictionary<string, string> sourceAliases = null)
        {
            IReadOnlyDictionary<string, string> aliases =
                sourceAliases ?? ExtractSourceTypeAliases(searchableCode);
            var result = new List<string>();
            foreach (Match match in DynamicGetWindowPattern.Matches(searchableCode))
            {
                string receiver = ResolveSourceTypeName(match.Groups["receiver"].Value, aliases);
                if (editorWindowTypeNames.Contains(receiver))
                    result.Add(receiver);
            }
            return result;
        }

        private static void AssertNoDynamicWindowCreationBypass(
            string searchableCode,
            string diagnosticIdentity,
            ISet<string> editorWindowTypeNames,
            IReadOnlyDictionary<string, string> sourceAliases = null)
        {
            IReadOnlyDictionary<string, string> aliases =
                sourceAliases ?? ExtractSourceTypeAliases(searchableCode);
            foreach (Match constraint in GenericWindowConstraintPattern.Matches(searchableCode))
            {
                bool windowConstrained = WindowSourceTypeTokenPattern
                    .Matches(constraint.Groups["constraints"].Value)
                    .Cast<Match>()
                    .Select(match => ResolveSourceTypeName(match.Value, aliases))
                    .Any(editorWindowTypeNames.Contains);
                if (!windowConstrained)
                    continue;

                string parameter = Regex.Escape(constraint.Groups["parameter"].Value);
                Assert.IsFalse(
                    Regex.IsMatch(
                        searchableCode,
                        @"(?:CreateInstance|CreateWindow)\s*<\s*" + parameter + @"\s*>",
                        RegexOptions.CultureInvariant),
                    "禁止通过 EditorWindow 泛型约束绕过单实例入口："
                    + diagnosticIdentity + "|" + constraint.Groups["parameter"].Value);
            }

            foreach (Match match in DirectTypeofWindowCreationPattern.Matches(searchableCode))
            {
                string typeName = ResolveSourceTypeName(match.Groups["type"].Value, aliases);
                Assert.IsFalse(
                    editorWindowTypeNames.Contains(typeName),
                    "禁止通过 typeof/Activator 绕过 EditorWindow 单实例入口："
                    + diagnosticIdentity + "|" + typeName);
            }

            foreach (Match match in DynamicCreateAssignmentPattern.Matches(searchableCode))
            {
                string declaredType = ResolveSourceTypeName(
                    match.Groups["declared"].Value,
                    aliases);
                Assert.IsFalse(
                    editorWindowTypeNames.Contains(declaredType),
                    "禁止把运行时 Type 创建结果直接赋给 EditorWindow 派生类型："
                    + diagnosticIdentity + "|" + declaredType);
            }

            foreach (Regex castPattern in InlineWindowCastPatterns)
            {
                foreach (Match match in castPattern.Matches(searchableCode))
                {
                    string castType = ResolveSourceTypeName(match.Groups["cast"].Value, aliases);
                    Assert.IsFalse(
                        editorWindowTypeNames.Contains(castType),
                        "禁止通过运行时 Type 创建后转换为 EditorWindow 派生类型："
                        + diagnosticIdentity + "|" + castType);
                }
            }

            foreach (Match match in RuntimeCreateWindowPattern.Matches(searchableCode))
            {
                string receiver = ResolveSourceTypeName(match.Groups["receiver"].Value, aliases);
                Assert.IsFalse(
                    editorWindowTypeNames.Contains(receiver),
                    "禁止通过 EditorWindow.CreateWindow(Type) 绕过具体类型单实例入口："
                    + diagnosticIdentity + "|" + receiver);
            }

            var knownWindowTypeVariables = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in RuntimeTypeValuePattern.Matches(searchableCode))
            {
                string typeName = ResolveSourceTypeName(match.Groups["type"].Value, aliases);
                if (editorWindowTypeNames.Contains(typeName))
                    knownWindowTypeVariables[match.Groups["variable"].Value] = typeName;
            }

            foreach (Match match in RuntimeTypeCreationPattern.Matches(searchableCode))
            {
                if (!knownWindowTypeVariables.TryGetValue(
                        match.Groups["variable"].Value,
                        out string typeName))
                    continue;
                Assert.Fail(
                    "禁止先保存 Window Type 再通过运行时工厂创建："
                    + diagnosticIdentity + "|" + typeName);
            }

            MatchCollection resultCasts = RuntimeResultCastPattern.Matches(searchableCode);
            foreach (Match creation in DynamicCreateResultPattern.Matches(searchableCode))
            {
                string resultVariable = creation.Groups["result"].Value;
                foreach (Match cast in resultCasts)
                {
                    if (cast.Index <= creation.Index + creation.Length
                        || !string.Equals(
                            cast.Groups["result"].Value,
                            resultVariable,
                            StringComparison.Ordinal))
                        continue;
                    string castType = ResolveSourceTypeName(cast.Groups["cast"].Value, aliases);
                    Assert.IsFalse(
                        editorWindowTypeNames.Contains(castType),
                        "禁止把运行时创建结果跨语句转换为 EditorWindow 派生类型："
                        + diagnosticIdentity + "|" + castType);
                }
            }
        }

        private static Dictionary<string, string> ExtractSourceTypeAliases(string searchableCode)
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in SourceTypeAliasPattern.Matches(searchableCode))
            {
                aliases[NormalizeSourceTypeName(match.Groups["alias"].Value)] = NormalizeSourceTypeName(
                    match.Groups["target"].Value);
            }
            return aliases;
        }

        private static void AssertNoInternalLifecycleBypass(
            string searchableCode,
            string diagnosticIdentity)
        {
            Dictionary<string, string> aliases = ExtractSourceTypeAliases(searchableCode);
            foreach (KeyValuePair<string, string> alias in aliases)
            {
                Assert.IsFalse(
                    IsInternalPresentationType(alias.Value, aliases),
                    "生产代码不得为 ESEditorPresentation 建立 type alias："
                    + diagnosticIdentity
                    + "|"
                    + alias.Key);
            }

            foreach (Match import in StaticSourceTypeImportPattern.Matches(searchableCode))
            {
                string target = import.Groups["target"].Value;
                Assert.IsFalse(
                    IsInternalPresentationType(target, aliases),
                    "生产代码不得 using static ESEditorPresentation："
                    + diagnosticIdentity);
            }

            foreach (Match reference in InternalLifecycleMemberReferencePattern.Matches(
                         searchableCode))
            {
                string receiver = reference.Groups["receiver"].Value;
                Assert.IsFalse(
                    IsInternalPresentationType(receiver, aliases),
                    "生产代码不得绕过 ESWindowFoundation 生命周期与 owner API："
                    + diagnosticIdentity
                    + "|"
                    + reference.Groups["member"].Value);
            }
        }

        private static bool IsInternalPresentationType(
            string sourceName,
            IReadOnlyDictionary<string, string> aliases)
        {
            string resolved = NormalizeLifecycleSourceName(sourceName);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(resolved))
            {
                int separator = resolved.IndexOf('.');
                string head = separator < 0 ? resolved : resolved.Substring(0, separator);
                if (!aliases.TryGetValue(head, out string target))
                    break;
                resolved = NormalizeLifecycleSourceName(target)
                    + (separator < 0 ? string.Empty : resolved.Substring(separator));
            }

            return string.Equals(
                       resolved,
                       "ESEditorPresentation",
                       StringComparison.Ordinal)
                   || resolved.EndsWith(
                       ".ESEditorPresentation",
                       StringComparison.Ordinal);
        }

        private static string NormalizeLifecycleSourceName(string sourceName)
        {
            return Regex.Replace(sourceName ?? string.Empty, @"\s+", string.Empty)
                .Replace("global::", string.Empty)
                .Replace("::", ".")
                .Replace("@", string.Empty);
        }

        private static string ResolveSourceTypeName(
            string sourceTypeName,
            IReadOnlyDictionary<string, string> aliases)
        {
            string current = NormalizeSourceTypeName(sourceTypeName);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(current) && aliases.TryGetValue(current, out string target))
                current = NormalizeSourceTypeName(target);
            return GetSimpleTypeName(current);
        }

        private static string NormalizeSourceTypeName(string sourceTypeName)
        {
            return Regex.Replace(sourceTypeName ?? string.Empty, @"\s+", string.Empty)
                .Replace("global::", string.Empty)
                .Replace("@", string.Empty);
        }

        private static void AssertSourceContainsInOrder(
            string projectRelativePath,
            string first,
            string second)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, projectRelativePath + " 缺少：" + first);
            Assert.Greater(secondIndex, firstIndex, projectRelativePath + " 创建顺序不符合单实例合同。");
        }

        private static void AssertSourceContainsInMethodInOrder(
            string projectRelativePath,
            string methodSignature,
            params string[] requiredText)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.IsFalse(string.IsNullOrWhiteSpace(projectRoot));
            string path = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string source = ReadSourceText(path);
            int methodIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, projectRelativePath + " missing method: " + methodSignature);

            int nextMethodIndex = source.IndexOf(
                "\n        private static ",
                methodIndex + methodSignature.Length,
                StringComparison.Ordinal);
            string methodSource = nextMethodIndex < 0
                ? source.Substring(methodIndex)
                : source.Substring(methodIndex, nextMethodIndex - methodIndex);
            int previousIndex = -1;
            foreach (string text in requiredText)
            {
                int index = methodSource.IndexOf(text, StringComparison.Ordinal);
                Assert.GreaterOrEqual(index, 0, methodSignature + " missing: " + text);
                Assert.Greater(index, previousIndex, methodSignature + " source order mismatch");
                previousIndex = index;
            }
        }

        private enum CSharpLexicalState : byte
        {
            Code,
            LineComment,
            BlockComment,
            RegularString,
            VerbatimString,
            Character,
            RawString,
        }

        private static string StripCSharpComments(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source ?? string.Empty;

            char[] result = source.ToCharArray();
            CSharpLexicalState state = CSharpLexicalState.Code;
            int rawQuoteCount = 0;
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                switch (state)
                {
                    case CSharpLexicalState.Code:
                        if (current == '/' && i + 1 < source.Length && source[i + 1] == '/')
                        {
                            result[i] = ' ';
                            result[++i] = ' ';
                            state = CSharpLexicalState.LineComment;
                        }
                        else if (current == '/' && i + 1 < source.Length && source[i + 1] == '*')
                        {
                            result[i] = ' ';
                            result[++i] = ' ';
                            state = CSharpLexicalState.BlockComment;
                        }
                        else if (current == '\'')
                        {
                            state = CSharpLexicalState.Character;
                        }
                        else if (current == '"')
                        {
                            int quoteCount = CountConsecutiveQuotes(source, i);
                            if (quoteCount >= 3)
                            {
                                rawQuoteCount = quoteCount;
                                i += quoteCount - 1;
                                state = CSharpLexicalState.RawString;
                            }
                            else
                            {
                                state = IsVerbatimStringOpening(source, i)
                                    ? CSharpLexicalState.VerbatimString
                                    : CSharpLexicalState.RegularString;
                            }
                        }
                        break;

                    case CSharpLexicalState.LineComment:
                        if (current == '\r' || current == '\n')
                            state = CSharpLexicalState.Code;
                        else
                            result[i] = ' ';
                        break;

                    case CSharpLexicalState.BlockComment:
                        if (current == '*' && i + 1 < source.Length && source[i + 1] == '/')
                        {
                            result[i] = ' ';
                            result[++i] = ' ';
                            state = CSharpLexicalState.Code;
                        }
                        else if (current != '\r' && current != '\n')
                        {
                            result[i] = ' ';
                        }
                        break;

                    case CSharpLexicalState.RegularString:
                        if (current == '\\' && i + 1 < source.Length)
                            i++;
                        else if (current == '"')
                            state = CSharpLexicalState.Code;
                        break;

                    case CSharpLexicalState.VerbatimString:
                        if (current != '"')
                            break;
                        if (i + 1 < source.Length && source[i + 1] == '"')
                            i++;
                        else
                            state = CSharpLexicalState.Code;
                        break;

                    case CSharpLexicalState.Character:
                        if (current == '\\' && i + 1 < source.Length)
                            i++;
                        else if (current == '\'')
                            state = CSharpLexicalState.Code;
                        break;

                    case CSharpLexicalState.RawString:
                        if (current != '"')
                            break;
                        int closingQuoteCount = CountConsecutiveQuotes(source, i);
                        if (closingQuoteCount >= rawQuoteCount)
                        {
                            i += rawQuoteCount - 1;
                            state = CSharpLexicalState.Code;
                        }
                        break;
                }
            }

            return new string(result);
        }

        private static string MaskCSharpStringAndCharacterLiterals(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source ?? string.Empty;

            char[] result = source.ToCharArray();
            CSharpLexicalState state = CSharpLexicalState.Code;
            int rawQuoteCount = 0;
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                switch (state)
                {
                    case CSharpLexicalState.Code:
                        if (current == '\'')
                        {
                            result[i] = ' ';
                            state = CSharpLexicalState.Character;
                        }
                        else if (current == '"')
                        {
                            int quoteCount = CountConsecutiveQuotes(source, i);
                            if (quoteCount >= 3)
                            {
                                rawQuoteCount = quoteCount;
                                for (int quote = 0; quote < quoteCount; quote++)
                                    result[i + quote] = ' ';
                                i += quoteCount - 1;
                                state = CSharpLexicalState.RawString;
                            }
                            else
                            {
                                result[i] = ' ';
                                state = IsVerbatimStringOpening(source, i)
                                    ? CSharpLexicalState.VerbatimString
                                    : CSharpLexicalState.RegularString;
                            }
                        }
                        break;

                    case CSharpLexicalState.RegularString:
                        if (current != '\r' && current != '\n')
                            result[i] = ' ';
                        if (current == '\\' && i + 1 < source.Length)
                        {
                            if (source[i + 1] != '\r' && source[i + 1] != '\n')
                                result[i + 1] = ' ';
                            i++;
                        }
                        else if (current == '"')
                        {
                            state = CSharpLexicalState.Code;
                        }
                        break;

                    case CSharpLexicalState.VerbatimString:
                        if (current != '\r' && current != '\n')
                            result[i] = ' ';
                        if (current != '"')
                            break;
                        if (i + 1 < source.Length && source[i + 1] == '"')
                        {
                            result[++i] = ' ';
                        }
                        else
                        {
                            state = CSharpLexicalState.Code;
                        }
                        break;

                    case CSharpLexicalState.Character:
                        if (current != '\r' && current != '\n')
                            result[i] = ' ';
                        if (current == '\\' && i + 1 < source.Length)
                        {
                            if (source[i + 1] != '\r' && source[i + 1] != '\n')
                                result[i + 1] = ' ';
                            i++;
                        }
                        else if (current == '\'')
                        {
                            state = CSharpLexicalState.Code;
                        }
                        break;

                    case CSharpLexicalState.RawString:
                        if (current != '\r' && current != '\n')
                            result[i] = ' ';
                        if (current != '"')
                            break;
                        int closingQuoteCount = CountConsecutiveQuotes(source, i);
                        if (closingQuoteCount < rawQuoteCount)
                            break;
                        for (int quote = 1; quote < rawQuoteCount; quote++)
                            result[i + quote] = ' ';
                        i += rawQuoteCount - 1;
                        state = CSharpLexicalState.Code;
                        break;
                }
            }

            return new string(result);
        }

        private static int CountConsecutiveQuotes(string source, int startIndex)
        {
            int count = 0;
            while (startIndex + count < source.Length && source[startIndex + count] == '"')
                count++;
            return count;
        }

        private static bool IsVerbatimStringOpening(string source, int quoteIndex)
        {
            return quoteIndex > 0 && source[quoteIndex - 1] == '@'
                || quoteIndex > 1
                && source[quoteIndex - 2] == '@'
                && source[quoteIndex - 1] == '$';
        }

        private static string BuildNativeDialogCallsiteSignature(
            string projectRelativePath,
            string commentFreeSource,
            string searchableCode,
            int callIndex)
        {
            int line = 1;
            int column = 1;
            for (int i = 0; i < callIndex; i++)
            {
                if (commentFreeSource[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            string member = FindContainingMemberIdentity(searchableCode, callIndex);
            string invocation = ExtractCSharpInvocation(
                commentFreeSource,
                searchableCode,
                callIndex);
            return NormalizeFingerprintText(projectRelativePath)
                + "|" + member
                + "|" + line + ":" + column
                + "|" + invocation;
        }

        private static string FindContainingMemberIdentity(string searchableCode, int callIndex)
        {
            var openBraces = new List<int>();
            for (int i = 0; i < callIndex; i++)
            {
                if (searchableCode[i] == '{')
                    openBraces.Add(i);
                else if (searchableCode[i] == '}' && openBraces.Count > 0)
                    openBraces.RemoveAt(openBraces.Count - 1);
            }

            for (int i = openBraces.Count - 1; i >= 0; i--)
            {
                int braceIndex = openBraces[i];
                int boundary = -1;
                for (int cursor = braceIndex - 1; cursor >= 0; cursor--)
                {
                    char current = searchableCode[cursor];
                    if (current == ';' || current == '}' || current == '{')
                    {
                        boundary = cursor;
                        break;
                    }
                }

                string declaration = searchableCode
                    .Substring(boundary + 1, braceIndex - boundary - 1)
                    .Trim();
                Match match = NativeDialogMemberDeclarationPattern.Match(declaration);
                if (match.Success
                    && !NativeDialogControlKeywords.Contains(match.Groups["name"].Value))
                    return match.Groups["name"].Value;
            }

            return "<top-level>";
        }

        private static string ExtractCSharpInvocation(
            string commentFreeSource,
            string searchableCode,
            int callIndex)
        {
            Assert.AreEqual(
                commentFreeSource.Length,
                searchableCode.Length,
                "C# 词法掩码必须保持源索引稳定。");
            int openingParenthesis = searchableCode.IndexOf('(', callIndex);
            Assert.GreaterOrEqual(openingParenthesis, callIndex, "原生对话框调用缺少左括号。");
            int depth = 0;
            for (int i = openingParenthesis; i < searchableCode.Length; i++)
            {
                if (searchableCode[i] == '(')
                {
                    depth++;
                }
                else if (searchableCode[i] == ')' && --depth == 0)
                {
                    return NormalizeFingerprintText(
                        commentFreeSource.Substring(callIndex, i - callIndex + 1));
                }
            }

            Assert.Fail("原生对话框调用缺少闭合右括号。");
            return string.Empty;
        }

        private static string ComputeStableSha256(IEnumerable<string> signatures)
        {
            Assert.IsNotNull(signatures);
            string[] ordered = signatures
                .Select(NormalizeFingerprintText)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string payload = "ES_NATIVE_DIALOG_CALLSITE_V1\n" + string.Join("\n---\n", ordered);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
                hash = algorithm.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        private static string NormalizeFingerprintText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();
        }

        private static string ReadSourceText(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static void SetField(Type ownerType, object target, string fieldName, object value)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            field.SetValue(target, value);
        }

        private static T GetFieldValue<T>(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            return (T)field.GetValue(target);
        }

        private static T InvokePrivate<T>(Type ownerType, string methodName, params object[] arguments)
        {
            MethodInfo method = ownerType
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            Assert.IsNotNull(method, ownerType.FullName + "." + methodName);
            object result = method.Invoke(null, arguments);
            return result == null ? default : (T)result;
        }

        private static void AssertFieldIsNull(Type ownerType, object target, string fieldName)
        {
            FieldInfo field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, ownerType.FullName + "." + fieldName);
            Assert.IsNull(field.GetValue(target), ownerType.FullName + "." + fieldName);
        }

        private static void AssertPendingOwnerKeyAbsent(string ownerKey)
        {
            Assert.IsFalse(
                PendingOwnerKeyExists(ownerKey),
                "不应保留 Pending owner key：" + ownerKey);
        }

        private static bool PendingOwnerKeyExists(string ownerKey)
        {
            Type presentationType = typeof(ESEditorPresentation);
            var pending = presentationType
                .GetField("pendingSleepOwners", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as IEnumerable;
            Assert.IsNotNull(pending);
            foreach (object item in pending)
            {
                if (item == null)
                    continue;
                FieldInfo keyField = item.GetType().GetField(
                    "ownerKey",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(keyField);
                if (string.Equals(ownerKey, keyField.GetValue(item) as string, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AssertSlotIsUnusedByOtherSleepingWindows(
            IDictionary bindings,
            Type bindingType,
            object requested,
            int slot)
        {
            foreach (DictionaryEntry entry in bindings)
            {
                object binding = entry.Value;
                if (binding == null || ReferenceEquals(binding, requested))
                    continue;
                bool sleeping = GetFieldValue<bool>(bindingType, binding, "semiSleeping");
                bool targetingSleep = GetFieldValue<bool>(
                    bindingType,
                    binding,
                    "semiSleepAnimating")
                    && GetFieldValue<bool>(bindingType, binding, "semiSleepTarget");
                if (!sleeping && !targetingSleep)
                    continue;
                Assert.AreNotEqual(
                    slot,
                    GetFieldValue<int>(bindingType, binding, "semiSleepSlot"));
            }
        }
    }
}
