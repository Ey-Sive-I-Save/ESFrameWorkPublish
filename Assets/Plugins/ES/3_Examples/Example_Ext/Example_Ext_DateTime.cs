using System;
using UnityEngine;

namespace ES
{
    // 示例：演示 ExtForDateTime.cs 中常用方法
    // 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForDateTime.cs
    public class Example_Ext_DateTime : MonoBehaviour
    {
        void Start()
        {
            // 1) 时间段格式化
            float seconds = 3661f; // 1小时1分1秒
            Debug.Log($"_ToStringDate_hh_mm_ss: {seconds} -> {seconds._ToStringDate_hh_mm_ss()}");
            Debug.Log($"_ToStringDate_mm_ss: {seconds} -> {seconds._ToStringDate_mm_ss()}");

            // 2) DateTime 基本判断与起止
            var now = DateTime.Now;
            Debug.Log($"Now: {now}, _IsToday: {now._IsToday()}, _IsYesterday: {now._IsYesterday()}, _IsTomorrow: {now._IsTomorrow()}");
            Debug.Log($"StartOfDay: {now._StartOfDay()}, EndOfDay: {now._EndOfDay()}");

            // 3) 时间差与总量计算
            DateTime future = now.AddDays(3).AddHours(5);
            Debug.Log($"_DaysBetween -> {now._DaysBetween(future)} days");
            Debug.Log($"TotalDaysFromNowToThis -> {future._TotalDaysFromNowToThis()}");

            // 4) 时间戳 / 毫秒时间戳
            long ts = now._AsTimestamp();
            long ms = now._AsTimestampMilliseconds();
            Debug.Log($"Timestamp: {ts}, Milliseconds: {ms}");
            Debug.Log($"_AsDateTime from ts: {ts._AsDateTime()}");

            // 5) ISO 格式及解析
            string iso = now._ToIsoString();
            Debug.Log($"ISO: {iso} -> parsed: {iso._ParseIsoString()}");

            // 6) 从年月日创建与解析短日期
            var fromYmd = (2020, 12, 31)._DateFromYMD();
            Debug.Log($"_DateFromYMD -> {fromYmd}");
            if ("2020-12-31"._TryParseDateOnly(out var d))
            {
                Debug.Log($"_TryParseDateOnly -> {d}");
            }

            // 7) 年龄计算
            int age = "1990-01-01"._AgeFromDateOnly();
            Debug.Log($"_AgeFromDateOnly('1990-01-01') -> {age}");

            // 8) Start/End of week/month
            Debug.Log($"StartOfWeek: {now._StartOfWeek()}, EndOfWeek: {now._EndOfWeek()}");
            Debug.Log($"StartOfMonth: {now._StartOfMonth()}, EndOfMonth: {now._EndOfMonth()}");

            // 9) Roundtrip with milliseconds
            long msec = now._AsTimestampMilliseconds();
            var fromMs = msec._AsDateTimeFromMilliseconds();
            Debug.Log($"Milliseconds roundtrip -> {fromMs}");
        }
    }
}
