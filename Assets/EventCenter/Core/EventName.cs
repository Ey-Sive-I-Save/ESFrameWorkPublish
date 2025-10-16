/*
 * 当你要创建一个新事件时，请在这个枚举中添加一项。
 * 并按照Example的注释的格式写清楚注释。
 * 注明每个参数是什么类型的，有什么意义，如果没有参数则留空。
 * 这样将鼠标放在事件名上就可以得知每个参数的详细信息，防止出错。
 */
namespace FlyRabbit.EventCenter
{
    /// <summary>
    /// 事件中心系统中的事件名
    /// </summary>
    public enum EventName : int
    {
        /// <summary>
        /// 只是一个例子，没有任何实际作用。
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>参数1：string→没有意义，这只是一个例子</description></item>
        /// <item><description>参数2：int→没有意义，这只是一个例子</description></item>
        /// <item><description>参数3：float→没有意义，这只是一个例子</description></item>
        /// <item><description>参数4：object→没有意义，这只是一个例子</description></item>
        /// <item><description>参数5：</description></item>
        /// </list>
        /// </remarks>
        Example,
        /// <summary>
        /// 测试用的只有一个参数的事件，实际使用时请删除它。
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>参数1：string→没有意义</description></item>
        /// </list>
        /// </remarks>
        Test1,
        /// <summary>
        /// 测试用的有两个参数的事件，实际使用时请删除它。
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>参数1：int→没有意义</description></item>
        /// <item><description>参数2：int→没有意义</description></item>
        /// </list>
        /// </remarks>
        Test2,
    }
}
