/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#region 第一组
public abstract class Initer_Abstract
{
    public abstract void Init();
}

public class EditorRegisterForSingle_Initer_Abstract : EditorRegister_FOR_Singleton<Initer_Abstract>
{
    public override void Handle(Initer_Abstract singleton)
    {
        singleton.Init();
    }
}

public class Initer_Debug : Initer_Abstract
{
    public override void Init()
    {
        Debug.Log("你好啊，InitInvoke");
    }
}

public class Initer_Debug2 : Initer_Abstract
{
    public override void Init()
    {
        Debug.Log("你2222好啊，InitInvoke");
    }
}

public class Initer_Debug3 : Initer_Abstract
{
    public override void Init()
    {
        Debug.Log("333你好啊，InitInvoke");
    }
}
#endregion

*/