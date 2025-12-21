#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class FileExtensions
    {
        /// <summary>
        /// Save an asset to a specified folder
        /// </summary>
        /// <param name="assetToSave"></param>
        /// <param name="folderPath"></param>
        /// <param name="pingObject"></param>
        /// <param name="openFileExplorer"></param>
        public static void SaveAssetToFile(Object assetToSave, string folderPath, bool pingObject = true, bool openFileExplorer = false)
        {
            string filePath = folderPath + "/" + assetToSave.name + ".asset";
            AssetDatabase.CreateAsset(assetToSave, filePath);
            AssetDatabase.SaveAssets();

            if (pingObject) { EditorGUIUtility.PingObject(assetToSave); }
            if (openFileExplorer) { OpenFileExplorer(filePath); }
        }

        /// <summary>
        /// Open the file explorer at a specified file path or folder path
        /// </summary>
        /// <param name="filePath"></param>
        public static void OpenFileExplorer(string filePath)
        {
            filePath = filePath.Replace(@"/", @"\"); // explorer doesn't like front slashes
            Process.Start("explorer.exe", "/select," + filePath);
        }
        
        /// <summary>
        /// Checks whether a string path is a valid folder path
        /// </summary>
        /// <param name="relativeFilePath"> E.g "FolderName" not "Assets/FolderName"</param>
        /// <returns></returns>
        public static bool IsFolderPathValid(string relativeFilePath)
        {
            string targetPath = string.IsNullOrEmpty(relativeFilePath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativeFilePath);

            return Directory.Exists(targetPath);
        }
        
        /// <summary>
        /// Checks whether a string path is a valid file path
        /// </summary>
        /// <param name="relativeFilePath"> E.g "FileName" not "Assets/Filename"</param>
        /// <returns></returns>
        public static bool IsFilePathValid(string relativeFilePath)
        {
            string targetPath = string.IsNullOrEmpty(relativeFilePath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativeFilePath);

            return File.Exists(targetPath);
        }
        
        /// <summary>
        /// Gets the relative file path for the users desktop
        /// </summary>
        /// <returns></returns>
        public static string DesktopFilePath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

    } // class end
}
#endif
