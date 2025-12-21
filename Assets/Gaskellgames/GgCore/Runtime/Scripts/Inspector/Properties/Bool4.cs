using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class Bool4
    {
        [SerializeField]
        public bool x;
        
        [SerializeField]
        public bool y;
        
        [SerializeField]
        public bool z;
        
        [SerializeField]
        public bool w;

        public Bool4()
        {
            this.x = false;
            this.y = false;
            this.z = false;
            this.w = false;
        }

        public Bool4(bool x, bool y, bool z, bool w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public bool LogicOutput(LogicGate logicType)
        {
            bool[] inputs = new[] { x, y, z, w };
            return GgMaths.LogicGateOutputValue(inputs, logicType);
        }

        public void None()
        {
            this.x = false;
            this.y = false;
            this.z = false;
            this.w = false;
        }

        public void All()
        {
            this.x = true;
            this.y = true;
            this.z = true;
            this.w = true;
        }

    } // class end
}