using GTX.Models;
using GTX.Common;
using Services;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {

    [RequireAdminRole]
    public class EmployeesController : BaseController {
        private const string UploadVirtualPath = "~/SiteImages/staff/";
        private const string UploadRelativePath = "/SiteImages/staff/";
        private readonly IEmployeesService _employeesService;

        public EmployeesController(ISessionData sessionData, IEmployeesService employeesService, IInventoryService inventoryService, IVinDecoderService vinDecoderService
            , ILogService logService) :
            base(sessionData, inventoryService, vinDecoderService, logService, employeesService)  {
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
            string savedPhotoPath = null;
            try
            {
                if (!ModelState.IsValid)
                    return PartialView("_EmployeeEdit", model);

                // 1) if file chosen => upload and overwrite PhotoPath
                if (PhotoFile != null && PhotoFile.ContentLength > 0)
                {
                    savedPhotoPath = SaveEmployeePhoto(PhotoFile);
                    model.PhotoPath = savedPhotoPath;
                }
                else
                {
                    model.PhotoPath = NormalizeEmployeePhotoPath(model.PhotoPath);
                }

                // 2) create entity and save
                var entity = EmployeeModel.ToEntity(model);
                _employeesService.SaveEmployee(entity);

                return Json(new { ok = true, id = entity.Id });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(savedPhotoPath))
                {
                    DeletePhoto(savedPhotoPath);
                }
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
            string newPhotoPath = null;
            try
            {
                if (!ModelState.IsValid)
                    return PartialView("_EmployeeEdit", model);

                var entity = _employeesService.GetEmployee(model.Id);
                if (entity == null) return HttpNotFound();

                // if file chosen => upload and use new path
                if (PhotoFile != null && PhotoFile.ContentLength > 0)
                {
                    var previousPhotoPath = entity.PhotoPath;
                    newPhotoPath = SaveEmployeePhoto(PhotoFile);
                    model.PhotoPath = newPhotoPath;

                    // Keep the working photo until the employee record accepts the replacement.
                    entity.PhotoPath = NormalizeEmployeePhotoPath(model.PhotoPath);
                    MapEmployeeFields(entity, model);
                    _employeesService.SaveEmployee(entity);
                    DeletePhoto(previousPhotoPath);

                    return Json(new { ok = true, id = entity.Id, photoPath = entity.PhotoPath });
                }
                else
                {
                    // no file: keep existing if user left PhotoPath empty
                    if (string.IsNullOrWhiteSpace(model.PhotoPath))
                    {
                        model.PhotoPath = entity.PhotoPath;
                    }
                    else
                    {
                        model.PhotoPath = NormalizeEmployeePhotoPath(model.PhotoPath) ?? entity.PhotoPath;
                    }
                }

                // map fields
                MapEmployeeFields(entity, model);
                entity.PhotoPath = NormalizeEmployeePhotoPath(model.PhotoPath);

                _employeesService.SaveEmployee(entity);

                return Json(new { ok = true, id = entity.Id, photoPath = entity.PhotoPath });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(newPhotoPath))
                {
                    DeletePhoto(newPhotoPath);
                }
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { ok = false, message = ex.Message });
            }
        }

        private static void MapEmployeeFields(Services.Employee entity, EmployeeModel model)
        {
            entity.FirstName = model.FirstName?.Trim();
            entity.LastName = model.LastName?.Trim();
            entity.Position = model.Position?.Trim();
            entity.Phone = model.Phone?.Trim();
            entity.Email = model.Email?.Trim();
            entity.Active = model.Active;
        }

        private string SaveEmployeePhoto(HttpPostedFileBase file)
        {
            // validate extension
            var ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowed.Contains(ext))
                throw new Exception("Unsupported image type. Allowed: jpg, jpeg, png, webp, gif.");

            var folderPhysical = Server.MapPath(UploadVirtualPath);
            if (!Directory.Exists(folderPhysical))
                Directory.CreateDirectory(folderPhysical);

            // unique file name
            var name = $"{Guid.NewGuid():N}{ext}";
            file.SaveAs(Path.Combine(folderPhysical, name));

            // return URL to store in DB
            return UploadRelativePath + name;
        }

        private void DeletePhoto(string photoPath) {
            try {
                var normalized = NormalizeEmployeePhotoPath(photoPath);
                if (string.IsNullOrWhiteSpace(normalized)) return;
                if (string.Equals(Path.GetFileName(normalized), "empty.jpg", StringComparison.OrdinalIgnoreCase)) return;

                var physical = Server.MapPath("~" + normalized);
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch {
            }
        }

        private string NormalizeEmployeePhotoPath(string photoPath)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
            {
                return null;
            }

            var value = photoPath.Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            {
                value = absoluteUri.AbsolutePath;
            }

            value = value.Replace('\\', '/');
            if (value.StartsWith("~/", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (!value.StartsWith(UploadRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fileName = Path.GetFileName(value);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            return UploadRelativePath + fileName;
        }
    }
}
