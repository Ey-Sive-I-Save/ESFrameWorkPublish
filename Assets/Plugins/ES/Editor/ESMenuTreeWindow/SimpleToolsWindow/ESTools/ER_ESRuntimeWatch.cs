#if UNITY_EDITOR
using System.Reflection;

namespace ES
{
    public class ER_ESRuntimeWatch : EditorRegister_FOR_FieldAttribute<ESRuntimeWatchAttribute>
    {
        public override void Handle(ESRuntimeWatchAttribute attribute, FieldInfo fieldInfo)
        {
            ESRuntimeWatchRegistry.RegisterField(attribute, fieldInfo);
        }
    }

    public class ER_ESRuntimeWatch_Property : EditorRegister_FOR_PropertyAttribute<ESRuntimeWatchAttribute>
    {
        public override void Handle(ESRuntimeWatchAttribute attribute, PropertyInfo propertyInfo)
        {
            ESRuntimeWatchRegistry.RegisterProperty(attribute, propertyInfo);
        }
    }

    public class ER_ESRuntimeWatch_Method : EditorRegister_FOR_MethodAttribute<ESRuntimeWatchAttribute>
    {
        public override void Handle(ESRuntimeWatchAttribute attribute, MethodInfo methodInfo)
        {
            ESRuntimeWatchRegistry.RegisterMethod(attribute, methodInfo);
        }
    }
}
#endif
