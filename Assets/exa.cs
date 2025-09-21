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
    [Button("测试")]
    void Debug()
    {

    }
}
