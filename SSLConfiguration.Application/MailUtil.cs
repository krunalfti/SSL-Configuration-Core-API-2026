using System.Net;
using System.Net.Mail;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Same as old SSLConfiguration_CommonUtility.MailUtil.
    /// </summary>
    public static class MailUtil
    {
        public static bool SendEmail(string sTo, string sFrom, string sCc, string sBcc, string sSubject, string sBody)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AppConfig.SmtpServer))
                    return false;

                using SmtpClient smtpMail = GetSmtpClient();
                using MailMessage email = new MailMessage();
                email.To.Add(sTo);
                email.From = new MailAddress(sFrom);

                if (!string.IsNullOrEmpty(sCc))
                    email.CC.Add(sCc);
                if (!string.IsNullOrEmpty(sBcc))
                    email.Bcc.Add(sBcc);
                if (!string.IsNullOrEmpty(sSubject))
                    email.Subject = sSubject;

                email.Body = sBody;
                email.IsBodyHtml = true;

                smtpMail.Send(email);
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                return false;
            }
        }

        private static SmtpClient GetSmtpClient()
        {
            SmtpClient smtpMail = new SmtpClient(AppConfig.SmtpServer);

            string? userName = AppConfig.SmtpUsername;
            string? password = AppConfig.SmtpPassword;
            bool enableSsl = AppConfig.EnableSsl;

            smtpMail.EnableSsl = enableSsl;
            smtpMail.UseDefaultCredentials = false;
            if (!string.IsNullOrEmpty(userName))
                smtpMail.Credentials = new NetworkCredential(userName, password);

            if (AppConfig.SmtpPort > 0)
                smtpMail.Port = AppConfig.SmtpPort;

            return smtpMail;
        }
    }
}
