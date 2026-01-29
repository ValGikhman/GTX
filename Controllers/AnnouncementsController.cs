using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GTX.Controllers
{

    public class AnnouncementsController : BaseController
    {
        private readonly IAnnouncementService _announcementService;
        public AnnouncementsController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, IEZ360Service _ez360Service,
                    ILogService logService, IAnnouncementService announcementService, IEmployeesService employeesService) : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, employeesService) {
            _announcementService = announcementService;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var entities = _announcementService.GetAll();
            var posts = entities.Select(AnnouncementModel.FromEntity).ToList();
            return View(posts);
        }


        public ActionResult ListPartial()
        {
            var entities = _announcementService.GetAll();
            var posts = entities.Select(AnnouncementModel.FromEntity).ToList();
            return PartialView("_Announcement", posts);
        }

        public ActionResult Create()
        {
            var model = new AnnouncementModel
            {
                Active = true,
                Type = 0,
                DisplayMode = 0,
                TargetMode = 0,
                DateStart = DateTime.Today,
                DateEnd = DateTime.Today
            };
            return PartialView("_AnnouncementEdit", model);
        }

        public ActionResult Edit(int id)
        {
            var entity = _announcementService.GetById(id);
            var model = AnnouncementModel.FromEntity(entity);
            return PartialView("_AnnouncementEdit", model);
        }

        [HttpPost]
        public ActionResult Create(AnnouncementModel model)
        {
            if (!ModelState.IsValid) return PartialView("_AnnouncementEdit", model);

            var ok = _announcementService.Create(AnnouncementModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Save(AnnouncementModel model)
        {
            if (!ModelState.IsValid) return PartialView("_AnnouncementEdit", model);

            var ok = _announcementService.Update(AnnouncementModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public JsonResult Delete(int id)
        {
            try
            {
                _announcementService.Delete(id);
                return Json(new { ok = true });
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { ok = false });
            }
        }

        [HttpGet]
        public ActionResult PreviewAlert(int id)
        {
            var entity = _announcementService.GetById(id);
            var model = AnnouncementModel.FromEntity(entity);

            return PartialView("_AnnouncementPreviewAlert", model);
        }
    }
}
