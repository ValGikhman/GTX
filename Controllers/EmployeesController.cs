using GTX.Models;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class EmployeesController : BaseController {
        private readonly string uploadPath = "~/GTXImages/staff/";
        private readonly IEmployeesService _employeesService;

        public EmployeesController(ISessionData sessionData, IEmployeesService employeesService, IInventoryService inventoryService, IVinDecoderService vinDecoderService
            , IEZ360Service eZ360Service,  ILogService logService) :
            base(sessionData, inventoryService, vinDecoderService, eZ360Service, logService, employeesService)  {
            _employeesService = employeesService;
        }
        public ActionResult ListPartial()
        {
            var entities = _employeesService.GetEmployees();
            var posts = entities.Select(EmployeeModel.FromEntity).ToList();
            return PartialView("_Employee", posts);
        }

        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                var employees = _employeesService.GetEmployees().Select(e => EmployeeModel.FromEntity(e)).ToList();
                if (employees == null) return HttpNotFound();
                return View(employees); 
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult Delete(int id) {
            var employee = _employeesService.GetEmployee(id);
            if (employee == null) return HttpNotFound();

            Log($"Deleting employee: {SerializeModel(employee)}");
            try {
                if (!string.IsNullOrWhiteSpace(employee.PhotoPath)) {
                    DeletePhoto(employee.PhotoPath);
                }
                _employeesService.DeleteEmployee(id);
                return RedirectToAction("Index");
            }
            catch (Exception ex) {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { message = ex.Message });
            }
        }

        [HttpGet]
        public ActionResult Create()
        {
            try
            {
                var model = new EmployeeModel
                {
                    Active = true
                };

                return PartialView("_EmployeeEdit", model);
            }
            catch (System.Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Content("Could not load create form: " + ex.Message);
            }
        }

        [HttpPost]
        public ActionResult Create(EmployeeModel model, HttpPostedFileBase PhotoFile)
        {
            try
            {
                if (!ModelState.IsValid)
                    return PartialView("_EmployeeEdit", model);

                // 1) if file chosen => upload and overwrite PhotoPath
                if (PhotoFile != null && PhotoFile.ContentLength > 0)
                    model.PhotoPath = SaveEmployeePhoto(PhotoFile);

                // 2) create entity and save
                var entity = EmployeeModel.ToEntity(model);
                _employeesService.SaveEmployee(entity);

                return Json(new { ok = true, id = entity.Id });
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { ok = false, message = ex.Message });
            }
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            try
            {
                var entity = _employeesService.GetEmployee(id);
                if (entity == null) return HttpNotFound();

                var model = EmployeeModel.FromEntity(entity);
                return PartialView("_EmployeeEdit", model); // <-- your form partial
            }
            catch (System.Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Content("Could not load edit form: " + ex.Message);
            }
        }

        [HttpPost]
        public ActionResult Save(EmployeeModel model, HttpPostedFileBase PhotoFile)
        {
            try
            {
                if (!ModelState.IsValid)
                    return PartialView("_EmployeeEdit", model);

                var entity = _employeesService.GetEmployee(model.Id);
                if (entity == null) return HttpNotFound();

                // if file chosen => upload and use new path
                if (PhotoFile != null && PhotoFile.ContentLength > 0)
                {
                    // optional: delete old local file if it was in /uploads/employees/
                    TryDeleteLocalEmployeePhoto(entity.PhotoPath);

                    model.PhotoPath = SaveEmployeePhoto(PhotoFile);
                }
                else
                {
                    // no file: keep existing if user left PhotoPath empty
                    if (string.IsNullOrWhiteSpace(model.PhotoPath))
                        model.PhotoPath = entity.PhotoPath;
                }

                // map fields
                entity.FirstName = model.FirstName?.Trim();
                entity.LastName = model.LastName?.Trim();
                entity.Position = model.Position?.Trim();
                entity.Phone = model.Phone?.Trim();
                entity.Email = model.Email?.Trim();
                entity.Active = model.Active;
                entity.PhotoPath = string.IsNullOrWhiteSpace(model.PhotoPath) ? null : model.PhotoPath.Trim();

                _employeesService.SaveEmployee(entity);

                return Json(new { ok = true, id = entity.Id, photoPath = entity.PhotoPath });
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { ok = false, message = ex.Message });
            }
        }

        private string SaveEmployeePhoto(HttpPostedFileBase file)
        {
            // validate extension
            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowed.Contains(ext))
                throw new Exception("Unsupported image type. Allowed: jpg, jpeg, png, webp, gif.");

            // folder
            var folderPhysical = Server.MapPath(uploadPath);
            if (!Directory.Exists(folderPhysical))
                Directory.CreateDirectory(folderPhysical);

            // unique file name
            var name = $"{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(folderPhysical, name);

            file.SaveAs(physicalPath);

            // return URL to store in DB
            return Url.Content(uploadPath + name);
        }

        private void TryDeleteLocalEmployeePhoto(string photoPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(photoPath)) return;

                // only delete if it's our uploads folder (avoid deleting external URLs)
                if (!photoPath.StartsWith(uploadPath, StringComparison.OrdinalIgnoreCase)) return;

                var physical = Server.MapPath("~" + photoPath);
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch { }
        }

        private void DeletePhoto(string photoPath) {
            try {
                var physical = Server.MapPath(photoPath);
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch {
            }
        }
    }
}