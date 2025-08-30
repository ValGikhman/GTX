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

    public class EmployeesController : BaseController {

        private readonly IEmployeesService _employeesService;

        public EmployeesController(ISessionData sessionData, IEmployeesService employeesService, IInventoryService inventoryService, ILogService logService) :
            base(sessionData, inventoryService,  logService)  {
            _employeesService = employeesService;
        }

        [HttpGet]
        public ActionResult Index(int Id) {
            var employees = _employeesService.GetEmployees();
            if (employees == null) return HttpNotFound();

            try {
                return RedirectToAction("Index", employees);
            }
            catch (Exception ex) {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult Delete(int Id) {
            var employee = _employeesService.GetEmployee(Id);
            if (employee == null) return HttpNotFound();

            Log($"Deleting employee: {SerializeModel(employee)}");
            try {
                if (!string.IsNullOrWhiteSpace(employee.PhotoPath)) {
                    DeletePhoto(employee.PhotoPath);
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex) {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult Save(EmployeeModel model) {
            Log($"Saving employee: {SerializeModel(model)}");
            try {
                if (ModelState.IsValid) {
                    Employee employee = new Employee();
                    employee.FirstName = model.FirstName;
                    employee.LastName = model.LastName;
                    employee.Phone = model.Phone;
                    employee.Email = model.Email;
                    _employeesService.SaveEmployee(employee);

                    return Json(new { data = employee });
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