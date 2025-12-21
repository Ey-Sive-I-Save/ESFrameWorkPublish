using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class Bool2
    {
        [SerializeField]
        public bool x;
        
        [SerializeField]
        public bool y;

        public Bool2()
        {
            x = false;
            y = false;
        }

        public Bool2(bool x, bool y)
        {
            this.x = x;
            this.y = y;
        }

        public bool LogicOutput(LogicGate logicType)
        {
            bool[] inputs = new[] { x, y };
            return GgMaths.LogicGateOutputValue(inputs, logicType);
        }

        public void None()
        {
            x = false;
            y = false;
        }

        public void All()
        {
            x = true;
            y = true;
        }

    } // class end
}