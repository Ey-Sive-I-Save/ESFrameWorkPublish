using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;
namespace ES
{
    [Serializable]
    public class ESResJsonData_ConsumerIdentity
    {
        public string ConsumerDisplayName;
        public string Version;
        public string ConsumerDescription;
        public List<RequiredLibrary> IncludedLibrariesFolders;
    }
}