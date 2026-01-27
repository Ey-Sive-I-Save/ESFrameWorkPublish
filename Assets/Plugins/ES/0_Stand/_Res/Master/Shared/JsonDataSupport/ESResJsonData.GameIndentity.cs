using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 游戏身份数据类。
    /// 存储游戏的构建信息，包括时间戳、版本号和必须安装的库名。
    /// </summary>
    [Serializable]
    public class ESResJsonData_GameIdentity
    {
        /// <summary>
        /// 构建时间戳（ISO 8601格式）。
        /// </summary>
        public string BuildTimestamp;

        /// <summary>
        /// 游戏版本号。
        /// </summary>
        public string Version;

        /// <summary>
        /// 必须安装的库名列表。
        /// </summary>
        public List<RequiredLibrary> RequiredLibrariesFolders;

    }

    [Serializable]
    public class RequiredLibrary
    {
        public string FolderName;
        public bool IsRemote; // true: 远程（需下载），false: 本地（内置）
    }
}
