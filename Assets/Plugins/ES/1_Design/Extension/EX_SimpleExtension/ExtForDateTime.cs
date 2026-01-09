using System;
using System.Runtime.CompilerServices;

namespace ES
{
    public static class ExtForDateTime
    {
        #region 浮点秒与String字符串显示的转化

        /// <summary>
        /// 将秒数转换为 "mm:ss" 字符串（分钟:秒）
        /// </summary>
        public static string _ToStringDate_mm_ss(this float seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        }
        /// <summary>
        /// 将秒数转换为"hh:mm:ss"格式
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
            return $"{ts.Hours:D2}-{ts.Minutes:D2}-{ts.Seconds:D2}";
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

            return $"{ts.TotalSeconds:F0}秒";
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

            string result = string.Empty;
            if (ts.Days > 0) result += $"{ts.Days}天";
            if (ts.Hours > 0) result += $"{ts.Hours}小时";
            if (ts.Minutes > 0) result += $"{ts.Minutes}分钟";
            if (ts.Seconds > 0) result += $"{ts.Seconds}秒";

            return string.IsNullOrEmpty(result) ? "0秒" : result;
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
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().DateTime;
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
        /// 计算从现在开始到一个目标 DateTime 之间的天数差（按整日计算，保留符号）。
        /// 使用 <c>(to.Date - DateTime.Now.Date).Days</c> 来避免对小数部分的截断歧义。
        /// 例如目标比现在早则返回负数。
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns>整天差（可为负）</returns>
        public static int _TotalDaysFromNowToThis(this DateTime to)
        {
            return (to.Date - DateTime.Now.Date).Days;
        }
        /// <summary>
        /// 计算从现在开始到一个目标 DateTime 之间的小时差（带符号）。
        /// 使用向下取整（<c>Math.Floor</c>）来避免对负数截断导致的不一致。
        /// 例如：若差值为 -1.9 小时，则返回 -2（而非 -1）。
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns>整小时差（可为负）</returns>
        public static int _TotalHoursFromNowToThis(this DateTime to)
        {
            return (int)Math.Floor((to - DateTime.Now).TotalHours);
        }
        /// <summary>
        /// 计算从现在开始到一个目标 DateTime 之间的分钟差（带符号）。
        /// 使用向下取整（<c>Math.Floor</c>），以保证对负数行为一致。
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns>整分钟差（可为负）</returns>
        public static int _TotalMinutesFromNowToThis(this DateTime to)
        {
            return (int)Math.Floor((to - DateTime.Now).TotalMinutes);
        }

        /// <summary>
        /// 计算从现在开始到一个目标 DateTime 之间的秒差（带符号）。
        /// 使用向下取整（<c>Math.Floor</c>）以保证对负数行为一致。
        /// </summary>
        /// <param name="to">最终时间</param>
        /// <returns>整秒差（可为负）</returns>
        public static int _TotalSecondsFromNowToThis(this DateTime to)
        {
            return (int)Math.Floor((to - DateTime.Now).TotalSeconds);
        }
        /// <summary>
        /// 计算从给定 DateTime 到现在过去了多少整天（非负）。
        /// 返回值表示过去了多少整天；如果给定时间在未来则返回 0。
        /// </summary>
        public static int _TotalDaysFromThisToNow(this DateTime from)
        {
            return (DateTime.Now.Date - from.Date).Days;
        }

        /// <summary>
        /// 计算从给定 DateTime 到现在过去了多少整小时（非负）。
        /// 使用向下取整（Math.Floor）。如果给定时间在未来则返回 0。
        /// </summary>
        public static int _TotalHoursFromThisToNow(this DateTime from)
        {
            return (int)Math.Floor((DateTime.Now - from).TotalHours);
        }

        /// <summary>
        /// 计算从给定 DateTime 到现在过去了多少整分钟（非负）。
        /// 使用向下取整（Math.Floor）。如果给定时间在未来则返回 0。
        /// </summary>
        public static int _TotalMinutesFromThisToNow(this DateTime from)
        {
           return (int)Math.Floor((DateTime.Now - from).TotalMinutes);

        }

        /// <summary>
        /// 计算从给定 DateTime 到现在过去了多少整秒（非负）。
        /// 使用向下取整（Math.Floor）。如果给定时间在未来则返回 0。
        /// </summary>
        public static int _TotalSecondsFromThisToNow(this DateTime from)
        {
            return (int)Math.Floor((DateTime.Now - from).TotalSeconds);
        }
        /// <summary>
        /// 将 DateTime 转为 Unix 毫秒时间戳
        /// </summary>
        public static long _AsTimestampMilliseconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 将 Unix 毫秒时间戳转换为本地 DateTime
        /// </summary>
        public static DateTime _AsDateTimeFromMilliseconds(this long milliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().DateTime;
        }

        /// <summary>
        /// 获取指定日期所在周的开始时间（00:00:00），默认周起始为 Monday
        /// </summary>
        public static DateTime _StartOfWeek(this DateTime dateTime, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            int diff = (7 + (dateTime.DayOfWeek - startOfWeek)) % 7;
            DateTime start = dateTime.Date.AddDays(-diff);
            return start.Date;
        }

        /// <summary>
        /// 获取指定日期所在周的结束时间（最后一刻）
        /// </summary>
        public static DateTime _EndOfWeek(this DateTime dateTime, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            DateTime start = _StartOfWeek(dateTime, startOfWeek);
            return start.AddDays(7).AddTicks(-1);
        }

        /// <summary>
        /// 获取指定日期所在月的开始时间（当月1日 00:00:00）
        /// </summary>
        public static DateTime _StartOfMonth(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1);
        }

        /// <summary>
        /// 获取指定日期所在月的结束时间（当月最后一刻）
        /// </summary>
        public static DateTime _EndOfMonth(this DateTime dateTime)
        {
            return _StartOfMonth(dateTime).AddMonths(1).AddTicks(-1);
        }

        /// <summary>
        /// 将 DateTime 转为 ISO 8601 字符串（Round-trip）
        /// </summary>
        public static string _ToIsoString(this DateTime dateTime)
        {
            return dateTime.ToString("o");
        }

        /// <summary>
        /// 尝试从 ISO 8601 字符串解析为 DateTime，解析失败返回 DateTime.MinValue
        /// </summary>
        public static DateTime _ParseIsoString(this string s)
        {
            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        /// <summary>
        /// 根据年、月、日构造本地 DateTime（时分秒为 00:00:00）
        /// 若参数非法会抛出 ArgumentOutOfRangeException，与 DateTime 构造行为一致
        /// </summary>
        public static DateTime _DateFromYMD(this (int year, int month, int day) ymd)
        {
            return new DateTime(ymd.year, ymd.month, ymd.day);
        }

        /// <summary>
        /// 尝试解析只包含年月日的字符串，支持格式 "yyyy-MM-dd"、"yyyy/MM/dd" 等。
        /// 返回 true 并输出 DateTime（时分秒为 00:00:00）表示解析成功，否则返回 false。
        /// </summary>
        public static bool _TryParseDateOnly(this string s, out DateTime date)
        {
            // 仅尝试常见日期格式，使用 invariant culture 避免地区性差异
            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd" };
            return DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
        }

        /// <summary>
        /// 根据只包含年月日的字符串计算年龄（周岁），若解析失败返回 -1
        /// </summary>
        public static int _AgeFromDateOnly(this string dateOnly)
        {
            if (!dateOnly._TryParseDateOnly(out var birth)) return -1;
            return birth._Age();
        }

        /// <summary>
        /// 计算出生日期到现在的周岁年龄（年）
        /// </summary>
        public static int _Age(this DateTime birthDate)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
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
            if (span.TotalSeconds >= 1) return "刚刚";

            // 如果传入的是将来的时间，返回具体日期
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
            if (span.TotalSeconds >= 1) return "just now";

            // future date: show exact date
            return dt.ToString("MMM dd, yyyy");
        }
        #endregion
    }
}
