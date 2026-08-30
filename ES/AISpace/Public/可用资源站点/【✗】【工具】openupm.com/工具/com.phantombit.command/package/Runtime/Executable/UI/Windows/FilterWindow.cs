using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Modals;
using WellFired.Command.Unity.Runtime.UnityGui;

namespace WellFired.Command.Unity.Runtime.UI.Windows
{
	public sealed class FilterWindow : PopupWindow
	{
		#region Fields
		private readonly Filter _filter;
		private Vector2 _scrollPosition = Vector2.zero;
		#endregion

		#region Properties
		public LogHistory LogHistory 
		{
			private get;
			set;
		}
		#endregion

		#region Methods
		public FilterWindow(ISkinData skinData, Filter filter) : base(skinData, "Filter Selection", 14)
	    {
			_filter = filter;
	    }
	
		[PublicAPI]
		public void Show(LogEntry logEntry)
	    {
	        base.Show();
	
			if(_filter == null)
	            throw new InvalidOperationException("Filter cannot be null");
	    }
	
	    protected override void OnShow()
	    {
			_scrollPosition = Vector2.zero;
	    }

		protected override void OnHide()
		{
			LogHistory.FilterLogItems();
		}
	
	    protected override void DrawWindow(int windowId)
		{
			using(Helper.HeaderBeginHorizontal(SkinData))
			{
				if(Helper.Button(SkinData, "Close"))
					Hide();
				
				GUILayout.FlexibleSpace();
			}

			_scrollPosition = Helper.BeginScrollView(SkinData, _scrollPosition);
			
			var multiplier = 4;
			
			using(Helper.BodyBeginVertical(SkinData))
			{
				GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
				
				using (new GuiBeginHorizontal())
				{
					GUILayout.FlexibleSpace();
					Helper.Label(SkinData, new GUIContent("Global controls"));
					GUILayout.FlexibleSpace();
				}
				
				using(new GuiBeginHorizontal())
				{
					GUILayout.FlexibleSpace();

					if (Helper.Button(SkinData, "Toggle All On"))
					{
						foreach (var filterState in _filter.Global)
							filterState.State = true;
						foreach (var filterState in _filter.Custom)
							filterState.State = true;
						LogHistory.FilterLogItems();
					}
					
					GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
					
					if (Helper.Button(SkinData, "Toggle All Off"))
					{
						foreach (var filterState in _filter.Global)
							filterState.State = false;
						foreach (var filterState in _filter.Custom)
							filterState.State = false;
						LogHistory.FilterLogItems();
					}
						
					GUILayout.FlexibleSpace();
				}
				
				GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard * multiplier, SkinData.HeaderPaddingTouch * multiplier));

				using (new GuiBeginHorizontal())
				{
					GUILayout.FlexibleSpace();
					Helper.Label(SkinData, new GUIContent("Log Type"));
					GUILayout.FlexibleSpace();
				}

				var buttonSize = 50 * Helper.Scale;
				var labelSpace = 160 * Helper.Scale;
				
				using(new GuiBeginHorizontal())
				{
					GUILayout.FlexibleSpace();

					foreach (var innerFilter in _filter.Global)
					{
						using (new GuiChangeColor(innerFilter.State ? UnityEngine.Color.green : SkinData.EntryErrorColor.ToColor()))
						{
							if (Helper.Button(SkinData, innerFilter.State ? "On" : "Off", GUILayout.MinWidth(buttonSize), GUILayout.MaxWidth(buttonSize)))
							{
								innerFilter.State = !innerFilter.State;
								LogHistory.FilterLogItems();
							}
						}

						Helper.Label(SkinData, new GUIContent(innerFilter.Name), GUILayout.MinWidth(labelSpace), GUILayout.MaxWidth(labelSpace));
					}
						
					GUILayout.FlexibleSpace();
				}
				
				GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard * multiplier, SkinData.HeaderPaddingTouch * multiplier));

				if (_filter.Custom.Any())
				{
					GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
					
					using (new GuiBeginHorizontal())
					{
						GUILayout.FlexibleSpace();
						Helper.Label(SkinData, new GUIContent("Log Filter"));
						GUILayout.FlexibleSpace();
					}

					GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
				}
				
				var startIndex = 0;
				while(startIndex < _filter.Custom.Count())
				{
					var colCount = Selector.OnPlatform(4, 2);
					using(new GuiBeginHorizontal())
					{
						GUILayout.FlexibleSpace();

						for(var col = 0; col < colCount; col++)
						{
							if (startIndex + col >= _filter.Custom.Count())
							{
								break;
							}

							var innerFilter = _filter.Custom.ElementAt(startIndex + col);
							
							using(new GuiChangeColor(innerFilter.State ? UnityEngine.Color.green : SkinData.EntryErrorColor.ToColor()))
							{
								if(Helper.Button(SkinData, innerFilter.State ? "On" : "Off", GUILayout.MinWidth(buttonSize), GUILayout.MaxWidth(buttonSize)))
								{
									innerFilter.State = !innerFilter.State;
									LogHistory.FilterLogItems();
								}
							}

							Helper.Label(SkinData, new GUIContent(innerFilter.Name), GUILayout.MinWidth(labelSpace), GUILayout.MaxWidth(labelSpace));
						}
						
						GUILayout.FlexibleSpace();
					}

					startIndex += colCount;
				}

				GUILayout.FlexibleSpace();
			}
			
			GUILayout.EndScrollView();
			
			GUI.FocusWindow(windowId);
			GUI.BringWindowToFront(windowId);
		}
		#endregion
	}
}