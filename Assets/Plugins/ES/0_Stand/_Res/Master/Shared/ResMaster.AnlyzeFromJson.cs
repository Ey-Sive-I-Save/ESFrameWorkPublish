
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
    public partial class ESResMaster
    {
        public static ESResJsonData_AssetsKeys MainESResData_AssetKeys = new ESResJsonData_AssetsKeys();
        public static ESResJsonData_ABKeys MainESResData_ABKeys = new ESResJsonData_ABKeys();
        public static ESResJsonData_Hashes MainESResData_WithHashes = new ESResJsonData_Hashes();
        public static ESResJsonData_Dependences MainESResData_Dependences = new ESResJsonData_Dependences();

        public static string[] ABNames;


           /// <summary>
        /// 将当前的 `ESResData_AssetKeys` 序列化为 JSON 并写入远端构建输出路径下的 `ESResData/AssetKeys.json`。
        /// 说明：历史上此方法会清理子目录（已弃用），相关旧代码已移除以简化实现。
        /// </summary>
        public static void JsonData_CreateAssetKeys(bool clearAtFirst = true)
        {
            // 目标目录：Path_RemoteResOutBuildPath/{Platform}/ESResData
            string path = ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath + "/" + RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform) + "/" + PathParentFolder_ESResJsonData;

            byte[] Keys_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(MainESResData_AssetKeys, DataFormat.JSON);
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
            string path = ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath + "/" + RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform) + "/" + PathParentFolder_ESResJsonData;
            ABNames = AssetDatabase.GetAllAssetBundleNames();
            //先卸载加载
            AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);
            string plat = RunTimePlatformFolderName(ESGlobalResSetting.Instance.applyPlatform);
            AssetBundle MainBundle = AssetBundle.LoadFromFile(Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, plat, plat));
            //FEST
            AssetBundleManifest manifest = MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            //带Hash值的AB
            string[] AllABWithHash = manifest.GetAllAssetBundles();
            MainESResData_WithHashes.PreToHashes.Clear();
            MainESResData_Dependences.Dependences.Clear();
            MainESResData_ABKeys.ABKeys.Clear();

            int len = ABNames.Length;
            for (int index = 0; index < len; index++)
            {
                var abName = ABNames[index];
                foreach (var withHash in AllABWithHash)
                {
                    string pre = PathAndNameTool_GetPreName(withHash);
                    if (pre == abName)
                    {
                        MainESResData_WithHashes.PreToHashes.TryAdd(abName, withHash);
                        MainESResData_WithHashes.HashesToPre.TryAdd(withHash, abName);

                        string[] abDepend = manifest.GetAllDependencies(withHash);
                        for (int i = 0; i < abDepend.Length; i++)
                        {
                            abDepend[i] = PathAndNameTool_GetPreName(abDepend[i]);
                        }
                        if (abDepend.Length > 0)
                            MainESResData_Dependences.Dependences.Add(abName, abDepend);

                        ESResKey key = new ESResKey() { LibName = null, ABName = pre, SourceLoadType = ESResSourceLoadType.AssetBundle, ResName = withHash, TargetType = typeof(AssetBundle) };
                        int count = MainESResData_ABKeys.ABKeys.Count;
                        MainESResData_ABKeys.ABKeys.Add(key);
                        // 去掉噪音日志：若需要打印，请考虑使用可控日志封装
                        // Debug.Log(pre);
                        MainESResData_ABKeys.NameToABKeys.TryAdd(pre, count);
                    }
                }
            }
            ESStandUtility.SafeEditor.Quick_System_CreateDirectory(path);
            byte[] HASH_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(MainESResData_WithHashes, DataFormat.JSON);
            string HASH_Json = System.Text.Encoding.UTF8.GetString(HASH_jsonBytes);
            string Hash_Path = path + "/" + PathJsonFileName_ESABHashs;
            File.WriteAllText(Hash_Path, HASH_Json);

            byte[] Depend_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(MainESResData_Dependences, DataFormat.JSON);
            string Depend_Json = System.Text.Encoding.UTF8.GetString(Depend_jsonBytes);
            string Depend_Path = path + "/" + PathJsonFileName_ESDependences;
            File.WriteAllText(Depend_Path, Depend_Json);


            byte[] ABkeys_jsonBytes = Sirenix.Serialization.SerializationUtility.SerializeValue(MainESResData_ABKeys, DataFormat.JSON);
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
