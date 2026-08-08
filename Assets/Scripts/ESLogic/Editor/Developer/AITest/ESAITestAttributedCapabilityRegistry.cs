#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ESFramework.ESAITest;
using UnityEngine;

namespace ES.Editor
{
    public static class ESAITestAttributedCapabilityRegistry
    {
        private const string ManifestSource =
            "Editor AssemblyStream authoring metadata; not a Player runtime capability manifest.";

        private static readonly object SyncRoot = new object();
        private static readonly List<ESAITestAttributedCapabilityCandidate> Candidates =
            new List<ESAITestAttributedCapabilityCandidate>();
        private static readonly HashSet<string> CandidateKeys = new HashSet<string>(StringComparer.Ordinal);
        private static ESAITestAttributedCapabilityManifestDto cachedManifest;

        public static void Register(
            ESAITestCapabilityKind kind,
            ESAITestCapabilityAttribute attribute,
            MethodInfo methodInfo)
        {
            if (attribute == null || methodInfo == null)
                return;

            string key = BuildMethodKey(methodInfo) + "|" + kind;
            lock (SyncRoot)
            {
                if (!CandidateKeys.Add(key))
                    return;

                Candidates.Add(new ESAITestAttributedCapabilityCandidate(kind, attribute, methodInfo));
                cachedManifest = null;
            }
        }

        public static ESAITestAttributedCapabilityManifestDto GetManifestSnapshot()
        {
            lock (SyncRoot)
            {
                if (cachedManifest == null)
                    cachedManifest = BuildManifest();
                return CloneManifest(cachedManifest);
            }
        }

        public static string GetManifestJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(GetManifestSnapshot(), prettyPrint);
        }

        public static string BuildDenseSummary()
        {
            ESAITestAttributedCapabilityManifestDto manifest = GetManifestSnapshot();
            var builder = new StringBuilder(1024);
            builder.Append("来源: ").AppendLine(manifest.source);
            builder.Append("发现/接受/拒绝: ")
                .Append(manifest.discoveredCount).Append(" / ")
                .Append(manifest.acceptedCount).Append(" / ")
                .AppendLine(manifest.rejectedCount.ToString());
            builder.Append("ToUse / ToSee / ToVerify: ")
                .Append(manifest.toUseCount).Append(" / ")
                .Append(manifest.toSeeCount).Append(" / ")
                .AppendLine(manifest.toVerifyCount.ToString());

            AppendDescriptors(builder, "已接受", manifest.acceptedCapabilities);
            AppendDescriptors(builder, "已拒绝", manifest.rejectedCapabilities);
            return builder.ToString().TrimEnd();
        }

        private static ESAITestAttributedCapabilityManifestDto BuildManifest()
        {
            List<ESAITestAttributedCapabilityCandidate> ordered = Candidates
                .OrderBy(candidate => NormalizeCapabilityId(candidate.Attribute.CapabilityId), StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Kind)
                .ThenBy(candidate => BuildMethodSignature(candidate.MethodInfo), StringComparer.Ordinal)
                .ToList();

            var duplicateCounts = ordered
                .Select(candidate => NormalizeCapabilityId(candidate.Attribute.CapabilityId))
                .Where(capabilityId => !string.IsNullOrEmpty(capabilityId))
                .GroupBy(capabilityId => capabilityId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var accepted = new List<ESAITestAttributedCapabilityDescriptorDto>();
            var rejected = new List<ESAITestAttributedCapabilityDescriptorDto>();
            var diagnostics = new List<ESAITestAttributedCapabilityDiagnosticDto>();

            for (int i = 0; i < ordered.Count; i++)
            {
                ESAITestAttributedCapabilityCandidate candidate = ordered[i];
                var candidateDiagnostics = new List<ESAITestAttributedCapabilityDiagnosticDto>();
                ValidateCandidate(candidate, duplicateCounts, candidateDiagnostics);
                ESAITestAttributedCapabilityDescriptorDto descriptor =
                    BuildDescriptor(candidate, candidateDiagnostics);

                diagnostics.AddRange(candidateDiagnostics);
                if (descriptor.accepted)
                    accepted.Add(descriptor);
                else
                    rejected.Add(descriptor);
            }

            return new ESAITestAttributedCapabilityManifestDto
            {
                source = ManifestSource,
                generatedUtcTicks = DateTime.UtcNow.Ticks,
                discoveredCount = ordered.Count,
                acceptedCount = accepted.Count,
                rejectedCount = rejected.Count,
                toUseCount = accepted.Count(item => item.kind == ESAITestCapabilityKind.ToUse.ToString()),
                toSeeCount = accepted.Count(item => item.kind == ESAITestCapabilityKind.ToSee.ToString()),
                toVerifyCount = accepted.Count(item => item.kind == ESAITestCapabilityKind.ToVerify.ToString()),
                acceptedCapabilities = accepted.ToArray(),
                rejectedCapabilities = rejected.ToArray(),
                diagnostics = diagnostics
                    .OrderBy(item => item.capabilityId, StringComparer.Ordinal)
                    .ThenBy(item => item.kind, StringComparer.Ordinal)
                    .ThenBy(item => item.code, StringComparer.Ordinal)
                    .ThenBy(item => item.methodSignature, StringComparer.Ordinal)
                    .ToArray(),
            };
        }

        private static void ValidateCandidate(
            ESAITestAttributedCapabilityCandidate candidate,
            IReadOnlyDictionary<string, int> duplicateCounts,
            List<ESAITestAttributedCapabilityDiagnosticDto> diagnostics)
        {
            MethodInfo method = candidate.MethodInfo;
            string capabilityId = NormalizeCapabilityId(candidate.Attribute.CapabilityId);

            if (string.IsNullOrEmpty(capabilityId))
                AddDiagnostic(candidate, diagnostics, "empty_capability_id", "capabilityId 不能为空。" );
            else if (duplicateCounts.TryGetValue(capabilityId, out int count) && count > 1)
                AddDiagnostic(candidate, diagnostics, "duplicate_capability_id", "capabilityId 重复，共发现 " + count + " 个声明。" );

            if (candidate.Attribute.Version <= 0)
                AddDiagnostic(candidate, diagnostics, "invalid_version", "version 必须大于 0。" );

            object[] capabilityAttributes = method.GetCustomAttributes(typeof(ESAITestCapabilityAttribute), false);
            if (capabilityAttributes.Length != 1)
                AddDiagnostic(candidate, diagnostics, "multiple_capability_attributes", "同一方法必须且只能声明一个 ESAITest 能力特性。" );

            if (!method.IsPublic || !method.IsStatic)
                AddDiagnostic(candidate, diagnostics, "unsupported_host", "首个切片只接受 public static 方法；实例宿主解析尚未开放。" );

            if (method.IsSpecialName)
                AddDiagnostic(candidate, diagnostics, "special_name_method", "属性访问器或运算符等特殊方法不能作为能力入口。" );

            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
                AddDiagnostic(candidate, diagnostics, "generic_method", "泛型方法不能作为能力入口。" );

            if (method.ReturnType == typeof(void)
                && method.IsDefined(typeof(AsyncStateMachineAttribute), false))
            {
                AddDiagnostic(candidate, diagnostics, "async_void", "async void 无法被确定性等待、取消或收集结果。" );
            }

            if (typeof(Task).IsAssignableFrom(method.ReturnType))
                AddDiagnostic(candidate, diagnostics, "async_return", "首个切片不接受 Task/Task<T> 返回；请提供同步、确定性的适配方法。" );

            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (parameter.IsOut || parameter.ParameterType.IsByRef)
                {
                    AddDiagnostic(candidate, diagnostics, "ref_out_parameter", "参数 " + parameter.Name + " 使用 ref/out，无法进入稳定计划协议。" );
                    continue;
                }

                if (!TryValidateDataType(parameter.ParameterType, out string parameterReason))
                {
                    AddDiagnostic(candidate, diagnostics, "unsupported_parameter_type", "参数 " + parameter.Name + " 不符合纯 DTO 边界：" + parameterReason);
                }
            }

            ValidateReturnType(candidate, diagnostics);
        }

        private static void ValidateReturnType(
            ESAITestAttributedCapabilityCandidate candidate,
            List<ESAITestAttributedCapabilityDiagnosticDto> diagnostics)
        {
            Type returnType = candidate.MethodInfo.ReturnType;
            switch (candidate.Kind)
            {
                case ESAITestCapabilityKind.ToUse:
                    if (returnType != typeof(void) && !TryValidateDataType(returnType, out string useReason))
                        AddDiagnostic(candidate, diagnostics, "unsupported_return_type", "ToUse 返回值不符合纯 DTO 边界：" + useReason);
                    break;

                case ESAITestCapabilityKind.ToSee:
                    if (returnType == typeof(void))
                    {
                        AddDiagnostic(candidate, diagnostics, "void_see", "ToSee 必须返回基础值、enum 或纯 DTO。" );
                    }
                    else if (!TryValidateDataType(returnType, out string seeReason))
                    {
                        AddDiagnostic(candidate, diagnostics, "unsupported_return_type", "ToSee 返回值不符合纯 DTO 边界：" + seeReason);
                    }
                    break;

                case ESAITestCapabilityKind.ToVerify:
                    if (returnType != typeof(bool) && returnType != typeof(ESAITestVerifyResultDto))
                    {
                        AddDiagnostic(candidate, diagnostics, "invalid_verify_return", "ToVerify 只允许返回 bool 或 ESAITestVerifyResultDto。" );
                    }
                    break;
            }
        }

        private static bool TryValidateDataType(Type type, out string reason)
        {
            return TryValidateDataType(type, new HashSet<Type>(), out reason);
        }

        private static bool TryValidateDataType(Type type, HashSet<Type> visiting, out string reason)
        {
            if (type == null)
            {
                reason = "类型为空。";
                return false;
            }

            if (type.IsByRef || type.IsPointer || type.ContainsGenericParameters)
            {
                reason = "不允许 ByRef、指针或开放泛型。";
                return false;
            }

            Type nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
                return TryValidateDataType(nullableType, visiting, out reason);

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                reason = string.Empty;
                return true;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                reason = "禁止返回或传入 GameObject、Component、ScriptableObject 等 UnityEngine.Object。";
                return false;
            }

            if (type == typeof(object) || type == typeof(Type)
                || typeof(MemberInfo).IsAssignableFrom(type)
                || typeof(Delegate).IsAssignableFrom(type))
            {
                reason = "禁止 object、Type、反射元数据或委托。";
                return false;
            }

            if (type.Name.EndsWith("Scope", StringComparison.Ordinal)
                || type.Name.EndsWith("Handle", StringComparison.Ordinal)
                || type.Name.EndsWith("Lease", StringComparison.Ordinal))
            {
                reason = "禁止跨边界暴露 Scope、Handle 或 Lease 所有权对象。";
                return false;
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    reason = "只允许一维 DTO 数组。";
                    return false;
                }

                return TryValidateDataType(type.GetElementType(), visiting, out reason);
            }

            if (type.IsGenericType)
            {
                reason = "首个切片不接受泛型容器；请使用一维数组或明确 DTO。";
                return false;
            }

            if (!type.IsSerializable)
            {
                reason = "自定义 DTO 必须标记 [Serializable]。";
                return false;
            }

            if (!visiting.Add(type))
            {
                reason = "检测到循环对象图。";
                return false;
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.IsNotSerialized)
                    continue;

                if (!TryValidateDataType(field.FieldType, visiting, out string fieldReason))
                {
                    visiting.Remove(type);
                    reason = type.FullName + "." + field.Name + "：" + fieldReason;
                    return false;
                }
            }

            visiting.Remove(type);
            reason = string.Empty;
            return true;
        }

        private static ESAITestAttributedCapabilityDescriptorDto BuildDescriptor(
            ESAITestAttributedCapabilityCandidate candidate,
            IReadOnlyList<ESAITestAttributedCapabilityDiagnosticDto> diagnostics)
        {
            MethodInfo method = candidate.MethodInfo;
            ParameterInfo[] parameters = method.GetParameters();
            return new ESAITestAttributedCapabilityDescriptorDto
            {
                capabilityId = NormalizeCapabilityId(candidate.Attribute.CapabilityId),
                kind = candidate.Kind.ToString(),
                description = candidate.Attribute.Description ?? string.Empty,
                version = candidate.Attribute.Version,
                category = candidate.Attribute.Category ?? string.Empty,
                accepted = diagnostics.Count == 0,
                executionStatus = diagnostics.Count == 0 ? "editor_discovery_only" : "rejected",
                rejectionCode = diagnostics.Count == 0 ? string.Empty : diagnostics[0].code,
                rejectionReason = diagnostics.Count == 0
                    ? string.Empty
                    : string.Join(" | ", diagnostics.Select(item => item.message)),
                assemblyName = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty,
                declaringType = GetTypeName(method.DeclaringType),
                methodName = method.Name,
                methodSignature = BuildMethodSignature(method),
                returnType = GetTypeName(method.ReturnType),
                parameterTypes = parameters.Select(parameter => GetTypeName(parameter.ParameterType)).ToArray(),
            };
        }

        private static void AddDiagnostic(
            ESAITestAttributedCapabilityCandidate candidate,
            ICollection<ESAITestAttributedCapabilityDiagnosticDto> diagnostics,
            string code,
            string message)
        {
            diagnostics.Add(new ESAITestAttributedCapabilityDiagnosticDto
            {
                capabilityId = NormalizeCapabilityId(candidate.Attribute.CapabilityId),
                kind = candidate.Kind.ToString(),
                code = code,
                message = message,
                methodSignature = BuildMethodSignature(candidate.MethodInfo),
            });
        }

        private static string BuildMethodKey(MethodInfo method)
        {
            return method.Module.ModuleVersionId + ":" + method.MetadataToken;
        }

        private static string BuildMethodSignature(MethodInfo method)
        {
            string parameters = string.Join(", ", method.GetParameters()
                .Select(parameter => GetTypeName(parameter.ParameterType) + " " + parameter.Name));
            return GetTypeName(method.ReturnType) + " " + GetTypeName(method.DeclaringType)
                + "." + method.Name + "(" + parameters + ")";
        }

        private static string GetTypeName(Type type)
        {
            if (type == null)
                return "<null>";
            if (type.IsArray)
                return GetTypeName(type.GetElementType()) + "[]";
            return type.FullName ?? type.Name;
        }

        private static string NormalizeCapabilityId(string capabilityId)
        {
            return string.IsNullOrWhiteSpace(capabilityId) ? string.Empty : capabilityId.Trim();
        }

        private static ESAITestAttributedCapabilityManifestDto CloneManifest(
            ESAITestAttributedCapabilityManifestDto source)
        {
            return JsonUtility.FromJson<ESAITestAttributedCapabilityManifestDto>(JsonUtility.ToJson(source));
        }

        private static void AppendDescriptors(
            StringBuilder builder,
            string title,
            IReadOnlyList<ESAITestAttributedCapabilityDescriptorDto> descriptors)
        {
            builder.AppendLine().Append(title).Append(" (").Append(descriptors.Count).AppendLine("):");
            if (descriptors.Count == 0)
            {
                builder.AppendLine("  <无>");
                return;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                ESAITestAttributedCapabilityDescriptorDto item = descriptors[i];
                builder.Append("  [").Append(item.kind).Append("] ")
                    .Append(string.IsNullOrEmpty(item.capabilityId) ? "<空 ID>" : item.capabilityId)
                    .Append(" v").Append(item.version)
                    .Append(" | ").Append(item.executionStatus)
                    .Append(" | ").AppendLine(item.methodSignature);
                if (!item.accepted)
                    builder.Append("    拒绝: ").Append(item.rejectionCode).Append(" | ").AppendLine(item.rejectionReason);
            }
        }

        private sealed class ESAITestAttributedCapabilityCandidate
        {
            public readonly ESAITestCapabilityKind Kind;
            public readonly ESAITestCapabilityAttribute Attribute;
            public readonly MethodInfo MethodInfo;

            public ESAITestAttributedCapabilityCandidate(
                ESAITestCapabilityKind kind,
                ESAITestCapabilityAttribute attribute,
                MethodInfo methodInfo)
            {
                Kind = kind;
                Attribute = attribute;
                MethodInfo = methodInfo;
            }
        }
    }

    public sealed class ESAITestToUseMethodRegistration
        : EditorRegister_FOR_MethodAttribute<ESAITestToUseAttribute>
    {
        public override void Handle(ESAITestToUseAttribute attribute, MethodInfo methodInfo)
        {
            ESAITestAttributedCapabilityRegistry.Register(ESAITestCapabilityKind.ToUse, attribute, methodInfo);
        }
    }

    public sealed class ESAITestToSeeMethodRegistration
        : EditorRegister_FOR_MethodAttribute<ESAITestToSeeAttribute>
    {
        public override void Handle(ESAITestToSeeAttribute attribute, MethodInfo methodInfo)
        {
            ESAITestAttributedCapabilityRegistry.Register(ESAITestCapabilityKind.ToSee, attribute, methodInfo);
        }
    }

    public sealed class ESAITestToVerifyMethodRegistration
        : EditorRegister_FOR_MethodAttribute<ESAITestToVerifyAttribute>
    {
        public override void Handle(ESAITestToVerifyAttribute attribute, MethodInfo methodInfo)
        {
            ESAITestAttributedCapabilityRegistry.Register(ESAITestCapabilityKind.ToVerify, attribute, methodInfo);
        }
    }
}
#endif
