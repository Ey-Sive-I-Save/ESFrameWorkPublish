
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 这部分用于生成必要的JsonData--->_Path_NetABPath/ESResData
    /// 键包：
    /// </summary>
    public partial class ESResMaster
    {
        public const string PathParent_ESResJsonData = "ESResData";//这个文件夹下只有这一种应用场景
        /*        public const string PathChild_ESResDataLib = "Libs";//这里存放每一个库*/
        /**/
        public const string PathFileName_ESABHashs = "ABHashes.json";//这里存放AB  获得的 Hash值 string ABName->Hash
        public const string PathFileName_ESDependences = "ABDependences.json"; //这里存放Ab 的 依赖关系 AbName-> SomeAB
        public const string PathFileName_ESAssetkeys = "AssetKeys.json";
        public const string PathFileName_ESABkeys = "ABKeys.json";

        public static ESResJsonData_AssetsKeys ESResData_AssetKeys = new ESResJsonData_AssetsKeys();
        public static ESResJsonData_ABKeys ESResData_ABKeys = new ESResJsonData_ABKeys();
        public static ESResJsonData_Hashes ESResData_WithHashes = new ESResJsonData_Hashes();
        public static ESResJsonData_Dependences ESResData_Dependences = new ESResJsonData_Dependences();
      
        public static string[] ABNames;
        public static void JsonData_CreateAssetKeys(bool clearAtFirst = true)
        {
            //目前是废案
            /* string path = ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath + "/" + PathParent_ESResJsonData + "/" + PathChild_ESResDataLib;
             //可以先清除
             if (clearAtFirst) ESStandUtility.SafeEditor.Quick_System_DeleteAllFilesInFolder_Always(path);
 */
            //此处已经完成键储备
            string path = ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath+"/"+ RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform) + "/" + PathParent_ESResJsonData;
            byte[] Keys_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(ESResData_AssetKeys, DataFormat.JSON);
            string Keys_Json = System.Text.Encoding.UTF8.GetString(Keys_jsonBytes);
            string Keys_Path = path + "/" + PathFileName_ESAssetkeys;

            ESStandUtility.SafeEditor.Quick_System_CreateDirectory(path);

            File.WriteAllText(Keys_Path, Keys_Json);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
#endif 
        }
        public static void JsonData_CreateHashAndDependence()
        {
#if UNITY_EDITOR
            string path = ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath + "/" + RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform) + "/" + PathParent_ESResJsonData;
            ABNames = AssetDatabase.GetAllAssetBundleNames();
            //先卸载加载
            AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);
            string plat = RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform);
            AssetBundle MainBundle = AssetBundle.LoadFromFile(Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, plat, plat));
            //FEST
            AssetBundleManifest manifest = MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            //带Hash值的AB
            string[] AllABWithHash = manifest.GetAllAssetBundles();
            ESResData_WithHashes.PreToHashes.Clear();
            ESResData_Dependences.Dependences.Clear();
            ESResData_ABKeys.ABKeys.Clear();

            int len = ABNames.Length;
            for (int index = 0; index < len; index++)
            {
                var abName = ABNames[index];
                foreach (var withHash in AllABWithHash)
                {
                    string pre = PathAndNameTool_GetPreName(withHash);
                    if (pre == abName)
                    {
                        ESResData_WithHashes.PreToHashes.TryAdd(abName, withHash);
                        ESResData_WithHashes.HashesToPre.TryAdd(withHash,abName);
                        string[] abDepend = manifest.GetAllDependencies(withHash);
                        for (int i = 0; i < abDepend.Length; i++)
                        {
                            abDepend[i] = PathAndNameTool_GetPreName(abDepend[i]);
                        }
                        if (abDepend.Length > 0) ESResData_Dependences.Dependences.Add(abName, abDepend);

                        ESResKey key = new ESResKey() { LibName = null, ABName = pre, SourceLoadType = ESResSourceLoadType.AssetBundle, ResName = withHash, TargetType = typeof(AssetBundle) };
                        int count = ESResData_ABKeys.ABKeys.Count;
                        ESResData_ABKeys.ABKeys.Add(key);
                        Debug.Log(pre);
                        ESResData_ABKeys.NameToABKeys.TryAdd(pre, count);
                    }
                }
            }
            ESStandUtility.SafeEditor.Quick_System_CreateDirectory(path);
            byte[] HASH_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(ESResData_WithHashes, DataFormat.JSON);
            string HASH_Json = System.Text.Encoding.UTF8.GetString(HASH_jsonBytes);
            string Hash_Path = path + "/" + PathFileName_ESABHashs;
            File.WriteAllText(Hash_Path, HASH_Json);

            byte[] Depend_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(ESResData_Dependences, DataFormat.JSON);
            string Depend_Json = System.Text.Encoding.UTF8.GetString(Depend_jsonBytes);
            string Depend_Path = path + "/" + PathFileName_ESDependences;
            File.WriteAllText(Depend_Path, Depend_Json);


            byte[] ABkeys_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(ESResData_ABKeys, DataFormat.JSON);
            string ABkeys_Json = System.Text.Encoding.UTF8.GetString(ABkeys_jsonBytes);
            string ABkeys_Path = path + "/" + PathFileName_ESABkeys;
            File.WriteAllText(ABkeys_Path, ABkeys_Json);



            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
#endif 

        }
    }

    /// <summary>
    /// 查询特定资源的最终标识
    /// </summary>
    [Serializable]
    public class ESResKey : IPoolableAuto
    {

        public ESResSourceLoadType SourceLoadType = ESResSourceLoadType.ABAsset;
        public string LibName;
        public string ABName;
        public string ResName;
        public Type TargetType;

        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {

        }

        public override string ToString()
        {
            return string.Format("资源查询键, 库名:{0} AB包名:{1} 类型:{2},资源名{3}", LibName, ABName,
                ResName, TargetType);
        }

        public void TryAutoPushedToPool()
        {
            ESResMaster.Instance.PoolForESResKey.PushToPool(this);
        }
    }

    [Serializable]
    public class ESResJsonData_Hashes
    {
        [NonSerialized, OdinSerialize]
        public Dictionary<string, string> PreToHashes = new Dictionary<string, string>();

        [NonSerialized, OdinSerialize]
        public Dictionary<string, string> HashesToPre = new Dictionary<string, string>();

    }

    [Serializable]
    public class ESResJsonData_Dependences
    {
        [NonSerialized, OdinSerialize]
        public Dictionary<string, string[]> Dependences = new Dictionary<string, string[]>();
    }

    [Serializable]
    public class ESResJsonData_AssetsKeys
    {

        public List<ESResKey> AssetKeys = new List<ESResKey>();
        [NonSerialized, OdinSerialize]
        public Dictionary<string, int> GUIDToAssetKeys = new Dictionary<string, int>();
        [NonSerialized, OdinSerialize]
        public Dictionary<string, int> PathToAssetKeys = new Dictionary<string, int>();


       


    }
    [Serializable]
    public class ESResJsonData_ABKeys
    {
        public List<ESResKey> ABKeys = new List<ESResKey>();
        [NonSerialized, OdinSerialize]
        public Dictionary<string, int> NameToABKeys = new Dictionary<string, int>();
    }


}
