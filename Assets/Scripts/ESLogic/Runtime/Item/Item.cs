using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Item/Item")]
    public class Item : Core
    {
        [Title("Item Basic Domain")]
        [HideLabel, SerializeReference]
        public ItemBasicDomain basicDomain = new ItemBasicDomain();

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            if (basicDomain == null)
                basicDomain = new ItemBasicDomain();
            RegisterDomain(basicDomain);
        }

        protected virtual void FixedUpdate()
        {
            basicDomain?.FixedUpdateExpand();
        }
    }
}
