using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers
{

    public class HomeController : BaseController {

        private readonly IContactService _contactService;
        private readonly IAnnouncementService _announcementService;

        public HomeController(ISessionData sessionData, ContactService contactService, IInventoryService inventoryService, IVinDecoderService vinDecoderService
                , IEZ360Service eZ360Service
                , ILogService logService
                , IAnnouncementService announcementService) :
            base(sessionData, inventoryService, vinDecoderService, eZ360Service, logService)  {
            _contactService = contactService;
            _announcementService = announcementService;
        }

        [HttpPost]
        public JsonResult Login(string password)
        {
            var ok = ValidateLogin(password, out var currentRole);

            if (!ok)
                return Json(new { ok = false, role = currentRole.ToString(), msg = "Invalid password." });

            SessionData.SetSession(Constants.SESSION_MAJORDOME, true);
            return Json(new { ok = true, role = currentRole.ToString() });
        }


        [HttpGet]
        public ActionResult LoginModal()
        {
            return PartialView("~/Views/Shared/_LoginModal.cshtml");
        }
        public ActionResult Index() {
            // Check for Announcements
            var announcement = GetAnnouncement();
            Model.Announcements = announcement == null ? new List<AnnouncementModel>(): new List<AnnouncementModel> { announcement };

            ViewBag.Message = "Home";
            ViewBag.Title = "Home";
            return View(Model);
        }

        public ActionResult TermsAndConditions()
        {
            ViewBag.Message = "Terms and conditions";
            ViewBag.Title = "Terms and conditions";
            return View();
        }

        public ActionResult PrivacyPolicy()
        {
            ViewBag.Message = "Privacy Policy";
            ViewBag.Title = "Privacy Policy";
            return View();
        }


        public ActionResult Home() {
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

            return View(Model);
        }

        [HttpGet]
        [Route("Test-Drive")]
        public ActionResult TestDrive() => Contact(testDrive: true);

        public ActionResult Contact(bool testDrive = false) {
            ViewBag.Message = testDrive ? "Test drive request" : "Contact request";
            ViewBag.Title = testDrive ? "Schedule test drive" : "Contact us";

            return View(new ContactModel(testDrive));
        }

        public ActionResult Application() {
            ViewBag.Message = "Application";
            ViewBag.Title = "Get prequalified";

            return View();
        }

        public ActionResult Blog() {
            ViewBag.Message = "Blog";
            ViewBag.Title = "Latest news";

            return View();
        }

        public ActionResult Testimonials() {
            ViewBag.Message = "Testimonials";
            ViewBag.Title = "Testimonials";

            return View();
        }

        [HttpGet]
        public string ContactForm(int id) {
            try {
                ContactModel model = new ContactModel();
                if (id != 0) {
                    model.EmployerId = id;
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
        public async Task<ActionResult> SendContact(ContactModel model) {
            Log($"Sending contact: {SerializeModel(model)}");
            try {
                if (ModelState.IsValid) {
                    Contact contact = new Contact();
                    contact.FirstName = model.FirstName;
                    contact.LastName = model.LastName;
                    contact.Phone = model.Phone;
                    contact.Email = model.Email;
                    if (!string.IsNullOrEmpty(model.PreferredDate)) {
                        model.Comment = $"{model.Comment}\nPreffered date and time to schedule: {model.PreferredDate}";
                        if (!string.IsNullOrEmpty(model.PreferredTime)) {
                            model.Comment = $"\n{model.Comment}:{model.PreferredTime}";
                        }
                    }

                    if (model.EmployerId > 0) {
                        var employer = Model.Employers.FirstOrDefault(m => m.id == model.EmployerId);
                        model.Comment = $"{model.Comment}\nAttn: {employer.Name}";
                    }

                    contact.Comment = model.Comment;

                    _contactService.SaveContact(contact);
                    await Utility.XMLHelpers.XmlRepository.SendAdfLeadAsync(model);
                    return Json(new {data = contact });
                }
            }
            catch (Exception ex) {
                base.Log(ex);
            }
            finally {
            }
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return Json(new { message = "Unexpected error occurred." });
        }

        private AnnouncementModel GetAnnouncement()
        {
            var entities = _announcementService.GetAllActive();
            var posts = entities.Select(AnnouncementModel.FromEntity).ToList();
            return posts.FirstOrDefault();
        }

    }
}