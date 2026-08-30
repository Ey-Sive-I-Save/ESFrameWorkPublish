namespace WellFired.Command.Unity.Runtime.Email
{
	/// <summary>
	/// You can implement this interface if you would like to provide specific functionality for your debug console to send email logs.
	/// </summary>
	public interface IEmailSender
	{
		/// <summary>
		/// If this instance of an Email Sender can send an email, you should return true from here, if you do this, your Development
		/// Console will have an Email button in certain bits of UI.
		/// </summary>
		/// <returns><c>true</c> if this instance can send email; otherwise, <c>false</c>.</returns>
		bool CanSendEmail();

		/// <summary>
		/// Implement this method if your custom email sender needs to send email. You can implement this in any way you see fit.
		/// </summary>
		/// <param name="attachmentPath">File path to attachment.</param>
		/// <param name="mimeType">MIME type.</param>
		/// <param name="attachmentFilename">Attachment filename.</param>
		/// <param name="recipientAddress">Recipient address.</param>
		/// <param name="subject">Subject.</param>
		/// <param name="body">Body.</param>
		void Email(string attachmentPath, string mimeType, string attachmentFilename, string recipientAddress, string subject, string body);
	}
}