namespace WellFired.Command.Unity.Runtime.Email
{
    public class NoEmailSender : IEmailSender
    {
        public bool CanSendEmail()
        {
            return false;
        }

        public void Email(string attachmentPath, string mimeType, string attachmentFilename, string recipientAddress, string subject, string body)
        {
        }
    }
}