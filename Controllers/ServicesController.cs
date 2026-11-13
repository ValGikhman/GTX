using GTX.Common;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers {
    [RequireAdminRole(RequireMajordome = true)]
    public sealed class ServicesController : BaseController {
        public ServicesController(ISessionData sessionData, IInventoryService inventoryService, IVinDecoderService vinDecoderService, ILogService logService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService) { }

        [HttpGet]
        public ActionResult Index() {
            Response.Cache.SetNoStore();
            return SettingsView();
        }

        [HttpPost, ValidateAntiForgeryToken, ValidateInput(false)]
        public async Task<ActionResult> OpenAiModels(string apiKey) {
            Response.Cache.SetNoStore();
            try {
                var models = await new OpenAiModelCatalog().ListAsync(apiKey);
                return Json(new { ok = true, models });
            }
            catch (OpenAiModelCatalogException ex) {
                return Json(new { ok = false, message = ex.Message });
            }
        }

        // Credentials may contain HTML-significant characters; the view always Razor-encodes them.
        [HttpPost, ValidateAntiForgeryToken, ValidateInput(false)]
        public ActionResult Save([Bind(Include = "ServiceId,Enabled,Fields,Revision")] ServiceSettingsSaveModel model) {
            Response.Cache.SetNoStore();
            var definition = ServiceConfiguration.Services.FirstOrDefault(service => service.Id == model.ServiceId);
            if (definition == null) return new HttpStatusCodeResult(400, "Unknown service.");

            var allowedFields = new HashSet<string>(definition.Fields.Select(field => field.Key), StringComparer.Ordinal);
            if (model.Fields == null || model.Fields.Keys.Any(key => !allowedFields.Contains(key)))
                ModelState.AddModelError("", "The service fields could not be saved. Reload the page and try again.");

            if (ModelState.IsValid) {
                try {
                    ServiceConfiguration.Save(model.ServiceId, model.Enabled, model.Fields, model.Revision);
                    TempData["SavedServiceId"] = model.ServiceId;
                    return RedirectToAction("Index");
                }
                catch (ValidationException) {
                    ModelState.AddModelError("", "Check the required settings, URLs and number ranges for this service.");
                }
                catch (InvalidOperationException) {
                    ModelState.AddModelError("", "The service settings changed or could not be saved. Reload the page and try again.");
                }
                catch (IOException) {
                    Trace.TraceError("Unable to save service settings.");
                    ModelState.AddModelError("", "The service settings could not be saved. Please try again.");
                }
                catch (UnauthorizedAccessException) {
                    Trace.TraceError("Unable to save service settings.");
                    ModelState.AddModelError("", "The service settings could not be saved. Please try again.");
                }
                catch (ConfigurationErrorsException) { return SettingsUnavailable(); }
            }

            return SettingsView(model);
        }

        private ActionResult SettingsView(ServiceSettingsSaveModel posted = null) {
            try {
                var snapshot = ServiceConfiguration.Read();
                var page = new ServiceSettingsPageModel { InvalidServiceId = posted?.ServiceId };
                foreach (var definition in ServiceConfiguration.Services) {
                    var preservePosted = posted != null && posted.ServiceId == definition.Id;
                    string enabledValue;
                    bool enabled;
                    snapshot.Values.TryGetValue(definition.EnabledKey, out enabledValue);
                    bool.TryParse(enabledValue, out enabled);
                    var card = new ServiceSettingsCardModel {
                        ServiceId = definition.Id, Title = definition.Title, Description = definition.Description,
                        Icon = definition.Icon, Style = definition.Style,
                        Enabled = preservePosted ? posted.Enabled : enabled,
                        Revision = preservePosted ? posted.Revision : snapshot.Revision
                    };
                    foreach (var field in definition.Fields) {
                        string value;
                        if (preservePosted) {
                            value = null;
                            posted.Fields?.TryGetValue(field.Key, out value);
                        }
                        else snapshot.Values.TryGetValue(field.Key, out value);
                        card.Fields.Add(new ServiceSettingsFieldModel {
                            Key = field.Key, Label = field.Label, Type = field.Type,
                            Minimum = field.Minimum, Maximum = field.Maximum, MaxLength = field.MaxLength,
                            RequiredWhenEnabled = field.RequiredWhenEnabled, Value = value ?? string.Empty
                        });
                    }
                    page.Services.Add(card);
                }
                return View("Index", page);
            }
            catch (IOException) { return SettingsUnavailable(); }
            catch (UnauthorizedAccessException) { return SettingsUnavailable(); }
            catch (InvalidOperationException) { return SettingsUnavailable(); }
            catch (ConfigurationErrorsException) { return SettingsUnavailable(); }
        }

        private ActionResult SettingsUnavailable() {
            Trace.TraceError("Unable to read service settings.");
            return new HttpStatusCodeResult(503, "Service settings are temporarily unavailable.");
        }
    }
}
