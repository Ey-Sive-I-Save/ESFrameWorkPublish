using UnityEngine;

namespace ES
{
    public class RelationMaskMapTest : MonoBehaviour
    {
        public enum Team
        {
            Red = 1,
            Blue = 2,
            Green = 3, 
            Yellow = 4,
            Purple = 5,
            Orange = 6,
            Black = 7,
            White = 8,
            Gray = 9,
            Pink = 10
        }

        [Header("配置关系遮罩 - String")]
        public RelationMaskStringMap stringMap = new RelationMaskStringMap();

        [Header("配置关系遮罩 - Int")]
        public RelationMaskIntMap intMap = new RelationMaskIntMap();

        [Header("配置关系遮罩 - Enum")]
        public RelationMaskEnumMap<Team> enumMap = new RelationMaskEnumMap<Team>();

        public bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunTest();
            }
        }

        [ContextMenu("Run RelationMask String/Int/Enum Test")]
        public void RunTest()
        {
            if (stringMap == null && intMap == null && enumMap == null)
            {
                Debug.LogWarning("RelationMaskStringMap/RelationMaskIntMap/RelationMaskEnumMap 为空");
                return;
            }

            if (stringMap != null)
            {
                stringMap.RebuildCache();
                Debug.Log($"[RelationMaskMapTest] String: Red->Blue = {stringMap.IsRelated("Red", "Blue")}");
                Debug.Log($"[RelationMaskMapTest] String: Red->Green = {stringMap.IsRelated("Red", "Green")}");
            }

            if (intMap != null)
            {
                intMap.RebuildCache();
                Debug.Log($"[RelationMaskMapTest] Int: Red->Blue = {intMap.IsRelated((int)Team.Red, (int)Team.Blue)}");
                Debug.Log($"[RelationMaskMapTest] Int: Red->Green = {intMap.IsRelated((int)Team.Red, (int)Team.Green)}");
            }

            if (enumMap != null)
            {
                enumMap.RebuildCache();
                Debug.Log($"[RelationMaskMapTest] Enum: Red->Blue = {enumMap.IsRelated(Team.Red, Team.Blue)}");
                Debug.Log($"[RelationMaskMapTest] Enum: Red->Green = {enumMap.IsRelated(Team.Red, Team.Green)}");
            }
        }
    }
}
