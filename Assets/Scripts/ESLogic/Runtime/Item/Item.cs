using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Item/Item")]
    public class Item : Core
    {
        [System.NonSerialized, ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("Item长期OpSupport")]
        public ESOpSupport opSupport;

        public ESOpSupport OpSupport
        {
            get
            {
                EnsureItemOpSupport();
                return opSupport;
            }
        }

        [Title("Item Basic Domain")]
        [HideLabel, SerializeReference]
        public ItemBasicDomain basicDomain = new ItemBasicDomain();

        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            EnsureItemOpSupport();
            if (basicDomain == null)
                basicDomain = new ItemBasicDomain();
            RegisterDomain(basicDomain);
        }

        protected virtual void FixedUpdate()
        {
            basicDomain?.FixedUpdateExpand();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            opSupport?.Dispose();
            opSupport = null;
        }

        public void EnsureItemOpSupport()
        {
            if (opSupport == null || opSupport.IsRecycled)
                opSupport = ESOpSupport.CreateStandalone();

            if (opSupport.Kind != ESOpSupportKind.Item || opSupport.OwnerItem != this)
                opSupport.InitializeItemOwner(this, GetInstanceID());
        }
    }
}
