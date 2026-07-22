using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSODataInfoWindow
    {
            private const string PrefKeySelectedPackType = "ES.SODataInfoWindow.SelectedPackType";
            private const string PrefKeySelectedGroupType = "ES.SODataInfoWindow.SelectedGroupType";
            private const string PrefKeySelectedNormalCategory = "ES.SODataInfoWindow.SelectedNormalCategory";
            private const string PrefKeySelectedNormalType = "ES.SODataInfoWindow.SelectedNormalType";
            private const string PrefKeyLastEditingGroupGuid = "ES.SODataInfoWindow.LastEditingGroupGuid";

            private string lastEditingGroupGuid_ = "";

            public override void ES_LoadData()
            {
                selectPackTypeName_ = EditorPrefs.GetString(PrefKeySelectedPackType, selectPackTypeName_);
                selectGroupTypeName_ = EditorPrefs.GetString(PrefKeySelectedGroupType, selectGroupTypeName_);
                selectNormalCategoryName_ = EditorPrefs.GetString(PrefKeySelectedNormalCategory, selectNormalCategoryName_);
                selectNormalTypeName_ = EditorPrefs.GetString(PrefKeySelectedNormalType, selectNormalTypeName_);
                lastEditingGroupGuid_ = EditorPrefs.GetString(PrefKeyLastEditingGroupGuid, lastEditingGroupGuid_);
                RestoreLastEditingGroupPage();

                if (EditorPrefs.HasKey("guidForCopiedGroup"))
                {
                    guidForCopiedGroup = EditorPrefs.GetString("guidForCopiedGroup");
                    CopyGroup = ESDesignUtility.SafeEditor.LoadAssetByGUIDString<ScriptableObject>(guidForCopiedGroup) ?? CopyGroup;
                }

                if (EditorPrefs.HasKey("guidForCopiedInfoKey"))
                    guidForCopiedInfoKey = EditorPrefs.GetString("guidForCopiedInfoKey");
            }

            public override void ES_SaveData()
            {
                EditorPrefs.SetString(PrefKeySelectedPackType, selectPackTypeName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedGroupType, selectGroupTypeName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedNormalCategory, selectNormalCategoryName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedNormalType, selectNormalTypeName_ ?? "");
                SaveLastEditingGroupGuidFromPage();
                EditorPrefs.SetString(PrefKeyLastEditingGroupGuid, lastEditingGroupGuid_ ?? "");

                if (CopyGroup != null)
                {
                    guidForCopiedGroup = ESDesignUtility.SafeEditor.GetAssetGUID(CopyGroup) ?? guidForCopiedGroup;
                    if (guidForCopiedGroup != null)
                    {
                        EditorPrefs.SetString("guidForCopiedGroup", guidForCopiedGroup);
                        EditorPrefs.SetString("guidForCopiedInfoKey", guidForCopiedInfoKey);
                    }
                }
            }

            public void SaveSelectedDataTypeNames()
            {
                EditorPrefs.SetString(PrefKeySelectedPackType, selectPackTypeName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedGroupType, selectGroupTypeName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedNormalCategory, selectNormalCategoryName_ ?? "");
                EditorPrefs.SetString(PrefKeySelectedNormalType, selectNormalTypeName_ ?? "");
                SaveLastEditingGroupGuidFromPage();
                EditorPrefs.SetString(PrefKeyLastEditingGroupGuid, lastEditingGroupGuid_ ?? "");
            }

            public void SetLastEditingGroup(ISoDataGroup group)
            {
                if (group is not ScriptableObject so)
                    return;

                lastEditingGroupGuid_ = ESDesignUtility.SafeEditor.GetAssetGUID(so) ?? lastEditingGroupGuid_;
                selectGroupTypeName_ = ESSODataWindowHelper.GetGroupName(so.GetType());
                if (pageForGroupOnChoose != null)
                    pageForGroupOnChoose.group = group;

                SaveSelectedDataTypeNames();
            }

            private void SaveLastEditingGroupGuidFromPage()
            {
                if (pageForGroupOnChoose?.group is ScriptableObject so)
                    lastEditingGroupGuid_ = ESDesignUtility.SafeEditor.GetAssetGUID(so) ?? lastEditingGroupGuid_;
            }

            private void RestoreLastEditingGroupPage()
            {
                if (pageForGroupOnChoose == null || pageForGroupOnChoose.group != null || lastEditingGroupGuid_.IsNullOrWhitespace())
                    return;

                var so = ESDesignUtility.SafeEditor.LoadAssetByGUIDString<ScriptableObject>(lastEditingGroupGuid_);
                if (so is ISoDataGroup group)
                {
                    pageForGroupOnChoose.group = group;
                    selectGroupTypeName_ = ESSODataWindowHelper.GetGroupName(so.GetType());
                }
            }

            public static string ResolveSavedSelection(string currentValue, string savedValue, IEnumerable<string> allNames)
            {
                string[] names = allNames?
                    .Where(static name => !name.IsNullOrWhitespace())
                    .ToArray();

                if (names == null || names.Length == 0)
                    return currentValue ?? "";

                if (!currentValue.IsNullOrWhitespace() && names.Contains(currentValue))
                    return currentValue;

                if (!savedValue.IsNullOrWhitespace() && names.Contains(savedValue))
                    return savedValue;

                return names[0];
            }
    }
}
