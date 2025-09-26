using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CreateAssetMenu(fileName = "AA",menuName = "BB")]
public class SO : ScriptableObject
{
    public UnityEngine.Object @object;
    public GUIStyle style;
    public float f;
    private void OnEnable()
    {
        Debug.Log(66);
    }
    private void OnValidate()
    {
        Debug.Log(330);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
public class Initer 
{
    [InitializeOnLoadMethod]
    public static void AA()
    {
        
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(SO).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log("AT1");
            SO globalData = AssetDatabase.LoadAssetAtPath<SO>(path);
            if (globalData != null)
            {
                globalData.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(globalData);
                Debug.Log("AT2");
            }  
        }
    }
}
