using ES;
using System;
using System.Reflection;
namespace ES
{
    public abstract class ESAS_RuntimeRegister_AB
    {
        public abstract ESAssemblyLoadTiming LoadTiming { get; }
    }

    public abstract class RuntimeRegister_FOR_Singleton<ForType> : ESAS_RuntimeRegister_AB
    {
        public abstract void Handle(ForType singleton);
    }

    public abstract class RuntimeRegister_FOR_ClassAttribute<ForAttribute> :ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, Type type);
    }

    public abstract class RuntimeRegister_FOR_FieldAttribute<ForAttribute> : ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
    }

    public abstract class RuntimeRegister_FOR_MethodAttribute<ForAttribute> :ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
    }
}
