using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Email
{
	/// <summary>
	/// If you want to provide specific email support for your platform, you can add your platform to this Email Factory, returning
	/// a new instance of your custom IEmailSender.
	/// </summary>
	public static class EmailFactory
	{
		private static IEmailSender _platformEmailSender;
	
		public static IEmailSender GetEmailSender(bool emailSupportEnabled)
		{
			if (_platformEmailSender != null)
				return _platformEmailSender;
			
			switch (Application.platform)
			{
				case RuntimePlatform.OSXEditor:
				case RuntimePlatform.OSXPlayer:
				case RuntimePlatform.WindowsPlayer:
				case RuntimePlatform.WindowsEditor:
				case RuntimePlatform.IPhonePlayer:
				case RuntimePlatform.Android:
				case RuntimePlatform.LinuxPlayer:
				case RuntimePlatform.LinuxEditor:
					_platformEmailSender = new EmailSender();
					break;
				default:
					_platformEmailSender = new NoEmailSender();
					break;
			}
			
			if(!emailSupportEnabled)
				_platformEmailSender = new NoEmailSender();
			
			return _platformEmailSender;
		}
	}
}