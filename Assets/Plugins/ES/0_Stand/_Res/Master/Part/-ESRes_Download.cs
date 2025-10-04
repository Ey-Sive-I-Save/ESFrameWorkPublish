using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SocialPlatforms;

namespace ES
{
    /// <summary>
    /// ESRes 总工具
    /// </summary>
    public partial class ESResMaster
    {
        [NonSerialized]
        public ESResDownloadState DownloadState;
        [TabGroup("AB包下载")]
        [LabelText("AB包状态"), ShowInInspector]
        public static Dictionary<string, int> ABStates = new Dictionary<string, int>();//-1 无 0更新 1完美
        [LabelText("尝试下载单个")]
        [ValueDropdown("TryDownloadABNames")]
        public string TryDownloadAB;

        private string[] TryDownloadABNames => ABStates.Keys.ToArray();
        [TabGroup("AB包下载")]
        [Button("尝试下载")]
        void TryDownLoad()
        {
            StartCoroutine(DownloadOneAB(ESResData_WithHashes.PreToHashes[TryDownloadAB]));
        }
        [TabGroup("AB包下载")]
        [Button("全部下载")]
        void TryDownLoadAll()
        {
            foreach (var i in ESResData_WithHashes.PreToHashes.Values)
            {
                StartCoroutine(DownloadOneAB(i));
            }
            DownloadState = ESResDownloadState.Ready;
        }
        public void StartResCompareAndDownload()
        {
            //全部卸载
            AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);


            StartCoroutine(DownLoadMainAndJsons());

        }

        private IEnumerator DownLoadMainAndJsons()
        {
            var netpath = GetDownloadNetPathWithPlatform();
            string netJson = netpath + "/" + PathParent_ESResJsonData;
            var local = GetDownloadLocalPath();
            string localJson = local + "/" + PathParent_ESResJsonData;
            string netpathMain = netpath + "/" + GetPlatformName();
            string netpathDependence = netJson + "/" + PathFileName_ESDependences;
            string netpathToHash = netJson + "/" + PathFileName_ESABHashs;
            string netpathAssetKeys = netJson + "/" + PathFileName_ESAssetkeys;
            string netpathABKeys = netJson + "/" + PathFileName_ESABkeys;



            string downloadAtMain = local + "/" + GetPlatformName();
            string downloadAtDependence = localJson + "/" + PathFileName_ESDependences;
            string downloadAtToHash = localJson + "/" + PathFileName_ESABHashs;
            string downloadAssetKeys = localJson + "/" + PathFileName_ESAssetkeys;
            string downloadABKeys = localJson + "/" + PathFileName_ESABkeys;

            var unityWebRequestMain = UnityWebRequest.Get(netpathMain);
            var unityWebRequestDependence = UnityWebRequest.Get(netpathDependence);
            var unityWebRequestHashes = UnityWebRequest.Get(netpathToHash);
            var unityWebRequestAssetKeys = UnityWebRequest.Get(netpathAssetKeys);
            var unityWebRequestABKeys = UnityWebRequest.Get(netpathABKeys);


            unityWebRequestMain.downloadHandler = new DownloadHandlerFile(downloadAtMain);
            unityWebRequestDependence.downloadHandler = new DownloadHandlerFile(downloadAtDependence);
            unityWebRequestHashes.downloadHandler = new DownloadHandlerFile(downloadAtToHash);
            unityWebRequestAssetKeys.downloadHandler = new DownloadHandlerFile(downloadAssetKeys);
            unityWebRequestABKeys.downloadHandler = new DownloadHandlerFile(downloadABKeys);



            unityWebRequestMain.SendWebRequest();
            unityWebRequestDependence.SendWebRequest();
            unityWebRequestHashes.SendWebRequest();
            unityWebRequestAssetKeys.SendWebRequest();
            unityWebRequestABKeys.SendWebRequest();

            while (true)
            {
                yield return null;
                Debug.Log("Main" + unityWebRequestMain.downloadProgress);
                Debug.Log("Dependence" + unityWebRequestDependence.downloadProgress);
                Debug.Log("Hashes" + unityWebRequestHashes.downloadProgress);
                Debug.Log("AssetKeys" + unityWebRequestAssetKeys.downloadProgress);
                Debug.Log("ABKeys" + unityWebRequestABKeys.downloadProgress);
                if (unityWebRequestMain.isDone && unityWebRequestDependence.isDone && unityWebRequestHashes.isDone && unityWebRequestAssetKeys.isDone&& unityWebRequestABKeys.isDone)
                {
                    Debug.Log("全部完成下载！！"+unityWebRequestMain.result);

                    //先获得Json
                    string jsonContentDepend = File.ReadAllText(downloadAtDependence);
                    string jsonContentHash = File.ReadAllText(downloadAtToHash);
                    string jsonContentAssetKeys = File.ReadAllText(downloadAssetKeys);
                    string jsonContentABKeys = File.ReadAllText(downloadABKeys);



                    byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonContentDepend);
                    ESResData_Dependences = SerializationUtility.DeserializeValue<ESResJsonData_Dependences>(jsonBytes, DataFormat.JSON);

                    byte[] jsonBytes2 = System.Text.Encoding.UTF8.GetBytes(jsonContentHash);
                    ESResData_WithHashes = SerializationUtility.DeserializeValue<ESResJsonData_Hashes>(jsonBytes2, DataFormat.JSON);

                    byte[] jsonBytes3 = System.Text.Encoding.UTF8.GetBytes(jsonContentAssetKeys);
                    ESResData_AssetKeys = SerializationUtility.DeserializeValue<ESResJsonData_AssetsKeys>(jsonBytes3, DataFormat.JSON);

                    byte[] jsonBytes4 = System.Text.Encoding.UTF8.GetBytes(jsonContentABKeys);
                    ESResData_ABKeys = SerializationUtility.DeserializeValue<ESResJsonData_ABKeys>(jsonBytes4, DataFormat.JSON);


                    MainBundle = AssetBundle.LoadFromFile(downloadAtMain);
                    Debug.Log("加载主包" + MainBundle);
                    MainManifest = MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    Debug.Log("加载主依赖" + MainManifest);


                    DownloadState = ESResDownloadState.Compare;
                    DetectAllAB();
                    break;
                }
            }


        }
        private void DetectAllAB()
        {
            List<string> UseFiles = new List<string>();
            var downloadPath = GetDownloadLocalPath();
            if (Directory.Exists(downloadPath))
            {
                Debug.Log("存在下载目标路径");
                // 设置搜索模式

                string fileExtension = "";

                string searchPattern = string.IsNullOrEmpty(fileExtension) ?
                    "*.*" : $"*.{fileExtension.TrimStart('.')}";

                // 获取所有文件
                string[] files = Directory.GetFiles(downloadPath, searchPattern, SearchOption.AllDirectories);
                UseFiles.AddRange(files.Select((n) => PathAndName_GetPostNameFromCompleteNameBySymbol(PathAndName_GetPostNameFromCompleteNameBySymbol(n, '/'), '\\')));

                // 过滤掉.meta文件
                UseFiles.RemoveAll(path => path.EndsWith(".meta"));
            }
            //测试
            foreach (var withHashes in UseFiles)
            {
                Debug.Log("I Have File" + withHashes+ (ESResData_WithHashes+" a "+ ESResData_WithHashes?.PreToHashes));
                string pre = PathAndNameTool_GetPreName(withHashes);
                
                if (ESResData_WithHashes.PreToHashes.TryGetValue(pre, out var itWithHash))
                {
                    if (itWithHash == withHashes) { ABStates[pre] = 1; }
                    else
                    {
                        ABStates[pre] = 0;
                    }
                }
            }
            foreach (var i in ESResData_WithHashes.PreToHashes.Keys)
            {
                if (ABStates.TryGetValue(i, out var State))
                {

                }
                else
                {
                    ABStates[i] = -1;
                }
            }
            DownloadState = ESResDownloadState.Download;
            if (AutoDownload)
            {
                TryDownLoadAll();
            }
        }

        private IEnumerator DownloadOneAB(string name)
        {
            var netpath = GetDownloadNetPathWithPlatform();
            string netpathAB = netpath + "/" + name;
            string downloadAtAB = GetDownloadLocalPath() + "/" + name;

            var unityWebRequest = UnityWebRequest.Get(netpathAB);
            unityWebRequest.downloadHandler = new DownloadHandlerFile(downloadAtAB);
            unityWebRequest.SendWebRequest();
            while (true)
            {
               
                Debug.Log("AB DownLoad" + unityWebRequest.downloadProgress);
              
                if (unityWebRequest.isDone)
                {
                    Debug.Log("DONE"+ unityWebRequest.result+unityWebRequest.error);
                    if(unityWebRequest.result== UnityWebRequest.Result.Success)
                    {
                        Debug.Log("SUCCESS");
                        ABStates[PathAndNameTool_GetPreName(name)] = 1;
                       // AssetBundle ab = AssetBundle.LoadFromFile(downloadAtAB);
                    }
                    break;
                }
                yield return null;
            }

        }
    }
    public enum ESResDownloadState
    {
        None,
        Compare,
        Download,
        Ready
    }
}

