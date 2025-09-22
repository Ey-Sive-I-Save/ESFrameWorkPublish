using ES;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class exa : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(ie());
    }
    IEnumerator ie()
    {
        yield return null;
    }
    // Update is called once per frame
    void Update()
    {
      
    }
    public float f;
    public string s;
    [Button("测试")]
    void Debug()
    {
        GlobalLinker.POOL.SendLink(f);
        GlobalLinker.POOL.SendLink(s);
    }
}
