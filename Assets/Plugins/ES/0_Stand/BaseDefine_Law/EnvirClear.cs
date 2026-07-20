namespace ES
{
    /// <summary>
    /// 环境明确化模板。
    /// Clear 在这里不是“清理”，而是把 NotClear / Any / 未指定 等泛态值明确成可执行值。
    /// </summary>
    public static class EnvirClear
    {
        /// <summary>
        /// 当 value 等于 unclearValue 时，写入调用方给出的 defaultValue。
        /// </summary>
        public static void ResolveDefault<T>(this ref T value, T unclearValue, T defaultValue) where T : struct
        {
            if (value.Equals(unclearValue)) value = defaultValue;
        }

        /// <summary>
        /// ResolveDefault 的语义别名，用于保持 ToClear 这种旧调用风格。
        /// </summary>
        public static void ToClear<T>(this ref T value, T unclearValue, T defaultValue) where T : struct
        {
            value.ResolveDefault(unclearValue, defaultValue);
        }
    }
}
