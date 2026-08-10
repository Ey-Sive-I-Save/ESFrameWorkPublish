using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCommandPaletteTests
    {
        private string[] originalFavorites;
        private string[] originalRecent;
        private bool originalShortcutEnabled;
        private bool originalShortcutOverridden;
        private ShortcutBinding originalShortcutBinding;

        [SetUp]
        public void SetUp()
        {
            ESCommandPaletteRegistry.ResetForTests(true);
            originalFavorites = Copy(ESCommandPaletteRegistry.Favorites);
            originalRecent = Copy(ESCommandPaletteRegistry.Recent);
            originalShortcutEnabled = ESCommandPaletteShortcutSettings.Enabled;
            originalShortcutOverridden = ShortcutManager.instance.IsShortcutOverridden(ESCommandPaletteShortcutSettings.ShortcutId);
            originalShortcutBinding = ShortcutManager.instance.GetShortcutBinding(ESCommandPaletteShortcutSettings.ShortcutId);
            ESCommandPaletteRegistry.ResetForTests(false);
        }

        [TearDown]
        public void TearDown()
        {
            ESCommandPaletteShortcutSettings.SetEnabled(originalShortcutEnabled);
            if (originalShortcutOverridden)
            {
                ShortcutManager.instance.RebindShortcut(ESCommandPaletteShortcutSettings.ShortcutId, originalShortcutBinding);
            }
            else
            {
                ShortcutManager.instance.ClearShortcutOverride(ESCommandPaletteShortcutSettings.ShortcutId);
            }
            ESCommandPaletteRegistry.ResetForTests(true);
            ESCommandPaletteRegistry.SetStoredIdsForTests(originalFavorites, originalRecent);
        }

        [Test]
        public void RegisterProvider_RejectsEmptyAndDuplicateProviderIds()
        {
            ESCommandPaletteRegistrationResult empty = ESCommandPaletteRegistry.RegisterProvider(
                new TestProvider(string.Empty, "@", Array.Empty<ESCommandPaletteItem>()));
            Assert.That(empty.ProviderAccepted, Is.False);
            AssertDiagnostic(empty, ESCommandPaletteRegistrationCode.EmptyProviderId);

            var provider = new TestProvider("tests.windows", "@", new[] { WindowItem("first", "asset_window") });
            Assert.That(ESCommandPaletteRegistry.RegisterProvider(provider).ProviderAccepted, Is.True);

            ESCommandPaletteRegistrationResult duplicate = ESCommandPaletteRegistry.RegisterProvider(
                new TestProvider("tests.windows", "@", new[] { WindowItem("second", "so_data_window") }));
            Assert.That(duplicate.ProviderAccepted, Is.False);
            AssertDiagnostic(duplicate, ESCommandPaletteRegistrationCode.DuplicateProviderId);
            Assert.That(ESCommandPaletteRegistry.ItemCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisterProvider_RejectsLaterDuplicateItemWithoutOverwritingFirst()
        {
            var provider = new TestProvider("tests.duplicates", "@", new[]
            {
                WindowItem("same", "asset_window"),
                WindowItem("same", "so_data_window")
            });

            ESCommandPaletteRegistrationResult result = ESCommandPaletteRegistry.RegisterProvider(provider);

            Assert.That(result.ProviderAccepted, Is.True);
            Assert.That(result.AcceptedItemCount, Is.EqualTo(1));
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.DuplicateItemId);
            Assert.That(ESCommandPaletteRegistry.TryGet("tests.duplicates:same", out ESCommandPaletteItem accepted), Is.True);
            Assert.That(accepted.TargetId, Is.EqualTo("asset_window"));
        }

        [Test]
        public void RegisterProvider_RejectsMutatingPrefixCategoryAndTargetViolations()
        {
            var provider = new TestProvider("tests.invalid", "@", new[]
            {
                new ESCommandPaletteItem("mutating", "Mutating", string.Empty, "窗口", string.Empty, "@", "asset_window", ESCommandPaletteActionKind.OpenWindow, true),
                new ESCommandPaletteItem("prefix", "Prefix", string.Empty, "窗口", string.Empty, "$", "asset_window", ESCommandPaletteActionKind.OpenWindow),
                new ESCommandPaletteItem("category", "Category", string.Empty, "AICommand", string.Empty, "@", "asset_window", ESCommandPaletteActionKind.CopyText),
                new ESCommandPaletteItem("menu", "Menu", string.Empty, "窗口", string.Empty, "@", "【ES】/不存在", ESCommandPaletteActionKind.OpenMenu),
                WindowItem("window", "missing_window"),
                new ESCommandPaletteItem("file", "File", string.Empty, "AICommand", string.Empty, "$", "Assets/Plugins/ES/AICommands/missing.md", ESCommandPaletteActionKind.OpenFile),
                new ESCommandPaletteItem("scene", "Scene", string.Empty, "场景", string.Empty, "#", "Assets/Missing.unity", ESCommandPaletteActionKind.Select)
            });

            ESCommandPaletteRegistrationResult result = ESCommandPaletteRegistry.RegisterProvider(provider);

            Assert.That(result.ProviderAccepted, Is.True);
            Assert.That(result.AcceptedItemCount, Is.Zero);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.MutatingItemRejected);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.PrefixMismatch);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.CategoryMismatch);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.MenuNotWhitelisted);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.WindowNotRegistered);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.FileNotAllowed);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.SceneNotRegistered);
        }

        [Test]
        public void Refresh_RetainsProvidersAndDoesNotDuplicateItems()
        {
            var provider = new TestProvider("tests.refresh", "@", new[] { WindowItem("asset", "asset_window") });
            ESCommandPaletteRegistry.RegisterProvider(provider);
            int providerCount = ESCommandPaletteRegistry.ProviderCount;

            ESCommandPaletteRegistry.Refresh();
            ESCommandPaletteRegistry.Refresh();

            Assert.That(ESCommandPaletteRegistry.ProviderCount, Is.EqualTo(providerCount));
            Assert.That(ESCommandPaletteRegistry.ItemCount, Is.EqualTo(1));
            Assert.That(provider.BuildCount, Is.EqualTo(3));
        }

        [Test]
        public void RegisterProvider_ItemEnumerationFailureDoesNotCommitPartialIndex()
        {
            var provider = new TestProvider("tests.transaction", "@", new ThrowingItemList(WindowItem("first", "asset_window")));

            ESCommandPaletteRegistrationResult result = ESCommandPaletteRegistry.RegisterProvider(provider);

            Assert.That(result.ProviderAccepted, Is.False);
            AssertDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed);
            Assert.That(ESCommandPaletteRegistry.ItemCount, Is.Zero);
            Assert.That(ESCommandPaletteRegistry.ProviderCount, Is.Zero);
        }

        [Test]
        public void StoredState_RemovesStaleAndDuplicateIdsAndAppliesLimits()
        {
            var items = new List<ESCommandPaletteItem>();
            for (int i = 0; i < 240; i++)
            {
                items.Add(WindowItem("item-" + i, "asset_window"));
            }
            ESCommandPaletteRegistry.RegisterProvider(new TestProvider("tests.state", "@", items));

            var favoriteIds = new List<string> { "missing:item", "tests.state:item-0", "tests.state:item-0" };
            var recentIds = new List<string> { "missing:item", "tests.state:item-0", "tests.state:item-0" };
            for (int i = 1; i < 240; i++)
            {
                favoriteIds.Add("tests.state:item-" + i);
                recentIds.Add("tests.state:item-" + i);
            }

            ESCommandPaletteRegistry.SetStoredIdsForTests(favoriteIds, recentIds);

            Assert.That(ESCommandPaletteRegistry.Favorites.Count, Is.EqualTo(ESCommandPaletteRegistry.MaximumFavorites));
            Assert.That(ESCommandPaletteRegistry.Recent.Count, Is.EqualTo(ESCommandPaletteRegistry.MaximumRecent));
            Assert.That(ESCommandPaletteRegistry.Favorites, Does.Not.Contain("missing:item"));
            Assert.That(ESCommandPaletteRegistry.Recent, Does.Not.Contain("missing:item"));
            Assert.That(Count(ESCommandPaletteRegistry.Favorites, "tests.state:item-0"), Is.EqualTo(1));
            Assert.That(Count(ESCommandPaletteRegistry.Recent, "tests.state:item-0"), Is.EqualTo(1));
        }

        [Test]
        public void Search_EnforcesResultLimitAndRecordsAllocationBudget()
        {
            var items = new List<ESCommandPaletteItem>();
            for (int i = 0; i < 100; i++)
            {
                items.Add(WindowItem("item-" + i, "asset_window", "Window " + i));
            }
            ESCommandPaletteRegistry.RegisterProvider(new TestProvider("tests.search", "@", items));
            var engine = new ESCommandPaletteSearchEngine();

            engine.Search("@Window", ESCommandPaletteRegistry.AllItems);
            IReadOnlyList<ESCommandPaletteItem> results = engine.Search("@Window", ESCommandPaletteRegistry.AllItems);

            Assert.That(results.Count, Is.EqualTo(ESCommandPaletteSearchEngine.MaximumResults));
            Assert.That(engine.LastMetrics.ResultCount, Is.EqualTo(ESCommandPaletteSearchEngine.MaximumResults));
            Assert.That(engine.LastMetrics.CandidateCount, Is.EqualTo(100));
            Assert.That(engine.LastMetrics.IsWithinAllocationBudget, Is.True,
                "Search allocated " + engine.LastMetrics.AllocatedBytes + " bytes");
        }

        [Test]
        public void PathPolicy_RejectsAbsoluteTraversalAndMissingFiles()
        {
            Assert.That(
                ESCommandPalettePathPolicy.TryValidateAICommandFile(
                    Path.Combine(ESCommandPalettePathPolicy.ProjectRoot, "absolute.md"), out _, out _),
                Is.False);
            Assert.That(
                ESCommandPalettePathPolicy.TryValidateAICommandFile(
                    "Assets/Plugins/ES/AICommands/../outside.md", out _, out _),
                Is.False);
            Assert.That(
                ESCommandPalettePathPolicy.TryValidateAICommandFile(
                    "Assets/Plugins/ES/AICommands/missing.md", out _, out _),
                Is.False);
        }

        [Test]
        public void PathPolicy_AcceptsExistingStrictUtf8AICommand()
        {
            string root = Path.Combine(
                ESCommandPalettePathPolicy.ProjectRoot,
                ESCommandPalettePathPolicy.AICommandRoot.Replace('/', Path.DirectorySeparatorChar));
            string[] files = Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly);
            Assert.That(files.Length, Is.GreaterThan(0));
            string relative = files[0].Substring(ESCommandPalettePathPolicy.ProjectRoot.Length + 1).Replace('\\', '/');

            Assert.That(ESCommandPalettePathPolicy.TryValidateAICommandFile(relative, out string normalized, out string reason),
                Is.True, reason);
            Assert.That(normalized, Is.EqualTo(relative));
        }

        [Test]
        public void PathPolicy_AcceptsExistingGlobalDataAsset()
        {
            string root = Path.Combine(
                ESCommandPalettePathPolicy.ProjectRoot,
                ESCommandPalettePathPolicy.GlobalDataRoot.Replace('/', Path.DirectorySeparatorChar));
            string[] files = Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(0));

            string relative = files[0].Substring(ESCommandPalettePathPolicy.ProjectRoot.Length + 1).Replace('\\', '/');
            Assert.That(ESCommandPalettePathPolicy.IsRegisteredGlobalData(relative), Is.True, relative);
        }

        [Test]
        public void GlobalDataProvider_RegistersExistingEssosWithoutPathSlicing()
        {
            string root = Path.Combine(
                ESCommandPalettePathPolicy.ProjectRoot,
                ESCommandPalettePathPolicy.GlobalDataRoot.Replace('/', Path.DirectorySeparatorChar));
            string[] files = Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories);
            var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i]
                    .Substring(ESCommandPalettePathPolicy.ProjectRoot.Length + 1)
                    .Replace('\\', '/');
                ESSO asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ESSO>(relative);
                if (asset is IESGlobalData)
                {
                    expectedPaths.Add(relative);
                }
            }

            ESCommandPaletteRegistry.ResetForTests(true);

            var actualPaths = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<ESCommandPaletteItem> all = ESCommandPaletteRegistry.AllItems;
            for (int i = 0; i < all.Count; i++)
            {
                ESCommandPaletteItem item = all[i];
                if (item.Prefix == "G")
                {
                    actualPaths.Add(item.TargetId);
                    Assert.That(
                        ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId),
                        Is.True,
                        item.TargetId);
                }
            }

            Assert.That(expectedPaths.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(actualPaths, Is.SupersetOf(expectedPaths));
        }

        [Test]
        public void ItemContract_DoesNotExposeDelegatesOrUnityObjects()
        {
            PropertyInfo[] properties = typeof(ESCommandPaletteItem).GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                Type type = properties[i].PropertyType;
                Assert.That(typeof(Delegate).IsAssignableFrom(type), Is.False, properties[i].Name);
                Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(type), Is.False, properties[i].Name);
            }
        }

        [Test]
        public void PaletteSources_DoNotScanAssetsDuringSearchOrSwitchScenes()
        {
            string directory = Path.Combine(
                ESCommandPalettePathPolicy.ProjectRoot,
                "Assets/Plugins/ES/Editor/EditorTools/ESCommandPalette");
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string source = File.ReadAllText(files[i]);
                Assert.That(source, Does.Not.Contain("AssetDatabase.FindAssets"), files[i]);
                Assert.That(source, Does.Not.Contain("EditorSceneManager.OpenScene"), files[i]);
            }
        }

        [Test]
        public void ShortcutPreference_CanBeDisabledAndReenabled()
        {
            ESCommandPaletteShortcutSettings.SetEnabled(false);
            Assert.That(ESCommandPaletteShortcutSettings.Enabled, Is.False);
            Assert.That(
                ShortcutManager.instance.GetShortcutBinding(ESCommandPaletteShortcutSettings.ShortcutId),
                Is.EqualTo(ShortcutBinding.empty));
            ESCommandPaletteShortcutSettings.SetEnabled(true);
            Assert.That(ESCommandPaletteShortcutSettings.Enabled, Is.True);
            Assert.That(
                ShortcutManager.instance.GetShortcutBinding(ESCommandPaletteShortcutSettings.ShortcutId),
                Is.Not.EqualTo(ShortcutBinding.empty));
        }

        private static ESCommandPaletteItem WindowItem(string itemId, string windowId, string title = "Window")
        {
            return new ESCommandPaletteItem(
                itemId,
                title,
                string.Empty,
                "窗口",
                string.Empty,
                "@",
                windowId,
                ESCommandPaletteActionKind.OpenWindow);
        }

        private static void AssertDiagnostic(
            ESCommandPaletteRegistrationResult result,
            ESCommandPaletteRegistrationCode expected)
        {
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Code == expected)
                {
                    return;
                }
            }

            Assert.Fail("Missing registration diagnostic: " + expected);
        }

        private static int Count(IReadOnlyList<string> values, string expected)
        {
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }
            return result;
        }

        private sealed class TestProvider : IESCommandPaletteProvider
        {
            private readonly IReadOnlyList<ESCommandPaletteItem> items;

            public TestProvider(string providerId, string prefix, IReadOnlyList<ESCommandPaletteItem> items)
            {
                ProviderId = providerId;
                Prefix = prefix;
                this.items = items;
            }

            public string ProviderId { get; }
            public string DisplayName => ProviderId;
            public string Prefix { get; }
            public int BuildCount { get; private set; }

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                BuildCount++;
                return items;
            }
        }

        private sealed class ThrowingItemList : IReadOnlyList<ESCommandPaletteItem>
        {
            private readonly ESCommandPaletteItem first;

            public ThrowingItemList(ESCommandPaletteItem first)
            {
                this.first = first;
            }

            public int Count => 2;

            public ESCommandPaletteItem this[int index]
            {
                get
                {
                    if (index == 0)
                    {
                        return first;
                    }

                    throw new InvalidOperationException("Injected item enumeration failure.");
                }
            }

            public IEnumerator<ESCommandPaletteItem> GetEnumerator()
            {
                yield return first;
                throw new InvalidOperationException("Injected item enumeration failure.");
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
