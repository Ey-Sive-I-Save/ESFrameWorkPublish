using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESCharacterAttributeCodeGeneratorTests
    {
        [Test]
        public void FixedApiSource_NonStructuralChangesRemainCurrent()
        {
            ESSuperAttributeTable table = CreateCharacterTable();
            Assert.That(ESCharacterAttributeCodeGenerator.TryBuildSource(table, out string expectedSource, out string error), Is.True, error);

            ESSuperFloatAttributeDefinition definition = table.floatAttributes[0];
            definition.displayName = "冲刺速度";
            definition.overrideBaseValue = true;
            definition.baseValue = 12f;
            definition.minValue = 1f;
            definition.maxValue = 30f;
            definition.migrationKey = "Character.Test.MoveSpeed.Legacy";

            Assert.That(ESCharacterAttributeCodeGenerator.TryBuildSource(table, out string changedSource, out error), Is.True, error);
            Assert.That(ESGeneratedSourceFile.MatchesExpectedContent(expectedSource, changedSource), Is.True);
        }

        [Test]
        public void FixedApiSource_StructuralChangesBecomeStale()
        {
            ESSuperAttributeTable table = CreateCharacterTable();
            Assert.That(ESCharacterAttributeCodeGenerator.TryBuildSource(table, out string expectedSource, out string error), Is.True, error);

            table.floatAttributes[0].fixedApiName = "RunSpeed";

            Assert.That(ESCharacterAttributeCodeGenerator.TryBuildSource(table, out string changedSource, out error), Is.True, error);
            Assert.That(ESGeneratedSourceFile.MatchesExpectedContent(expectedSource, changedSource), Is.False);
        }

        [Test]
        public void FixedApiSource_TamperedMappingWithOriginalSignatureIsStale()
        {
            ESSuperAttributeTable table = CreateCharacterTable();
            Assert.That(ESCharacterAttributeCodeGenerator.TryBuildSource(table, out string expectedSource, out string error), Is.True, error);

            const string fieldName = "FloatKeys";
            int mappingIndex = expectedSource.IndexOf("private static readonly string[] " + fieldName, System.StringComparison.Ordinal);
            Assert.That(mappingIndex, Is.GreaterThanOrEqualTo(0));
            int fieldNameIndex = expectedSource.IndexOf(fieldName, mappingIndex, System.StringComparison.Ordinal);
            string tamperedSource = expectedSource.Substring(0, fieldNameIndex)
                                    + "FloatKeyz"
                                    + expectedSource.Substring(fieldNameIndex + fieldName.Length);

            Assert.That(tamperedSource, Does.Contain("// Fixed API structural signature:"));
            Assert.That(ESGeneratedSourceFile.MatchesExpectedContent(tamperedSource, expectedSource), Is.False);
        }

        private static ESSuperAttributeTable CreateCharacterTable()
        {
            return new ESSuperAttributeTable
            {
                catalogScope = ESAttributeBakeTable.CharacterScope,
                floatAttributes =
                {
                    new ESSuperFloatAttributeDefinition
                    {
                        enumKey = 700,
                        key = "Character.Test.MoveSpeed",
                        storagePolicy = ESKeyStoragePolicy.HotSlot,
                        fixedApiName = "MoveSpeed"
                    }
                },
                permitAttributes =
                {
                    new ESSuperPermitAttributeDefinition
                    {
                        enumKey = 701,
                        key = "Character.Test.CanMove",
                        storagePolicy = ESKeyStoragePolicy.HotSlot,
                        fixedApiName = "CanMove"
                    }
                }
            };
        }
    }
}
