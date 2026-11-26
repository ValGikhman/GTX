using GTX.Common;
using GTX.Models;
using Services;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Web.Mvc;

namespace GTX.Controllers {
    [RequireAdminRole]
    public sealed class ProfileController : BaseController {
        private readonly SiteProfileStore store = new SiteProfileStore();

        public ProfileController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) { }

        [HttpGet]
        public ActionResult Index() {
            Response.Cache.SetNoStore();
            try { return View(SiteProfileStore.Current); }
            catch (SqlException ex) {
                Trace.TraceError("Unable to load company profile (SQL error {0}).", ex.Number);
                return new HttpStatusCodeResult(503, "Company profile is temporarily unavailable.");
            }
            catch (InvalidOperationException) {
                Trace.TraceError("Unable to load company profile.");
                return new HttpStatusCodeResult(503, "Company profile is temporarily unavailable.");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Index([Bind(Include = "CompanyName,Email,Phone,StreetAddress,AddressLine2,City,State,ZipCode,ResearchRadiusMiles,DocumentaryFee,BusinessHours")] SiteProfileModel profile) {
            Response.Cache.SetNoStore();
            if (ModelState.IsValid) {
                try {
                    store.Save(profile);
                    TempData["ProfileSaved"] = true;
                    return RedirectToAction("Index");
                }
                catch (SqlException ex) {
                    Trace.TraceError("Unable to save company profile (SQL error {0}).", ex.Number);
                    ModelState.AddModelError("", "The profile could not be saved. Please try again.");
                }
                catch (InvalidOperationException) {
                    Trace.TraceError("Unable to save company profile.");
                    ModelState.AddModelError("", "The profile could not be saved. Please try again.");
                }
            }
            return View(profile);
        }
    }
}
