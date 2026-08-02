using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Type = System.Type;

namespace ES.Tests
{
    public sealed class ESInfoGroupContractTests
    {
        [Test]
        public void ConcreteEsLogicSoDataInfos_HaveMatchingGroups()
        {
            Type[] types = GetLoadableTypes(typeof(ESAudioCueInfo).Assembly);
            var missingGroups = new List<string>();

            for (int infoIndex = 0; infoIndex < types.Length; infoIndex++)
            {
                Type infoType = types[infoIndex];
                if (infoType == null || infoType.IsAbstract || !typeof(SoDataInfo).IsAssignableFrom(infoType))
                    continue;

                bool hasMatchingGroup = false;
                for (int groupIndex = 0; groupIndex < types.Length; groupIndex++)
                {
                    Type groupType = types[groupIndex];
                    if (groupType != null && !groupType.IsAbstract && IsGroupFor(groupType, infoType))
                    {
                        hasMatchingGroup = true;
                        break;
                    }
                }

                if (!hasMatchingGroup)
                    missingGroups.Add(infoType.FullName);
            }

            Assert.That(missingGroups, Is.Empty, "Missing SoDataGroup<TInfo>: " + string.Join(", ", missingGroups));
        }

        private static bool IsGroupFor(Type candidate, Type infoType)
        {
            for (Type current = candidate; current != null; current = current.BaseType)
            {
                if (current.IsGenericType
                    && current.GetGenericTypeDefinition() == typeof(SoDataGroup<>)
                    && current.GetGenericArguments()[0] == infoType)
                    return true;
            }

            return false;
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var loaderErrors = new List<string>();
                foreach (System.Exception loaderException in exception.LoaderExceptions)
                {
                    if (loaderException != null)
                        loaderErrors.Add(loaderException.GetType().Name + ": " + loaderException.Message);
                }

                Assert.Fail(
                    "Unable to inspect the full SoDataInfo type closure in assembly "
                    + assembly.FullName
                    + ". Loader exceptions: "
                    + string.Join(" | ", loaderErrors));
                return new Type[0];
            }
        }
    }
}
