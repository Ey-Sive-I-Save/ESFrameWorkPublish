using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_Password : MonoBehaviour
    {
        // ---------- Password ----------

        [SerializeField]
        private Password password = new Password("StrongPassword");

        [SerializeField, ReadOnly, Space]
        private string passwordDebug;

        private void OnDrawGizmos()
        {
            RemoveConsoleWarning();
        }

        private void RemoveConsoleWarning()
        {
            passwordDebug = password.value;
        }

    } // class end
}