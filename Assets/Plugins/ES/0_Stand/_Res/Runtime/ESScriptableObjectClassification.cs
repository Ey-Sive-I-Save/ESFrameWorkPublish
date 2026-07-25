using UnityEngine;

namespace ES
{
    public interface ISimpleSO { }
    /// <summary>启动期核心数据。实现类负责把自身数据注入对应 GameCoreTable。</summary>
    public interface IGameCoreSO
    {
        void InjectGameCoreTables();
    }
    /// <summary>同一根 SO 类型仅部分业务分类进入 GameCore 启动包时使用。</summary>
    public interface IConditionalGameCoreSO
    {
        bool IsGameCoreRoot { get; }
    }
    public interface IInternalSO { }

    public enum ESScriptableObjectClass : byte { Simple, GameCore, Internal }

    public static class ESScriptableObjectClassification
    {
        public static ESScriptableObjectClass GetClass(ScriptableObject asset)
        {
            if (asset is IInternalSO) return ESScriptableObjectClass.Internal;
            if (asset is IGameCoreSO)
            {
                if (asset is IConditionalGameCoreSO conditional && !conditional.IsGameCoreRoot)
                    return ESScriptableObjectClass.Simple;
                return ESScriptableObjectClass.GameCore;
            }
            return ESScriptableObjectClass.Simple;
        }
    }
}
