using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_GGEvent : MonoBehaviour
    {
        // ---------- GGEvent ----------

        [SerializeField]
        public GgEvent zeroArgsEvent;

        [SerializeField]
        public GgEvent<int> singleArgsEvent;

        [SerializeField]
        public GgEvent<int, int> doubleArgsEvent;

        [SerializeField]
        public GgEvent<int, int, int> tripleArgsEvent;

    } // class end
}
