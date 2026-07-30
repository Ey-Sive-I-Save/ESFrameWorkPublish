using System;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>Monster 独立领域根 SO；直接注入 Monster Table，不依赖中央类别分发。</summary>
    [ESCreatePath("数据信息/GameCore", "Monster 数据")]
    public sealed class MonsterDataInfo : SoDataInfo, IGameCoreSO
    {
        [LabelText("显示名称")]
        public string displayName;

        [MultiLineProperty(3)]
        public string description;

        [HideLabel, InlineProperty]
        public ESMonsterConfigKey monsterKey = new ESMonsterConfigKey();

        [HideLabel]
        public EntityMotionSharedData motionShared = EntityMotionSharedData.Default;

        [HideLabel]
        public EntityMotionVariableData motionVariable = EntityMotionVariableData.Default;

        public void InjectGameCoreTables()
        {
            ESMonsterGameCoreTable.Inject(this);
        }
    }

    /// <summary>NPC 独立领域根 SO；直接注入 NPC Table，不依赖中央类别分发。</summary>
    [ESCreatePath("数据信息/GameCore", "NPC 数据")]
    public sealed class NpcDataInfo : SoDataInfo, IGameCoreSO
    {
        [LabelText("显示名称")]
        public string displayName;

        [MultiLineProperty(3)]
        public string description;

        [HideLabel, InlineProperty]
        public ESNpcConfigKey npcKey = new ESNpcConfigKey();

        [HideLabel]
        public EntityMotionSharedData motionShared = EntityMotionSharedData.Default;

        [HideLabel]
        public EntityMotionVariableData motionVariable = EntityMotionVariableData.Default;

        public void InjectGameCoreTables()
        {
            ESNpcGameCoreTable.Inject(this);
        }
    }

    /// <summary>Monster 领域自己的强类型表入口。启动期写入，运行期直接强类型查表。</summary>
    public static class ESMonsterGameCoreTable
    {
        public static ESMonsterConfigKeyTable Table => ESRuntimeDataGameCore.Monsters;

        public static void Inject(MonsterDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.motionShared ??= EntityMotionSharedData.Default;
                if (info.monsterKey == null || !info.monsterKey.IsConfigured)
                    throw new InvalidOperationException("Monster 必须显式配置 EnumKey 或 StringKey；KeyName 仅供编辑器与策划使用：" + info.name);
                if (Table.TryGet(info.monsterKey, out ESMonsterRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("Monster GameCore Key 重复：" + info.name);
                }

                ESMonsterRuntimeData data = Table.AcquireRetained(info.monsterKey);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(info.monsterKey.EnumKeyInt, info.monsterKey.StringKey);
                    data.displayName = string.IsNullOrWhiteSpace(info.displayName) ? info.name : info.displayName;
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = info.motionShared;
                    data.defaultVariableData = info.motionVariable;
                    int runtimeKey = Table.CommitRetained(info.monsterKey, data, debugName: info.name);
                    if (runtimeKey == 0)
                        throw new InvalidOperationException("Monster GameCore 注入失败：" + info.name);
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

    /// <summary>NPC 领域自己的强类型表入口。启动期写入，运行期直接强类型查表。</summary>
    public static class ESNpcGameCoreTable
    {
        public static ESNpcConfigKeyTable Table => ESRuntimeDataGameCore.Npcs;

        public static void Inject(NpcDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.motionShared ??= EntityMotionSharedData.Default;
                if (info.npcKey == null || !info.npcKey.IsConfigured)
                    throw new InvalidOperationException("NPC 必须显式配置 EnumKey 或 StringKey；KeyName 仅供编辑器与策划使用：" + info.name);
                if (Table.TryGet(info.npcKey, out ESNpcRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("NPC GameCore Key 重复：" + info.name);
                }

                ESNpcRuntimeData data = Table.AcquireRetained(info.npcKey);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(info.npcKey.EnumKeyInt, info.npcKey.StringKey);
                    data.displayName = string.IsNullOrWhiteSpace(info.displayName) ? info.name : info.displayName;
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = info.motionShared;
                    data.defaultVariableData = info.motionVariable;
                    int runtimeKey = Table.CommitRetained(info.npcKey, data, debugName: info.name);
                    if (runtimeKey == 0)
                        throw new InvalidOperationException("NPC GameCore 注入失败：" + info.name);
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
