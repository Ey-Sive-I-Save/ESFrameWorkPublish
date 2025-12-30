using System;
using System.Reflection;
using UnityEngine;

public enum EditorRegisterOrder //-100 100 
{
    [InspectorName("最开始_0")]Level0,
    [InspectorName("最开始_1")] Level1,
    [InspectorName("最开始_2")] Level2,
    [InspectorName("最开始_3")] Level3,

    [InspectorName("刷新阶段")] Refresh,
    [InspectorName("So数据应用")] SODataApply,
    [InspectorName("建造阶段")] Build,
    [InspectorName("最终")] EditorEnd=100,

}
public abstract class ESAS_EditorRegister_AB
{
    public abstract int Order { get; }
}
/// <summary>
/// 实例化单例模式
/// </summary>
/// <typeparam name="ForType"></typeparam>
public abstract class EditorRegister_FOR_Singleton<ForType> : global::ESAS_EditorRegister_AB
{
    public abstract void Handle(ForType singleton);
    
}
/// <summary>
/// 子类型模式
/// </summary>
/// <typeparam name="ParentType"></typeparam>
public abstract class EditorRegister_FOR_AsSubclass<ParentType> : global::ESAS_EditorRegister_AB
{
    public abstract void Handle(Type subClassType);
    
}
/// <summary>
/// 类特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
public abstract class EditorRegister_FOR_ClassAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute,Type type);
    public override int Order => 999;
}
/// <summary>
/// 字段特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
public abstract class EditorRegister_FOR_FieldAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, FieldInfo fieldInfo);
    public override int Order => 1000;
}
/// <summary>
/// 方法特性模式
/// </summary>
/// <typeparam name="ForAttribute"></typeparam>
public abstract class EditorRegister_FOR_MethodAttribute<ForAttribute> : global::ESAS_EditorRegister_AB where ForAttribute : Attribute
{
    public abstract void Handle(ForAttribute attribute, MethodInfo methodInfo);
    public override int Order => 1001;
}

