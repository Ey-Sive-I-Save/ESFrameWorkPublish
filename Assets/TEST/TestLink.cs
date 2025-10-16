using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TestLink : MonoBehaviour
{
    public string age;

    [DisplayAsString(overflow:true)]
    public string sort;
   
   
    [Button("发射")]
    void Button()
    {
        
        sort = age._ToCode();
    }
    // Update is called once per frame
 
}
public struct Link_Move
{
    public float f;
    public int i;
}

public enum HurtFrom
{
    ByEnemy,
    ByTrap,
    BySelf,
    ByItem,
}
public struct Link_Hurt
{
    public float damage;
    public int fromWho;
}