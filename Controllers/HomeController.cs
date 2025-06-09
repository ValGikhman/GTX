using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;

        public HomeController(IContactService contactService) {
            _contactService = contactService;
        }

        public ActionResult Index() {
            ViewBag.Message = "Home";
            ViewBag.Title = "Home";

            return View();
        }

        public ActionResult About() {
            ViewBag.Message = "About";
            ViewBag.Title = "About us";

            return View();
        }

        public ActionResult Staff() {
            ViewBag.Message = "Staff";
            ViewBag.Title = "Our staff";
            IList<Employer> model = Utility.XMLHelpers.XmlStaffRepository.GetEmployers();
            return View(model);
        }

        public ActionResult Contact() {
            ViewBag.Message = "Contact";
            ViewBag.Title = "Contact us";
            ContactModel model = new ContactModel();
            model.OpenHours = Utility.XMLHelpers.XmlStaffRepository.GetOpenHours();
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
        public String ContactForm() {
            try {
                ContactUs model = new ContactUs();
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