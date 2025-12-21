using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class Password
    {
        [SerializeField] private string password;
        
        public string value
        {
            get { return password; }
            set { password = value; }
        }

        public Password(string newPassword)
        {
            this.value = newPassword;
        }

    } // class end
}