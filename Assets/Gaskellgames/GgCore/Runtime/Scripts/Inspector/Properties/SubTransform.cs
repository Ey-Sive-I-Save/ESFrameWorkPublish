using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class SubTransform
    {
        public Vector3 position;
        public Vector3 eulerAngles;

        public SubTransform()
        {
            this.position = Vector3.zero;
            this.eulerAngles = Vector3.zero;
        }

        public SubTransform(Vector3 position, Vector3 eulerAngles)
        {
            this.position = position;
            this.eulerAngles = eulerAngles;
        }

    } // class end
}
