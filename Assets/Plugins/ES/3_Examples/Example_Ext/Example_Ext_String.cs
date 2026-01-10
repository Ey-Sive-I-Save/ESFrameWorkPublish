using System;
using UnityEngine;

// 示例：演示 ExtForString_Main.cs 中部分字符串扩展
namespace ES
{
    public class Example_Ext_String : MonoBehaviour
    {
        void Start()
        {
              string path = "Assets/Scripts/Test.cs";
              string email = "user@example.com";
              string number = "123.45";
              string hashSource = "password";
              string str = "Hello, World!";
              string str2 = "  123  ";
              string strWithChinese = "Hello世界";

              // 截取系列
              Debug.Log($"_KeepBeforeByFirst: {path._KeepBeforeByFirst("/")} // 保留第一个'/'前的内容");
              Debug.Log($"_KeepBeforeByLast: {path._KeepBeforeByLast("/")} // 保留最后一个'/'前的内容");
              Debug.Log($"_KeepAfterByFirst: {path._KeepAfterByFirst("/")} // 保留第一个'/'后的内容");
              Debug.Log($"_KeepAfterByLast: {path._KeepAfterByLast("/")} // 保留最后一个'/'后的内容");
              Debug.Log($"_KeepBeforeByFirstChar: {path._KeepBeforeByFirstChar('/')} // 保留第一个'/'前的内容(字符版)");
              Debug.Log($"_KeepBeforeByLastChar: {path._KeepBeforeByLastChar('/')} // 保留最后一个'/'前的内容(字符版)");
              Debug.Log($"_KeepAfterByFirstChar: {path._KeepAfterByFirstChar('/')} // 保留第一个'/'后的内容(字符版)");
              Debug.Log($"_KeepAfterByLastChar: {path._KeepAfterByLastChar('/')} // 保留最后一个'/'后的内容(字符版)");
              Debug.Log($"_KeepBeforeByMaxLength: {str._KeepBeforeByMaxLength(5)} // 限制最大长度5，超出加...结尾");
              Debug.Log($"_KeepBetween: {"[Hello World]"._KeepBetween("[", "]")} // 保留[和]之间的内容");

              // 特征查询
              Debug.Log($"_IsValidEmail: {email._IsValidEmail()} // 判断是否为有效邮箱");
              Debug.Log($"_IsValidUrl: {"https://www.example.com"._IsValidUrl()} // 判断是否为有效URL");
              Debug.Log($"_IsNumeric: {number._IsNumeric()} // 判断是否为数字字符串");
              Debug.Log($"_HasSpace: {str2._HasSpace()} // 是否包含空格");
              Debug.Log($"_ContainsChineseCharacter: {strWithChinese._ContainsChineseCharacter()} // 是否包含中文汉字");
              Debug.Log($"_IsValidIdentName: {"valid_name"._IsValidIdentName()} // 是否为有效C#标识符");
              Debug.Log($"_GetSlashCount: {path._GetSlashCount()} // 统计斜线数量");

              // 操作部分
              Debug.Log($"_RemoveExtraSpaces: {"a   b   c"._RemoveExtraSpaces()} // 移除多余空格");
              Debug.Log($"_RemoveSubStrings: {"abcdefg"._RemoveSubStrings("bcd", "fg")} // 移除指定子串");
              Debug.Log($"_RemoveExtension: {"file.txt"._RemoveExtension()} // 移除文件扩展名");
              Debug.Log($"_AddPreAndLast: {"core"._AddPreAndLast("<", ">")} // 前后加字符串");
              Debug.Log($"_FirstCharToUpperCapitalize: {"  hello"._FirstCharToUpperCapitalize()} // 首个非空白字符大写");
              Debug.Log($"_FirstUpper: {"hello"._FirstUpper(System.Globalization.CultureInfo.InvariantCulture)} // 首字母大写");
              Debug.Log($"_FirstLower: {"HELLO"._FirstLower(System.Globalization.CultureInfo.InvariantCulture)} // 首字母小写");
              Debug.Log($"_ToValidIdentName: {"123 abc"._ToValidIdentName()} // 转为合法C#标识符");
              Debug.Log($"_ToCode: {"public class A { void B() { } }"._ToCode()} // 格式化为代码风格");
              Debug.Log($"_ToCodePro: {"public class A { void B() { } }"._ToCodePro()} // 高性能代码格式化");

              // 转化部分
              Debug.Log($"_AsStringValue: {str._AsStringValue()} // 转为带引号字符串");
              Debug.Log($"_AsInt: {str2._AsInt(-1)} // 转为int");
              Debug.Log($"_AsLong: {str2._AsLong(-1)} // 转为long");
              Debug.Log($"_AsDateTime: {"2026-01-10"._AsDateTime(DateTime.MinValue)} // 转为DateTime");
              Debug.Log($"_AsFloat: {number._AsFloat(0f)} // 转为float");
              Debug.Log($"_ToMD5Hash: {hashSource._ToMD5Hash()} // 生成MD5哈希");
              Debug.Log($"_ToSHA1Hash: {hashSource._ToSHA1Hash()} // 生成SHA1哈希");
              Debug.Log($"_ToSha256Hash: {hashSource._ToSha256Hash()} // 生成SHA256哈希");

              // StringBuilder专属
              var sb = str._AsBuilder();
              sb._AddPre("[PRE]");
              Debug.Log($"_AsBuilder/_AddPre: {sb.ToString()} // StringBuilder操作");
        }
    }
}
