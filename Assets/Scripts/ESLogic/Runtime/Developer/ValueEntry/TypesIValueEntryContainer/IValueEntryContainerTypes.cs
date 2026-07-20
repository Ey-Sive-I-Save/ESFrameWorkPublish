namespace ES
{
    /*
     * IValueEntryContainer 用于解决 UnityObject / ScriptableObject 无法直接按接口多态序列化的问题。
     *
     * 常见写法：
     *
     * [System.Serializable]
     * public sealed class ItemValueEntryContainer : IValueEntryContainer
     * {
     *     public ItemDataInfo itemInfo;
     *     public override IValueEntry GetValueEntry => itemInfo;
     * }
     *
     * ItemDataInfo 只需要实现 IStringValueEntry、ISpriteValueEntry 等显示读取接口即可。
     */
}
