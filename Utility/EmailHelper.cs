using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc;
using Services;

namespace Utility {

    public static class EmailHelper {

        public static Boolean SendEmail(string emailTo, string emailSubject, string emailBody) {
            Boolean success = true;

            MailAddress from = new MailAddress("valentin.gikhman@gmail.com");
            MailAddress to = new MailAddress("valentin.gikhman@gmail.com");
            MailMessage email = new MailMessage(from, to);
            email.Subject = "GTX contact message.";
            email.IsBodyHtml = true;
            SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential("valentin.gikhman@gmail.com", "@@Kpot1965");       

            try {
                email.Body = emailBody;
                smtp.Send(email);
            }
            catch(Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
                success = false;
            }

            return success;
        }

        public static Boolean SendEmailConfirmation(ControllerContext context, Contact contact) {
            String emailBody = String.Empty;
            ViewDataDictionary viewData = new ViewDataDictionary(contact);

            using (StringWriter sw = new StringWriter()) {
                ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(context, "Emails/_ContactTemplate");
                ViewContext viewContext = new ViewContext(context, viewResult.View, viewData, new TempDataDictionary(), sw);
                viewResult.View.Render(viewContext, sw);
                emailBody = sw.GetStringBuilder().ToString();
            }
            return SendEmail(contact.Email, "GTX contact lead", emailBody);
        }
    }
}