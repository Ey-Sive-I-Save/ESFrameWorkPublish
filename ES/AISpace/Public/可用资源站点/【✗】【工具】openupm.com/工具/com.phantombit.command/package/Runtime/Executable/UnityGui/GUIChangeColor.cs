using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
	public class GuiChangeColor : IDisposable
	{
		private Color PreviousColor
		{
			get;
			set;
		}
	
		public GuiChangeColor(Color newColor)
		{
			PreviousColor = GUI.color;
			GUI.color = newColor;
		}
	
		public void Dispose() 
		{
			GUI.color = PreviousColor;
		}
	}
}	