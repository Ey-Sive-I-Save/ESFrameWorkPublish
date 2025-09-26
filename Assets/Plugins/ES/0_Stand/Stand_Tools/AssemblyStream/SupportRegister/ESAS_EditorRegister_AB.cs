using System;
using System.Reflection;
using UnityEngine;

public enum EditorRegisterOrder
{
    [InspectorName("最开始_0")]Level0,
    [InspectorName("最开始_1")] Level1,
    [InspectorName("最开始_2")] Level2,
    [InspectorName("最开始_3")] Level3,

    [InspectorName("刷新阶段")] Refresh,
    [InspectorName("So数据应用")] SODataApply,
    [InspectorName("建造阶段")] Build,
    [InspectorName("最终")] EditorEnd,

}
public abstract class ESAS_EditorRegister_AB
{
    public abstract int Order { get; }
}

public abstract class EditorRegister_FOR_Singleton<ForType> : global::ESAS_EditorRegister_AB
{
    public abstract void Handle(ForType singleton);
    
}

public abstract class EditorRegister_FOR_ClassAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute,Type type);
    public override int Order => 999;
}

public abstract class EditorRegister_FOR_FieldAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
    public override int Order => 1000;
}

public abstract class EditorRegister_FOR_MethodAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
    public override int Order => 1001;
}

