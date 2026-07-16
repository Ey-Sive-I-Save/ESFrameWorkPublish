using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("模块基类/运行模块")]
    public abstract class ESRuntimeModule : ESFlowModule
    {
    }

    [Serializable]
    [TypeRegistryItem("模块基类/玩家模块")]
    public abstract class ESPlayerModule : ESWorldModule
    {
    }

    [Serializable]
    [TypeRegistryItem("模块基类/表现模块")]
    public abstract class ESPresentationModule : ESFlowModule
    {
    }
}
