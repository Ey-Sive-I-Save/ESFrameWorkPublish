using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public abstract class EditorInvoker_Level0
    {
        public abstract void InitInvoke();  
    }

    public abstract class EditorInvoker_Level1
    {
        public abstract void InitInvoke();
    }
    public abstract class EditorInvoker_Level2
    {
        public abstract void InitInvoke();
    }
    public abstract class EditorInvoker_SoApply
    {
        public abstract void InitInvoke();
    }

    public abstract class EditorInvoker_Level50
    {
        public abstract void InitInvoke();
    }
    public class EditorRegisterForSingle_EditorInvoker0 : EditorRegister_FOR_Singleton<EditorInvoker_Level0>
    {
        public override int Order => EditorRegisterOrder.Level0.GetHashCode();
        public override void Handle(EditorInvoker_Level0 singleton)
        {
            singleton.InitInvoke();
        }
    }
    public class EditorRegisterForSingle_EditorInvoker1 : EditorRegister_FOR_Singleton<EditorInvoker_Level1>
    {
        public override int Order => EditorRegisterOrder.Level1.GetHashCode();
        public override void Handle(EditorInvoker_Level1 singleton)
        {
            singleton.InitInvoke();
        }
    }
    public class EditorRegisterForSingle_EditorInvoker2 : EditorRegister_FOR_Singleton<EditorInvoker_Level2>
    {
        public override int Order => EditorRegisterOrder.Level2.GetHashCode();
        public override void Handle(EditorInvoker_Level2 singleton)
        {
            singleton.InitInvoke();
        }
    }
    public class EditorRegisterForSingle_SoApply : EditorRegister_FOR_Singleton<EditorInvoker_SoApply>
    {
        public override int Order => EditorRegisterOrder.SODataApply.GetHashCode();
        public override void Handle(EditorInvoker_SoApply singleton)
        {
            singleton.InitInvoke();
        }
    }
    public class EditorRegisterForSingle_EditorInvoker50 : EditorRegister_FOR_Singleton<EditorInvoker_Level50>
    {
        public override int Order => 50;
        public override void Handle(EditorInvoker_Level50 singleton)
        {
            singleton.InitInvoke();
        }
    }
}
