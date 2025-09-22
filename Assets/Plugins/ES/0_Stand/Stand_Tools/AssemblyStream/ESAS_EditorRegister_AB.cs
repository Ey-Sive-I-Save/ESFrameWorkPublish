using System;
using System.Reflection;
public abstract class ESAS_EditorRegister_AB
{
    
}
public abstract class EditorRegisterFORSingleton<ForType> : global::ESAS_EditorRegister_AB
{
    public abstract void Handle(ForType singleton);
}

public abstract class EditorRegisterFORClassAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute,Type type);
}

public abstract class EditorRegisterFORFieldAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
}

public abstract class EditorRegisterFORMethodAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
}

