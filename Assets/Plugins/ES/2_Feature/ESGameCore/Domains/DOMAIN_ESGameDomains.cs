using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [LabelText("\u8fd0\u884c\u57df")]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u57df/\u8fd0\u884c\u57df")]
    public class ESRuntimeDomain : ESGameDomain<ESRuntimeModule>
    {
    }

    [Serializable]
    [LabelText("\u4e16\u754c\u57df")]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u57df/\u4e16\u754c\u57df")]
    public class ESWorldDomain : ESGameDomain<ESWorldModule>
    {
    }

    [Serializable]
    [LabelText("\u73a9\u5bb6\u57df")]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u57df/\u73a9\u5bb6\u57df")]
    public class ESPlayerDomain : ESGameDomain<ESPlayerModule>
    {
    }

    [Serializable]
    [LabelText("\u8868\u73b0\u57df")]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u57df/\u8868\u73b0\u57df")]
    public class ESPresentationDomain : ESGameDomain<ESPresentationModule>
    {
    }
}
