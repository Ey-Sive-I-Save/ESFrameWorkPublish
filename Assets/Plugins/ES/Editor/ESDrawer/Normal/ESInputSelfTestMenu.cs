using ES;
using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    public static class ESInputSelfTestMenu
    {
        [MenuItem(MenuItemPathDefine.DEBUG_PATH + "Input/运行输入底层自测", false, 9300)]
        public static void RunInputSelfTest()
        {
            try
            {
                string result = ESInputFullSelfTest.RunAll();
                Debug.Log(result);
                EditorUtility.DisplayDialog("ES 输入底层自测", result, "确定");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("ES 输入底层自测失败", e.Message, "确定");
            }
        }
    }
}
