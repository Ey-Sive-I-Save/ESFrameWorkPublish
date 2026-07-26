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
        public static ESConfigKeyTable<ESMonsterRuntimeData> Table => ESRuntimeDataGameCore.Monsters;

        public static void Inject(MonsterDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.motionShared ??= EntityMotionSharedData.Default;
                info.monsterKey ??= new ESMonsterConfigKey();
                if (Table.TryGet(info.monsterKey, out ESMonsterRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("Monster GameCore Key 重复：" + info.KeyName);
                }

                var data = new ESMonsterRuntimeData
                {
                    keyName = info.KeyName,
                    displayName = string.IsNullOrWhiteSpace(info.displayName) ? info.KeyName : info.displayName,
                    sourcePackage = info.name,
                    soSource = info,
                    sharedData = info.motionShared,
                    defaultVariableData = info.motionVariable
                };
                data.runtimeKey = Table.Bake(info.monsterKey, info.KeyName);
                if (!Table.Upsert(info.monsterKey, data, info.KeyName))
                    throw new InvalidOperationException("Monster GameCore 注入失败：" + info.KeyName);
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
        public static ESConfigKeyTable<ESNpcRuntimeData> Table => ESRuntimeDataGameCore.Npcs;

        public static void Inject(NpcDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                info.motionShared ??= EntityMotionSharedData.Default;
                info.npcKey ??= new ESNpcConfigKey();
                if (Table.TryGet(info.npcKey, out ESNpcRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("NPC GameCore Key 重复：" + info.KeyName);
                }

                var data = new ESNpcRuntimeData
                {
                    keyName = info.KeyName,
                    displayName = string.IsNullOrWhiteSpace(info.displayName) ? info.KeyName : info.displayName,
                    sourcePackage = info.name,
                    soSource = info,
                    sharedData = info.motionShared,
                    defaultVariableData = info.motionVariable
                };
                data.runtimeKey = Table.Bake(info.npcKey, info.KeyName);
                if (!Table.Upsert(info.npcKey, data, info.KeyName))
                    throw new InvalidOperationException("NPC GameCore 注入失败：" + info.KeyName);
            }
            finally
            {
                if (ownsBuild) Table.EndBuild();
            }
        }
    }
}
