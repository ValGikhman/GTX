using GTX.Models;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Xml;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;
        private static readonly HttpClient client = new HttpClient();

        public HomeController(ISessionData sessionData, ContactService contactService, ILogService logService) :
            base(sessionData, logService)  {
            _contactService = contactService;
        }

        public ActionResult Index() {
            ViewBag.Message = "Home";
            ViewBag.Title = "Home";

            return View();
        }

        public async Task<ActionResult> Staff() {
            ViewBag.Message = "Staff";
            ViewBag.Title = "Our staff";

            BaseModel model = new BaseModel();
            try {
                if (SessionData?.Employers == null) {
                    Employer[] employers = await Utility.XMLHelpers.XmlRepository.GetEmployers();
                    SessionData.SetSession(Constants.SESSION_EMPLOYERS, employers);
                }

                model.Employers = SessionData.Employers;
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }

            return View(model);
        }

        public ActionResult Contact() {
            ViewBag.Message = "Contact";
            ViewBag.Title = "Contact us";

            ContactModel model = new ContactModel();
            try {
                model.OpenHours = Utility.XMLHelpers.XmlRepository.GetOpenHours();
                model.Contact = new ContactUs();
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }

            return View(model);
        }

        public ActionResult Application() {
            ViewBag.Message = "Application";

            return View();
        }

        public ActionResult Blog() {
            ViewBag.Message = "Blog";
            ViewBag.Title = "Latest news";

            return View();
        }

        [HttpGet]
        public String ContactForm(int id) {
            try {
                ContactUs model = new ContactUs();
                model.Employer = SessionData.Employers.FirstOrDefault(m => m.id == id);
                return RenderViewToString(this.ControllerContext, "_ContactForm", model);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpPost]
        public JsonResult SaveContact(ContactUs model) {
            Log($"Saving contact: {SerializeModel(model)}");
            try {
                if (ModelState.IsValid) {
                    Contact contact = new Contact();
                    contact.FirstName = model.FirstName;
                    contact.LastName = model.LastName;
                    contact.Phone = model.Phone;
                    contact.Email = model.Email;
                    contact.Comment = model.Comment;

                    _contactService.SaveContact(contact);
                    // EmailHelper.SendEmailConfirmation(this.ControllerContext, contact);
                    return Json(new { success = true, data = model });
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return Json(new { success = false, message = "Invalid data" });
        }

        [HttpPost]
        public async Task<JsonResult> SendAdfLeadAsync(ContactUs model) {
            try {
                var filePath = Server.MapPath("~/App_Data/adf.xml");

                if (!System.IO.File.Exists(filePath))
                    return Json(new { success = false, message = "ADF file not found." });

                // Load XML
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);

                // Update XML Nodes (Example)
                xmlDoc.SelectSingleNode("//requestdate")!.InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

                xmlDoc.SelectSingleNode("//name[@part='first']")!.InnerText = model.FirstName;
                xmlDoc.SelectSingleNode("//name[@part='last']")!.InnerText = model.LastName;
                xmlDoc.SelectSingleNode("//email")!.InnerText = model.Email;
                xmlDoc.SelectSingleNode("//phone")!.InnerText = model.Phone;

                // Set vehicle info
                xmlDoc.SelectSingleNode("//prospect/vehicle/year")!.InnerText = "Super year";
                xmlDoc.SelectSingleNode("//prospect/vehicle/make")!.InnerText = "Super make";
                xmlDoc.SelectSingleNode("//prospect/vehicle/model")!.InnerText = "Super model";

                // Set customer contact info
                xmlDoc.SelectSingleNode("//prospect/customer/contact/name[@part='first']")!.InnerText =  model.FirstName; ;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/name[@part='last']")!.InnerText = model.FirstName; ;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/email")!.InnerText = model.Email;
                xmlDoc.SelectSingleNode("//prospect/customer/contact/phone")!.InnerText = model.Phone;

                // Set comments
                xmlDoc.SelectSingleNode("//prospect/customer/comments")!.InnerText = model.Comment;

                // Convert to string
                string xmlString;
                using (var stringWriter = new StringWriter()) {
                    xmlDoc.Save(stringWriter);
                    xmlString = stringWriter.ToString();
                }

                // Send to AutoRaptor
                var url = "https://ar.autoraptor.com/incoming/adf/ARAP2237-GB"; // Replace with real endpoint

                using (var client = new HttpClient()) {
                    var content = new StringContent(xmlString, Encoding.UTF8, "application/xml");
                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode) {
                        return Json(new { success = true, message = "Lead sent successfully!" });
                    }
                    else {
                        var error = await response.Content.ReadAsStringAsync();
                        return Json(new { success = false, message = $"Failed to send. {error}" });
                    }
                }
            }
            catch (Exception ex) {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}