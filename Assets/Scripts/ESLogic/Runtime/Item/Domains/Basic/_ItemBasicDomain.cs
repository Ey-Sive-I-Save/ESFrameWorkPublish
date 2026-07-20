using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("Item Basic Domain")]
    public class ItemBasicDomain : Domain<Item, ItemBasicModuleBase>
    {
        public override void _AwakeRegisterAllModules()
        {
            EnsureCoreModules();
            base._AwakeRegisterAllModules();
        }

        public override void UpdateAsHosting()
        {
            MyModules.ApplyBuffers();
            int count = MyModules.ValuesNow.Count;

            for (int i = 0; i < count; i++)
            {
                ItemBasicModuleBase module = MyModules.ValuesNow[i];
                if (module is ItemMotionModule)
                    continue;

                UpdateModule(module);
            }

            for (int i = 0; i < count; i++)
            {
                ItemBasicModuleBase module = MyModules.ValuesNow[i];
                if (module is ItemMotionModule)
                    UpdateModule(module);
            }
        }

        public override void FixedUpdateExpand()
        {
            MyModules.ApplyBuffers();
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                ItemBasicModuleBase module = MyModules.ValuesNow[i];
                if (module != null)
                    module.FixedUpdateExpand();
            }
        }

        private void UpdateModule(ItemBasicModuleBase module)
        {
            if (module == null)
                return;

            if (module.Signal_Dirty && TestModuleStateBefoUpdate(module) == ESTryResult.Fail)
                return;

            module.TryUpdateSelf();
        }

        private void EnsureCoreModules()
        {
            if (MyModules == null)
                MyModules = new SafeNormalList<ItemBasicModuleBase>();
            if (MyModules.ValuesNow == null)
                MyModules.ValuesNow = new System.Collections.Generic.List<ItemBasicModuleBase>();

            bool hasMotion = false;
            for (int i = 0; i < MyModules.ValuesNow.Count; i++)
            {
                if (MyModules.ValuesNow[i] is ItemMotionModule)
                {
                    hasMotion = true;
                    break;
                }
            }

            if (!hasMotion)
            {
                MyModules.Add(new ItemMotionModule());
                MyModules.ApplyBuffers(true);
            }
        }
    }

    [Serializable, TypeRegistryItem("Item Basic Module Base")]
    public abstract class ItemBasicModuleBase : Module<Item, ItemBasicDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }
}
