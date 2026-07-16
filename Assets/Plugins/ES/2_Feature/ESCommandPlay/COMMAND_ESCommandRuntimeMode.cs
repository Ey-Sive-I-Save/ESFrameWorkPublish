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
            get { return "\u5f39\u51fa\u9876\u5c42\u8fd0\u884c\u6a21\u5f0f"; }
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
        [LabelText("\u8fd0\u884c\u6807\u8bb0")]
        public ESRuntimeModeTag tag = ESRuntimeModeTag.Combat;

        public override string CommandName
        {
            get { return "\u6dfb\u52a0\u8fd0\u884c\u6807\u8bb0"; }
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
        [LabelText("\u8fd0\u884c\u6807\u8bb0")]
        public ESRuntimeModeTag tag = ESRuntimeModeTag.Combat;

        public override string CommandName
        {
            get { return "\u79fb\u9664\u8fd0\u884c\u6807\u8bb0"; }
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
            get { return "\u6e05\u7a7a\u8fd0\u884c\u6a21\u5f0f\u548c\u6807\u8bb0"; }
        }

        public override void Invoke()
        {
            ESRuntimeModeService service = ESCommandServices.RuntimeMode;
            if (service != null)
                service.Clear();
        }
    }
}
