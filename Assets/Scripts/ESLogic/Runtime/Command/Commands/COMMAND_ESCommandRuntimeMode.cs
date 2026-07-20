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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service != null)
                service.PushMode(mode);
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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service == null)
                return;

            for (int i = service.ModeCount - 1; i >= 0; i--)
            {
                ESRuntimeModeEntry entry = service.GetModeEntryAt(i);
                if (entry.mode == mode)
                {
                    service.RemoveMode(entry.handle);
                    return;
                }
            }
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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service != null)
                service.PopTopMode();
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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service != null)
                service.AddTag(tag);
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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service == null)
                return;

            for (int i = service.TagCount - 1; i >= 0; i--)
            {
                ESRuntimeModeTagEntry entry = service.GetTagEntryAt(i);
                if (entry.tag == tag)
                {
                    service.RemoveTag(entry.handle);
                    return;
                }
            }
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
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service != null)
                service.Clear();
        }
    }
}
