using GTX.Models;
using GTX;
using Services;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Web.Script.Serialization;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;

        public HomeController(ISessionData sessionData, ContactService contactService) :
            base(sessionData)  {
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
            if (SessionData?.Employers == null) {
                Employer[] employers = await Utility.XMLHelpers.XmlRepository.GetEmployers();
                SessionData.SetSession(Constants.SESSION_EMPLOYERS, employers);
            }

            model.Employers = SessionData.Employers;
            return View(model);
        }

        public ActionResult Contact() {
            ViewBag.Message = "Contact";
            ViewBag.Title = "Contact us";

            ContactModel model = new ContactModel();
            model.OpenHours = Utility.XMLHelpers.XmlRepository.GetOpenHours();

            model.Contact = new ContactUs();
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
        public ActionResult SaveContact(ContactUs model) {
            if (ModelState.IsValid) {
                Contact contact = new Contact();
                contact.FirstName = model.FirstName;
                contact.LastName = model.LastName;
                contact.Phone = model.Phone;
                contact.Email = model.Email;
                contact.Comment = model.Comment;

                _contactService.SaveContact(contact);
                // EmailHelper.SendEmailConfirmation(this.ControllerContext, contact);
            }
            return RedirectToAction("Contact", model);
        }
        #region private
        #endregion private
    }
}