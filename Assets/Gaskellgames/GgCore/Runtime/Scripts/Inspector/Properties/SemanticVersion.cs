using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class SemanticVersion : IComparable<SemanticVersion>
    {
        #region Variables

        public int major;
        public int minor;
        public int patch;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IComparable

        public int CompareTo(SemanticVersion otherSemanticVersion)
        {
            if (this < otherSemanticVersion)
            {
                return -1;
            }
            
            if (this > otherSemanticVersion)
            {
                return 1;
            }
            
            return 0;
        }
        
        public override bool Equals (object obj)
        {
            SemanticVersion other = obj as SemanticVersion;
            if (other == null) return false;
            return this.major == other.major && this.minor == other.minor && this.patch == other.patch;
        }
 
        public override int GetHashCode()
        {
            return new Vector3Int(major, minor, patch).GetHashCode();
        }
        
        // define the is equal to operator
        public static bool operator == (SemanticVersion versionA, SemanticVersion versionB)
        {
            if (versionA != null && versionB != null)
            {
                return versionA.CompareTo(versionB) == 0;
            }
            else if (versionA == null && versionB != null)
            {
                return false;
            }
            else if (versionA != null && versionB == null)
            {
                return false;
            }
            else 
            {
                return true;
            }
        }
        
        // define the is not equal to operator
        public static bool operator != (SemanticVersion versionA, SemanticVersion versionB)
        {
            if (versionA != null && versionB != null)
            {
                return versionA.CompareTo(versionB) < 0 || 0 < versionA.CompareTo(versionB);
            }
            else if (versionA == null && versionB != null)
            {
                return true;
            }
            else if (versionA != null && versionB == null)
            {
                return true;
            }
            else 
            {
                return false;
            }
        }
        
        // define the is less than operator
        public static bool operator < (SemanticVersion versionA, SemanticVersion versionB)
        {
            if (versionA != null && versionB != null)
            {
                if (versionA.major < versionB.major)
                {
                    return true;
                }
                if (versionA.minor < versionB.minor)
                {
                    return true;
                }
                if (versionA.patch < versionB.patch)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // define the is more than operator
        public static bool operator > (SemanticVersion versionA, SemanticVersion versionB)
        {
            if (versionA != null && versionB != null)
            {
                if (versionA.major > versionB.major)
                {
                    return true;
                }
                if (versionA.minor > versionB.minor)
                {
                    return true;
                }
                if (versionA.patch > versionB.patch)
                {
                    return true;
                }
            }
            
            return false;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Constructors

        public SemanticVersion()
        {
            this.major = 0;
            this.minor = 0;
            this.patch = 0;
        }

        public SemanticVersion(int major, int minor, int patch)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Public Functions

        /// <summary>
        /// Get the semantic version as a string in the format 'v{0}.{1}.{2}'
        /// </summary>
        /// <returns></returns>
        public string GetVersionShort()
        {
            return $"v{major}.{minor}.{patch}";
        }
        
        /// <summary>
        /// Get the semantic version as a string in the format 'Version {0}.{1}.{2}'
        /// </summary>
        /// <returns></returns>
        public string GetVersionLong()
        {
            return $"Version {major}.{minor}.{patch}";
        }

        #endregion

    } // class end
}
