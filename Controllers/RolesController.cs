using GTX.Common;
using GTX.Models;
using Services;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;

namespace GTX.Controllers {
    [RequireAdminRole(RequireMajordome = true)]
    public sealed class RolesController : BaseController {
        private readonly SiteRoleStore store = new SiteRoleStore();
        public RolesController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) { }
        [HttpGet] public ActionResult Index() { Response.Cache.SetNoStore(); return View(store.All()); }
        [HttpGet] public ActionResult Edit(int id = 0) {
            Response.Cache.SetNoStore();
            if (id < 0) return HttpNotFound();
            var role = id == 0 ? new SiteRoleModel { Permissions = MajordomeMenu.Defaults("new role") } : store.All().FirstOrDefault(r => r.Id == id);
            if (role == null) return HttpNotFound();
            return PartialView("_Edit", role);
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Save([Bind(Include = "Id,Name,Password,ConfirmPassword,Permissions")] SiteRoleModel model) {
            // Bind only editable fields: hashes and revisions never come from the browser.
            if (model.Id < 0 || string.Equals(model.Name?.Trim(), MajordomeAccount.Name, StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(400, "That role is reserved.");
            if (ModelState.IsValid) {
                try { store.Save(model); return Json(new { ok=true }); }
                catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { ModelState.AddModelError("Name", "That role name already exists."); }
            }
            model.Password = null;
            model.ConfirmPassword = null;
            return PartialView("_Edit", model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LoginAs(int id) {
            var actor = SiteAccess.Current(Request);
            if (!SiteAccess.IsMajordome(actor)) return new HttpStatusCodeResult(403);
            if (id <= 0) return HttpNotFound();
            var role = store.All().FirstOrDefault(r => r.Id == id);
            if (role == null) return HttpNotFound();
            Log("Administrator switched login to role: " + role.Name + " (Id=" + role.Id + ")");
            SiteAccess.SignIn(role, Request, Response, Session, persist: true);
            SessionData.SetSession(Constants.SESSION_MAJORDOME, true);
            return Json(new { ok = true, returnUrl = Url.Action("Index", "Home") });
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Delete(int id) {
            if (id <= 0) return HttpNotFound();
            try { store.Delete(id); return Json(new { ok=true }); }
            catch (InvalidOperationException ex) { Response.StatusCode=400; return Json(new { message=ex.Message }); }
        }
    }
}
