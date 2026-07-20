using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [LabelText("系统域")]
    public class ESSystemDomain : Domain<ESGameManager, ESSystemModule>
    {
    }

    [Serializable]
    [LabelText("流程域")]
    public class ESFlowDomain : Domain<ESGameManager, ESFlowModule>
    {
    }

    [Serializable]
    [LabelText("世界域")]
    public class ESWorldDomain : Domain<ESGameManager, ESWorldModule>
    {
    }
}
