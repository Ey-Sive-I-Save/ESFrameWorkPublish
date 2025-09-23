using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#region 第一组
public abstract class Initer_Abstarct
{
    public abstract void Init();
}

public class EditorRegisterForSingle_Initer_Abstarct : EditorRegister_FOR_Singleton<Initer_Abstarct>
{
    public override void Handle(Initer_Abstarct singleton)
    {
        singleton.Init();
    }
}

public class Initer_Debug : Initer_Abstarct
{
    public override void Init()
    {
        Debug.Log("你好啊，Init");
    }
}

public class Initer_Debug2 : Initer_Abstarct
{
    public override void Init()
    {
        Debug.Log("你2222好啊，Init");
    }
}

public class Initer_Debug3 : Initer_Abstarct
{
    public override void Init()
    {
        Debug.Log("333你好啊，Init");
    }
}
#endregion

