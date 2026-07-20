using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    #region ValueEntry 设计约束
    /*
     * ValueEntry 是“显示值 Get / Set 协议”，不是 GetName / GetIcon 快捷工具。
     *
     * 保留 ref back：
     * - Try 返回值只表达“是否支持该 Key / 是否处理成功”。
     * - back 表达真实输出值，允许输出 null、0、false 等合法显示值。
     * - 避免返回值和失败状态混在一起。
     *
     * 保留明确 Key 类型：
     * - ValueEntryStringKey / ValueEntrySpriteKey 等枚举是协议的一部分。
     * - 不把 Name、Description、Icon 固化成散落的碎 API。
     * - 不同数据源以同一套 Key 格式对 UI 提供显示值。
     * - 新增 Key 必须使用 [InspectorName("【分组】名称")] 格式，保证编辑器中文友好。
     * - 通用 Key 只放所有显示对象都可能用到的语义；伤害、暴击、Boss、任务等业务 Key 应拆到业务枚举。
     * - TKey 是扩展口，不是替代口。业务可以定义 ItemStringKey / SkillSpriteKey，但不能因此废弃通用 Key。
     * - 能用通用 Key 表达的显示值，应优先走 ValueEntryStringKey / ValueEntrySpriteKey 等默认协议。
     * - 业务 Key 只承载通用 Key 表达不了的专属语义，例如物品来源、技能消耗、任务目标说明。
     *
     * 保留 object help：
     * - 默认可以传 null。
     * - 给复杂 UI、上下文显示、临时格式化参数留扩展入口。
     * - 热路径不要传值类型，避免装箱；展示刷新路径可以接受。
     *
     * 保留 lan：
     * - 多语言是显示值协议的天然参数。
     * - NotClear 由实现方或 EnvirClear 解析为当前默认语言。
     *
     * Get 是主协议，Set 是可选协议：
     * - 普通数据只实现 IValueEntry<TValue, TKey> / IValueEntryGetter<TValue, TKey>。
     * - UI 编辑面板、调试面板、数据面板需要写回时，额外实现 IValueEntrySetter<TValue, TKey>。
     *
     * 后续可以添加 GetName / GetIcon 等薄扩展，但不能替代本协议。
     */
    #endregion

    /// <summary>
    /// 显示值协议总入口。
    /// 物品、技能、Buff、任务、UI 面板等对象可通过统一格式向界面提供名称、描述、图标等显示数据。
    /// </summary>
    public interface IValueEntry
    {
        public bool TryGetValueEntry(
            ref string back,
            ValueEntryStringKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            return this is IStringValueEntry entry && entry.TryGetValueEntry(ref back, key, help, lan);
        }

        public bool TryGetValueEntry(
            ref int back,
            ValueEntryIntKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            return this is IIntValueEntry entry && entry.TryGetValueEntry(ref back, key, help, lan);
        }

        public bool TryGetValueEntry(
            ref float back,
            ValueEntryFloatKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            return this is IFloatValueEntry entry && entry.TryGetValueEntry(ref back, key, help, lan);
        }

        public bool TryGetValueEntry(
            ref bool back,
            ValueEntryBoolKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            return this is IBoolValueEntry entry && entry.TryGetValueEntry(ref back, key, help, lan);
        }

        public bool TryGetValueEntry(
            ref Sprite back,
            ValueEntrySpriteKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            return this is ISpriteValueEntry entry && entry.TryGetValueEntry(ref back, key, help, lan);
        }
    }

    [Serializable, TypeRegistryItem("显示值提供者")]
    public abstract class IValueEntryContainer
    {
        public abstract IValueEntry GetValueEntry { get; }
    }

    public interface IValueEntryGetter<TValue, TKey> : IValueEntry
    {
        public bool TryGetValueEntry(
            ref TValue back,
            TKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear);
    }

    /// <summary>
    /// 显示值协议总入口。
    /// 物品、技能、Buff、任务、UI 面板等对象可通过统一格式向界面提供名称、描述、图标等显示数据。
    /// </summary>
    public interface IValueEntry<TValue, TKey> : IValueEntryGetter<TValue, TKey>
    {
    }

    /// <summary>
    /// 显示值协议总入口。
    /// 物品、技能、Buff、任务、UI 面板等对象可通过统一格式向界面提供名称、描述、图标等显示数据。
    /// </summary>
    public interface IValueEntrySetter<TValue, TKey> : IValueEntry
    {
        public bool TrySetValueEntry(
            TValue value,
            TKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear);
    }

    public enum ValueEntryStringKey
    {
        [InspectorName("【通用】默认文本")] DefaultValue,

        [InspectorName("【身份】名称")] Name,
        [InspectorName("【身份】短名称")] ShortName,
        [InspectorName("【身份】标题")] Title,
        [InspectorName("【身份】副标题")] Subtitle,

        [InspectorName("【归类】分类")] Category,
        [InspectorName("【归类】标签")] Tag,
        [InspectorName("【归类】分组")] Group,

        [InspectorName("【正文】摘要")] Summary,
        [InspectorName("【正文】描述")] Description,
        [InspectorName("【正文】内容")] Content,
        [InspectorName("【正文】提示文本")] Tooltip,

        [InspectorName("【反馈】状态文本")] StatusText,
        [InspectorName("【反馈】原因文本")] ReasonText,
        [InspectorName("【反馈】警告文本")] WarningText
    }

    public enum ValueEntryFloatKey
    {
        [InspectorName("【通用】默认数值")] DefaultValue,

        [InspectorName("【数值】当前值")] Value,
        [InspectorName("【数值】基础值")] BaseValue,
        [InspectorName("【数值】目标值")] TargetValue,

        [InspectorName("【数值】最小值")] MinValue,
        [InspectorName("【数值】最大值")] MaxValue,
        [InspectorName("【数值】增量")] Delta,

        [InspectorName("【比例】比例")] Ratio,
        [InspectorName("【比例】百分比")] Percent,
        [InspectorName("【比例】进度")] Progress,
        [InspectorName("【比例】概率")] Rate,

        [InspectorName("【排序】权重")] Weight
    }

    public enum ValueEntryIntKey
    {
        [InspectorName("【通用】默认整数")] DefaultValue,

        [InspectorName("【数量】数量")] Count,
        [InspectorName("【数量】容量")] Capacity,
        [InspectorName("【数量】剩余数量")] RemainingCount,
        [InspectorName("【数量】最大数量")] MaxCount,

        [InspectorName("【数量】索引")] Index,
        [InspectorName("【排序】顺序")] Order,
        [InspectorName("【排序】层级")] Layer,

        [InspectorName("【成长】等级")] Level,
        [InspectorName("【成长】阶段")] Phase,
        [InspectorName("【成长】阶级")] Rank,

        [InspectorName("【品质】品质")] Quality,
        [InspectorName("【品质】稀有度")] Rarity,
        [InspectorName("【品质】星级")] Star
    }

    public enum ValueEntryBoolKey
    {
        [InspectorName("【通用】默认开关")] DefaultValue,

        [InspectorName("【状态】启用")] IsEnabled,
        [InspectorName("【状态】激活")] IsActive,
        [InspectorName("【状态】可用")] IsAvailable,
        [InspectorName("【状态】加载完成")] IsLoaded,
        [InspectorName("【状态】完成")] IsCompleted,
        [InspectorName("【状态】锁定")] IsLocked,

        [InspectorName("【交互】选中")] IsSelected,
        [InspectorName("【交互】可交互")] IsInteractable,
        [InspectorName("【交互】可点击")] IsClickable,

        [InspectorName("【显示】可见")] IsVisible,
        [InspectorName("【显示】高亮")] IsHighlighted,
        [InspectorName("【显示】展开")] IsExpanded
    }

    public enum ValueEntrySpriteKey
    {
        [InspectorName("【通用】默认图")] DefaultValue,
        [InspectorName("【图标】标准图标")] Icon,
        [InspectorName("【图标】小图标")] SmallIcon,
        [InspectorName("【图标】大图标")] LargeIcon,
        [InspectorName("【角色】头像")] Portrait,
        [InspectorName("【角色】半身像")] Bust,
        [InspectorName("【界面】背景图")] Background,
        [InspectorName("【界面】横幅图")] Banner,
        [InspectorName("【状态】高亮图")] Highlighted,
        [InspectorName("【状态】禁用图")] Disabled,
        [InspectorName("【状态】选中图")] Selected
    }

    public interface IIntValueEntry : IValueEntry<int, ValueEntryIntKey>
    {
    }

    public interface IFloatValueEntry : IValueEntry<float, ValueEntryFloatKey>
    {
    }

    public interface IBoolValueEntry : IValueEntry<bool, ValueEntryBoolKey>
    {
    }
}
