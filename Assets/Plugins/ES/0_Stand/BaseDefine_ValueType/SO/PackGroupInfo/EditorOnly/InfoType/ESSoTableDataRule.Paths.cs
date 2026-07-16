#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Output Path Resolution
        private string GetOutputPath(string relativeFolder, string extension)
        {
            string root = string.IsNullOrEmpty(GetActiveOutputRoot()) ? "SoTableConfig/Tables" : GetActiveOutputRoot();
            string folder = string.IsNullOrEmpty(relativeFolder) ? string.Empty : relativeFolder;
            string file = string.IsNullOrEmpty(GetActiveFileName()) ? ruleKey : GetActiveFileName();
            if (string.IsNullOrEmpty(file))
                file = "NewTable";

            string path = Path.Combine(Application.dataPath, "..", root, folder, file + extension);
            return Path.GetFullPath(path);
        }
        #endregion
    }
}
#endif
