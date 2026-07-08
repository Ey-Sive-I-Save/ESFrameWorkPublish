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
}
