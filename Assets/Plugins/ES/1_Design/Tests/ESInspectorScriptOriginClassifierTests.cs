using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESInspectorScriptOriginClassifierTests
    {
        [Test]
        public void MissingSlot_ReturnsMissing()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Assets/Plugins/ES/Editor/Example.cs",
                "ES_Editor",
                true);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.Missing));
        }

        [Test]
        public void EsPath_ReturnsEs()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Assets/Plugins/ES/Editor/Example.cs",
                "ES_Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.ES));
        }

        [Test]
        public void EsPathWithBackslashes_ReturnsEs()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Assets\\Plugins\\ES\\Editor\\Example.cs",
                "ES_Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.ES));
        }

        [Test]
        public void PackagePath_ReturnsThirdParty()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Packages/com.example/Editor/Example.cs",
                "Example.Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.ThirdParty));
        }

        [Test]
        public void ProjectPath_ReturnsProject()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Assets/Scripts/Editor/Example.cs",
                "Example.Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.Project));
        }

        [Test]
        public void PathOutsideAssetsAndPackages_ReturnsUnknown()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Library/PackageCache/com.example/Example.cs",
                "Example.Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.Unknown));
        }

        [Test]
        public void EmptyPath_ReturnsUnknown()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                string.Empty,
                "Example.Editor",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.Unknown));
        }

        [Test]
        public void UnknownDisplayName_DoesNotClaimPackage()
        {
            string displayName = ESInspectorScriptOriginClassifier.GetDisplayName(
                ESInspectorScriptOriginKind.Unknown);

            Assert.That(displayName, Is.EqualTo("未知"));
        }

        [Test]
        public void UnityNativeDisplayName_ReturnsNative()
        {
            string displayName = ESInspectorScriptOriginClassifier.GetDisplayName(
                ESInspectorScriptOriginKind.UnityNative);

            Assert.That(displayName, Is.EqualTo("原生"));
        }

        [Test]
        public void AssemblyName_DoesNotOverrideAssetPath()
        {
            ESInspectorScriptOriginKind kind = ESInspectorScriptOriginClassifier.Classify(
                "Assets/Scripts/Editor/Example.cs",
                "NotTheActualAssembly",
                false);

            Assert.That(kind, Is.EqualTo(ESInspectorScriptOriginKind.Project));
        }
    }
}
