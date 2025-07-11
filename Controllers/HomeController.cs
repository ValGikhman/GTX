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
using Utility;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;
        private static readonly HttpClient client = new HttpClient();

        public HomeController(ISessionData sessionData, ContactService contactService, IInventoryService inventoryService, ILogService logService) :
            base(sessionData, inventoryService,  logService)  {
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
                if (id != 0) {
                    model.Employer = SessionData.Employers.FirstOrDefault(m => m.id == id);
                }
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
        public async Task<JsonResult> SendContact(ContactUs model) {
            Log($"Sending contact: {SerializeModel(model)}");
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
                    await Utility.XMLHelpers.XmlRepository.SendAdfLeadAsync(model, Model.CurrentVehicle);
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
    }
}