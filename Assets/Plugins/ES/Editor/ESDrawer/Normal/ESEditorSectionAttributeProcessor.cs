using System;
using System.Collections.Generic;
using System.Reflection;
using ES;
using Sirenix.OdinInspector.Editor;

namespace ES.EditorInternal
{
    /// <summary>
    /// Expands Begin / continue / End section syntax before Odin creates property groups.
    /// This operates on Odin's mutable attribute list, rather than relying on draw order,
    /// so continuation remains deterministic through tree rebuilds and domain reloads.
    /// </summary>
    // Run after Odin's built-in member and inherited-attribute processors. This makes removal
    // of the shorthand marker final for the current member and prevents a bare
    // [ESEditorSection] from reaching group construction.
    [ResolverPriority(-200000)]
    public sealed class ESEditorSectionAttributeProcessor : OdinAttributeProcessor
    {
        private static readonly Dictionary<Type, Dictionary<MemberKey, SectionResolution>> ResolutionCache
            = new Dictionary<Type, Dictionary<MemberKey, SectionResolution>>();
        private static readonly HashSet<string> ReportedWarnings = new HashSet<string>(StringComparer.Ordinal);

        public override bool CanProcessChildMemberAttributes(InspectorProperty parent, MemberInfo member)
        {
            return member != null && HasSectionSyntax(member);
        }

        public override void ProcessChildMemberAttributes(
            InspectorProperty parent,
            MemberInfo member,
            List<Attribute> attributes)
        {
            if (member == null || attributes == null)
                return;

            Type hostType = parent?.Tree?.TargetType ?? member.DeclaringType;
            SectionResolution resolution = GetResolution(hostType, member);

            attributes.RemoveAll(IsSectionSyntaxAttribute);
            if (resolution.Section != null)
            {
                attributes.Add(resolution.Section.CreateAttribute());
                return;
            }

            if (!string.IsNullOrEmpty(resolution.Warning))
                ReportWarningOnce(resolution.Warning);
        }

        private static bool HasSectionSyntax(MemberInfo member)
        {
            object[] attributes = member.GetCustomAttributes(true);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (IsSectionSyntaxAttribute(attributes[i] as Attribute))
                    return true;
            }

            return false;
        }

        private static bool IsSectionSyntaxAttribute(Attribute attribute)
        {
            return attribute is ESEditorSectionAttribute
                   || attribute is ESEditorBeginSectionAttribute
                   || attribute is ESEditorEndSectionAttribute;
        }

        private static SectionResolution GetResolution(Type hostType, MemberInfo member)
        {
            hostType = hostType ?? member.DeclaringType;
            if (hostType == null)
                return SectionResolution.None;

            if (!ResolutionCache.TryGetValue(hostType, out Dictionary<MemberKey, SectionResolution> resolutions))
            {
                resolutions = BuildResolutions(hostType);
                ResolutionCache.Add(hostType, resolutions);
            }

            return resolutions.TryGetValue(MemberKey.From(member), out SectionResolution resolution)
                ? resolution
                : SectionResolution.None;
        }

        private static Dictionary<MemberKey, SectionResolution> BuildResolutions(Type hostType)
        {
            var members = new List<MemberInfo>();
            var hierarchy = new Stack<Type>();
            for (Type type = hostType; type != null && type != typeof(object); type = type.BaseType)
                hierarchy.Push(type);

            const BindingFlags flags = BindingFlags.Instance
                                       | BindingFlags.Public
                                       | BindingFlags.NonPublic
                                       | BindingFlags.DeclaredOnly;
            while (hierarchy.Count > 0)
            {
                Type type = hierarchy.Pop();
                // Keep the inheritance chain deterministic. A metadata token is only
                // comparable within one module; sorting all base/derived members together
                // can move an inherited Begin/End pair across the active-section boundary.
                var declaredMembers = new List<MemberInfo>();
                AddSectionMembers(declaredMembers, type.GetFields(flags));
                AddSectionMembers(declaredMembers, type.GetProperties(flags));
                AddSectionMembers(declaredMembers, type.GetMethods(flags));
                declaredMembers.Sort(CompareMemberOrder);
                members.AddRange(declaredMembers);
            }

            var activeSections = new Dictionary<string, SectionDefinition>(StringComparer.Ordinal);
            var activationOrder = new List<string>();
            var results = new Dictionary<MemberKey, SectionResolution>();

            for (int i = 0; i < members.Count; i++)
            {
                MemberInfo member = members[i];
                ReadSyntax(member, out ESEditorBeginSectionAttribute begin, out ESEditorSectionAttribute section,
                    out bool continuesPrevious, out ESEditorEndSectionAttribute end);

                string warning = null;
                if (end != null && end.Mode == ESEditorSectionEndMode.BeforeMember)
                {
                    if (GetActiveSection(end.NavigatorId, activeSections, activationOrder) == null)
                    {
                        warning = "[ESEditorEndSection] 找不到可关闭的活动分区："
                                  + hostType.FullName + "." + member.Name
                                  + "。请先使用 [ESEditorBeginSection(...)] 或完整 ESEditorSection 声明。";
                    }

                    CloseActiveSection(end.NavigatorId, activeSections, activationOrder);
                }

                SectionDefinition appliedSection = null;
                if (begin != null)
                {
                    appliedSection = SectionDefinition.From(begin);
                    Activate(appliedSection, activeSections, activationOrder);
                }
                else if (section != null)
                {
                    appliedSection = SectionDefinition.From(section);
                    Activate(appliedSection, activeSections, activationOrder);
                }
                else if (continuesPrevious)
                {
                    appliedSection = GetActiveSection(null, activeSections, activationOrder);
                    if (appliedSection == null)
                    {
                        warning = "[ESEditorSection] 找不到可合并的上一个分区："
                                  + hostType.FullName + "." + member.Name
                                  + "。请先使用 [ESEditorBeginSection(...)] 或完整 ESEditorSection 声明。";
                    }
                }
                else if (end != null && end.Mode == ESEditorSectionEndMode.AfterMember)
                {
                    appliedSection = GetActiveSection(end.NavigatorId, activeSections, activationOrder);
                    if (appliedSection == null)
                    {
                        warning = "[ESEditorEndSection] 找不到可关闭的活动分区："
                                  + hostType.FullName + "." + member.Name
                                  + "。请先使用 [ESEditorBeginSection(...)] 或完整 ESEditorSection 声明。";
                    }
                }

                if (appliedSection != null)
                    warning = null;

                results[MemberKey.From(member)] = new SectionResolution(appliedSection, warning);

                if (end != null && end.Mode == ESEditorSectionEndMode.AfterMember)
                    CloseActiveSection(end.NavigatorId, activeSections, activationOrder);
            }

            return results;
        }

        private static void AddSectionMembers<T>(List<MemberInfo> destination, T[] source) where T : MemberInfo
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (HasSectionSyntax(source[i]))
                    destination.Add(source[i]);
            }
        }

        private static int CompareMemberOrder(MemberInfo left, MemberInfo right)
        {
            int leftToken = GetMetadataToken(left);
            int rightToken = GetMetadataToken(right);
            int tokenCompare = leftToken.CompareTo(rightToken);
            return tokenCompare != 0
                ? tokenCompare
                : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }

        private static int GetMetadataToken(MemberInfo member)
        {
            try
            {
                return member.MetadataToken;
            }
            catch (InvalidOperationException)
            {
                return int.MaxValue;
            }
        }

        private static void ReadSyntax(
            MemberInfo member,
            out ESEditorBeginSectionAttribute begin,
            out ESEditorSectionAttribute section,
            out bool continuesPrevious,
            out ESEditorEndSectionAttribute end)
        {
            begin = null;
            section = null;
            continuesPrevious = false;
            end = null;

            object[] attributes = member.GetCustomAttributes(true);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i] is ESEditorBeginSectionAttribute beginAttribute)
                    begin = beginAttribute;
                else if (attributes[i] is ESEditorSectionAttribute sectionAttribute)
                {
                    if (sectionAttribute.IsContinuation)
                        continuesPrevious = true;
                    else
                        section = sectionAttribute;
                }
                else if (attributes[i] is ESEditorEndSectionAttribute endAttribute)
                    end = endAttribute;
            }
        }

        private static void Activate(
            SectionDefinition section,
            Dictionary<string, SectionDefinition> activeSections,
            List<string> activationOrder)
        {
            activeSections[section.NavigatorId] = section;
            activationOrder.Remove(section.NavigatorId);
            activationOrder.Add(section.NavigatorId);
        }

        private static SectionDefinition GetActiveSection(
            string navigatorId,
            Dictionary<string, SectionDefinition> activeSections,
            List<string> activationOrder)
        {
            if (!string.IsNullOrWhiteSpace(navigatorId))
                return activeSections.TryGetValue(navigatorId.Trim(), out SectionDefinition selected) ? selected : null;

            if (activationOrder.Count == 0)
                return null;

            string lastNavigatorId = activationOrder[activationOrder.Count - 1];
            return activeSections.TryGetValue(lastNavigatorId, out SectionDefinition current) ? current : null;
        }

        private static void CloseActiveSection(
            string navigatorId,
            Dictionary<string, SectionDefinition> activeSections,
            List<string> activationOrder)
        {
            SectionDefinition section = GetActiveSection(navigatorId, activeSections, activationOrder);
            if (section == null)
                return;

            activeSections.Remove(section.NavigatorId);
            activationOrder.Remove(section.NavigatorId);
        }

        private static void ReportWarningOnce(string warning)
        {
            if (ReportedWarnings.Add(warning))
                UnityEngine.Debug.LogWarning(warning);
        }

        private readonly struct MemberKey : IEquatable<MemberKey>
        {
            private readonly Module module;
            private readonly int metadataToken;

            private MemberKey(Module module, int metadataToken)
            {
                this.module = module;
                this.metadataToken = metadataToken;
            }

            public static MemberKey From(MemberInfo member)
                => new MemberKey(member.Module, GetMetadataToken(member));

            public bool Equals(MemberKey other)
                => module == other.module && metadataToken == other.metadataToken;

            public override bool Equals(object obj)
                => obj is MemberKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((module != null ? module.GetHashCode() : 0) * 397) ^ metadataToken;
                }
            }
        }

        private sealed class SectionDefinition
        {
            public readonly string NavigatorId;
            public readonly string SectionId;
            public readonly string DisplayName;
            public readonly float Order;
            public readonly string Subtitle;

            private SectionDefinition(string navigatorId, string sectionId, string displayName, float order, string subtitle)
            {
                NavigatorId = navigatorId;
                SectionId = sectionId;
                DisplayName = displayName;
                Order = order;
                Subtitle = subtitle;
            }

            public static SectionDefinition From(ESEditorBeginSectionAttribute attribute)
                => new SectionDefinition(
                    attribute.NavigatorId,
                    attribute.SectionId,
                    attribute.DisplayName,
                    attribute.Order,
                    attribute.Subtitle);

            public static SectionDefinition From(ESEditorSectionAttribute attribute)
                => new SectionDefinition(
                    attribute.NavigatorId,
                    attribute.SectionId,
                    attribute.DisplayName,
                    attribute.Order,
                    attribute.Subtitle);

            public ESEditorSectionAttribute CreateAttribute()
                => new ESEditorSectionAttribute(NavigatorId, SectionId, DisplayName, Order, Subtitle);
        }

        private readonly struct SectionResolution
        {
            public static readonly SectionResolution None = new SectionResolution(null, null);

            public readonly SectionDefinition Section;
            public readonly string Warning;

            public SectionResolution(SectionDefinition section, string warning)
            {
                Section = section;
                Warning = warning;
            }
        }
    }
}
