using GTX.Helpers;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Security;

namespace GTX.Controllers
{
    public class HomeController : BaseController {

        private readonly IContactService _contactService;
        private readonly IAnnouncementService _announcementService;

        public HomeController(ISessionData sessionData, ContactService contactService, IInventoryService inventoryService, IVinDecoderService vinDecoderService
                , IEZ360Service eZ360Service
                , ILogService logService
                , IAnnouncementService announcementService, IEmployeesService employeesService) :
            base(sessionData, inventoryService, vinDecoderService, eZ360Service, logService, employeesService)  {
            _contactService = contactService;
            _announcementService = announcementService;
        }

        public ActionResult NeedLogin(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public JsonResult Login(string password, string returnUrl)
        {
            CommonUnit.Roles role;
            var ok = ValidateLogin(password, out role, Session, Request, Response, rememberOnThisComputer: true);

            if (!ok) return Json(new { ok = false, role = CommonUnit.Roles.User.ToString(), msg = "Invalid password." });

            SessionData.SetSession(Constants.SESSION_MAJORDOME, true);

            return Json(new { ok = true, role = role.ToString(), returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home") });
        }


        [HttpPost]
        public ActionResult Logout()
        {
            // 1) Clear per-user server session
            Session.Remove(Constants.SESSION_MAJORDOME);
            Session.Remove("CurrentRole");     // if you used this
            Session.Clear();
            Session.Abandon();

            // 2) If you're using FormsAuthentication, sign out too
            // (harmless if you aren't)
            FormsAuthentication.SignOut();

            // 3) Clear the "computer-level" role cookie
            RoleCookie.Clear(Response);

            // 4) Optional: also expire your custom auth cookie if you have one
            // Response.Cookies.Add(new HttpCookie("YourAuthCookie", "") { Expires = DateTime.UtcNow.AddDays(-1), Path="/" });

            return RedirectToAction("Index", "Home");
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
            ViewBag.Title = I18n.R("Title_Home");
            return View("Index", Model);
        }

        public ActionResult TermsAndConditions()
        {
            ViewBag.Message = "Terms and conditions";
            ViewBag.Title = I18n.R("Title_Terms");
            return View();
        }

        public ActionResult PrivacyPolicy()
        {
            ViewBag.Message = "Privacy Policy";
            ViewBag.Title = I18n.R("Title_PrivacyPolicy");
            return View();
        }

        public ActionResult About() {
            ViewBag.Message = "About";
            ViewBag.Title = I18n.R("Title_AboutUs");

            return View();
        }

        public ActionResult Staff() {
            ViewBag.Message = "Staff";
            ViewBag.Title = I18n.R("Title_OutStaff");

            return View(Model);
        }

        [HttpGet]
        [Route("Test-Drive")]
        public ActionResult TestDrive() => Contact(testDrive: true);

        public ActionResult Contact(bool testDrive = false) {
            ViewBag.Message = testDrive ? "Test drive request" : "Contact request";
            ViewBag.Title = testDrive ? I18n.R("Nav_ScheduleTestDrive") : I18n.R("Title_ContactUs");

            return View(new ContactModel(testDrive));
        }

        public ActionResult Application() {
            ViewBag.Message = "Application";
            ViewBag.Title = I18n.R("Title_GetPrequalified");

            return View();
        }

        public ActionResult Testimonials() {
            ViewBag.Message = "Testimonials";
            ViewBag.Title = I18n.R("Nav_Testimonials");

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
                        var employer = Model.Employers.FirstOrDefault(m => m.Id == model.EmployerId);
                        model.Comment = $"{model.Comment}\nAttn: {employer.FirstName} {employer.LastName}";
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