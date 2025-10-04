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
         Design 的 Matcher
         和 RunTime的Matcher 职能完全不一样
         */
        public static class Matcher
        {
            #region 类型转换
            /// <summary>
            /// 将对象安全地转换为指定类型。
            /// </summary>
            /// <typeparam name="T">目标类型。</typeparam>
            /// <param name="from">源对象。</param>
            /// <param name="defaultValue">转换失败时返回的默认值。</param>
            /// <returns>转换后的对象，失败则返回默认值。</returns>
            public static T SystemObjectToT<T>(object from, T defaultValue = default(T))
            {
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
                    Console.WriteLine($"无法将类型 {from.GetType()} 转换为 {targetType}。");
                    return defaultValue;
                }
                catch (FormatException)
                {
                    Console.WriteLine($"对象 {from} 的格式不符合 {targetType}。");
                    return defaultValue;
                }
                catch (OverflowException)
                {
                    Console.WriteLine($"对象 {from} 对于类型 {targetType} 是一个溢出值。");
                    return defaultValue;
                }
                catch (Exception ex) // 捕获其他可能的异常
                {
                    Console.WriteLine($"转换对象 {from} 到类型 {targetType} 时发生未知错误: {ex.Message}");
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
                    Console.WriteLine($"无效的转换: 无法将类型 {from.GetType()} 转换为 {targetType}。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"格式错误: 对象 '{from}' 的格式不符合 {targetType}。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (OverflowException ex)
                {
                    Console.WriteLine($"溢出错误: 对象 '{from}' 对于类型 {targetType} 是一个溢出值。异常信息: {ex.Message}");
                    return defaultValue;
                }
                catch (Exception ex) // 捕获其他可能的异常
                {
                    Console.WriteLine($"转换对象 '{from}' 到类型 {targetType} 时发生未知错误: {ex.Message}");
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
            /// 使用 JsonUtility 将对象序列化为 JSON 字符串
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="prettyPrint">压缩？</param>
            /// <returns></returns>
            public static string ToJson<T>(T obj, bool prettyPrint = false)
            {
                if (obj == null) return string.Empty;
                return JsonUtility.ToJson(obj, prettyPrint);
            }
            /// <summary>
            /// 使用 JsonUtility 从 JSON 字符串反序列化为对象
            /// </summary>
            public static T FromJson<T>(string json)
            {
                if (string.IsNullOrEmpty(json)) return default;
                return JsonUtility.FromJson<T>(json);
            }
            /// <summary>
            /// 使用 JsonUtility 从 JSON "文件"反序列化为对象
            /// <param name="filePath">文件路径(File的)</param>
            /// </summary>
            public static T FromJsonFile<T>(string filePath)
            {
                if (!File.Exists(filePath)) return default;
                string json = File.ReadAllText(filePath);
                return FromJson<T>(json);
            }
            /// <summary>
            /// 使用 JsonUtility 将对象序列化到 JSON 文件
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="obj"></param>
            /// <param name="filePath">文件路径(File的)</param>
            /// <param name="prettyPrint">压缩？</param>
            public static void ToJsonFile<T>(T obj, string filePath, bool prettyPrint = false) where T : class
            {
                string json = ToJson(obj, prettyPrint);
                File.WriteAllText(filePath, json);
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

            #region Odin 序列化支持
#if ODIN_INSPECTOR
            /// <summary>
            /// 使用 Odin Serializer 将对象序列化为字节数组
            /// </summary>
            public static byte[] ToOdinBinary<T>(T obj)
            {
                if (obj == null) return null;
                return SerializationUtility.SerializeValue(obj, DataFormat.Binary);
            }
            /// <summary>
            /// 使用 Odin Serializer 从字节数组反序列化为对象
            /// </summary>
            public static T FromOdinBinary<T>(byte[] data)
            {
                if (data == null || data.Length == 0) return default;
                return SerializationUtility.DeserializeValue<T>(data, DataFormat.Binary);
            }
            /// <summary>
            /// 使用 Odin Serializer 将对象序列化为 JSON 字符串
            /// </summary>
            public static string ToOdinJson<T>(T obj)
            {
                if (obj == null) return string.Empty;
                byte[] jsonBytes = SerializationUtility.SerializeValue(obj, DataFormat.JSON);
                return System.Text.Encoding.UTF8.GetString(jsonBytes);
            }
            /// <summary>
            /// 使用 Odin Serializer 从 JSON 字符串反序列化为对象
            /// </summary>
            public static T FromOdinJson<T>(string json)
            {
                if (string.IsNullOrEmpty(json)) return default;
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
                return SerializationUtility.DeserializeValue<T>(jsonBytes, DataFormat.JSON);
            }
            /// <summary>
            /// 使用 Odin Serializer 深拷贝对象
            /// </summary>
            public static T DeepCopy<T>(T obj) where T : class
            {
                if (obj == null) return default;
                return SerializationUtility.CreateCopy(obj) as T;
            }
#endif
            #endregion


            #region XML支持
            /// <summary>
            /// 将对象序列化为 XML 字符串
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <param name="obj">要序列化的对象</param>
            /// <param name="encoding">编码格式，默认 UTF-8</param>
            /// <returns>XML 格式的字符串，失败返回空字符串</returns>
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
            /// 从 XML 字符串反序列化为对象
            /// </summary>
            /// <typeparam name="T">目标对象类型</typeparam>
            /// <param name="xmlString">XML 字符串</param>
            /// <returns>反序列化后的对象，失败返回 null</returns>
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
            /// 将对象序列化并保存到 XML 文件
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <param name="obj">要序列化的对象</param>
            /// <param name="filePath">文件保存路径</param>
            /// <param name="encoding">编码格式，默认 UTF-8</param>
            /// <returns>是否成功</returns>
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
            /// 从 XML 文件读取并反序列化为对象
            /// </summary>
            /// <typeparam name="T">目标对象类型</typeparam>
            /// <param name="filePath">XML 文件路径(FIle的)</param>
            /// <returns>反序列化后的对象，失败返回 null</returns>
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
        }

        #region XML规范案例
        // 必须添加 [System.Serializable] 特性
        [System.Serializable]
        // 可选：指定 XML 根元素名称，如果不指定，则使用类名
        [XmlRoot("ExmapleXML")]
        public class ExmapleXML
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
            public ExmapleXML()
            {
                inventoryItems = new List<string>();
            }
            public ExmapleXML(string name, int lv, float hp, int id)
            {
                playerName = name;
                level = lv;
                health = hp;
                this.id = id;
                inventoryItems = new List<string>();
            }
        }
        #endregion







        #endregion
    }

}

