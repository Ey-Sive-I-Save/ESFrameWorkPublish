using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

public class exa : SerializedMonoBehaviour 
{
    public ESResRefer refer;
    public ESResReferPrefab prefab;
    public ESResReferAudioClip clip;
    public ESResReferMat mat;

    public Dictionary<string, string> keyValues = new Dictionary<string, string>();

    [TextArea]
    public string test;
    [Button("测试")]
    public void Test()
    {

        test= ESDesignUtility.Matcher.ToOdinJson(keyValues);
    }
}
[Serializable]
public class LevelArea
{
    [FoldoutGroup("储备")]
    public Vector2 posCenter;
    [FoldoutGroup("储备")]
    public Vector2 Range = new Vector2(100, 100);
    [FoldoutGroup("储备")]
    public List<PosAndPrefab> prefabs = new List<PosAndPrefab>();
}
[Serializable]
public class PosAndPrefab
{
    public Vector2 pos;
    public GameObject prefab;
}
