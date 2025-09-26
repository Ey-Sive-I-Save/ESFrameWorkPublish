using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class exa : MonoBehaviour 
{
    public LevelArea level;
    // Update is called once per frame


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
