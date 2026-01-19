using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ES.VMCP
{
    /// <summary>
    /// ESVMCP命令的JSON多态转换器
    /// 根据"type"字段自动创建对应的命令类实例
    /// </summary>
    public class ESVMCPCommandConverter : JsonConverter<ESVMCPCommandBase>
    {
        private static Dictionary<string, Type> _commandTypeMap;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化命令类型映射表（自动扫描所有带ESVMCPCommandAttribute的类）
        /// </summary>
        private static void Initialize()
        {
            if (_initialized) return;

            _commandTypeMap = new Dictionary<string, Type>();

            // 扫描所有程序集中的命令类
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var commandTypes = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ESVMCPCommandBase)))
                        .Where(t => t.GetCustomAttribute<ESVMCPCommandAttribute>() != null);

                    foreach (var type in commandTypes)
                    {
                        var attribute = type.GetCustomAttribute<ESVMCPCommandAttribute>();
                        if (!string.IsNullOrEmpty(attribute.CommandType))
                        {
                            _commandTypeMap[attribute.CommandType] = type;
                            Debug.Log($"[ESVMCP] 注册命令类型: {attribute.CommandType} -> {type.Name}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ESVMCP] 扫描程序集 {assembly.FullName} 时出错: {e.Message}");
                }
            }

            _initialized = true;
            Debug.Log($"[ESVMCP] 命令类型映射初始化完成，共注册 {_commandTypeMap.Count} 个命令类型");
        }

        /// <summary>
        /// 手动注册命令类型（用于动态加载的命令）
        /// </summary>
        public static void RegisterCommandType(string commandType, Type type)
        {
            if (!_initialized) Initialize();

            if (!type.IsSubclassOf(typeof(ESVMCPCommandBase)))
            {
                Debug.LogError($"[ESVMCP] 类型 {type.Name} 不是 ESVMCPCommandBase 的子类");
                return;
            }

            _commandTypeMap[commandType] = type;
            Debug.Log($"[ESVMCP] 手动注册命令类型: {commandType} -> {type.Name}");
        }

        /// <summary>
        /// 获取已注册的命令类型列表
        /// </summary>
        public static string[] GetRegisteredCommandTypes()
        {
            if (!_initialized) Initialize();
            return _commandTypeMap.Keys.ToArray();
        }

        public override bool CanWrite => false; // 只处理反序列化

        public override ESVMCPCommandBase ReadJson(JsonReader reader, Type objectType, ESVMCPCommandBase existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (!_initialized) Initialize();

            if (reader.TokenType == JsonToken.Null)
                return null;

            // 读取JSON对象
            JObject jsonObject = JObject.Load(reader);

            // 获取type字段
            JToken typeToken = jsonObject["type"];
            if (typeToken == null)
            {
                Debug.LogError("[ESVMCP] JSON命令缺少 'type' 字段");
                return null;
            }

            string commandType = typeToken.ToString();

            // 查找对应的命令类型
            if (!_commandTypeMap.TryGetValue(commandType, out Type targetType))
            {
                Debug.LogError($"[ESVMCP] 未注册的命令类型: {commandType}");
                return null;
            }

            // 创建命令实例并反序列化
            try
            {
                ESVMCPCommandBase command = (ESVMCPCommandBase)Activator.CreateInstance(targetType);
                serializer.Populate(jsonObject.CreateReader(), command);
                return command;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ESVMCP] 反序列化命令 {commandType} 失败: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, ESVMCPCommandBase value, JsonSerializer serializer)
        {
            throw new NotImplementedException("WriteJson not implemented for ESVMCPCommandConverter");
        }
    }

    /// <summary>
    /// Vector3的JSON转换器（支持数组格式 [x, y, z]）
    /// </summary>
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector3.zero;

            if (reader.TokenType == JsonToken.StartArray)
            {
                JArray array = JArray.Load(reader);
                if (array.Count >= 3)
                {
                    return new Vector3(
                        array[0].Value<float>(),
                        array[1].Value<float>(),
                        array[2].Value<float>()
                    );
                }
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                return new Vector3(
                    obj["x"]?.Value<float>() ?? 0,
                    obj["y"]?.Value<float>() ?? 0,
                    obj["z"]?.Value<float>() ?? 0
                );
            }

            return Vector3.zero;
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.x);
            writer.WriteValue(value.y);
            writer.WriteValue(value.z);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Color的JSON转换器（支持数组格式 [r, g, b, a]）
    /// </summary>
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Color.white;

            if (reader.TokenType == JsonToken.StartArray)
            {
                JArray array = JArray.Load(reader);
                if (array.Count >= 3)
                {
                    return new Color(
                        array[0].Value<float>(),
                        array[1].Value<float>(),
                        array[2].Value<float>(),
                        array.Count >= 4 ? array[3].Value<float>() : 1f
                    );
                }
            }
            else if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                return new Color(
                    obj["r"]?.Value<float>() ?? 1,
                    obj["g"]?.Value<float>() ?? 1,
                    obj["b"]?.Value<float>() ?? 1,
                    obj["a"]?.Value<float>() ?? 1
                );
            }
            else if (reader.TokenType == JsonToken.String)
            {
                // 支持HTML颜色码 #RRGGBB
                string colorString = reader.Value.ToString();
                if (ColorUtility.TryParseHtmlString(colorString, out Color color))
                {
                    return color;
                }
            }

            return Color.white;
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.r);
            writer.WriteValue(value.g);
            writer.WriteValue(value.b);
            writer.WriteValue(value.a);
            writer.WriteEndArray();
        }
    }
}
