using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using GTX.Common;
using GTX.Controllers;
using GTX.Models;
using Services;

namespace GTX
{
    public class SpecialsController : BaseController
    {
        private readonly ISpecialsService specials;
        public SpecialsController(ISessionData sessionData, IInventoryService inventoryService,
            IVinDecoderService vinDecoderService, ILogService logService,
            IEmployeesService employeesService, ISpecialsService specialsService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService)
        {
            specials = specialsService;
        }

        [HttpGet, RequireAdminRole]
        public ActionResult Index() => View(specials.GetAll(true));

        [HttpGet, RequireAdminRole]
        public ActionResult ListPartial() => PartialView("_Special", specials.GetAll(true));

        [HttpGet]
        public ActionResult List() => View(specials.GetAll());

        [HttpGet]
        public ActionResult Links() => Json(specials.GetAll()
            .Select(s => new { id = s.Id, title = s.Title }).ToArray(), JsonRequestBehavior.AllowGet);

        [HttpGet]
        public ActionResult Special(int id)
        {
            var special = specials.GetById(id);
            if (special == null || !special.IsPublished) return HttpNotFound();
            return View(special);
        }

        [HttpGet, RequireAdminRole]
        public ActionResult Create() => PartialView("_SpecialEdit", new SpecialModel());

        [HttpGet, RequireAdminRole]
        public ActionResult Edit(int id)
        {
            var special = specials.GetById(id);
            return special == null ? (ActionResult)HttpNotFound() : PartialView("_SpecialEdit", special);
        }

        [HttpPost, RequireAdminRole, ValidateAntiForgeryToken]
        public ActionResult Save(SpecialModel model)
        {
            if (model.Id < 0) return new HttpStatusCodeResult(400);
            model.Sanitize();
            if (string.IsNullOrWhiteSpace(model.CardContent)) ModelState.AddModelError("CardContent", "Add content for the card.");
            if (!ModelState.IsValid) return PartialView("_SpecialEdit", model);
            try
            {
                if (!specials.Save(model)) return HttpNotFound();
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Special save failed: {0}", ex);
                ModelState.AddModelError("", "The special could not be saved. Please try again.");
                return PartialView("_SpecialEdit", model);
            }
        }

        [HttpPost, RequireAdminRole, ValidateAntiForgeryToken]
        public ActionResult Delete(int id) => Json(new { ok = specials.Delete(id) });

        [HttpPost, RequireAdminRole, ValidateAntiForgeryToken]
        public ActionResult UploadImage(HttpPostedFileBase file) => RichContentImageUpload.Save(file, Server, Url);

        [HttpGet]
        public ActionResult Image(string id)
        {
            Guid imageId;
            if (!Guid.TryParseExact(id, "N", out imageId)) return HttpNotFound();
            var path = Server.MapPath(RichContentImageUpload.DirectoryPath + "/" + imageId.ToString("N") + ".png");
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            return File(path, "image/png");
        }
    }
}
