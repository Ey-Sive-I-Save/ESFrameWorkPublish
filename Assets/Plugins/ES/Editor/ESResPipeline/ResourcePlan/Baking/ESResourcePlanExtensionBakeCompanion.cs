using System.Collections.Generic;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>Editor-only authoring companion. Never referenced by the Player ResourcePlan.</summary>
    public sealed class ESResourcePlanExtensionBakeCompanion : ScriptableObject
    {
        public ESResourcePlanInfo plan;
        public List<ESResourcePlanExtensionSourceEntry> sources = new List<ESResourcePlanExtensionSourceEntry>(2);
    }
}
