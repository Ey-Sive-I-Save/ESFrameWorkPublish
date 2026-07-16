using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u8fd0\u884c\u57df/\u6a21\u5757\u57fa\u7c7b")]
    public abstract class ESRuntimeModule : ESGameModule<ESRuntimeDomain>
    {
    }

    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u4e16\u754c\u57df/\u6a21\u5757\u57fa\u7c7b")]
    public abstract class ESWorldModule : ESGameModule<ESWorldDomain>
    {
    }

    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u73a9\u5bb6\u57df/\u6a21\u5757\u57fa\u7c7b")]
    public abstract class ESPlayerModule : ESGameModule<ESPlayerDomain>
    {
    }

    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u8868\u73b0\u57df/\u6a21\u5757\u57fa\u7c7b")]
    public abstract class ESPresentationModule : ESGameModule<ESPresentationDomain>
    {
    }
}
