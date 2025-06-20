﻿using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;

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
        public ActionResult SaveContact(ContactUs model) {
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
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }

            return RedirectToAction("Contact", model);
        }
    }
}