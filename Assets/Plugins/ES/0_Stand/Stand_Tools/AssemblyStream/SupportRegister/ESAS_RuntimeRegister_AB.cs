using ES;
using System;
using System.Reflection;
namespace ES
{
    public abstract class ESAS_RuntimeRegister_AB
    {
        public abstract int LoadTiming { get; }
    }
/// <summary>
/// 实例化单例模式
/// </summary>
/// <typeparam name="ForType"></typeparam>
    public abstract class RuntimeRegister_FOR_Singleton<ForType> : ESAS_RuntimeRegister_AB
    {
        public abstract void Handle(ForType singleton);
    }

    /// <summary>
/// 子类型模式
/// </summary>
/// <typeparam name="ParentType"></typeparam>
public abstract class RuntimeRegister_FOR_AsSubclass<ParentType> : ESAS_RuntimeRegister_AB
{
    public abstract void Handle(Type subClassType);
    
}

/// <summary>
/// 类特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
    public abstract class RuntimeRegister_FOR_ClassAttribute<ForAttribute> :ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, Type type);
    }
/// <summary>
/// 字段特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
    public abstract class RuntimeRegister_FOR_FieldAttribute<ForAttribute> : ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
    }
/// <summary>
/// 方法特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
    public abstract class RuntimeRegister_FOR_MethodAttribute<ForAttribute> :ESAS_RuntimeRegister_AB where ForAttribute : Attribute
    {
        public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
    }
}
