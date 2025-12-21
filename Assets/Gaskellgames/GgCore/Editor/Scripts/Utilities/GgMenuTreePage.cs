#if UNITY_EDITOR

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class GgMenuTreePage
    {
        public delegate void MethodDelegate(); // defines what type of method you're going to call.
        
        public string pageName;
        public MethodDelegate drawPageMethod;
        
        public GgMenuTreePage()
        {
            this.pageName = "";
            this.drawPageMethod = null;
        }
        
        public GgMenuTreePage(MethodDelegate drawPageMethod)
        {
            this.pageName = drawPageMethod.Method.Name.NicifyName();
            this.drawPageMethod = drawPageMethod;
        }
        
        public GgMenuTreePage(MethodDelegate drawPageMethod, string pageName)
        {
            this.pageName = pageName;
            this.drawPageMethod = drawPageMethod;
        }
        
    } // class end
}

#endif