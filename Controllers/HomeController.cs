using GTX.Models;
using GTX;
using Services;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Linq;

namespace GTX.Controllers {

    public class HomeController : BaseController {

        private readonly IContactService _contactService;

        public HomeController(ISessionData sessionData, ContactService contactService) :
            base(sessionData)  {
            _contactService = contactService;

        }

        public async Task<ActionResult> Index() {
            ViewBag.Message = "Home";
            ViewBag.Title = "Home";

            var vehicles = await Utility.XMLHelpers.XmlRepository.GetInventory();
            SessionData.SetSession(Constants.SESSION_INVENTORY, vehicles.Where(m => m.RetailPrice > 0).OrderByDescending(m => m.PurchaseDate).ThenBy(m => m.Make).ToArray());
            Filters filters = new Filters();
            filters.Makes = SessionData.Vehicles.Select(m => m.Make).Distinct().OrderBy(m => m).ToArray();
            filters.Models = SessionData.Vehicles.Select(m => m.Model).Distinct().OrderBy(m => m).ToArray();
            filters.Engines = SessionData.Vehicles.Select(m => m.Engine).Distinct().OrderBy(m => m).ToArray();
            filters.FuelTypes = SessionData.Vehicles.Select(m => m.FuelType).Distinct().OrderBy(m => m).ToArray();

            SessionData.SetSession(Constants.SESSION_FILTERS, filters);

            return View();
        }

        public async Task<ActionResult> Staff() {
            ViewBag.Message = "Staff";
            ViewBag.Title = "Our staff";

            StaffModel model = new StaffModel();
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

        [HttpGet]
        public JsonResult GetMakes() {
            try {
                return Json(SessionData.Filters.Makes, JsonRequestBehavior.AllowGet); 
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetModels() {
            try {
                return Json(SessionData.Filters.Models, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetEngines() {
            try {
                return Json(SessionData.Filters.Engines, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            return null;
        }

        [HttpGet]
        public JsonResult GetFuelTypes() {
            try {
                return Json(SessionData.Filters.FuelTypes, JsonRequestBehavior.AllowGet);
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