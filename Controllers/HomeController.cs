using GTX.Models;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Net;
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

        public ActionResult About() {
            ViewBag.Message = "About";
            ViewBag.Title = "About us";

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

            return View(new ContactModel());
        }

        public ActionResult Application() {
            ViewBag.Message = "Application";
            ViewBag.Title = "Loan application";

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
        public String ContactForm(int id) {
            try {
                ContactModel model = new ContactModel();
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
        public async Task<ActionResult> SendContact(ContactModel model) {
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
                    await Utility.XMLHelpers.XmlRepository.SendAdfLeadAsync(model, Model.CurrentVehicle);
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
    }
}