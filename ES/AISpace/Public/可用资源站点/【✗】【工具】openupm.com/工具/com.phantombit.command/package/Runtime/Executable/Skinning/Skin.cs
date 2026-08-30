using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Skinning
{
	public class Skin : ScriptableObject
	{
		#region Fields
		[SerializeField]
		private GUISkin _guiSkin;
		[SerializeField]
		private GUIStyle _consoleTextField;
		[SerializeField]
		private GUIStyle _popupHeaderStyle;
		[SerializeField]
		private GUIStyle _popupWidowStyle;
		[SerializeField]
		private GUIStyle _scrollViewStyle;
		[SerializeField]
		private GUIStyle _consoleItemBorderlessLabelStyle;
		[SerializeField]
		private GUIStyle _fullSizeBorderlessLabelStyle;
		[SerializeField]
		private GUIStyle _tooltipNormalStyle;
		[SerializeField]
		private GUIStyle _tooltipHighlightedStyle;
		[SerializeField]
		private GUIStyle _tooltipOptionBackgroundStyle;
		[SerializeField]
		private GUIStyle _tooltipMoreBackgroundStyle;
		[SerializeField]
		private GUIStyle _consoleWindowBackgroundStyle;
		[SerializeField]
		private GUIStyle _tooltipLabelBackground;
		[SerializeField]
		private GUIStyle _headerStyle;
		[SerializeField]
		private GUIStyle _itemAlternateBackgroundStyle;
		[SerializeField]
		private GUIStyle _highlightedItemBackgroundStyle;
		[SerializeField]
		private GUIStyle _suggestionButtonBackgroundStyle;
		[SerializeField]
		private GUIStyle _suggestionButtonMoreBackgroundStyle;
		[SerializeField]
		private int _consoleRowTextHeight;
		[SerializeField]
		private int _consoleRowHeight;
		[SerializeField]
		private int _consoleRowTextLeftMargin;
		#endregion

		#region Properties
		public GUISkin GuiSkin
		{
			get => _guiSkin;
			set { _guiSkin = value; }
		}

		public GUIStyle ConsoleTextField
		{
			get => _consoleTextField;
			set { _consoleTextField = value; }
		}
		
		public GUIStyle PopupHeaderStyle
		{
			get => _popupHeaderStyle;
			set { _popupHeaderStyle = value; }
		}
		
		public GUIStyle PopupWidowStyle
		{
			get => _popupWidowStyle;
			set { _popupWidowStyle = value; }
		}
		
		public GUIStyle ScrollViewStyle
		{
			get => _scrollViewStyle;
			set { _scrollViewStyle = value; }
		}
		
		public GUIStyle ConsoleItemBorderlessLabelStyle
		{
			get => _consoleItemBorderlessLabelStyle;
			set { _consoleItemBorderlessLabelStyle = value; }
		}
		
		public GUIStyle FullSizeBorderlessLabelStyle
		{
			get => _fullSizeBorderlessLabelStyle;
			set { _fullSizeBorderlessLabelStyle = value; }
		}
		
		public GUIStyle TooltipNormalStyle
		{
			get => _tooltipNormalStyle;
			set { _tooltipNormalStyle = value; }
		}
		
		public GUIStyle TooltipHighlightedStyle
		{
			get => _tooltipHighlightedStyle;
			set { _tooltipHighlightedStyle = value; }
		}
		
		public GUIStyle TooltipOptionBackgroundStyle
		{
			get => _tooltipOptionBackgroundStyle;
			set { _tooltipOptionBackgroundStyle = value; }
		}
		
		public GUIStyle TooltipMoreBackgroundStyle
		{
			get => _tooltipMoreBackgroundStyle;
			set { _tooltipMoreBackgroundStyle = value; }
		}
		
		public GUIStyle ConsoleWindowBackgroundStyle
		{
			get => _consoleWindowBackgroundStyle;
			set { _consoleWindowBackgroundStyle = value; }
		}
		
		public GUIStyle TooltipLabelBackground
		{
			get => _tooltipLabelBackground;
			set { _tooltipLabelBackground = value; }
		}
		
		public GUIStyle HeaderStyle
		{
			get => _headerStyle;
			set { _headerStyle = value; }
		}
		
		public GUIStyle ItemAlternateBackgroundStyle
		{
			get => _itemAlternateBackgroundStyle;
			set { _itemAlternateBackgroundStyle = value; }
		}
		
		public GUIStyle HighlightedItemBackgroundStyle
		{
			get => _highlightedItemBackgroundStyle;
			set { _highlightedItemBackgroundStyle = value; }
		}

		public GUIStyle SuggestionButtonBackgroundStyle
		{
			get => _suggestionButtonBackgroundStyle;
			set { _suggestionButtonBackgroundStyle = value; }
		}

		public GUIStyle SuggestionButtonMoreBackgroundStyle
		{
			get => _suggestionButtonMoreBackgroundStyle;
			set { _suggestionButtonMoreBackgroundStyle = value; }
		}
		
		public int ConsoleRowTextHeight
		{
			get => _consoleRowTextHeight;
			set { _consoleRowTextHeight = value; }
		}
		
		public int ConsoleRowHeight
		{
			get => _consoleRowHeight;
			set { _consoleRowHeight = value; }
		}
		
		public int ConsoleRowTextLeftMargin
		{
			get => _consoleRowTextLeftMargin;
			set { _consoleRowTextLeftMargin = value; }
		}
		#endregion
	}
}