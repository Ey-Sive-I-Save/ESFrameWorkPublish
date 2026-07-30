using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// The second fixed Enum authoring group. Its values are stable identities only and may be
    /// baked to either HotSlot or Sparse storage by ESTagBakeTable.
    /// </summary>
    public enum ESGameTagOptional : ushort
    {
        None = 0
    }

    public enum ESGameTag : ushort
    {
        [InspectorName("无")]
        None = 0,

        [InspectorName("生命类/死亡")]
        生命类_死亡 = 1,

        [InspectorName("控制类/眩晕")]
        控制类_眩晕 = 2,

        [InspectorName("控制类/沉默")]
        控制类_沉默 = 3,

        [InspectorName("控制类/定身")]
        控制类_定身 = 4,

        [InspectorName("防御类/霸体")]
        防御类_霸体 = 5,

        [InspectorName("防御类/无敌")]
        防御类_无敌 = 6,

        [InspectorName("感知类/隐身")]
        感知类_隐身 = 7,

        [InspectorName("元素类/燃烧")]
        元素类_燃烧 = 8,

        [InspectorName("元素类/冰冻")]
        元素类_冰冻 = 9,

        [InspectorName("元素类/中毒")]
        元素类_中毒 = 10,

        [InspectorName("元素类/感电")]
        元素类_感电 = 11,

        [InspectorName("战斗类/战斗中")]
        战斗类_战斗中 = 12,

        [InspectorName("战斗类/瞄准中")]
        战斗类_瞄准中 = 13,

        [InspectorName("技能类/施法中")]
        技能类_施法中 = 14,

        [InspectorName("技能类/引导中")]
        技能类_引导中 = 15,

        [InspectorName("移动类/冲刺中")]
        移动类_冲刺中 = 16,

        [InspectorName("移动类/跳跃中")]
        移动类_跳跃中 = 17,

        [InspectorName("移动类/下落中")]
        移动类_下落中 = 18,

        [InspectorName("移动类/攀爬中")]
        移动类_攀爬中 = 19,

        [InspectorName("移动类/骑乘中")]
        移动类_骑乘中 = 20,

        [InspectorName("交互类/可锁定")]
        交互类_可锁定 = 21,

        [InspectorName("交互类/可交互")]
        交互类_可交互 = 22,

        [InspectorName("交互类/可受击")]
        交互类_可受击 = 23,

        [InspectorName("交互类/可被治疗")]
        交互类_可被治疗 = 24,

        [InspectorName("阵营类/友方")]
        阵营类_友方 = 25,

        [InspectorName("阵营类/敌方")]
        阵营类_敌方 = 26,

        [InspectorName("阵营类/中立")]
        阵营类_中立 = 27,

        [InspectorName("身份类/玩家")]
        身份类_玩家 = 28,

        [InspectorName("身份类/NPC")]
        身份类_NPC = 29,

        [InspectorName("身份类/召唤物")]
        身份类_召唤物 = 30,

        [InspectorName("身份类/投射物")]
        身份类_投射物 = 31,

        [InspectorName("保留/32")]
        Reserved32 = 32,
        [InspectorName("保留/33")]
        Reserved33 = 33,
        [InspectorName("保留/34")]
        Reserved34 = 34,
        [InspectorName("保留/35")]
        Reserved35 = 35,
        [InspectorName("保留/36")]
        Reserved36 = 36,
        [InspectorName("保留/37")]
        Reserved37 = 37,
        [InspectorName("保留/38")]
        Reserved38 = 38,
        [InspectorName("保留/39")]
        Reserved39 = 39,
        [InspectorName("保留/40")]
        Reserved40 = 40,
        [InspectorName("保留/41")]
        Reserved41 = 41,
        [InspectorName("保留/42")]
        Reserved42 = 42,
        [InspectorName("保留/43")]
        Reserved43 = 43,
        [InspectorName("保留/44")]
        Reserved44 = 44,
        [InspectorName("保留/45")]
        Reserved45 = 45,
        [InspectorName("保留/46")]
        Reserved46 = 46,
        [InspectorName("保留/47")]
        Reserved47 = 47,
        [InspectorName("保留/48")]
        Reserved48 = 48,
        [InspectorName("保留/49")]
        Reserved49 = 49,
        [InspectorName("保留/50")]
        Reserved50 = 50,
        [InspectorName("保留/51")]
        Reserved51 = 51,
        [InspectorName("保留/52")]
        Reserved52 = 52,
        [InspectorName("保留/53")]
        Reserved53 = 53,
        [InspectorName("保留/54")]
        Reserved54 = 54,
        [InspectorName("保留/55")]
        Reserved55 = 55,
        [InspectorName("保留/56")]
        Reserved56 = 56,
        [InspectorName("保留/57")]
        Reserved57 = 57,
        [InspectorName("保留/58")]
        Reserved58 = 58,
        [InspectorName("保留/59")]
        Reserved59 = 59,
        [InspectorName("保留/60")]
        Reserved60 = 60,
        [InspectorName("保留/61")]
        Reserved61 = 61,
        [InspectorName("保留/62")]
        Reserved62 = 62,
        [InspectorName("保留/63")]
        Reserved63 = 63
    }

    /// <summary>
    /// 核心 Tag 的业务使用方式。该分类用于治理和编辑器校验，不能替代具体业务系统。
    /// </summary>
    public enum ESGameTagUsagePolicy : byte
    {
        /// <summary>实体可由多个来源授予/撤销的运行时事实。</summary>
        RuntimeFact = 0,

        /// <summary>由对应能力组件维护的可用性事实，不能单靠 Tag 实现该能力。</summary>
        Capability = 1,

        /// <summary>仅为旧数据兼容保留；新功能不得再写入。</summary>
        Deprecated = 2
    }

    /// <summary>
    /// ESGameTag 的稳定编号和语义边界。
    /// <para>1–31 为当前已定义核心位，32–63 为保留位。不得重排既有编号。</para>
    /// <para>阵营与实体身份属于关系/Spawn 数据，保留旧位仅用于兼容，不能用于新玩法。</para>
    /// </summary>
    public static class ESGameTagCatalog
    {
        public const ushort FirstDefinedValue = 1;
        public const ushort LastDefinedValue = 31;
        public const ushort FirstReservedValue = 32;
        public const ushort LastReservedValue = 63;

        public static bool IsDefinedCore(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            return value >= FirstDefinedValue && value <= LastDefinedValue;
        }

        public static bool IsReserved(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            return value >= FirstReservedValue && value <= LastReservedValue;
        }

        public static bool TryFromCoreId(ESTagId id, out ESGameTag tag)
        {
            ushort value = id.Value;
            if (value >= FirstDefinedValue && value <= LastDefinedValue)
            {
                tag = (ESGameTag)value;
                return true;
            }

            tag = ESGameTag.None;
            return false;
        }

        public static ESGameTagUsagePolicy GetUsagePolicy(ESGameTag tag)
        {
            ushort value = (ushort)tag;
            if (value >= 1 && value <= 20)
                return ESGameTagUsagePolicy.RuntimeFact;
            if (value >= 21 && value <= 24)
                return ESGameTagUsagePolicy.Capability;

            return ESGameTagUsagePolicy.Deprecated;
        }

        /// <summary>
        /// 新配置允许引用的核心 Tag。废弃位只能用于已有数据的迁移读取，不能再写入新配置。
        /// </summary>
        public static bool IsUsableInNewConfiguration(ESGameTag tag)
        {
            return IsDefinedCore(tag) && !IsDeprecated(tag);
        }

        /// <summary>实体的常规写入入口允许保存运行时事实和能力入口，但拒绝废弃的身份/阵营旧位。</summary>
        public static bool CanBeWrittenToEntity(ESGameTag tag)
        {
            return IsUsableInNewConfiguration(tag);
        }

        /// <summary>
        /// Buff 只能授予运行时事实。能力入口必须由对应的 Ability/Interaction/Receiver 组件维护，
        /// 否则一个临时 Buff 会伪造对象实际上并不具备的能力。
        /// </summary>
        public static bool CanBeGrantedByBuff(ESGameTag tag)
        {
            return GetUsagePolicy(tag) == ESGameTagUsagePolicy.RuntimeFact;
        }

        public static bool IsDeprecated(ESGameTag tag)
        {
            return GetUsagePolicy(tag) == ESGameTagUsagePolicy.Deprecated;
        }
    }

    /// <summary>不依赖场景的核心 Tag 回归自检，供编辑器菜单和 CI 入口调用。</summary>
    public static class STATIC_ESGameTagSelfTest
    {
        public static int RunOrThrow()
        {
            int checks = 0;
            Expect(ESGameTagCatalog.IsDefinedCore(ESGameTag.生命类_死亡), "死亡必须是已定义核心 Tag。", ref checks);
            Expect(!ESGameTagCatalog.IsDefinedCore(ESGameTag.Reserved32), "保留位不能视为已定义核心 Tag。", ref checks);
            Expect(ESGameTagCatalog.IsDeprecated(ESGameTag.阵营类_友方), "阵营旧位必须标记为废弃。", ref checks);
            Expect(!ESGameTagCatalog.CanBeWrittenToEntity(ESGameTag.阵营类_友方), "废弃阵营位不能再写入实体。", ref checks);
            Expect(!ESGameTagCatalog.CanBeGrantedByBuff(ESGameTag.交互类_可交互), "Buff 不能伪造能力入口 Tag。", ref checks);

            ESTagRefCountSet64 active = default;
            active.Warmup();
            Expect(active.Add(ESGameTag.元素类_燃烧), "首次添加燃烧应成功。", ref checks);
            Expect(active.Add(ESGameTag.元素类_燃烧), "第二个燃烧来源应成功。", ref checks);
            Expect(active.Has(ESGameTag.元素类_燃烧) && active.GetCount(ESGameTag.元素类_燃烧) == 2, "燃烧应保留两个来源计数。", ref checks);
            Expect(active.Remove(ESGameTag.元素类_燃烧), "移除一个燃烧来源应成功。", ref checks);
            Expect(active.Has(ESGameTag.元素类_燃烧) && active.GetCount(ESGameTag.元素类_燃烧) == 1, "剩余来源存在时不能清掉燃烧位。", ref checks);
            Expect(active.Remove(ESGameTag.元素类_燃烧) && !active.Has(ESGameTag.元素类_燃烧), "最后一个来源移除后应清掉燃烧位。", ref checks);

            ESTagMask64 burningOrFrozen = ESTagMask64.From(ESGameTag.元素类_燃烧, ESGameTag.元素类_冰冻);
            ESTagMask64 dead = ESTagMask64.From(ESGameTag.生命类_死亡);
            ESGameTagRequirement requirement = new ESGameTagRequirement
            {
                requiredAny = burningOrFrozen,
                blockedAny = dead
            };
            ESTagMask64 frozen = ESTagMask64.From(ESGameTag.元素类_冰冻);
            Expect(requirement.Matches(frozen), "冰冻应满足燃烧或冰冻条件。", ref checks);
            frozen.Add(ESGameTag.生命类_死亡);
            Expect(!requirement.Matches(frozen), "死亡时应被 BlockedAny 拒绝。", ref checks);

            ESGameTagRequirementConfig configuredRequirement = new ESGameTagRequirementConfig();
            configuredRequirement.requiredAll.Add(ESGameTag.战斗类_战斗中);
            configuredRequirement.blockedAny.Add(ESGameTag.生命类_死亡);
            Expect(configuredRequirement.TryCompile(out ESGameTagRequirement compiledRequirement, out _), "合法的配置型 Tag 条件必须可以编译。", ref checks);
            ESTagMask64 combat = ESTagMask64.From(ESGameTag.战斗类_战斗中);
            Expect(compiledRequirement.Matches(combat), "配置型条件编译后必须保持匹配语义。", ref checks);
            configuredRequirement.blockedAny.Add(ESGameTag.战斗类_战斗中);
            Expect(!configuredRequirement.TryCompile(out _, out _), "相同 Tag 不能同时被要求存在和禁止存在。", ref checks);

            return checks;
        }

        private static void Expect(bool value, string message, ref int checks)
        {
            checks++;
            if (!value)
                throw new InvalidOperationException("[GameTagSelfTest] " + message);
        }
    }
}
