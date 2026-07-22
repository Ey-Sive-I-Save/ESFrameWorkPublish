using System;
using System.Reflection;
using UnityEngine;

public enum EditorRegisterOrder
{
    [InspectorName("【启动】最早阶段 0")]
    Level0 = 0,

    [InspectorName("【启动】最早阶段 1")]
    Level1 = 1,

    [InspectorName("【启动】最早阶段 2")]
    Level2 = 2,

    [InspectorName("【启动】最早阶段 3")]
    Level3 = 3,

    [InspectorName("【编辑器】刷新阶段")]
    Refresh = 20,

    [InspectorName("【SO数据】应用阶段")]
    SODataApply = 40,

    [InspectorName("【构建】构建阶段")]
    Build = 60,

    [InspectorName("【结束】编辑器注册结束")]
    EditorEnd = 100,
}

public abstract class ESAS_EditorRegister_AB
{
    public abstract int Order { get; }
}

/// <summary>
/// 实例注册模式。程序集流会实例化符合类型的无参非抽象类，并交给注册器处理。
/// </summary>
public abstract class EditorRegister_FOR_Singleton<ForType> : ESAS_EditorRegister_AB
{
    public abstract void Handle(ForType singleton);
}

/// <summary>
/// 子类注册模式。程序集流会把符合父类/接口约束的非抽象类型交给注册器处理。
/// </summary>
public abstract class EditorRegister_FOR_AsSubclass<ParentType> : ESAS_EditorRegister_AB
{
    public abstract void Handle(Type subClassType);
}

/// <summary>
/// 类特性注册模式。
/// </summary>
public abstract class EditorRegister_FOR_ClassAttribute<ForAttribute> : ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, Type type);
    public override int Order => 60;
}

/// <summary>
/// 字段特性注册模式。
/// </summary>
public abstract class EditorRegister_FOR_FieldAttribute<ForAttribute> : ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
    public override int Order => 80;
}

/// <summary>
/// 属性特性注册模式。
/// </summary>
public abstract class EditorRegister_FOR_PropertyAttribute<ForAttribute> : ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, PropertyInfo propertyInfo);
    public override int Order => 81;
}

/// <summary>
/// 方法特性注册模式。
/// </summary>
public abstract class EditorRegister_FOR_MethodAttribute<ForAttribute> : ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
    public override int Order => 90;
}
