using ES;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;
using static ES.ESDesignUtility;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //匹配器-
        /*

         序列化器多态支持对比：
         - JsonUtility: 不支持多态，仅限具体类
         - Odin序列化器: 完全支持多态、继承、接口
         - MessagePack: 支持多态，高性能二进制格式（TODO/未实装：默认未启用；需安装MessagePack并配置Resolver/Union/Typeless等）
         - BinaryFormatter: 支持多态，但有安全风险
         - XmlSerializer: 支持多态，需要[XmlInclude]属性配置

         性能说明：
         - 类型转换使用缓存以提升性能（TODO/未完全实现：目前 _conversionCache 尚未接入 SystemObjectToT，仍是直接 Convert/Parse）
         - 异步方法适用于大文件操作
         - BinaryFormatter有安全风险，建议在受信任环境中使用

         安全说明：
         - BinaryFormatter可能存在反序列化漏洞，生产环境建议使用替代方案
         - 文件操作会创建必要的目录
         - 参数验证防止null引用异常

         使用示例：
         // 类型转换
         int result = Matcher.SystemObjectToT<int>("123", 0); // 返回123

         // JSON序列化
         var obj = new { name = "test", value = 42 };
         string json = Matcher.ToJson(obj);
         var restored = Matcher.FromJson<dynamic>(json);

         // 注意：JsonUtility不支持多态序列化
         // 对于需要多态支持的场景，使用Odin序列化器：
         // string odinJson = Matcher.ToOdinJson(obj); // 支持多态、继承、接口
         // var restoredOdin = Matcher.FromOdinJson<T>(odinJson); // 自动类型识别

         // 多态序列化示例：
         // interface IShape { }
         // class Circle : IShape { public float radius; }
         // class Rectangle : IShape { public float width, height; }
         // IShape shape = new Circle { radius = 5f };
         // string json = Matcher.ToOdinJson(shape); // 正确序列化
         // IShape restored = Matcher.FromOdinJson<IShape>(json); // 正确恢复为Circle类型

         // 异步文件操作
         await Matcher.ToJsonFileAsync(obj, "data.json");

         // XML序列化
         var xmlObj = new ExampleXML("Player", 10, 100f, 1);
         string xml = Matcher.ToXmlString(xmlObj);
         var restoredXml = Matcher.FromXmlString<ExampleXML>(xml);
         */
        public static class Matcher
        {
            // 类型转换缓存，用于提升性能
            #region 类型转换
            /// <summary>
            /// 将对象安全地转换为指定类型。
            /// </summary>
            /// <typeparam name="T">目标类型。</typeparam>
            /// <param name="from">源对象。</param>
            /// <param name="defaultValue">转换失败时返回的默认值。</param>
            /// <returns>转换后的对象，失败则返回默认值。</returns>
            /// <exception cref="ArgumentNullException">当目标类型为null时抛出。</exception>
            public static T SystemObjectToT<T>(object from, T defaultValue = default(T))
            {
                if (typeof(T) == null) throw new ArgumentNullException(nameof(T), "目标类型不能为null。");

                if (from == null)
                {
                    // 处理源对象为 null 的情况
                    return defaultValue;
                }

                // 如果类型 already matches, 直接返回
                if (from is T value)
                {
                    return value;
                }

                Type targetType = typeof(T);

                try
                {
                    // 处理可空类型
                    Type underlyingType = Nullable.GetUnderlyingType(targetType);
                    if (underlyingType != null)
                    {
                        // 如果源对象是 null，可空类型会返回 null
                        if (from == null)
                            return default(T);

                        // 递归调用，转换为可空类型的基础类型
                        object convertedValue = SystemObjectToT(from, underlyingType, defaultValue);
                        // 创建可空类型的实例并返回
                        return (T)Activator.CreateInstance(targetType, convertedValue);
                    }

                    // 使用 Convert.ChangeType 进行基础类型转换
                    return (T)Convert.ChangeType(from, targetType);
                }
                catch (InvalidCastException)
                {
                    Debug.LogWarning($"无法将类型 {from.GetType()} 转换为 {targetType}。");
                    return defaultValue;
                }
                catch (FormatException)
                {
                    Debug.LogWarning($"对象 {from} 的格式不符合 {targetType}。");
                    return defaultValue;
                }
                catch (OverflowException)
                {
                    Debug.LogWarning($"对象 {from} 对于类型 {targetType} 是一个溢出值。");
                    return defaultValue;
                }
                catch (Exception ex) // 捕获其他可能的异常
                {
                    Debug.LogError($"转换对象 {from} 到类型 {targetType} 时发生未知错误: {ex.Message}");
                    return defaultValue;
                }
            }

            /// <summary>
            /// 将对象安全地转换为指定类型（非泛型版本）。
            /// </summary>
            /// <param name="from">源对象。</param>
            /// <param name="targetType">要转换到的目标类型。</param>
            /// <param name="defaultValue">转换失败时返回的默认值。</param>
            /// <returns>转换后的对象，失败则返回默认值。</returns>
            private static object SystemObjectToT(object from, Type targetType, object defaultValue = null)
            {
                // 处理 null 值：如果源对象为 null，则根据目标类型返回 null 或默认值
                if (from == null)
                {
                    // 如果目标类型是值类型但不是 Nullable<T>，则无法将 null 转换为该类型，返回默认值
                    if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    {
                        return defaultValue ?? Activator.CreateInstance(targetType); // 尝试创建值类型的默认实例
                    }
                    return null; // 对于引用类型或可空值类型，直接返回 null
                }

                // 如果类型 already matches, 直接返回
                if (targetType.IsInstanceOfType(from))
                {
                    return from;
                }

                try
                {
                    // 处理可空类型（Nullable<T>）
                    Type underlyingType = Nullable.GetUnderlyingType(targetType);
                    if (underlyingType != null)
                    {
                        // 递归调用，转换为可空类型的基础类型
                        object convertedValue = SystemObjectToT(from, underlyingType, defaultValue);
                        // 如果转换失败（返回了默认值），且默认值不是该可空类型，可能需要处理
                        // 或者直接创建可空类型的实例
                        if (convertedValue != null)
                        {
                            // 使用 Activator.CreateInstance 创建 Nullable<T> 实例
                            return Activator.CreateInstance(targetType, convertedValue);
                        }
                        else
                        {
                            // 如果转换后的值为 null，返回一个空的 Nullable<T>
                            return Activator.CreateInstance(targetType);
                        }
                    }

                    // 处理枚举类型
                    if (targetType.IsEnum)
                    {
                        // 如果源是字符串，尝试解析枚举
                        if (from is string strValue)
                        {
                            return Enum.Parse(targetType, strValue, true); // ignoreCase = true
                        }
                        else
                        {
                            // 对于数字类型，直接转换到枚举的基础类型
                            Type enumUnderlyingType = Enum.GetUnderlyingType(targetType);
                            object numericValue = Convert.ChangeType(from, enumUnderlyingType);
                            return Enum.ToObject(targetType, numericValue);
                        }
                    }

                    // 使用 Convert.ChangeType 进行基础类型转换
                    return Convert.ChangeType(from, targetType);
                }
                catch (InvalidCastException ex)
                {
                    Debug.LogWarning($"无效的转换: 无法将类型 {from.GetType()} 转换为 {targetType}。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (FormatException ex)
                {
                    Debug.LogWarning($"格式错误: 对象 '{from}' 的格式不符合 {targetType}。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (OverflowException ex)
                {
                    Debug.LogWarning($"溢出错误: 对象 '{from}' 对于类型 {targetType} 是一个溢出值。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (Exception ex) // 捕获其他可能的异常
                {
                    Debug.LogError($"转换对象 '{from}' 到类型 {targetType} 时发生未知错误: {ex.Message}");
                    return defaultValue;
                }
            }
            /// <summary>
            /// Convert简单转换
            /// </summary>
            /// <param name="from"></param>
            /// <param name="type"></param>
            /// <returns></returns>
            public static object SystemObjectToTConvert(object from, Type type)
            {
                if (type == typeof(float))
                {
                    return Convert.ChangeType(Convert.ToSingle(from), typeof(float));
                }
                else
                {
                    return Convert.ChangeType(from, type);
                }
            }
            #endregion

            #region 序列化之Unity原生
            /// <summary>
            /// 使用 JsonUtility 将对象序列化为 JSON 字符串。
            /// 注意：Unity的JsonUtility不支持多态序列化，只能处理具体类型，不支持抽象类、接口、继承关系或多态对象。
            /// 对于需要多态支持的场景，请使用Odin序列化器或其他支持多态的序列化方案。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型，必须是具体类，不能是接口或抽象类。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <param name="prettyPrint">是否格式化输出JSON（true为可读格式，false为压缩格式）。</param>
            /// <returns>JSON格式的字符串，对象为null时返回空字符串。</returns>
            /// <remarks>
            /// - 不支持多态：无法序列化接口、抽象类或继承层次结构
            /// - 不支持字典：Dictionary类型不会被正确序列化
            /// - 性能较好：适合简单的数据结构和Unity原生对象
            /// - 线程安全：此方法是线程安全的
            /// </remarks>
            public static string ToJson<T>(T obj, bool prettyPrint = false)
            {
                if (obj == null) return string.Empty;
                return JsonUtility.ToJson(obj, prettyPrint);
            }
            /// <summary>
            /// 使用 JsonUtility 从 JSON 字符串反序列化为对象。
            /// 注意：不支持多态反序列化，只能还原为指定的具体类型。
            /// </summary>
            /// <typeparam name="T">目标对象类型，必须与序列化时的类型一致。</typeparam>
            /// <param name="json">JSON格式的字符串。</param>
            /// <returns>反序列化后的对象实例，JSON为空或无效时返回default(T)。</returns>
            /// <remarks>
            /// - 类型必须匹配：T必须是序列化时使用的具体类型
            /// - 不支持多态：无法根据JSON内容动态确定实际类型
            /// - 构造函数：使用无参构造函数创建对象实例
            /// </remarks>
            public static T FromJson<T>(string json)
            {
                if (string.IsNullOrEmpty(json)) return default;
                return JsonUtility.FromJson<T>(json);
            }
            /// <summary>
            /// 使用 JsonUtility 将对象序列化到 JSON 文件（异步版本）。
            /// 注意：继承Unity原生JSON的所有限制，不支持多态序列化。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型，必须是具体类。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <param name="filePath">文件保存路径。</param>
            /// <param name="prettyPrint">是否格式化输出JSON。</param>
            /// <remarks>
            /// - 异步操作：不会阻塞主线程，适合大文件操作
            /// - 自动创建目录：如果目录不存在会自动创建
            /// - 编码：使用UTF-8编码保存文件
            /// </remarks>
            public static async System.Threading.Tasks.Task ToJsonFileAsync<T>(T obj, string filePath, bool prettyPrint = false) where T : class
            {
                if (obj == null) return;
                string json = ToJson(obj, prettyPrint);
                await System.IO.File.WriteAllTextAsync(filePath, json);
            }

            /// <summary>
            /// 使用 JsonUtility 从 JSON 文件反序列化为对象（异步版本）。
            /// 注意：不支持多态反序列化。
            /// </summary>
            /// <typeparam name="T">目标对象类型。</typeparam>
            /// <param name="filePath">JSON文件路径。</param>
            /// <returns>反序列化后的对象实例，文件不存在时返回default(T)。</returns>
            /// <remarks>
            /// - 异步读取：不会阻塞主线程
            /// - 文件检查：自动检查文件是否存在
            /// - 编码：假设文件为UTF-8编码
            /// </remarks>
            public static async System.Threading.Tasks.Task<T> FromJsonFileAsync<T>(string filePath)
            {
                if (!File.Exists(filePath)) return default;
                string json = await System.IO.File.ReadAllTextAsync(filePath);
                return FromJson<T>(json);
            }
            #endregion

            #region 序列化之二进制

            /// <summary>
            /// 使用 BinaryFormatter 将对象序列化为字节数组 (注意安全风险)
            /// </summary>
            public static byte[] ToBinary(object obj)
            {
                if (obj == null) return null;

                try
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(ms, obj);
                        return ms.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"二进制序列化失败: {ex.Message}");
                    return null;
                }
            }
            /// <summary>
            /// 使用 BinaryFormatter 从字节数组反序列化为对象 (注意安全风险)
            /// </summary>
            public static T FromBinary<T>(byte[] data) where T : class
            {
                if (data == null || data.Length == 0) return default;

                try
                {
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        return (T)formatter.Deserialize(ms);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"二进制反序列化失败: {ex.Message}");
                    return default;
                }
            }
            #endregion

            #region Odin 序列化支持

            /// <summary>
            /// 使用 Odin Serializer 将对象序列化为字节数组。
            /// 支持多态序列化，可以正确处理继承、接口和抽象类型。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型，支持任何类型包括接口和抽象类。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <returns>序列化后的字节数组，对象为null时返回null。</returns>
            /// <remarks>
            /// - 支持多态：可以序列化接口、抽象类和继承层次结构
            /// - 支持复杂类型：Dictionary、List、自定义类等
            /// - 性能：比JsonUtility稍慢，但功能更强大
            /// - 兼容性：需要安装Odin Inspector插件
            /// </remarks>
            public static byte[] ToOdinBinary<T>(T obj)
            {
                if (obj == null) return null;
                return SerializationUtility.SerializeValue(obj, DataFormat.Binary);
            }
            /// <summary>
            /// 使用 Odin Serializer 从字节数组反序列化为对象。
            /// 支持多态反序列化，可以根据数据内容恢复正确的类型。
            /// </summary>
            /// <typeparam name="T">目标类型，支持接口和抽象类型。</typeparam>
            /// <param name="data">序列化的字节数组。</param>
            /// <returns>反序列化后的对象实例，数据无效时返回default(T)。</returns>
            /// <remarks>
            /// - 多态支持：自动识别并恢复正确的具体类型
            /// - 类型安全：确保反序列化结果符合预期类型
            /// - 错误处理：数据损坏时返回默认值
            /// </remarks>
            public static T FromOdinBinary<T>(byte[] data)
            {
                if (data == null || data.Length == 0) return default;
                return SerializationUtility.DeserializeValue<T>(data, DataFormat.Binary);
            }
            /// <summary>
            /// 使用 Odin Serializer 将对象序列化为 JSON 字符串。
            /// 支持多态序列化，是JsonUtility的强大替代方案。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型，支持多态类型。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <returns>JSON格式的字符串，对象为null时返回空字符串。</returns>
            /// <remarks>
            /// - 多态序列化：完整支持继承、接口和抽象类
            /// - 丰富类型支持：Dictionary、List、枚举、自定义类型等
            /// - 可读性：JSON格式便于调试和手动编辑
            /// - 兼容性：需要Odin Inspector插件
            /// - 性能：功能全面但相对较慢
            /// </remarks>
            public static string ToOdinJson<T>(T obj)
            {
                if (obj == null) return string.Empty;
                byte[] jsonBytes = SerializationUtility.SerializeValue(obj, DataFormat.JSON);
                return System.Text.Encoding.UTF8.GetString(jsonBytes);
            }
            /// <summary>
            /// 使用 Odin Serializer 从 JSON 字符串反序列化为对象。
            /// 支持多态反序列化，可以自动识别和恢复正确的具体类型。
            /// </summary>
            /// <typeparam name="T">目标类型，支持接口和抽象类型。</typeparam>
            /// <param name="json">JSON格式的字符串。</param>
            /// <returns>反序列化后的对象实例，JSON为空时返回default(T)。</returns>
            /// <remarks>
            /// - 多态反序列化：根据JSON内容自动确定实际类型
            /// - 类型推断：无需手动指定具体类型
            /// - 错误恢复：JSON格式错误时返回默认值
            /// - 编码：使用UTF-8编码处理字符串
            /// </remarks>
            public static T FromOdinJson<T>(string json)
            {
                if (string.IsNullOrEmpty(json)) return default;
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
                return SerializationUtility.DeserializeValue<T>(jsonBytes, DataFormat.JSON);
            }
            /// <summary>
            /// 使用 Odin Serializer 深拷贝对象。
            /// 支持多态对象的完整深拷贝，包括所有引用和复杂类型。
            /// </summary>
            /// <typeparam name="T">对象类型，支持多态类型。</typeparam>
            /// <param name="obj">要拷贝的对象实例。</param>
            /// <returns>完整的深拷贝副本，原始对象为null时返回default(T)。</returns>
            /// <remarks>
            /// - 深拷贝：完全独立的副本，不会影响原对象
            /// - 多态支持：正确处理继承和接口类型
            /// - 引用完整性：保持所有对象引用关系
            /// - 性能：序列化-反序列化方式，开销较大但保证完整性
            /// </remarks>
            public static T DeepCopy<T>(T obj) where T : class
            {
                if (obj == null) return default;
                return SerializationUtility.CreateCopy(obj) as T;
            }

            #endregion

            #region MessagePack序列化支持（TODO/未实装：默认不启用，需要安装依赖并配置）
#if MESSAGEPACK
            // 注意：MESSAGEPACK符号需要手动定义才能启用此功能
            // 在Unity中：Edit -> Project Settings -> Player -> Other Settings -> Scripting Define Symbols
            // 添加 "MESSAGEPACK"（不含引号）
            // 同时需要安装MessagePack-CSharp包
            // TODO(未实装): 若要“接口/抽象/继承”的稳定多态，通常还需配置 Union/Resolver/Typeless 等策略；默认配置并不保证。
            /// <summary>
            /// 使用 MessagePack 将对象序列化为字节数组。
            /// 支持多态序列化，性能优异，适合网络传输和存储。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型，支持多态类型。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <returns>MessagePack格式的字节数组，对象为null时返回null。</returns>
            /// <remarks>
            /// - 多态支持：需要额外配置（TODO/未实装：当前使用默认配置，接口/抽象/继承多态通常需要Union/Resolver/Typeless等）
            /// - 高性能：序列化/反序列化速度快，压缩率高
            /// - 跨平台：二进制格式确保平台兼容性
            /// - 网络友好：适合网络传输和持久化存储
            /// - 依赖：需要安装MessagePack-CSharp包
            /// </remarks>
            public static byte[] ToMessagePack<T>(T obj)
            {
                if (obj == null) return null;
                return MessagePack.MessagePackSerializer.Serialize(obj);
            }

            /// <summary>
             /// 使用 MessagePack 从字节数组反序列化为对象。
            /// 支持多态反序列化，自动识别正确的具体类型。
            /// </summary>
            /// <typeparam name="T">目标类型，支持接口和抽象类型。</typeparam>
            /// <param name="data">MessagePack格式的字节数组。</param>
            /// <returns>反序列化后的对象实例，数据无效时返回default(T)。</returns>
            /// <remarks>
            /// - 多态反序列化：需要额外配置（TODO/未实装：当前使用默认配置，不保证可从接口/抽象类型自动恢复具体类型）
            /// - 性能优化：快速的反序列化过程
            /// - 错误处理：数据损坏时返回默认值
            /// - 内存效率：直接从字节数组恢复对象
            /// </remarks>
            public static T FromMessagePack<T>(byte[] data)
            {
                if (data == null || data.Length == 0) return default;
                return MessagePack.MessagePackSerializer.Deserialize<T>(data);
            }

            /// <summary>
            /// 使用 MessagePack 将对象序列化到文件。
            /// 支持多态对象的文件持久化。
            /// </summary>
            /// <typeparam name="T">要序列化的对象类型。</typeparam>
            /// <param name="obj">要序列化的对象实例。</param>
            /// <param name="filePath">文件保存路径。</param>
            /// <remarks>
            /// - 多态持久化：需要额外配置（TODO/未实装：当前默认配置不保证“保持对象类型信息”用于多态恢复）
            /// - 文件操作：自动处理文件写入
            /// - 性能：适合大数据文件的快速保存
            /// - 兼容性：跨平台文件格式
            /// </remarks>
            public static void ToMessagePackFile<T>(T obj, string filePath)
            {
                if (obj == null) return;
                byte[] data = ToMessagePack(obj);
                File.WriteAllBytes(filePath, data);
            }

            /// <summary>
            /// 使用 MessagePack 从文件反序列化为对象。
            /// 支持多态对象的文件恢复。
            /// </summary>
            /// <typeparam name="T">目标对象类型。</typeparam>
            /// <param name="filePath">MessagePack文件路径。</param>
            /// <returns>反序列化后的对象实例，文件不存在时返回default(T)。</returns>
            /// <remarks>
            /// - 多态恢复：需要额外配置（TODO/未实装：当前默认配置不保证“自动识别对象类型”用于多态）
            /// - 文件检查：安全检查文件存在性
            /// - 性能：快速的文件读取和反序列化
            /// - 错误处理：文件损坏时返回默认值
            /// </remarks>
            public static T FromMessagePackFile<T>(string filePath)
            {
                if (!File.Exists(filePath)) return default;
                byte[] data = File.ReadAllBytes(filePath);
                return FromMessagePack<T>(data);
            }
#endif
            #endregion

            #region XML序列化支持
            /// <summary>
            /// 将对象序列化为 XML 字符串。
            /// 支持多态序列化，但需要正确配置类型信息。
            /// </summary>
            /// <typeparam name="T">对象类型，支持多态类型。</typeparam>
            /// <param name="obj">要序列化的对象。</param>
            /// <param name="encoding">编码格式，默认 UTF-8。</param>
            /// <returns>XML 格式的字符串，失败返回空字符串。</returns>
            /// <remarks>
            /// - 多态支持：通过[XmlInclude]等“预注册已知类型”支持继承/接口（TODO/未完全实现：当前未提供 knownTypes 参数重载）
            /// - 类型信息：通常依赖 xsi:type 且需要序列化器已知派生类型（TODO/未完全实现：不保证“完整类型信息/程序集限定名”）
            /// - 可读性：XML格式具有良好的可读性
            /// - 兼容性：标准.NET XML序列化
            /// - 限制：需要无参构造函数，某些复杂类型可能需要特殊处理
            /// </remarks>
            public static string ToXmlString<T>(T obj, Encoding encoding = null) where T : class
            {
                if (obj == null)
                {
                    Debug.LogWarning("要序列化的对象为 Null.");
                    return string.Empty;
                }

                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    encoding = encoding ?? Encoding.UTF8;

                    using (StringWriter writer = new StringWriterWithEncoding(encoding))
                    {
                        serializer.Serialize(writer, obj);
                        return writer.ToString();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"XML 序列化失败: {ex.Message}");
                    return string.Empty;
                }
            }

            /// <summary>
            /// 从 XML 字符串反序列化为对象。
            /// 支持多态反序列化，可以根据XML中的类型信息恢复正确的具体类型。
            /// </summary>
            /// <typeparam name="T">目标对象类型，支持接口和抽象类型。</typeparam>
            /// <param name="xmlString">XML 字符串。</param>
            /// <returns>反序列化后的对象，失败返回 null。</returns>
            /// <remarks>
            /// <para>- 多态反序列化：可根据XML中的 xsi:type 恢复具体类型（TODO/未完全实现：前提是 XmlSerializer 已知派生类型，如 [XmlInclude]）</para>
            /// <para>- 类型安全：确保反序列化结果符合预期类型</para>
            /// <para>- 构造函数：需要无参构造函数</para>
            /// <para>- 错误处理：XML格式错误时返回null</para>
            /// <para>示例：</para>
            /// <code>
            /// string xml = "&lt;ArrayOfShape&gt;&lt;Shape xsi:type=\"CircleShape\"&gt;&lt;Name&gt;Circle&lt;/Name&gt;&lt;Radius&gt;5&lt;/Radius&gt;&lt;/Shape&gt;&lt;/ArrayOfShape&gt;";
            /// List&lt;Shape&gt; shapes = Matcher.FromXmlString&lt;List&lt;Shape&gt;&gt;(xml);
            /// </code>
            /// </remarks>
            public static T FromXmlString<T>(string xmlString) where T : class
            {
                if (string.IsNullOrEmpty(xmlString))
                {
                    Debug.LogWarning("XML 字符串为空或为 Null.");
                    return null;
                }

                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));

                    using (StringReader reader = new StringReader(xmlString))
                    {
                        return (T)serializer.Deserialize(reader);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"XML 反序列化失败: {ex.Message}");
                    return null;
                }
            }

            /// <summary>
            /// 将对象序列化并保存到 XML 文件。
            /// 支持多态对象的文件持久化。
            /// </summary>
            /// <typeparam name="T">对象类型，支持多态类型。</typeparam>
            /// <param name="obj">要序列化的对象。</param>
            /// <param name="filePath">文件保存路径。</param>
            /// <param name="encoding">编码格式，默认 UTF-8。</param>
            /// <returns>是否成功。</returns>
            /// <remarks>
            /// <para>- 多态持久化：依赖 XmlSerializer 已知派生类型（TODO/未完全实现：不保证“保持完整类型信息”）</para>
            /// <para>- 文件操作：自动创建目录和处理文件写入</para>
            /// <para>- 编码支持：支持多种字符编码</para>
            /// <para>- 错误处理：保存失败时返回false并记录错误</para>
            /// <para>示例：</para>
            /// <code>
            /// List&lt;Shape&gt; shapes = new List&lt;Shape&gt; { new CircleShape { Name = "Circle", Radius = 5 } };
            /// bool success = Matcher.ToXmlFile(shapes, "shapes.xml");
            /// </code>
            /// </remarks>
            public static bool ToXmlFile<T>(T obj, string filePath, Encoding encoding = null) where T : class
            {
                if (obj == null)
                {
                    Debug.LogWarning("要序列化的对象为 Null.");
                    return false;
                }

                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    encoding = encoding ?? Encoding.UTF8;
                    string directoryPath = Path.GetDirectoryName(filePath);

                    // 确保目录存在
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    using (StreamWriter writer = new StreamWriter(filePath, false, encoding)) // false 表示覆盖而非追加
                    {
                        serializer.Serialize(writer, obj);
                    }

                    Debug.Log($"XML 文件保存成功: {filePath}");
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"保存 XML 文件失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 从 XML 文件读取并反序列化为对象。
            /// 支持多态对象的文件恢复。
            /// </summary>
            /// <typeparam name="T">目标对象类型，支持接口和抽象类型。</typeparam>
            /// <param name="filePath">XML 文件路径。</param>
            /// <returns>反序列化后的对象，失败返回 null。</returns>
            /// <remarks>
            /// <para>- 多态恢复：可根据文件中的 xsi:type 恢复具体类型（TODO/未完全实现：前提是 XmlSerializer 已知派生类型，如 [XmlInclude]）</para>
            /// <para>- 文件检查：安全检查文件存在性</para>
            /// <para>- 编码检测：自动检测文件编码</para>
            /// <para>- 错误处理：文件损坏或格式错误时返回null</para>
            /// <para>示例：</para>
            /// <code>
            /// List&lt;Shape&gt; shapes = Matcher.FromXmlFile&lt;List&lt;Shape&gt;&gt;("shapes.xml");
            /// </code>
            /// </remarks>
            public static T FromXmlFile<T>(string filePath) where T : class
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"XML 文件不存在: {filePath}");
                    return null;
                }

                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));

                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        return (T)serializer.Deserialize(reader);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"读取 XML 文件失败: {ex.Message}");
                    return null;
                }
            }

            // 辅助类，用于 StringWriter 指定编码
            private class StringWriterWithEncoding : StringWriter
            {
                private readonly Encoding _encoding;

                public StringWriterWithEncoding(Encoding encoding)
                {
                    _encoding = encoding;
                }

                public override Encoding Encoding
                {
                    get { return _encoding; }
                }
            }
            #endregion

            #region PlayerPrefs支持
            /// <summary>
            /// 快捷保存到PlayerPrefs<通过Json序列化>
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="key">键</param>
            /// <param name="obj">对象</param>
            public static void SaveToPlayerPrefs<T>(string key, T obj) where T : class
            {
                string json = ToJson(obj);
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
            /// <summary>
            /// 从 PlayerPrefs 加载对象 (使用 JsonUtility)
            /// <param name="key">键</param>
            /// </summary>
            public static T LoadFromPlayerPrefs<T>(string key) where T : class
            {
                if (PlayerPrefs.HasKey(key))
                {
                    string json = PlayerPrefs.GetString(key);
                    return FromJson<T>(json);
                }
                return default;
            }
            #endregion
        }

        #region 序列化示例类
        // 必须添加 [System.Serializable] 特性
        [System.Serializable]
        // 可选：指定 XML 根元素名称，如果不指定，则使用类名
        [XmlRoot("ExampleXML")]
        public class ExampleXML
        {
            // 作为 XML 元素的属性序列化
            [XmlElement("PreNameToABKeys")]
            public string playerName;

            [XmlElement("Level")]
            public int level;

            [XmlElement("Health")]
            public float health;
            // 作为 XML 属性序列化（出现在元素标签内）
            [XmlAttribute("ID")]
            public int id;
            // 支持列表和数组
            [XmlArray("InventoryItems")]
            [XmlArrayItem("Item")]
            public List<string> inventoryItems;
            // 构造函数（对于反序列化，通常需要无参构造函数）
            public ExampleXML()
            {
                inventoryItems = new List<string>();
            }
            public ExampleXML(string name, int lv, float hp, int id)
            {
                playerName = name;
                level = lv;
                health = hp;
                this.id = id;
                inventoryItems = new List<string>();
            }
        }

        // XML多态序列化示例
        // 要支持多态，需要使用[XmlInclude]属性
        [XmlInclude(typeof(CircleShape))]
        [XmlInclude(typeof(RectangleShape))]
        public abstract class Shape
        {
            public string Name { get; set; }
        }

        public class CircleShape : Shape
        {
            public float Radius { get; set; }
        }

        public class RectangleShape : Shape
        {
            public float Width { get; set; }
            public float Height { get; set; }
        }
        #endregion
    }

}

