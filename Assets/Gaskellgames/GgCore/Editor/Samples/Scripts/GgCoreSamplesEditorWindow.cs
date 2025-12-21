#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class GgCoreSamplesEditorWindow : GgEditorWindow_MenuTree
    {
        #region Variables

        private const string packageRefName = "GgCore";
        private const string relativePath = "/Editor/Icons/";
        
        private Editor editor_AssetsOnly;
        private Sample_Attribute_AssetsOnly sampleAttributeAssetsOnly;
        private Editor editor_Bitfield;
        private Sample_Attribute_Bitfield sampleAttributeBitfield;
        private Editor editor_Button;
        private Sample_Attribute_Button sampleAttributeButton;
        private Editor editor_ContainsType;
        private Sample_Attribute_ContainsType sampleAttributeContainsType;
        private Editor editor_CustomCurve;
        private Sample_Attribute_CustomCurve sampleAttributeCustomCurve;
        private Editor editor_DisableIf;
        private Sample_Attribute_DisableIf sampleAttributeDisableIf;
        private Editor editor_DisableInEditMode;
        private Sample_Attribute_DisableInEditMode sampleAttributeDisableInEditMode;
        private Editor editor_DisableInPlayMode;
        private Sample_Attribute_DisableInPlayMode sampleAttributeDisableInPlayMode;
        private Editor editor_EnableIf;
        private Sample_Attribute_EnableIf sampleAttributeEnableIf;
        private Editor editor_EnumButtons;
        private Sample_Attribute_EnumButtons sampleAttributeEnumButtons;
        private Editor editor_FilePath;
        private Sample_Attribute_FilePath sampleAttributeFilePath;
        private Editor editor_FolderPath;
        private Sample_Attribute_FolderPath sampleAttributeFolderPath;
        private Editor editor_Graph;
        private Sample_Attribute_Graph sampleAttributeGraph;
        private Editor editor_GUIColor;
        private Sample_Attribute_GUIColor sampleAttributeGUIColor;
        private Editor editor_Hidden;
        private Sample_Attribute_Hidden sampleAttributeHidden;
        private Editor editor_HideIf;
        private Sample_Attribute_HideIf sampleAttributeHideIf;
        private Editor editor_HideInEditMode;
        private Sample_Attribute_HideInEditMode sampleAttributeHideInEditMode;
        private Editor editor_HideInLine;
        private Sample_Attribute_HideInLine sampleAttributeHideInLine;
        private Editor editor_HideInPlayMode;
        private Sample_Attribute_HideInPlayMode sampleAttributeHideInPlayMode;
        private Editor editor_Highlight;
        private Sample_Attribute_Highlight sampleAttributeHighlight;
        private Editor editor_Indent;
        private Sample_Attribute_Indent sampleAttributeIndent;
        private Editor editor_InfoBox;
        private Sample_Attribute_InfoBox sampleAttributeInfoBox;
        private Editor editor_InLineButton;
        private Sample_Attribute_InLineButton sampleAttributeInLineButton;
        private Editor editor_InLineEditor;
        private Sample_Attribute_InLineEditor sampleAttributeInLineEditor;
        private Editor editor_LabelText;
        private Sample_Attribute_LabelText sampleAttributeLabelText;
        private Editor editor_LabelWidth;
        private Sample_Attribute_LabelWidth sampleAttributeLabelWidth;
        private Editor editor_LineSeparator;
        private Sample_Decorator_LineSeparator sampleDecoratorLineSeparator;
        private Editor editor_Max;
        private Sample_Attribute_Max sampleAttributeMax;
        private Editor editor_Min;
        private Sample_Attribute_Min sampleAttributeMin;
        private Editor editor_MinMax;
        private Sample_Attribute_MinMax sampleAttributeMinMax;
        private Editor editor_MinMaxSlider;
        private Sample_Attribute_NavMeshMask sampleAttributeNavMeshMask;
        private Editor editor_NavMeshMask;
        private Sample_Attribute_MinMaxSlider sampleAttributeMinMaxSlider;
        private Editor editor_OnValueChanged;
        private Sample_Attribute_OnValueChanged sampleAttributeOnValueChanged;
        private Editor editor_ProgressBar;
        private Sample_Attribute_ProgressBar sampleAttributeProgressBar;
        private Editor editor_Range;
        private Sample_Attribute_Range sampleAttributeRange;
        private Editor editor_ReadOnly;
        private Sample_Attribute_ReadOnly sampleAttributeReadOnly;
        private Editor editor_Required;
        private Sample_Attribute_Required sampleAttributeRequired;
        private Editor editor_ShowAsString;
        private Sample_Attribute_ShowAsString sampleAttributeShowAsString;
        private Editor editor_ShowAsTag;
        private Sample_Attribute_ShowAsTag sampleAttributeShowAsTag;
        private Editor editor_ShowIf;
        private Sample_Attribute_ShowIf sampleAttributeShowIf;
        private Editor editor_StringDropdown;
        private Sample_Attribute_StringDropdown sampleAttributeStringDropdown;
        private Editor editor_TagDropdown;
        private Sample_Attribute_TagDropdown sampleAttributeTagDropdown;
        private Editor editor_Title;
        private Sample_Decorator_Title sampleDecoratorTitle;
        private Editor editor_ToggleLeft;
        private Sample_Attribute_ToggleLeft sampleAttributeToggleLeft;
        private Editor editor_Unit;
        private Sample_Attribute_Unit sampleAttributeUnit;
        private Editor editor_Wrap;
        private Sample_Attribute_Wrap sampleAttributeWrap;
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Menu Item
        
        [MenuItem(MenuItemUtility.GgCoreSamples_ToolsMenu_Path, false, MenuItemUtility.GgCoreSamples_Priority)]
        private static void OpenWindow_ToolsMenu()
        {
            OpenWindow_WindowMenu();
        }

        [MenuItem(MenuItemUtility.GgCoreSamples_WindowMenu_Path, false, MenuItemUtility.GgCoreSamples_Priority)]
        public static void OpenWindow_WindowMenu()
        {
            OpenWindow<GgCoreSamplesEditorWindow>("GgCore Samples", 500, 750, false, true, Placement.SceneView);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Overriding Functions

        protected override void OnInitialise()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            banner = EditorWindowUtility.LoadInspectorBanner();
            GetAllAssetReferences();
        }

        protected override void OnFocusChange(bool hasFocus)
        {
            if (!hasFocus) { return; }
            GetAllAssetReferences();
        }

        protected override List<GgToolbarItem> LeftToolbar()
        {
            string copyright = isWindowWide ? "Copyright \u00a9 2022-2025 Gaskellgames. All rights reserved." : "\u00a9 Gaskellgames.";
            List<GgToolbarItem> leftToolbar = new List<GgToolbarItem>
            {
                new (null, new GUIContent(copyright)),
            };

            return leftToolbar;
        }

        protected override List<GgToolbarItem> RightToolbar()
        {
            bool hasVersion = GgPackageRef.TryGetVersion(packageRefName, out version);
            string versionAsString = isWindowWide
                ? hasVersion ? version.GetVersionLong() : "Version ?.?.?"
                : hasVersion ? version.GetVersionShort() : "v?.?.?";
            List<GgToolbarItem> leftToolbar = new List<GgToolbarItem>
            {
                new (null, new GUIContent(versionAsString)),
            };

            return leftToolbar;
        }

        protected override GenericMenu OptionsToolbar()
        {
            GenericMenu toolsMenu = new GenericMenu();
            toolsMenu.AddItem(new GUIContent("Gaskellgames Unity Page"), false, OnSupport_AssetStoreLink);
            toolsMenu.AddItem(new GUIContent("Gaskellgames Discord"), false, OnSupport_DiscordLink);
            toolsMenu.AddItem(new GUIContent("Gaskellgames Website"), false, OnSupport_WebsiteLink);
            return toolsMenu;
        }

        protected override GgMenuTree MenuTree()
        {
            return new GgMenuTree
            {
                header = "GgCore Samples",
                underlineColor = new Color32(223, 223, 223, 255),
                pages = new List<GgMenuTreePage>()
                {
                    new GgMenuTreePage(Page_AssetsOnly, "AssetsOnly"),
                    new GgMenuTreePage(Page_Bitfield, "Bitfield"),
                    new GgMenuTreePage(Page_Button, "Button"),
                    new GgMenuTreePage(Page_ContainsType, "ContainsType"),
                    new GgMenuTreePage(Page_CustomCurve, "CustomCurve"),
                    new GgMenuTreePage(Page_DisableIf, "DisableIf"),
                    new GgMenuTreePage(Page_DisableInEditMode, "DisableInEditMode"),
                    new GgMenuTreePage(Page_DisableInPlayMode, "DisableInPlayMode"),
                    new GgMenuTreePage(Page_EnableIf, "EnableIf"),
                    new GgMenuTreePage(Page_EnumButtons, "EnumButtons"),
                    new GgMenuTreePage(Page_FilePath, "FilePath"),
                    new GgMenuTreePage(Page_FolderPath, "FolderPath"),
                    new GgMenuTreePage(Page_Graph, "Graph"),
                    new GgMenuTreePage(Page_GUIColor, "GUIColor"),
                    new GgMenuTreePage(Page_Hidden, "Hidden"),
                    new GgMenuTreePage(Page_HideIf, "HideIf"),
                    new GgMenuTreePage(Page_HideInEditMode, "HideInEditMode"),
                    new GgMenuTreePage(Page_HideInLine, "HideInLine"),
                    new GgMenuTreePage(Page_HideInPlayMode, "HideInPlayMode"),
                    new GgMenuTreePage(Page_Highlight, "Highlight"),
                    new GgMenuTreePage(Page_Indent, "Indent"),
                    new GgMenuTreePage(Page_InfoBox, "InfoBox"),
                    new GgMenuTreePage(Page_InLineButton, "InLineButton"),
                    new GgMenuTreePage(Page_InLineEditor, "InLineEditor"),
                    new GgMenuTreePage(Page_LabelText, "LabelText"),
                    new GgMenuTreePage(Page_LabelWidth, "LabelWidth"),
                    new GgMenuTreePage(Page_LineSeparator, "LineSeparator"),
                    new GgMenuTreePage(Page_Max, "Max"),
                    new GgMenuTreePage(Page_Min, "Min"),
                    new GgMenuTreePage(Page_MinMax, "MinMax"),
                    new GgMenuTreePage(Page_MinMaxSlider, "MinMaxSlider"),
                    new GgMenuTreePage(Page_NavMeshMask, "NavMeshMask"),
                    new GgMenuTreePage(Page_OnValueChanged, "OnValueChanged"),
                    new GgMenuTreePage(Page_ProgressBar, "ProgressBar"),
                    new GgMenuTreePage(Page_Range, "Range"),
                    new GgMenuTreePage(Page_ReadOnly, "ReadOnly"),
                    new GgMenuTreePage(Page_Required, "Required"),
                    new GgMenuTreePage(Page_ShowAsString, "ShowAsString"),
                    new GgMenuTreePage(Page_ShowAsTag, "ShowAsTag"),
                    new GgMenuTreePage(Page_ShowIf, "ShowIf"),
                    new GgMenuTreePage(Page_StringDropdown, "StringDropdown"),
                    new GgMenuTreePage(Page_TagDropdown, "TagDropdown"),
                    new GgMenuTreePage(Page_Title, "Title"),
                    new GgMenuTreePage(Page_ToggleLeft, "ToggleLeft"),
                    new GgMenuTreePage(Page_Unit, "Unit"),
                    new GgMenuTreePage(Page_Wrap, "Wrap"),
                }
            };
        }
        
        protected override string GetBannerLabel()
        {
            return MenuTree().header;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Pages

        private void DrawPage(Editor scriptableObjectEditor, string header, string description, string code, bool specialCase = false)
        {
            bool defaultGui = GUI.enabled;
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            GUI.enabled = false;
            GUILayout.TextArea(description, EditorStyles.wordWrappedLabel);
            GUI.enabled = defaultGui;
            EditorExtensions.DrawInspectorLine(InspectorExtensions.backgroundSeperatorColor, 4, 0);
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.Space();
            if (specialCase) { DrawButtonExample(); }
            else { DrawScriptableObjectEditor(scriptableObjectEditor); }
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorExtensions.DrawInspectorLine(InspectorExtensions.backgroundSeperatorColor, 4, 0);
            EditorGUILayout.LabelField("Code:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            GUILayout.TextArea(code, EditorStyles.textArea);
            EditorGUILayout.Space();
        }
        
        private void Page_AssetsOnly()
        {
            string header = "AssetsOnly:";
            string description = "The [AssetsOnly] attribute limits an object field to only allow project assets to be added (e.g Prefabs). No Scene assets will be allowed.";
            string code = "\n        [SerializeField, AssetsOnly]\n        private GameObject prefab;\n\n        [field: SerializeField, AssetsOnly]\n        private GameObject Prefab { get; set; }\n";
            DrawPage(editor_AssetsOnly, header, description, code);
        }
        
        private void Page_Bitfield()
        {
            string header = "Bitfield:";
            string description = "The [Bitfield] attribute shows an integer (32Bit) as a bitfield.\n\nNOTE: This will also make the field readonly!!";
            string code = "\n        [SerializeField]\n        private int input = 0;\n        \n        [SerializeField, Bitfield, Space]\n        private int bitfield;\n\n        [field: SerializeField, Bitfield]\n        private int Bitfield { get; set; }\n\n        private void OnValidate()\n        {\n            bitfield = input;\n            Bitfield = input;\n        }\n";
            DrawPage(editor_Bitfield, header, description, code);
        }
        
        private void Page_Button()
        {
            string header = "Button:";
            string description = "The [Button] attribute adds an interactable button to the inspector, that will call the method in both edit and play modes.\n\nNOTE: Button attributes will not show up in InLineEditors!";
            string code = "\n        [Button]\n        private void MethodRow1()\n        {\n            Debug.Log(\"Method 1: Button Pressed\");\n        }\n\n        [Button(\"Row2\")]\n        private void MethodRow2a()\n        {\n            Debug.Log(\"Method 2a: Button Pressed\");\n        }\n\n        [Button(\"Row2\")]\n        private void MethodRow2b()\n        {\n            Debug.Log(\"Method 2b: Button Pressed\");\n        }\n";
            DrawPage(editor_Button, header, description, code, true);
        }
        
        private void Page_ContainsType()
        {
            string header = "ContainsType:";
            string description = "The [ContainsType] attribute only allows a reference if the reference's gameObject contains a component of specified type.";
            string code = "\n        [SerializeField, ContainsType(typeof(Sample_Attribute_ContainsType))]\n        private GameObject containsType;\n\n        [field: SerializeField, ContainsType(typeof(Sample_Attribute_ContainsType))]\n        private GameObject ContainsType { get; set; }\n";
            DrawPage(editor_ContainsType, header, description, code);
        }
        
        private void Page_CustomCurve()
        {
            string header = "CustomCurve:";
            string description = "The [CustomCurve] attribute will change the line color of an AnimationCurve.";
            string code = "\n        [SerializeField, CustomCurve]\n        private AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);\n\n        [field: SerializeField, CustomCurve(223, 179, 000, 255)]\n        private AnimationCurve CustomCurve { get; set; } = AnimationCurve.Linear(0, 0, 1, 1);\n";
            DrawPage(editor_CustomCurve, header, description, code);
        }
        
        private void Page_DisableIf()
        {
            string header = "DisableIf:";
            string description = "The [DisableIf] attribute will make the field ReadOnly if the specified conditions are met.";
            string code = "\n        [SerializeField]\n        private bool value1;\n\n        [SerializeField]\n        private bool value2;\n\n        [SerializeField, DisableIf(nameof(value1))]\n        private int disableIfValue1True;\n\n        [field: SerializeField, DisableIf(nameof(value2))]\n        private int DisableIfValue2True { get; set; }\n\n        [SerializeField, DisableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.AND)]\n        private int disableIfBothTrue;\n\n        [SerializeField, DisableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.OR)]\n        private int disableIfEitherTrue;\n";
            DrawPage(editor_DisableIf, header, description, code);
        }

        private void Page_DisableInEditMode()
        {
            string header = "DisableInEditMode:";
            string description = "The [DisableInEditMode] attribute will make the field ReadOnly if the editor is in edit mode.";
            string code = "\n        [SerializeField, DisableInEditMode]\n        private byte disableInEditMode;\n\n        [field: SerializeField, DisableInEditMode]\n        private byte DisableInEditMode { get; set; }\n";
            DrawPage(editor_DisableInEditMode, header, description, code);
        }

        private void Page_DisableInPlayMode()
        {
            string header = "DisableInPlayMode:";
            string description = "The [DisableInPlayMode] attribute will make the field ReadOnly if the editor is in play mode.";
            string code = "\n        [SerializeField, DisableInPlayMode]\n        private byte disableInPlayMode;\n\n        [field: SerializeField, DisableInPlayMode]\n        private byte DisableInPlayMode { get; set; }\n";
            DrawPage(editor_DisableInPlayMode, header, description, code);
        }

        private void Page_EnableIf()
        {
            string header = "EnableIf:";
            string description = "The [EnableIf] attribute will make the field ReadOnly if the specified conditions are not met.";
            string code = "\n        [SerializeField]\n        private bool value1;\n\n        [SerializeField]\n        private bool value2;\n\n        [SerializeField, EnableIf(nameof(value1))]\n        private int enableIfValue1True;\n\n        [field: SerializeField, EnableIf(nameof(value2))]\n        private int EnableIfValue2True { get; set; }\n\n        [SerializeField, EnableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.AND)]\n        private int enableIfBothTrue;\n\n        [SerializeField, EnableIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.OR)]\n        private int enableIfEitherTrue;\n";
            DrawPage(editor_EnableIf, header, description, code);
        }

        private void Page_EnumButtons()
        {
            string header = "EnumButtons:";
            string description = "The [EnumButtons] attribute will show an enum as toggleable buttons in the inspector. This attribute can also be used on enum flags to allow easy multi-selection.";
            string code = "\n        private enum ExampleEnum\n        {\n            One,\n            Two,\n            Three,\n            Four,\n        }\n\n        [Flags]\n        private enum ExampleEnumFlags\n        {\n            None = 0,\n            All = P1 | P2 | P3 | P4 | P5, // also supports being defined as '~0'\n\n            P1 = 1 << 0,\n            P2 = 1 << 1,\n            P3 = 1 << 2,\n            P4 = 1 << 3,\n            P5 = 1 << 4,\n        }\n\n        [Title(\"Enum Buttons\")]\n        [SerializeField, EnumButtons]\n        private ExampleEnum enumButtons;\n        \n        [Title(\"Enum Buttons (No Label)\")]\n        [SerializeField, LabelText(\"\"), EnumButtons]\n        private ExampleEnum enumButtonsNoLabel;\n\n        [Title(\"Enum Buttons: Flags\")]\n        [SerializeField, EnumButtons(true)]\n        private ExampleEnumFlags enumFlags;\n";
            DrawPage(editor_EnumButtons, header, description, code);
        }

        private void Page_FilePath()
        {
            string header = "FilePath:";
            string description = "The [FilePath] attribute opens the file browser in a pop-up window to allow selection of a file within the assets folder.";
            string code = "\n        [SerializeField, FilePath]\n        private string filePath;\n\n        [field: SerializeField, FilePath]\n        private string FilePath { get; set; }\n";
            DrawPage(editor_FilePath, header, description, code);
        }

        private void Page_FolderPath()
        {
            string header = "FolderPath:";
            string description = "The [FolderPath] attribute opens the file browser in a pop-up window to allow selection of a folder within the assets folder.";
            string code = "\n        [SerializeField, FolderPath]\n        private string folderPath;\n\n        [field: SerializeField, FolderPath]\n        private string FolderPath { get; set; }\n";
            DrawPage(editor_FolderPath, header, description, code);
        }

        private void Page_Graph()
        {
            string header = "Graph:";
            string description = "The [Graph] attribute shows a Vector2 visualised as an interactable 2d graph with sliders.";
            string code = "\n        [SerializeField, Graph]\n        private Vector2 graph;\n\n        [field:SerializeField, Graph(-10, 10, \"Custom x label\", \"Custom y label\")]\n        private Vector2 Graph2 { get; set; }\n";
            DrawPage(editor_Graph, header, description, code);
        }

        private void Page_GUIColor()
        {
            string header = "GUIColor:";
            string description = "The [GUIColor] attribute changes the color of the background, content or all.";
            string code = "\n        [SerializeField, GUIColor(223, 050, 050, 255, GUIColorAttribute.Target.All)]\n        private GameObject objectField1;\n\n        [SerializeField, GUIColor(223, 050, 050, 255, GUIColorAttribute.Target.Background)]\n        private GameObject objectField2;\n\n        [field: SerializeField, GUIColor(223, 050, 050, 255, GUIColorAttribute.Target.Content)]\n        private GameObject ObjectProperty { get; set; }\n\n\n        [SerializeField, GUIColor(050, 179, 050, 255, GUIColorAttribute.Target.All), Space]\n        private LayerMask dropdownField1;\n\n        [SerializeField, GUIColor(050, 179, 050, 255, GUIColorAttribute.Target.Background)]\n        private LayerMask dropdownField2;\n\n        [field: SerializeField, GUIColor(050, 179, 050, 255, GUIColorAttribute.Target.Content)]\n        private LayerMask DropdownProperty { get; set; }\n\n\n        [SerializeField, GUIColor(000, 179, 223, 255, GUIColorAttribute.Target.All), Space]\n        private string stringField1;\n\n        [SerializeField, GUIColor(000, 179, 223, 255, GUIColorAttribute.Target.Background)]\n        private string stringField2;\n\n        [field: SerializeField, GUIColor(000, 179, 223, 255, GUIColorAttribute.Target.Content)]\n        private string StringProperty { get; set; }\n";
            DrawPage(editor_GUIColor, header, description, code);
        }

        private void Page_Hidden()
        {
            string header = "Hidden:";
            string description = "The [Hidden] attribute will hide the property, without hiding other attributes that are applied.";
            string code = "\n        [SerializeField]\n        private byte above;\n\n        [SerializeField, Hidden]\n        private byte hidden;\n\n        [field: SerializeField, Hidden]\n        private byte Hidden { get; set; }\n\n        [SerializeField]\n        private byte below;\n";
            DrawPage(editor_Hidden, header, description, code);
        }

        private void Page_HideIf()
        {
            string header = "HideIf:";
            string description = "The [HideIf] attribute will hide a field if the specified conditions are met.";
            string code = "\n        [SerializeField]\n        private bool value1;\n\n        [SerializeField]\n        private bool value2;\n\n        [SerializeField, HideIf(nameof(value1))]\n        private int hideIfValue1True;\n\n        [field: SerializeField, HideIf(nameof(value2))]\n        private int HideIfValue2True { get; set; }\n\n        [SerializeField, HideIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.AND)]\n        private int hideIfBothTrue;\n\n        [SerializeField, HideIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.OR)]\n        private int hideIfEitherTrue;\n";
            DrawPage(editor_HideIf, header, description, code);
        }

        private void Page_HideInEditMode()
        {
            string header = "HideInEditMode:";
            string description = "The [HideInEditMode] attribute will hide the field if the editor is in edit mode.";
            string code = "\n        [SerializeField, HideInEditMode]\n        private byte hideInEditMode;\n\n        [field: SerializeField, HideInEditMode]\n        private byte HideInEditMode { get; set; }\n";
            DrawPage(editor_HideInEditMode, header, description, code);
        }

        private void Page_HideInLine()
        {
            string header = "HideInLine:";
            string description = "The [HideInLine] attribute will hide the field if the editor is being displayed via an InLineEditor.";
            string code = "\n        [SerializeField]\n        private byte notHidden;\n\n        [SerializeField, HideInLine]\n        private byte hideInLine;\n\n        [field: SerializeField, HideInLine]\n        private byte HideInLine { get; set; }\n";
            DrawPage(editor_HideInLine, header, description, code);
        }

        private void Page_HideInPlayMode()
        {
            string header = "HideInPlayMode:";
            string description = "The [HideInPlayMode] attribute will hide the field if the editor is in play mode.";
            string code = "\n        [SerializeField, HideInPlayMode]\n        private byte hideInPlayMode;\n\n        [field: SerializeField, HideInPlayMode]\n        private byte HideInPlayMode;\n";
            DrawPage(editor_HideInPlayMode, header, description, code);
        }

        private void Page_Highlight()
        {
            string header = "Highlight:";
            string description = "The [Highlight] attribute will highlight a field in the editor.";
            string code = "\n        [SerializeField, Highlight]\n        private GameObject highlight;\n\n        [field: SerializeField, Highlight(000, 179, 223, 255), Space]\n        private GameObject Highlight { get; set; }\n";
            DrawPage(editor_Highlight, header, description, code);
        }

        private void Page_Indent()
        {
            string header = "Indent:";
            string description = "The [Indent] attribute will indent a field to the specified indent level, or indent to the next level if no indent level specified.";
            string code = "\n        [SerializeField, Indent(0)]\n        private int indent0;\n\n        [SerializeField, Indent(1)]\n        private int indent1;\n\n        [field: SerializeField, Indent(2)]\n        private int Indent2 { get; set; }\n";
            DrawPage(editor_Indent, header, description, code);
        }

        private void Page_InfoBox()
        {
            string header = "InfoBox:";
            string description = "The [InfoBox] attribute adds an informative box above the attribute with a message and symbol.";
            string code = "\n        [Title(\"InfoBox Types\")]\n        [SerializeField, InfoBox(\"Example InfoBox: Type None.\", InfoMessageType.None)]\n        private byte infoBoxNone;\n\n        [SerializeField, InfoBox(\"Example InfoBox: Type Info.\", InfoMessageType.Info)]\n        private byte infoBoxInfo;\n\n        [SerializeField, InfoBox(\"Example InfoBox: Type Warning.\", InfoMessageType.Warning)]\n        private byte infoBoxWarning;\n\n        [SerializeField, InfoBox(\"Example InfoBox: Type Error.\", InfoMessageType.Error)]\n        private byte infoBoxError;\n\n        [Title(\"InfoBox Conditional\", \"Info box will show the chosen type if toggle is true.\")] [SerializeField]\n        private bool showInfoBox;\n\n        [SerializeField, Range(0, 3)]\n        private int type;\n\n        [SerializeField]\n        [InfoBox(\"Conditional InfoBox: Type None\", InfoMessageType.None, nameof(showInfoBox0))]\n        [InfoBox(\"Conditional InfoBox: Type Info\", InfoMessageType.Info, nameof(showInfoBox1))]\n        [InfoBox(\"Conditional InfoBox: Type Warning\", InfoMessageType.Warning, nameof(showInfoBox2))]\n        [InfoBox(\"Conditional InfoBox: Type Error\", InfoMessageType.Error, nameof(showInfoBox3))]\n        private byte infoBoxConditional;\n\n        [SerializeField, Hidden] private bool showInfoBox0;\n        [SerializeField, Hidden] private bool showInfoBox1;\n        [SerializeField, Hidden] private bool showInfoBox2;\n        [SerializeField, Hidden] private bool showInfoBox3;\n\n        private void OnValidate()\n        {\n            showInfoBox0 = showInfoBox && type == 0;\n            showInfoBox1 = showInfoBox && type == 1;\n            showInfoBox2 = showInfoBox && type == 2;\n            showInfoBox3 = showInfoBox && type == 3;\n        }\n";
            DrawPage(editor_InfoBox, header, description, code);
        }
        
        private void Page_InLineButton()
        {
            string header = "InLineButton:";
            string description = "The [InLineButton] attribute will display a button to the right of an inspector property, that invokes a specified method.";
            string code = "\n        [SerializeField, InLineButton(nameof(Method1))]\n        private byte inLineButton;\n\n        [field: SerializeField, InLineButton(nameof(Method2))]\n        private byte InLineButton { get; set; }\n\n        private void Method1()\n        {\n            Debug.LogFormat(\"{0} invoked.\", nameof(Method1));\n        }\n\n        private void Method2()\n        {\n            Debug.LogFormat(\"{0} invoked.\", nameof(Method2));\n        }\n";
            DrawPage(editor_InLineButton, header, description, code);
        }
        
        private void Page_InLineEditor()
        {
            string header = "InLineEditor:";
            string description = "The [InLineEditor] attribute will display the editor for a component or scriptable object, inside the inspector where it is being referenced.\n\nNOTE: InLineEditor attributes will not show up inside other InLineEditors!";
            string code = "\n        [SerializeField, InLineEditor]\n        private ScriptableObject inLineEditor;\n\n        [field: SerializeField, InLineEditor]\n        private ScriptableObject InLineEditor { get; set; }\n";
            DrawPage(editor_InLineEditor, header, description, code);
        }

        private void Page_LabelText()
        {
            string header = "LabelText:";
            string description = "The [LabelText] attribute alters the label's text for the property";
            string code = "\n        [SerializeField, LabelText(\"Label Text\")]\n        private float label;\n\n        [field: SerializeField, LabelText(\"Label Text\")]\n        private float Label { get; set; }\n";
            DrawPage(editor_LabelText, header, description, code);
        }

        private void Page_LabelWidth()
        {
            string header = "LabelWidth:";
            string description = "The [LabelWidth] attribute alters the label's width for the property";
            string code = "\n        [SerializeField, LabelWidth(250)]\n        private float overrideWidth;\n\n        [field: SerializeField, LabelWidth(250)]\n        private float OverrideWidth { get; set; }\n";
            DrawPage(editor_LabelWidth, header, description, code);
        }

        private void Page_LineSeparator()
        {
            string header = "LineSeparator:";
            string description = "The [LineSeparator] attribute adds a line with a given space above and below.";
            string code = "\n        [SerializeField, LineSeparator]\n        private float lineSeparator;\n\n        [LineSeparator(true, false)]\n        [SerializeField, LineSeparator]\n        private float lineSeparator2;\n\n        [SerializeField, LineSeparator(000, 028, 045, 255)]\n        private float lineSeparator4;\n\n        [field: SerializeField, LineSeparator(5)]\n        private float lineSeparator5 { get; set; }\n";
            DrawPage(editor_LineSeparator, header, description, code);
        }

        private void Page_Max()
        {
            string header = "Max:";
            string description = "The [Max] attribute limits the inspector input to a maximum value.";
            string code = "\n        [SerializeField, Max(10)]\n        private int max;\n\n        [field: SerializeField, Max(10)]\n        private int Max { get; set; }\n";
            DrawPage(editor_Max, header, description, code);
        }

        private void Page_Min()
        {
            string header = "Min:";
            string description = "The [Min] attribute limits the inspector input to a maximum value.";
            string code = "\n        [SerializeField, Min(0)]\n        private int min;\n\n        [field: SerializeField, Min(0)]\n        private int Min { get; set; }\n";
            DrawPage(editor_Min, header, description, code);
        }

        private void Page_MinMax()
        {
            string header = "MinMax:";
            string description = "The [MinMax] attribute limits the inspector input to a minimum and maximum value.";
            string code = "\n        [SerializeField, MinMax(0, 10)]\n        private int minMax;\n\n        [field: SerializeField, MinMax(0, 10)]\n        private int MinMax { get; set; }\n";
            DrawPage(editor_MinMax, header, description, code);
        }

        private void Page_MinMaxSlider()
        {
            string header = "MinMaxSlider:";
            string description = "The [MinMaxSlider] attribute shows a Vector2 as a slider, where x is the min and y is the max value.";
            string code = "\n        [SerializeField, MinMaxSlider(0, 1)]\n        private Vector2 rangeMinMax = new Vector2(0.25f, 0.5f);\n\n        [SerializeField, MinMaxSlider(0, 1, \"Label 1\", \"Label 2\")]\n        private Vector2 rangeMinMax2 = new Vector2(0.25f, 0.75f);\n\n        [field: SerializeField, MinMaxSlider(0, 1, true)]\n        private Vector2 rangeMinMax3 { get; set; } = new Vector2(0.5f, 0.75f);\n";
            DrawPage(editor_MinMaxSlider, header, description, code);
        }

        private void Page_NavMeshMask()
        {
            string header = "NavMeshMask:";
            string description = "The [NavMeshMask] attribute shows an int as a mask field, where the values of the mask are the NavMeshAreas.";
            string code = "\n        [SerializeField, NavMeshMask]\n        private int navMeshMask;\n\n        [field: SerializeField, NavMeshMask]\n        private int NavMeshMask { get; set; }\n";
            DrawPage(editor_NavMeshMask, header, description, code);
        }

        private void Page_OnValueChanged()
        {
            string header = "OnValueChanged:";
            string description = "The [OnValueChanged] attribute invokes a method when the property changes value. This will only be applied when the property value is changed directly from the inspector. OnValueChanged allows method calls that OnValidate does not allow, such as DestroyImmediate().";
            string code = "\n        [SerializeField, OnValueChanged(nameof(ExampleMethod))]\n        private int onValueChanged;\n\n        [field: SerializeField, OnValueChanged(nameof(ExampleMethod))]\n        private int OnValueChanged { get; set; }\n\n        private void ExampleMethod()\n        {\n            Debug.Log($\"ExampleMethod called by {name}\");\n        }\n";
            DrawPage(editor_OnValueChanged, header, description, code);
        }

        private void Page_ProgressBar()
        {
            string header = "ProgressBar:";
            string description = "The [ProgressBar] attribute shows a float or int as an interactable slider.";
            string code = "\n        [SerializeField, ProgressBar]\n        private float progressBar1 = 10;\n\n        [SerializeField, ProgressBar(true)]\n        private float progressBar2 = 20;\n\n        [SerializeField, ProgressBar(200)]\n        private float progressBar3 = 60;\n\n        [SerializeField, ProgressBar(\"Mana\")]\n        private int progressBar4 = 70;\n\n        [SerializeField, ProgressBar(050, 179, 050, 255)]\n        private int progressBar5 = 80;\n\n        [field: SerializeField, ProgressBar(200, \"Health\", 223, 050, 050, 255)]\n        private int progressBar6 { get; set; } = 180;\n";
            DrawPage(editor_ProgressBar, header, description, code);
        }

        private void Page_Range()
        {
            string header = "Range:";
            string description = "The [Range] attribute is used to restrict a float or int variable to a specific range.";
            string code = "\n        [SerializeField, Range(0, 1)]\n        private float range;\n\n        [SerializeField, Range(0, 1, true)]\n        private float range1;\n\n        [field: SerializeField, Range(0, 1, \"Label 1\", \"Label 2\")]\n        private float Range2 { get; set; }\n";
            DrawPage(editor_Range, header, description, code);
        }

        private void Page_ReadOnly()
        {
            string header = "ReadOnly:";
            string description = "The [ReadOnly] attribute as readable but not interactable.";
            string code = "\n        [SerializeField, ReadOnly]\n        private float readOnly;\n\n        [field: SerializeField, ReadOnly]\n        private GameObject ReadOnly { get; set; }\n";
            DrawPage(editor_ReadOnly, header, description, code);
        }

        private void Page_Required()
        {
            string header = "Required:";
            string description = "The [Required] attribute will show a warning in the inspector if the value is null.";
            string code = "\n        [SerializeField, Required]\n        private GameObject required;\n\n        [field: SerializeField, Required(000, 179, 223, 255)]\n        private GameObject Required { get; set; }\n";
            DrawPage(editor_Required, header, description, code);
        }

        private void Page_ShowAsString()
        {
            string header = "ShowAsString:";
            string description = "The [ShowAsString] attribute is used to show a field value as a label (readable but not interactable).";
            string code = "\n        [SerializeField, ShowAsString]\n        private Color32 colour = new Color32(000, 179, 223, 255);\n\n        [field: SerializeField, ShowAsString]\n        private Vector2 Vector2 { get; set; } = new Vector2(0, 0);\n";
            DrawPage(editor_ShowAsString, header, description, code);
        }

        private void Page_ShowAsTag()
        {
            string header = "ShowAsTag:";
            string description = "The [ShowAsTag] attribute is used to show a string value as a tag (readable but not interactable).";
            string code = "\n        [SerializeField]\n        private string stringValue = \"TagValue\";\n\n        [SerializeField, ShowAsTag, Space]\n        private string showAsTag;\n\n        private void OnValidate()\n        {\n            showAsTag = stringValue;\n        }\n";
            DrawPage(editor_ShowAsTag, header, description, code);
        }

        private void Page_ShowIf()
        {
            string header = "ShowIf:";
            string description = "The [ShowIf] attribute will hide a field if the specified conditions are not met.";
            string code = "\n        [SerializeField]\n        private bool value1;\n\n        [SerializeField]\n        private bool value2;\n\n        [SerializeField, ShowIf(nameof(value1))]\n        private int showIfValue1True;\n\n        [field: SerializeField, ShowIf(nameof(value2))]\n        private int ShowIfValue2True { get; set; }\n\n        [SerializeField, ShowIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.AND)]\n        private int showIfBothTrue;\n\n        [SerializeField, ShowIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, GgMaths.LogicGate.OR)]\n        private int showIfEitherTrue;\n";
            DrawPage(editor_ShowIf, header, description, code);
        }

        private void Page_StringDropdown()
        {
            string header = "StringDropdown:";
            string description = "The [StringDropdown] attribute is used to provide default values for a string field.";
            string code = "\n        [SerializeField, StringDropdown(\"Option1\", \"Option2\", \"Option3\")]\n        private string stringDropdown;\n\n        [field: SerializeField, StringDropdown(\"Option1\", \"Option2\", \"Option3\")]\n        private string StringDropdown { get; set; }\n";
            DrawPage(editor_StringDropdown, header, description, code);
        }

        private void Page_TagDropdown()
        {
            string header = "TagDropdown:";
            string description = "The [TagDropdown] attribute is used to select a tag from the gameObject tags list.";
            string code = "\n        [SerializeField, TagDropdown]\n        private string tagDropdown = \"\";\n\n        [field: SerializeField, TagDropdown]\n        private string TagDropdown { get; set; } = \"\";\n";
            DrawPage(editor_TagDropdown, header, description, code);
        }

        private void Page_Title()
        {
            string header = "Title:";
            string description = "The [Title] attribute adds an underlined header and/or subheader above a field.";
            string code = "\n        [SerializeField]\n        private GameObject placeholder01;\n\n        [SerializeField]\n        private GameObject placeholder02;\n\n        [SerializeField, Title(\"Heading\", \"SubHeading\")]\n        private GameObject placeholder03;\n\n        [SerializeField]\n        private GameObject placeholder04;\n\n        [SerializeField, Title(\"Heading\")]\n        private GameObject placeholder05;\n\n        [SerializeField]\n        private GameObject placeholder06;\n\n        [SerializeField, Title(\"\", \"SubHeading\")]\n        private GameObject placeholder07;\n\n        [SerializeField]\n        private GameObject placeholder08;\n\n        [field: SerializeField, Title]\n        private GameObject placeholder09 { get; set; }\n\n        [field: SerializeField]\n        private GameObject placeholder10 { get; set; }\n";
            DrawPage(editor_Title, header, description, code);
        }

        private void Page_ToggleLeft()
        {
            string header = "ToggleLeft:";
            string description = "The [ToggleLeft] attribute shows a toggle with the label on the right.";
            string code = "\n        [SerializeField, ToggleLeft]\n        private bool toggleLeft;\n\n        [field: SerializeField, ToggleLeft]\n        private bool ToggleLeft { get; set; }\n";
            DrawPage(editor_ToggleLeft, header, description, code);
        }

        private void Page_Unit()
        {
            string header = "Unit:";
            string description = "The [Unit] attribute adds units to fields as a suffix.";
            string code = "        [SerializeField, Unit(Units.Seconds)]\n        private int unit;\n\n        [field: SerializeField, Unit(Units.Percentage)]\n        private int Unit { get; set; }\n";
            DrawPage(editor_Unit, header, description, code);
        }

        private void Page_Wrap()
        {
            string header = "Wrap:";
            string description = "The [Wrap] attribute will automatically wrap a value if above/below a specified range.";
            string code = "\n        [SerializeField, Wrap(0, 360)]\n        private int wrap;\n\n        [field: SerializeField, Wrap(0, 360)]\n        private int Wrap { get; set; }\n";
            DrawPage(editor_Wrap, header, description, code);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private void DrawButtonExample()
        {
            if (GUILayout.Button("MethodRow1")) { Log(GgLogType.Info, "Method 1: Button Pressed"); }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("MethodRow2a")) { Log(GgLogType.Info, "Method 2a: Button Pressed"); }
            if (GUILayout.Button("MethodRow2b")) { Log(GgLogType.Info, "Method 2b: Button Pressed"); }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawScriptableObjectEditor(Editor scriptableObjectEditor)
        {
            if (scriptableObjectEditor && scriptableObjectEditor.target)
            {
                scriptableObjectEditor.OnInspectorGUI(true, true);
            }
            else { EditorGUILayout.HelpBox("Error: Sample asset not found.", MessageType.Error); }
        }
        
        private void GetAllAssetReferences()
        {
            sampleAttributeAssetsOnly = EditorExtensions.GetAssetByType<Sample_Attribute_AssetsOnly>();
            editor_AssetsOnly = sampleAttributeAssetsOnly ? Editor.CreateEditor(sampleAttributeAssetsOnly) : null;
            
            sampleAttributeBitfield = EditorExtensions.GetAssetByType<Sample_Attribute_Bitfield>();
            editor_Bitfield = sampleAttributeBitfield ? Editor.CreateEditor(sampleAttributeBitfield) : null;
            
            sampleAttributeButton = EditorExtensions.GetAssetByType<Sample_Attribute_Button>();
            editor_Button = sampleAttributeButton ? Editor.CreateEditor(sampleAttributeButton) : null;

            sampleAttributeContainsType = EditorExtensions.GetAssetByType<Sample_Attribute_ContainsType>();
            editor_ContainsType = sampleAttributeContainsType ? Editor.CreateEditor(sampleAttributeContainsType) : null;

            sampleAttributeCustomCurve = EditorExtensions.GetAssetByType<Sample_Attribute_CustomCurve>();
            editor_CustomCurve = sampleAttributeCustomCurve ? Editor.CreateEditor(sampleAttributeCustomCurve) : null;
            
            sampleAttributeDisableIf = EditorExtensions.GetAssetByType<Sample_Attribute_DisableIf>();
            editor_DisableIf = sampleAttributeDisableIf ? Editor.CreateEditor(sampleAttributeDisableIf) : null;
            
            sampleAttributeDisableInEditMode = EditorExtensions.GetAssetByType<Sample_Attribute_DisableInEditMode>();
            editor_DisableInEditMode = sampleAttributeDisableInEditMode ? Editor.CreateEditor(sampleAttributeDisableInEditMode) : null;
            
            sampleAttributeDisableInPlayMode = EditorExtensions.GetAssetByType<Sample_Attribute_DisableInPlayMode>();
            editor_DisableInPlayMode = sampleAttributeDisableInPlayMode ? Editor.CreateEditor(sampleAttributeDisableInPlayMode) : null;
            
            sampleAttributeEnableIf = EditorExtensions.GetAssetByType<Sample_Attribute_EnableIf>();
            editor_EnableIf = sampleAttributeEnableIf ? Editor.CreateEditor(sampleAttributeEnableIf) : null;
            
            sampleAttributeEnumButtons = EditorExtensions.GetAssetByType<Sample_Attribute_EnumButtons>();
            editor_EnumButtons = sampleAttributeEnumButtons ? Editor.CreateEditor(sampleAttributeEnumButtons) : null;
            
            sampleAttributeFilePath = EditorExtensions.GetAssetByType<Sample_Attribute_FilePath>();
            editor_FilePath = sampleAttributeFilePath ? Editor.CreateEditor(sampleAttributeFilePath) : null;
            
            sampleAttributeFolderPath = EditorExtensions.GetAssetByType<Sample_Attribute_FolderPath>();
            editor_FolderPath = sampleAttributeFolderPath ? Editor.CreateEditor(sampleAttributeFolderPath) : null;
            
            sampleAttributeGraph = EditorExtensions.GetAssetByType<Sample_Attribute_Graph>();
            editor_Graph = sampleAttributeGraph ? Editor.CreateEditor(sampleAttributeGraph) : null;
            
            sampleAttributeGUIColor = EditorExtensions.GetAssetByType<Sample_Attribute_GUIColor>();
            editor_GUIColor = sampleAttributeGUIColor ? Editor.CreateEditor(sampleAttributeGUIColor) : null;
            
            sampleAttributeHidden = EditorExtensions.GetAssetByType<Sample_Attribute_Hidden>();
            editor_Hidden = sampleAttributeHidden ? Editor.CreateEditor(sampleAttributeHidden) : null;
            
            sampleAttributeHideIf = EditorExtensions.GetAssetByType<Sample_Attribute_HideIf>();
            editor_HideIf = sampleAttributeHideIf ? Editor.CreateEditor(sampleAttributeHideIf) : null;
            
            sampleAttributeHideInEditMode = EditorExtensions.GetAssetByType<Sample_Attribute_HideInEditMode>();
            editor_HideInEditMode = sampleAttributeHideInEditMode ? Editor.CreateEditor(sampleAttributeHideInEditMode) : null;
            
            sampleAttributeHideInLine = EditorExtensions.GetAssetByType<Sample_Attribute_HideInLine>();
            editor_HideInLine = sampleAttributeHideInLine ? Editor.CreateEditor(sampleAttributeHideInLine) : null;
            
            sampleAttributeHideInPlayMode = EditorExtensions.GetAssetByType<Sample_Attribute_HideInPlayMode>();
            editor_HideInPlayMode = sampleAttributeHideInPlayMode ? Editor.CreateEditor(sampleAttributeHideInPlayMode) : null;
            
            sampleAttributeHighlight = EditorExtensions.GetAssetByType<Sample_Attribute_Highlight>();
            editor_Highlight = sampleAttributeHighlight ? Editor.CreateEditor(sampleAttributeHighlight) : null;
            
            sampleAttributeIndent = EditorExtensions.GetAssetByType<Sample_Attribute_Indent>();
            editor_Indent = sampleAttributeIndent ? Editor.CreateEditor(sampleAttributeIndent) : null;
            
            sampleAttributeInfoBox = EditorExtensions.GetAssetByType<Sample_Attribute_InfoBox>();
            editor_InfoBox = sampleAttributeInfoBox ? Editor.CreateEditor(sampleAttributeInfoBox) : null;
            
            sampleAttributeInLineButton = EditorExtensions.GetAssetByType<Sample_Attribute_InLineButton>();
            editor_InLineButton = sampleAttributeInLineButton ? Editor.CreateEditor(sampleAttributeInLineButton) : null;
            
            sampleAttributeInLineEditor = EditorExtensions.GetAssetByType<Sample_Attribute_InLineEditor>();
            editor_InLineEditor = sampleAttributeInLineEditor ? Editor.CreateEditor(sampleAttributeInLineEditor) : null;
            
            sampleAttributeLabelText = EditorExtensions.GetAssetByType<Sample_Attribute_LabelText>();
            editor_LabelText = sampleAttributeLabelText ? Editor.CreateEditor(sampleAttributeLabelText) : null;
            
            sampleAttributeLabelWidth = EditorExtensions.GetAssetByType<Sample_Attribute_LabelWidth>();
            editor_LabelWidth = sampleAttributeLabelWidth ? Editor.CreateEditor(sampleAttributeLabelWidth) : null;
            
            sampleDecoratorLineSeparator = EditorExtensions.GetAssetByType<Sample_Decorator_LineSeparator>();
            editor_LineSeparator = sampleDecoratorLineSeparator ? Editor.CreateEditor(sampleDecoratorLineSeparator) : null;
            
            sampleAttributeMax = EditorExtensions.GetAssetByType<Sample_Attribute_Max>();
            editor_Max = sampleAttributeMax ? Editor.CreateEditor(sampleAttributeMax) : null;
            
            sampleAttributeMin = EditorExtensions.GetAssetByType<Sample_Attribute_Min>();
            editor_Min = sampleAttributeMin ? Editor.CreateEditor(sampleAttributeMin) : null;
            
            sampleAttributeMinMax = EditorExtensions.GetAssetByType<Sample_Attribute_MinMax>();
            editor_MinMax = sampleAttributeMinMax ? Editor.CreateEditor(sampleAttributeMinMax) : null;
            
            sampleAttributeMinMaxSlider = EditorExtensions.GetAssetByType<Sample_Attribute_MinMaxSlider>();
            editor_MinMaxSlider = sampleAttributeMinMaxSlider ? Editor.CreateEditor(sampleAttributeMinMaxSlider) : null;
            
            sampleAttributeNavMeshMask = EditorExtensions.GetAssetByType<Sample_Attribute_NavMeshMask>();
            editor_NavMeshMask = sampleAttributeNavMeshMask ? Editor.CreateEditor(sampleAttributeNavMeshMask) : null;
            
            sampleAttributeOnValueChanged = EditorExtensions.GetAssetByType<Sample_Attribute_OnValueChanged>();
            editor_OnValueChanged = sampleAttributeOnValueChanged ? Editor.CreateEditor(sampleAttributeOnValueChanged) : null;
            
            sampleAttributeProgressBar = EditorExtensions.GetAssetByType<Sample_Attribute_ProgressBar>();
            editor_ProgressBar = sampleAttributeProgressBar ? Editor.CreateEditor(sampleAttributeProgressBar) : null;
            
            sampleAttributeRange = EditorExtensions.GetAssetByType<Sample_Attribute_Range>();
            editor_Range = sampleAttributeRange ? Editor.CreateEditor(sampleAttributeRange) : null;
            
            sampleAttributeReadOnly = EditorExtensions.GetAssetByType<Sample_Attribute_ReadOnly>();
            editor_ReadOnly = sampleAttributeReadOnly ? Editor.CreateEditor(sampleAttributeReadOnly) : null;
            
            sampleAttributeRequired = EditorExtensions.GetAssetByType<Sample_Attribute_Required>();
            editor_Required = sampleAttributeRequired ? Editor.CreateEditor(sampleAttributeRequired) : null;
            
            sampleAttributeShowAsString = EditorExtensions.GetAssetByType<Sample_Attribute_ShowAsString>();
            editor_ShowAsString = sampleAttributeShowAsString ? Editor.CreateEditor(sampleAttributeShowAsString) : null;
            
            sampleAttributeShowAsTag = EditorExtensions.GetAssetByType<Sample_Attribute_ShowAsTag>();
            editor_ShowAsTag = sampleAttributeShowAsTag ? Editor.CreateEditor(sampleAttributeShowAsTag) : null;
            
            sampleAttributeShowIf = EditorExtensions.GetAssetByType<Sample_Attribute_ShowIf>();
            editor_ShowIf = sampleAttributeShowIf ? Editor.CreateEditor(sampleAttributeShowIf) : null;
            
            sampleAttributeStringDropdown = EditorExtensions.GetAssetByType<Sample_Attribute_StringDropdown>();
            editor_StringDropdown = sampleAttributeStringDropdown ? Editor.CreateEditor(sampleAttributeStringDropdown) : null;
            
            sampleAttributeTagDropdown = EditorExtensions.GetAssetByType<Sample_Attribute_TagDropdown>();
            editor_TagDropdown = sampleAttributeTagDropdown ? Editor.CreateEditor(sampleAttributeTagDropdown) : null;
            
            sampleDecoratorTitle = EditorExtensions.GetAssetByType<Sample_Decorator_Title>();
            editor_Title = sampleDecoratorTitle ? Editor.CreateEditor(sampleDecoratorTitle) : null;
            
            sampleAttributeToggleLeft = EditorExtensions.GetAssetByType<Sample_Attribute_ToggleLeft>();
            editor_ToggleLeft = sampleAttributeToggleLeft ? Editor.CreateEditor(sampleAttributeToggleLeft) : null;
            
            sampleAttributeUnit = EditorExtensions.GetAssetByType<Sample_Attribute_Unit>();
            editor_Unit = sampleAttributeUnit ? Editor.CreateEditor(sampleAttributeUnit) : null;
            
            sampleAttributeWrap = EditorExtensions.GetAssetByType<Sample_Attribute_Wrap>();
            editor_Wrap = sampleAttributeWrap ? Editor.CreateEditor(sampleAttributeWrap) : null;
        }

        #endregion
        
    } // class end
}
#endif