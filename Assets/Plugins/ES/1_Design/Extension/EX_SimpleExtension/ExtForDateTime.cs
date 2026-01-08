using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    public static class ExtForDateTime
    {
        #region 浮点秒与String字符串显示的转化

        /// <summary>
        /// 将秒数转换为"mm分钟:ss秒"格式
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static string _ToStringDate_mm_ss(this float seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        }
        /// <summary>
        /// 将秒数转换为"hh小时:mm分钟:ss秒"格式
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static string _ToStringDate_hh_mm_ss(this float seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
        /// <summary>
        /// 将秒数转换为文件命名友好格式（HH-mm-ss）
        /// </summary>
        public static string ToStringDate_SimpleFormat(this double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.Hours:D2}-{ts.Minutes:D2}-{ts.Seconds:D2}";
        }
        /// <summary>
        /// 将秒数转换为带单位的中文简短描述（智能选择最合适单位）
        /// </summary>
        public static string _ToStringDate_简短中文天小时分秒(this double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);

            if (ts.TotalDays >= 1)
                return $"{ts.TotalDays:F1}天";
            if (ts.TotalHours >= 1)
                return $"{ts.TotalHours:F1}小时";
            if (ts.TotalMinutes >= 1)
                return $"{ts.TotalMinutes:F1}分钟";

            return $"{ts.TotalSeconds}秒";
        }
        /// <summary>
        /// 将秒数转换为天、小时、分钟、秒的完整中文描述
        /// </summary>
        public static string _ToStringDate_标准中文天小时分秒(this double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);

            if (ts.TotalDays >= 1)
                return $"{ts.Days}天{ts.Hours}小时{ts.Minutes}分钟{ts.Seconds}秒";
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}小时{ts.Minutes}分钟{ts.Seconds}秒";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}分钟{ts.Seconds}秒";

            return $"{ts.Seconds}秒";
        }
        /// <summary>
        /// 将秒数转换为语音播报友好格式（中文）天、小时、分钟、秒按需求添加
        /// </summary>
        public static string ToStringDate_播报式中文天小时分秒(this int seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);

            string result = "";
            if (ts.Days > 0) result += $"{ts.Days}天";
            if (ts.Hours > 0) result += $"{ts.Hours}小时";
            if (ts.Minutes > 0) result += $"{ts.Minutes}分钟";
            if (ts.Seconds > 0) result += $"{ts.Seconds}秒";

            return result;
        }

        #endregion

        #region 时间戳与天判据
        /// <summary>
        /// 将时间戳转换为DateTime
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public static DateTime _AsDateTime(this long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
        }

        /// <summary>
        /// 将DateTime转换为时间戳
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static long _AsTimestamp(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }

       /// <summary>
       /// 检查DateTime是否在今天
       /// </summary>
       /// <param name="dateTime"></param>
       /// <returns></returns>
        public static bool _IsToday(this DateTime dateTime)
        {
            return dateTime.Date == DateTime.Today;
        }

        /// <summary>
        /// 检查DateTime是否在昨天
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static bool _IsYesterday(this DateTime dateTime)
        {
            return dateTime.Date == DateTime.Today.AddDays(-1);
        }

        /// <summary>
        /// 检查DateTime是否在明天
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static bool _IsTomorrow(this DateTime dateTime)
        {
            return dateTime.Date == DateTime.Today.AddDays(1);
        }

        /// <summary>
        /// 获取DateTime的开始时间一天的(00:00:00)
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTime _StartOfDay(this DateTime dateTime)
        {
            return dateTime.Date;
        }

        /// <summary>
        /// 获取DateTime的结束时间(23:59:59)
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTime _EndOfDay(this DateTime dateTime)
        {
            return dateTime.Date.AddDays(1).AddTicks(-1);
        }
        #endregion

        #region 相对现在

        /// <summary>
        /// 计算两个DateTime之间的天数差
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to">最终时间</param>
        /// <returns></returns>
        public static int _DaysBetween(this DateTime from, DateTime to)
        {
            return (int)(to.Date - from.Date).TotalDays;
        }
        /// <summary>
        /// 计算从现在开始到一个目标DateTime之间的天数差
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns></returns>
        public static int _TotalDaysFromNowToThis(this DateTime to)
        {
            return (int)(to.Date - DateTime.Now).TotalDays;
        }
        /// <summary>
        /// 计算从现在开始到一个目标DateTime之间的小时差
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns></returns>
        public static int _TotalHoursFromNowToThis(this DateTime to)
        {
            return (int)(to.Date - DateTime.Now).TotalDays;
        }
        /// <summary>
        /// 计算从现在开始到一个目标DateTime之间的分钟差
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns></returns>
        public static int _TotalMinutesFromNowToThis(this DateTime to)
        {
            return (int)(to.Date - DateTime.Now).TotalMinutes;
        }
        /// <summary>
        /// 转换为中文相对时间描述（如"2小时前"）
        /// </summary>
        public static string ToStringDate_过去的中文相对时间表达(this DateTime dt)
        {
            TimeSpan span = DateTime.Now - dt;

            if (span.TotalDays > 60) return dt.ToString("yyyy-MM-dd");
            if (span.TotalDays > 30) return "1个月前";
            if (span.TotalDays > 14) return "2周前";
            if (span.TotalDays > 7) return "1周前";
            if (span.TotalDays > 1) return $"{(int)span.TotalDays}天前";
            if (span.TotalHours > 1) return $"{(int)span.TotalHours}小时前";
            if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalSeconds >= 0) return "刚刚";

            return dt.ToString("yyyy-MM-dd");
        }
        /// <summary>
        /// 转换为英文相对时间描述（如"2 days ago"）
        /// </summary>
        public static string ToStringDate_过去的英文相对时间表达(this DateTime dt)
        {
            TimeSpan span = DateTime.Now - dt;

            if (span.TotalDays > 60) return dt.ToString("MMM dd, yyyy");
            if (span.TotalDays > 30) return "1 month ago";
            if (span.TotalDays > 14) return "2 weeks ago";
            if (span.TotalDays > 7) return "1 week ago";
            if (span.TotalDays > 1) return $"{(int)span.TotalDays} days ago";
            if (span.TotalHours > 1) return $"{(int)span.TotalHours} hours ago";
            if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes} minutes ago";
            if (span.TotalSeconds >= 30) return "30 seconds ago";
            if (span.TotalSeconds >= 10) return "10 seconds ago";
            if (span.TotalSeconds >= 0) return "just now";

            return dt.ToString("MMM dd, yyyy");
        }
        #endregion
    }
}
