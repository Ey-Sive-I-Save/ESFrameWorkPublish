using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("模块基类/系统模块")]
    public abstract class ESSystemModule : Module<ESGameManager, ESSystemDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }

    [Serializable]
    [TypeRegistryItem("模块基类/流程模块")]
    public abstract class ESFlowModule : Module<ESGameManager, ESFlowDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }

    [Serializable]
    [TypeRegistryItem("模块基类/世界模块")]
    public abstract class ESWorldModule : Module<ESGameManager, ESWorldDomain>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }
}
