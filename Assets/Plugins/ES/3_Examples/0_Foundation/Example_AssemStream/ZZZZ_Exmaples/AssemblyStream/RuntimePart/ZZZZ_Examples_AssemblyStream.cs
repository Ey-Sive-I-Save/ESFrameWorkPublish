using ES;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public abstract class RuntimeGlobalLinker
{
    //加入这里有一个全局事件中心
    public static LinkReceivePool POOL = new LinkReceivePool();
    public abstract void Level0();
    public abstract void Level1();
    public abstract void Level2_ApplyGlobalLinker();
}

public abstract class RuntimeGlobalLinker<Link> : RuntimeGlobalLinker, IReceiveLink<Link>
{
    public override void Level0()
    {
        
    }
    public override void Level1()
    {
        
    }
    public override  void Level2_ApplyGlobalLinker()
    {
       
        POOL.AddReceiver(this);
    }
    public abstract void OnLink(Link link);
}
public class EditorRegister_FOR_RuntimeGlobalLinker_Level0 : RuntimeRegister_FOR_Singleton<RuntimeGlobalLinker>
{
    public override int LoadTiming =>  -100;

    public override void Handle(RuntimeGlobalLinker singleton)
    {
        singleton.Level0();
    }
}
public class EditorRegister_FOR_RuntimeGlobalLinker_Level1 : RuntimeRegister_FOR_Singleton<RuntimeGlobalLinker>
{
    public override int LoadTiming => ESAssemblyLoadTiming._1_BeforeFirstSceneLoad;
    public override void Handle(RuntimeGlobalLinker singleton) 
    {
        singleton.Level1();
    }
}

public class EditorRegister_FOR_RuntimeGlobalLinker_Level2: RuntimeRegister_FOR_Singleton<RuntimeGlobalLinker>
{
    public override int LoadTiming => ESAssemblyLoadTiming._2_AfterFirstSceneLoad;
    public override void Handle(RuntimeGlobalLinker singleton)
    {
        singleton.Level2_ApplyGlobalLinker();
    }
}
public class Linker_AAAr : RuntimeGlobalLinker<float>
{
    public override void Level0()
    {
        Debug.Log("第零阶段，可以初始化一些与Unity无关的");
    }
    public override void OnLink(float link)
    {
        Debug.Log("事件触发"+link);
    }
}
public class Linker_AAA2r : RuntimeGlobalLinker<float>
{
    public override void Level1()
    {
        Debug.Log("第一阶段，场景还没加载啊，但是基本设施可以搭建了");
    }
    public override void OnLink(float link)
    {
        Debug.Log("事件触发"+link);
    }
}
public class Linker_AAA3r : RuntimeGlobalLinker<string>
{
    public override void Level2_ApplyGlobalLinker()
    {
        base.Level2_ApplyGlobalLinker();
        Debug.Log("第二阶段,场景物体已经完成AWAKE了");
    }
    public override void OnLink(string link)
    {
        Debug.Log("事件触发"+link);
    }
}