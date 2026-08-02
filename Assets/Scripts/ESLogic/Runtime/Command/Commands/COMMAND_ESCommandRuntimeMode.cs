using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModePush)]
    public sealed class ESCommand_RuntimeMode_PushMode : ESCommand
    {
        [LabelText("运行模式")]
        public ESRuntimeMode mode = ESRuntimeMode.Gameplay;

        public override string CommandName
        {
            get { return "压入运行模式"; }
        }

        public override void Invoke()
        {
            RejectLegacyCommand(CommandName);
        }

        internal static void RejectLegacyCommand(string commandName)
        {
            UnityEngine.Debug.LogWarning("RuntimeMode ESCommand 已冻结，拒绝执行无实例所有权的命令：" + commandName);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModeRemove)]
    public sealed class ESCommand_RuntimeMode_RemoveMode : ESCommand
    {
        [LabelText("运行模式")]
        public ESRuntimeMode mode = ESRuntimeMode.Gameplay;

        public override string CommandName
        {
            get { return "移除运行模式"; }
        }

        public override void Invoke()
        {
            ESCommand_RuntimeMode_PushMode.RejectLegacyCommand(CommandName);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModePopTop)]
    public sealed class ESCommand_RuntimeMode_PopTopMode : ESCommand
    {
        public override string CommandName
        {
            get { return "弹出顶层运行模式"; }
        }

        public override void Invoke()
        {
            ESCommand_RuntimeMode_PushMode.RejectLegacyCommand(CommandName);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModeAddTag)]
    public sealed class ESCommand_RuntimeMode_AddTag : ESCommand
    {
        [LabelText("运行标记")]
        public ESRuntimeModeTag tag = ESRuntimeModeTag.Combat;

        public override string CommandName
        {
            get { return "添加运行标记"; }
        }

        public override void Invoke()
        {
            ESCommand_RuntimeMode_PushMode.RejectLegacyCommand(CommandName);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModeRemoveTag)]
    public sealed class ESCommand_RuntimeMode_RemoveTag : ESCommand
    {
        [LabelText("运行标记")]
        public ESRuntimeModeTag tag = ESRuntimeModeTag.Combat;

        public override string CommandName
        {
            get { return "移除运行标记"; }
        }

        public override void Invoke()
        {
            ESCommand_RuntimeMode_PushMode.RejectLegacyCommand(CommandName);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.RuntimeModeClear)]
    public sealed class ESCommand_RuntimeMode_Clear : ESCommand
    {
        public override string CommandName
        {
            get { return "清空运行模式和标记"; }
        }

        public override void Invoke()
        {
            ESCommand_RuntimeMode_PushMode.RejectLegacyCommand(CommandName);
        }
    }
}
