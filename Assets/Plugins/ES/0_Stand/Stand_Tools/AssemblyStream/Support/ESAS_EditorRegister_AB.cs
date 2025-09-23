using System;
using System.Reflection;
public abstract class ESAS_EditorRegister_AB
{
    
}

public abstract class EditorRegister_FOR_Singleton<ForType> : global::ESAS_EditorRegister_AB
{
    public abstract void Handle(ForType singleton);
}

public abstract class EditorRegister_FOR_ClassAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute,Type type);
}

public abstract class EditorRegister_FOR_FieldAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
}

public abstract class EditorRegister_FOR_MethodAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
}

