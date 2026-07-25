using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;
namespace ES
{
    [Serializable]
    public class ESResJsonData_LibIndentity
    {
        public int SchemaVersion = 2;
        public string ReleaseId;
        public string LibraryDisplayName;
        public string LibFolderName;
        public string LibraryDescription;
        public int ChangeCount;
        public string LibraryVersion;
        public string DownloadGroup;
        public bool RequiredAtBoot;
        public string AssetKeysSha256;
        public string ABMetadataSha256;
        public int BundleCount;
        public long TotalBundleBytes;
        
    }
}
