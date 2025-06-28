using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Services;
using System;
using System.IO;
using System.Web.Mvc;

namespace Utility {

    public static class EmailHelper {

        public static void SendEmail(string emailTo, string emailSubject, string emailBody) {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Administrator", "admin@usedcarscincinnati.com"));
            message.To.Add(new MailboxAddress("Valentin Gikhman", "valentin.gikhman@gmail.com"));
            message.Subject = "Test Email from VPS";
            message.Body = new TextPart("plain") {
                Text = emailBody
            };

            using (var client = new SmtpClient()) {
                try {
                    // Connect to IONOS SMTP server
                    client.Connect("smtp.ionos.com", 587, SecureSocketOptions.StartTls);

                    // Authenticate
                    client.Authenticate("admin@usedcarscincinnati.com", "nowORnever2017!");

                    // Send
                    client.Send(message);
                    Console.WriteLine("Email sent successfully.");

                    // Disconnect
                    client.Disconnect(true);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error: {ex.Message}");
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

            SendEmail(contact.Email, "GTX contact lead", emailBody);
        }
    }
}