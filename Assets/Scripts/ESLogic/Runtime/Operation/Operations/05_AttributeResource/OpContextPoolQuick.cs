using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("Float加", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_AddFloat : ESOutputOp
    {
        public string key;
        public FloatExpressionSource delta = new FloatExpressionSource { directFloat = 1f };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetFloatQuick_Add(key, delta != null ? delta.Evaluate(target, support) : 1f, false);
        }
    }

    [Serializable, TypeRegistryItem("Int加", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_AddInt : ESOutputOp
    {
        public string key;
        public IntExpressionSource delta = new IntExpressionSource { directInt = 1 };

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetIntQuick_Add(key, delta != null ? delta.Evaluate(target, support) : 1);
        }
    }

    [Serializable, TypeRegistryItem("Bool取反", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ToggleBool : ESOutputOp
    {
        public string key;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetBoolQuick_Not(key);
        }
    }

    [Serializable, TypeRegistryItem("String替换", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_ReplaceString : ESOutputOp
    {
        public string key;
        public string from;
        public string to;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.SetStringQuick_Replace(key, from, to);
        }
    }

    [Serializable, TypeRegistryItem("启用Link", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_EnableLink : ESOutputOp
    {
        public string key;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.EnableLink(key);
        }
    }

    [Serializable, TypeRegistryItem("禁用Link", OperationTypeRegistryNames.Value)]
    public sealed class OpContext_DisableLink : ESOutputOp
    {
        public string key;

        protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
        {
            ESOpSupport support = RuntimeSupport(scopeSupport, hostSupport);
            support?.Context?.DisableLink(key);
        }
    }
}
