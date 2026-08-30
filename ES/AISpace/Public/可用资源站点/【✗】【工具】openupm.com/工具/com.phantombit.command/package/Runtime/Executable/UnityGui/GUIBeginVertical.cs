using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
	public class GuiBeginVertical : IDisposable
	{
		public GuiBeginVertical()
		{
			GUILayout.BeginVertical();
		}
	
		public GuiBeginVertical(params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginVertical(layoutOptions);
		}
		
		public GuiBeginVertical(GUIStyle guiStyle, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginVertical(guiStyle, layoutOptions);
		}
		
		public void Dispose() 
		{
			GUILayout.EndVertical();
		}
	}
}