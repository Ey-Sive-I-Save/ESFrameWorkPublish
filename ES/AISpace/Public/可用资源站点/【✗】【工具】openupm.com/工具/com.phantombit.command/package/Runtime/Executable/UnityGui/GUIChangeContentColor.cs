using System;
using JetBrains.Annotations;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
	[PublicAPI]
	public class GuiChangeContentColor : IDisposable
	{
		private Color PreviousColor
		{
			get;
			set;
		}
	
		public GuiChangeContentColor(Color newColor)
		{
			PreviousColor = GUI.contentColor;
			GUI.contentColor = newColor;
		}
	
		public void Dispose() 
		{
			GUI.contentColor = PreviousColor;
		}
	}
}