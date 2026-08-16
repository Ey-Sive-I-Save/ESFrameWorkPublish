using System;
using System.Reflection;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ES
{
    [McpForUnityTool(
        "es_content_registration",
        Description = "Inspect, validate and explicitly commit ES AssetLibrary or GameCore Group/Consumer registration with StringKey, CAS revisions and session-scoped idempotent request IDs.",
        AutoRegister = true,
        Group = "core")]
    public static class ESContentRegistrationMcpTool
    {
        public sealed class Parameters
        {
            [ToolParameter("inspect, register_asset, update_asset_key, register_gamecore, register_gamecore_root, synchronize, validate, bake or status")]
            public string action { get; set; }

            [ToolParameter("Idempotency key required for commit=true", Required = false)]
            public string requestId { get; set; }

            [ToolParameter("Explicit write authorization; false performs read-only validation", Required = false, DefaultValue = "false")]
            public bool commit { get; set; }

            [ToolParameter("Asset path for ordinary asset registration", Required = false)]
            public string assetPath { get; set; }

            [ToolParameter("ESAssetLibrary asset path", Required = false)]
            public string libraryPath { get; set; }

            [ToolParameter("GameCore DataInfo asset path", Required = false)]
            public string dataInfoPath { get; set; }

            [ToolParameter("Standalone GameCore root asset path", Required = false)]
            public string gameCorePath { get; set; }

            [ToolParameter("GameCore SoDataGroup asset path", Required = false)]
            public string groupPath { get; set; }

            [ToolParameter("ESAssetLibraryConsumer asset path", Required = false)]
            public string consumerPath { get; set; }

            [ToolParameter("StringOnly, EnumOnly, DualAlias or Auto", Required = false, DefaultValue = "Auto")]
            public string keyMode { get; set; }

            [ToolParameter("Stable EnumKey; zero means absent", Required = false, DefaultValue = "0")]
            public int enumKey { get; set; }

            [ToolParameter("Stable ordinal StringKey; never auto-trimmed or generated", Required = false)]
            public string stringKey { get; set; }

            [ToolParameter("Base Item EnumKey for item.weapon/item.shot dual projection; zero means absent", Required = false, DefaultValue = "0")]
            public int itemEnumKey { get; set; }

            [ToolParameter("Base Item ordinal StringKey for item.weapon/item.shot dual projection", Required = false)]
            public string itemStringKey { get; set; }

            [ToolParameter("CAS value for the existing base Item EnumKey", Required = false, DefaultValue = "0")]
            public int expectedCurrentItemEnumKey { get; set; }

            [ToolParameter("CAS value for the existing base Item StringKey", Required = false)]
            public string expectedCurrentItemStringKey { get; set; }

            [ToolParameter("True when expected current base Item Key values are explicitly supplied", Required = false, DefaultValue = "false")]
            public bool hasExpectedCurrentItemKey { get; set; }

            [ToolParameter("Expected target revision returned by inspect or validation", Required = false)]
            public string expectedLibraryRevision { get; set; }

            [ToolParameter("Expected immutable asset GUID", Required = false)]
            public string expectedGuid { get; set; }

            [ToolParameter("Expected normalized LocalFileId; main assets use zero", Required = false, DefaultValue = "0")]
            public long expectedLocalFileId { get; set; }

            [ToolParameter("Explicit ESAssetReferKind or auto", Required = false, DefaultValue = "auto")]
            public string assetKind { get; set; }

            [ToolParameter("Editor-only Group dictionary key; never used as Runtime StringKey", Required = false)]
            public string groupKey { get; set; }

            [ToolParameter("Expected DataInfo GUID", Required = false)]
            public string expectedSourceGuid { get; set; }

            [ToolParameter("Expected Group GUID", Required = false)]
            public string expectedGroupGuid { get; set; }

            [ToolParameter("Expected Consumer GUID", Required = false)]
            public string expectedConsumerGuid { get; set; }

            [ToolParameter("Expected DataInfo revision returned by inspect or validation", Required = false)]
            public string expectedSourceRevision { get; set; }

            [ToolParameter("Expected Group revision returned by inspect or validation", Required = false)]
            public string expectedGroupRevision { get; set; }

            [ToolParameter("Expected Consumer revision returned by inspect or validation", Required = false)]
            public string expectedConsumerRevision { get; set; }

            [ToolParameter("CAS value for an existing GameCore EnumKey", Required = false, DefaultValue = "0")]
            public int expectedCurrentEnumKey { get; set; }

            [ToolParameter("CAS value for an existing GameCore StringKey", Required = false)]
            public string expectedCurrentStringKey { get; set; }

            [ToolParameter("True when expected current Key values are explicitly supplied", Required = false, DefaultValue = "false")]
            public bool hasExpectedCurrentKey { get; set; }

            [ToolParameter("auto, item, item.weapon or item.shot", Required = false, DefaultValue = "auto")]
            public string gameCoreRoute { get; set; }

            [ToolParameter("Bake run identifier used by status", Required = false)]
            public string runId { get; set; }

        }

        public static object HandleCommand(JObject parameters)
        {
            if (parameters == null)
                return JObject.FromObject(ESContentRegistrationResult.Failure(null, "invalid_request", "Parameters cannot be null."));

            try
            {
                var request = new ESContentRegistrationRequest
                {
                    action = ParseAction(GetString(parameters, "action")),
                    requestId = GetString(parameters, "requestId", "request_id"),
                    commit = GetBool(parameters, false, "commit"),
                    assetPath = GetString(parameters, "assetPath", "asset_path"),
                    libraryPath = GetString(parameters, "libraryPath", "library_path"),
                    expectedGuid = GetString(parameters, "expectedGuid", "expected_guid"),
                    expectedLocalFileId = GetLong(parameters, 0, "expectedLocalFileId", "expected_local_file_id"),
                    expectedLibraryRevision = GetString(parameters, "expectedLibraryRevision", "expected_library_revision"),
                    assetKind = GetString(parameters, "assetKind", "asset_kind"),
                    dataInfoPath = GetString(parameters, "dataInfoPath", "data_info_path"),
                    gameCorePath = GetString(parameters, "gameCorePath", "game_core_path"),
                    groupPath = GetString(parameters, "groupPath", "group_path"),
                    consumerPath = GetString(parameters, "consumerPath", "consumer_path"),
                    groupKey = GetString(parameters, "groupKey", "group_key"),
                    expectedSourceGuid = GetString(parameters, "expectedSourceGuid", "expected_source_guid"),
                    expectedGroupGuid = GetString(parameters, "expectedGroupGuid", "expected_group_guid"),
                    expectedConsumerGuid = GetString(parameters, "expectedConsumerGuid", "expected_consumer_guid"),
                    expectedSourceRevision = GetString(parameters, "expectedSourceRevision", "expected_source_revision"),
                    expectedGroupRevision = GetString(parameters, "expectedGroupRevision", "expected_group_revision"),
                    expectedConsumerRevision = GetString(parameters, "expectedConsumerRevision", "expected_consumer_revision"),
                    expectedCurrentEnumKey = GetInt(parameters, 0, "expectedCurrentEnumKey", "expected_current_enum_key"),
                    expectedCurrentStringKey = GetString(parameters, "expectedCurrentStringKey", "expected_current_string_key"),
                    hasExpectedCurrentKey = GetBool(parameters, false, "hasExpectedCurrentKey", "has_expected_current_key"),
                    keyMode = ParseKeyMode(GetString(parameters, "keyMode", "key_mode")),
                    enumKey = GetInt(parameters, 0, "enumKey", "enum_key"),
                    stringKey = GetString(parameters, "stringKey", "string_key"),
                    itemEnumKey = GetInt(parameters, 0, "itemEnumKey", "item_enum_key"),
                    itemStringKey = GetString(parameters, "itemStringKey", "item_string_key"),
                    expectedCurrentItemEnumKey = GetInt(parameters, 0, "expectedCurrentItemEnumKey", "expected_current_item_enum_key"),
                    expectedCurrentItemStringKey = GetString(parameters, "expectedCurrentItemStringKey", "expected_current_item_string_key"),
                    hasExpectedCurrentItemKey = GetBool(parameters, false, "hasExpectedCurrentItemKey", "has_expected_current_item_key"),
                    gameCoreRoute = GetString(parameters, "gameCoreRoute", "game_core_route"),
                    runId = GetString(parameters, "runId", "run_id")
                };
                ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
                return JObject.FromObject(result);
            }
            catch (Exception exception)
            {
                return JObject.FromObject(ESContentRegistrationResult.Failure(null, "invalid_request", exception.Message));
            }
        }

        private static ESContentRegistrationAction ParseAction(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "inspect": return ESContentRegistrationAction.Inspect;
                case "register_asset": return ESContentRegistrationAction.RegisterAsset;
                case "update_asset_key": return ESContentRegistrationAction.UpdateAssetKey;
                case "register_gamecore": return ESContentRegistrationAction.RegisterGameCore;
                case "register_gamecore_root": return ESContentRegistrationAction.RegisterGameCoreRoot;
                case "synchronize": return ESContentRegistrationAction.Synchronize;
                case "validate": return ESContentRegistrationAction.Validate;
                case "bake": return ESContentRegistrationAction.Bake;
                case "status": return ESContentRegistrationAction.Status;
                default: throw new ArgumentException("Unknown action: " + value);
            }
        }

        private static ESContentStableKeyMode ParseKeyMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ESContentStableKeyMode.Auto;
            if (Enum.TryParse(value, true, out ESContentStableKeyMode mode))
                return mode;
            throw new ArgumentException("Unknown keyMode: " + value);
        }

        private static string GetString(JObject source, params string[] names)
        {
            foreach (string name in names)
            {
                JToken token = source[name];
                if (token != null && token.Type != JTokenType.Null)
                    return token.ToString();
            }
            return string.Empty;
        }

        private static bool GetBool(JObject source, bool fallback, params string[] names)
        {
            foreach (string name in names)
            {
                JToken token = source[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();
                if (bool.TryParse(token.ToString(), out bool parsed))
                    return parsed;
            }
            return fallback;
        }

        private static int GetInt(JObject source, int fallback, params string[] names)
        {
            foreach (string name in names)
            {
                JToken token = source[name];
                if (token != null && int.TryParse(token.ToString(), out int parsed))
                    return parsed;
            }
            return fallback;
        }

        private static long GetLong(JObject source, long fallback, params string[] names)
        {
            foreach (string name in names)
            {
                JToken token = source[name];
                if (token != null && long.TryParse(token.ToString(), out long parsed))
                    return parsed;
            }
            return fallback;
        }
    }

    public sealed class ESContentRegistrationMcpRegistryInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            CommandRegistry.Initialize();
            if (IsRegistered())
            {
                InvalidateToolMetadata();
                return;
            }

            MethodInfo register = typeof(CommandRegistry).GetMethod(
                "RegisterCommandType",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Type), typeof(bool) },
                null);
            if (register == null)
            {
                Debug.LogError("[ESContentRegistration][MCP] 当前 MCPForUnity 版本没有可用的单工具注册入口。");
                return;
            }

            try
            {
                bool registered = register.Invoke(
                    null,
                    new object[] { typeof(ESContentRegistrationMcpTool), false }) is true;
                if (!registered || !IsRegistered())
                {
                    Debug.LogError("[ESContentRegistration][MCP] es_content_registration 注册失败。");
                    return;
                }

                InvalidateToolMetadata();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESContentRegistration][MCP] 注册异常：" + exception.Message);
            }
        }

        private static bool IsRegistered()
        {
            try
            {
                return CommandRegistry.GetHandler("es_content_registration") != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void InvalidateToolMetadata()
        {
            if (MCPServiceLocator.ToolDiscovery is ToolDiscoveryService discovery)
                discovery.InvalidateCache();
        }
    }
}
