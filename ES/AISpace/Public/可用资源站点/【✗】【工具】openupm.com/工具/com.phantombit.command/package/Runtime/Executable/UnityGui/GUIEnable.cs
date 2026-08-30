using System;
using JetBrains.Annotations;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
	[PublicAPI]
	public class GuiEnable : IDisposable
	{
		private bool PreviousState
		{
			get;
			set;
		}
	
		public GuiEnable(bool newState)
		{
			PreviousState = GUI.enabled;
			GUI.enabled = newState;
		}
	
		public void Dispose() 
		{
			GUI.enabled = PreviousState;
		}
	}
}