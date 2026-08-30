using UnityEngine;
using WellFired.Command.Unity.Runtime.Modals;

namespace WellFired.Command.Unity.Runtime.Input
{
	/// <summary>
	/// If you want to provide specific input support for your platform, you can add your platform to this Input Factory, returning
	/// a new instance of your custom IInputField.
	/// </summary>
	public static class InputFieldFactory
	{
		private static IInputField _platformInputField;
		
		public static IInputField GetInputField(LogHistoryGui logHistoryView)
		{
			if(_platformInputField != default(IInputField))
			{
				return _platformInputField;
			}
			
			// Deal with IPhone and Android only.
			if(Application.platform == RuntimePlatform.IPhonePlayer)
			{
				_platformInputField = new TouchInputField(logHistoryView);
			}
			else if(Application.platform == RuntimePlatform.Android)
			{
				_platformInputField = new TouchInputField(logHistoryView);
			}
			
			// If we have no email sender, simply use an emtpy email sender, which defaults to doing nothing.
			if(_platformInputField == default(IInputField))
			{
				_platformInputField = new KeyboardInputField(logHistoryView);
			}
			
			return _platformInputField;
		}
	}
}