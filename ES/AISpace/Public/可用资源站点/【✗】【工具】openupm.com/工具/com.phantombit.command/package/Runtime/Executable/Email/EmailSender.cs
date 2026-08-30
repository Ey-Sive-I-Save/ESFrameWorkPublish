using UnityEngine;
using UnityEngine.Networking;

namespace WellFired.Command.Unity.Runtime.Email
{
	public class EmailSender : IEmailSender
	{
		public bool CanSendEmail()
		{
			return true;
		}

		public void Email(string attachmentPath, string mimeType, string attachmentFilename, string recipientAddress, string subject, string body)
		{
			var email = recipientAddress;
			subject = MyEscapeUrl(subject);
			body = MyEscapeUrl(body);
			
			attachmentPath = MyEscapeUrl(attachmentPath);
			
			var url = "mailto:" + email + "?subject=" + subject + "&body=" + body + "&mimeType=" + mimeType + "&attachment=" + attachmentPath;
			
			Application.OpenURL(url);
		}
		
		private static string MyEscapeUrl (string url)
		{
			return UnityWebRequest.EscapeURL(url).Replace("+","%20");
		}
		
		public static string Base64Encode(string plainText) 
		{
			var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
			return System.Convert.ToBase64String(plainTextBytes);
		}
	}
}