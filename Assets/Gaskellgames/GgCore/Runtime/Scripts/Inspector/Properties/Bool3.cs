using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class Bool3
    {
        [SerializeField]
        public bool x;
        
        [SerializeField]
        public bool y;
        
        [SerializeField]
        public bool z;

        public Bool3()
        {
            this.x = false;
            this.y = false;
            this.z = false;
        }

        public Bool3(bool x, bool y, bool z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool LogicOutput(LogicGate logicType)
        {
            bool[] inputs = new[] { x, y, z };
            return GgMaths.LogicGateOutputValue(inputs, logicType);
        }

        public void None()
        {
            this.x = false;
            this.y = false;
            this.z = false;
        }

        public void All()
        {
            this.x = true;
            this.y = true;
            this.z = true;
        }

    } // class end
}