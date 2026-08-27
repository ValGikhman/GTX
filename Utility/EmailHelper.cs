using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Utility {

    public static class EmailHelper {

        public static async Task<bool> SendEmailAsync(string emailTo, string emailSubject, string emailBody, string contentSubtype = "plain") {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Administrator", "admin@usedcarscincinnati.com"));
            message.To.Add(new MailboxAddress(emailTo, emailTo));
            message.Subject = emailSubject;
            message.Body = new TextPart(contentSubtype == "xml" ? "xml" : "plain") {
                Text = emailBody
            };

            using (var client = new SmtpClient()) {
                try {
                    // Connect to IONOS SMTP server
                    await client.ConnectAsync("smtp.ionos.com", 587, SecureSocketOptions.StartTls);

                    // Authenticate
                    await client.AuthenticateAsync("admin@usedcarscincinnati.com", "nowORnever2017!");

                    // Send
                    await client.SendAsync(message);
                    Console.WriteLine("Email sent successfully.");

                    // Disconnect
                    await client.DisconnectAsync(true);
                    return true;
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error: {ex.Message}");
                    return false;
                }
            }
        }

        public static void SendEmailConfirmation(ControllerContext context, Contact contact) {
            String emailBody = String.Empty;
            ViewDataDictionary viewData = new ViewDataDictionary(contact);

            using (StringWriter sw = new StringWriter()) {
                ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(context, "Emails/_ContactTemplate");
                ViewContext viewContext = new ViewContext(context, viewResult.View, viewData, new TempDataDictionary(), sw);
                viewResult.View.Render(viewContext, sw);
                emailBody = sw.GetStringBuilder().ToString();
            }

            SendEmailAsync(contact.Email, "GTX contact lead", emailBody).GetAwaiter().GetResult();
        }
    }
}
