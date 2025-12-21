using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/GgCore/Comment")]
    public class Comment : GgMonoBehaviour
    {
        [SerializeField, Min(0)]
        private int lines = 3;
        
        [SerializeField, TextArea]
        private string comment = "Add comment here...";

        private void RemoveWarnings()
        {
            if (string.IsNullOrEmpty(comment) && lines < 3) {  };
        }
        
    } // class end
}