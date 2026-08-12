using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESVfxCategory : byte
    {
        Combat,
        Character,
        Environment,
        UI,
        Cinematic,
        Interaction
    }

    public enum ESVfxTimeMode : byte
    {
        ScaledGameTime,
        UnscaledTime
    }

    public enum ESVfxPreemptionPolicy : byte
    {
        RejectNew,
        StopOldest,
        StopLowestPriority
    }

    [Serializable]
    public sealed class ESVfxVariant
    {
        [LabelText("Prefab")]
        public ESAssetReferPrefabConfigKey prefabKey = new ESAssetReferPrefabConfigKey();

        [LabelText("权重"), MinValue(0.01f)]
        public float weight = 1f;
    }

    [ESCreatePath("数据信息/GameCore", "VFX 定义")]
    public sealed class ESVfxInfo : SoDataInfo, IGameCoreSO
    {
        [TitleGroup("基础")]
        [LabelText("VFX Key"), InlineProperty]
        public ESVfxKey key = new ESVfxKey();

        [TitleGroup("基础")]
        public ESVfxCategory category = ESVfxCategory.Combat;

        [TitleGroup("播放规则")]
        public bool loop;

        [TitleGroup("播放规则"), Range(0, 256)]
        public int priority = 128;

        [TitleGroup("播放规则"), MinValue(0)]
        public int maxConcurrent;

        [TitleGroup("播放规则")]
        public ESVfxPreemptionPolicy preemptionPolicy = ESVfxPreemptionPolicy.RejectNew;

        [TitleGroup("生命周期"), MinValue(0f)]
        [InfoBox("0 表示由粒子自然结束判定；循环效果必须配置最大生命周期。")]
        public float maxLifetime;

        [TitleGroup("生命周期")]
        public ESVfxTimeMode timeMode = ESVfxTimeMode.ScaledGameTime;

        [TitleGroup("变体")]
        public ESVfxVariant[] variants = Array.Empty<ESVfxVariant>();

        public void InjectGameCoreTables() => ESVfxGameCoreTable.Inject(this);

        public bool TrySelectVariant(out ESVfxVariant selected)
        {
            selected = null;
            if (variants == null || variants.Length == 0)
                return false;

            float total = 0f;
            for (int i = 0; i < variants.Length; i++)
            {
                ESVfxVariant variant = variants[i];
                if (variant == null || variant.prefabKey == null || !variant.prefabKey.IsConfigured || variant.weight <= 0f)
                    continue;
                total += variant.weight;
            }
            if (total <= 0f)
                return false;

            float cursor = UnityEngine.Random.value * total;
            for (int i = 0; i < variants.Length; i++)
            {
                ESVfxVariant variant = variants[i];
                if (variant == null || variant.prefabKey == null || !variant.prefabKey.IsConfigured || variant.weight <= 0f)
                    continue;
                cursor -= variant.weight;
                if (cursor <= 0f)
                {
                    selected = variant;
                    return true;
                }
            }
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (key == null || !key.IsConfigured)
            {
                error = "VFX Key 未配置。";
                return false;
            }
            if (priority < 0 || priority > 256 || maxConcurrent < 0 || maxLifetime < 0f)
            {
                error = "VFX 播放规则包含无效数值。";
                return false;
            }
            if (variants == null || variants.Length == 0)
            {
                error = "VFX 至少需要一个有效 Prefab 变体。";
                return false;
            }
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] == null || variants[i].prefabKey == null || !variants[i].prefabKey.IsConfigured || variants[i].weight <= 0f)
                {
                    error = "VFX 存在无效 Prefab 变体。";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }

    public static class ESVfxGameCoreTable
    {
        public static ESVfxConfigKeyTable Table => ESRuntimeDataGameCore.Vfx;

        public static void Inject(ESVfxInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (!info.TryValidate(out string error))
                throw new InvalidOperationException("VFX 配置无效：" + info.name + "，" + error);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                if (Table.TryGet(info.key, out ESVfxRuntimeData existing))
                {
                    if (ReferenceEquals(existing.source, info)) return;
                    throw new InvalidOperationException("VFX GameCore Key 重复：" + info.name);
                }

                ESVfxRuntimeData data = Table.AcquireRetained(info.key);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(info.key.EnumKeyInt, info.key.StringKey);
                    data.displayName = info.name;
                    data.source = info;
                    if (Table.CommitRetained(info.key, data, info.name) == 0)
                        throw new InvalidOperationException("VFX GameCore 注入失败：" + info.name);
                }
                catch
                {
                    Table.AbandonRetained(data);
                    throw;
                }
            }
            finally
            {
                if (ownsBuild) Table.EndBuild();
            }
        }
    }
}
