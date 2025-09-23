using ES;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class GlobalLinker
{
    //加入这里有一个全局事件中心
    public static LinkReceivePool POOL = new LinkReceivePool();
    public abstract void ApplyGlobalLinker();
}
public abstract class GlobalLinker<Link> : GlobalLinker, IReceiveLink<Link>
{
    public override  void ApplyGlobalLinker()
    {
        Debug.Log("完成注册" +typeof(Link)+"BY"+ this);
        POOL.AddReceive(this);
    }
    public abstract void OnLink(Link link);
}
public class EditorRegister_GlobalLinker : EditorRegister_FOR_Singleton<GlobalLinker>
{
    public override void Handle(GlobalLinker singleton)
    {
        singleton.ApplyGlobalLinker();
    }
}

public class Linker_AAA : GlobalLinker<float>
{
    public override void OnLink(float link)
    {
        Debug.Log("事件触发"+link);
    }
}
public class Linker_AAA2 : GlobalLinker<float>
{
    public override void OnLink(float link)
    {
        Debug.Log("事件触发"+link);
    }
}
public class Linker_AAA3 : GlobalLinker<string>
{
    public override void OnLink(string link)
    {
        Debug.Log("事件触发"+link);
    }
}