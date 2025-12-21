using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_SceneData : MonoBehaviour
    {
        // ---------- Scene Data ----------

        [SerializeField]
        private SceneData sceneData;

        [SerializeField]
        private SceneData sceneData2;

        [SerializeField, Space]
        private List<SceneData> sceneDataList;

        private void RemoveConsoleWarning()
        {
            sceneData = new SceneData();
            sceneData2 = new SceneData();
            sceneDataList = new List<SceneData>();
        }

    } // class end
}
