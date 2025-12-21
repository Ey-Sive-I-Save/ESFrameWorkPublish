using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_Bool2 : MonoBehaviour
    {
        // ---------- Bool2 ----------

        [SerializeField]
        private Bool2 bool2;

        [SerializeField, Space]
        private LogicGate logicType = LogicGate.AND;

        [SerializeField]
        private bool output;

        private void OnValidate()
        {
            bool2 ??= new Bool2();
            output = bool2.LogicOutput(logicType);
        }

    } // class end
}