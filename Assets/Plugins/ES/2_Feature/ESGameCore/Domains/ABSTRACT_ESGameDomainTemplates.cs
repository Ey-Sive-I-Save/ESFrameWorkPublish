using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u57df\u6a21\u677f")]
    public abstract class ESGameDomain<Module_> : Domain<ESGameManager, Module_>
        where Module_ : class, IModule, IESModule
    {
        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public ESRuntimeDomain Runtime
        {
            get { return ESGameManager.RuntimeDomain; }
        }

        public ESWorldDomain World
        {
            get { return ESGameManager.WorldDomain; }
        }

        public ESPlayerDomain Player
        {
            get { return ESGameManager.PlayerDomain; }
        }

        public ESPresentationDomain Presentation
        {
            get { return ESGameManager.PresentationDomain; }
        }
    }

    [Serializable]
    [TypeRegistryItem("\u6e38\u620f\u6838\u5fc3/\u6a21\u5757\u6a21\u677f")]
    public abstract class ESGameModule<Domain_> : Module<ESGameManager, Domain_>
        where Domain_ : class, IDomain<ESGameManager>
    {
        public override Type TableKeyType
        {
            get { return GetType(); }
        }

        public ESGameManager Game
        {
            get { return MyCore != null ? MyCore : ESGameManager.Instance; }
        }

        public ESRuntimeDomain Runtime
        {
            get { return ESGameManager.RuntimeDomain; }
        }

        public ESWorldDomain World
        {
            get { return ESGameManager.WorldDomain; }
        }

        public ESPlayerDomain Player
        {
            get { return ESGameManager.PlayerDomain; }
        }

        public ESPresentationDomain Presentation
        {
            get { return ESGameManager.PresentationDomain; }
        }

        public T GetModule<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }
    }
}
