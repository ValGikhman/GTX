using GTX;
using GTX.Controllers;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

internal static class EmployeesCacheTests {
    private static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var dependency = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(dependency) ? Assembly.LoadFrom(dependency) : null;
        };
        try { Run(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    private static void Run() {
        using (var f = new Fixture()) {
            var old = f.CachedStaff();
            var edit = f.Model(); edit.FirstName = "Renamed"; edit.LastName = "Updated";
            Success(f.Controller.Save(edit, null));
            var fresh = f.CachedStaff();
            Check(!ReferenceEquals(old, fresh) && fresh.Single().FirstName == "Renamed" && fresh.Single().LastName == "Updated", "A successful edit must refresh the next public staff snapshot");
            Check(f.Employees.Reads == 2, "The staff snapshot must reload from the service after editing");
            edit = f.Model(); edit.Active = false;
            Success(f.Controller.Save(edit, null));
            Check(f.CachedStaff().Length == 0, "Deactivating an employee must remove them from public staff immediately");
            f.CheckUnrelatedCache();
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff();
            var model = f.Model(); model.Id = 0; model.FirstName = "New"; model.PhotoPath = null;
            Success(f.Controller.Create(model, null));
            var fresh = f.CachedStaff();
            Check(!ReferenceEquals(old, fresh) && fresh.Length == 2 && fresh.Any(e => e.FirstName == "New"), "Creating an employee must invalidate public staff");
            f.CheckUnrelatedCache();
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff(); f.Employees.FailSave = true;
            var edit = f.Model(); edit.FirstName = "Must not save";
            Failure(f.Controller.Save(edit, null), f);
            Check(ReferenceEquals(old, f.CachedStaff()) && f.CachedStaff().Single().FirstName == "Original", "Failed saves must retain the last successful staff cache");
            Check(f.Employees.GetEmployee(1).FirstName == "Original" && File.Exists(f.OldPhoto), "Failed saves must preserve stored data and its photo");
            f.CheckUnrelatedCache();
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff(); f.Employees.FailSave = true;
            var model = f.Model(); model.Id = 0;
            var upload = new Upload();
            Failure(f.Controller.Create(model, upload), f);
            Check(ReferenceEquals(old, f.CachedStaff()) && f.Employees.Rows.Count == 1, "Failed creation must not invalidate staff or invent an employee");
            Check(upload.SavedPath != null && !File.Exists(upload.SavedPath) && File.Exists(f.OldPhoto), "Failed creation must clean up only its newly uploaded photo");
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff();
            var upload = new Upload();
            Success(f.Controller.Save(f.Model(), upload));
            var fresh = f.CachedStaff();
            Check(!ReferenceEquals(old, fresh) && fresh.Single().PhotoPath != "/SiteImages/staff/old.png", "Successful photo replacement must refresh public staff");
            Check(File.Exists(upload.SavedPath) && !File.Exists(f.OldPhoto), "Successful photo replacement must keep the new file and remove the old file");
            Check(f.Employees.GetEmployee(1).PhotoPath == fresh.Single().PhotoPath, "Public staff must show the saved photo path");
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff(); f.Employees.FailSave = true;
            var upload = new Upload();
            Failure(f.Controller.Save(f.Model(), upload), f);
            Check(ReferenceEquals(old, f.CachedStaff()), "Failed photo replacement must not discard the valid public staff cache");
            Check(File.Exists(f.OldPhoto) && upload.SavedPath != null && !File.Exists(upload.SavedPath), "Failed photo replacement must preserve the old image and remove the unsuccessful upload");
            Check(f.Employees.GetEmployee(1).PhotoPath == "/SiteImages/staff/old.png", "Failed photo replacement must retain the stored photo reference");
        }
        using (var f = new Fixture()) {
            f.CachedStaff();
            Check(f.Controller.Delete(1) is RedirectToRouteResult, "Successful deletion must retain the existing redirect");
            Check(f.CachedStaff().Length == 0 && !File.Exists(f.OldPhoto), "Successful deletion must refresh public staff and remove its photo");
            Check(f.Log.Calls == 1, "Deletion should retain its audit log");
            f.CheckUnrelatedCache();
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff(); f.Employees.FailDelete = true;
            var result = f.Controller.Delete(1) as JsonResult;
            Check(result != null && f.Context.ResultResponse.StatusCode == 500, "Failed deletion must return an error");
            Check(ReferenceEquals(old, f.CachedStaff()) && f.Employees.Rows.Count == 1 && File.Exists(f.OldPhoto), "Failed deletion must preserve staff, cache, and the old photo");
        }
        using (var f = new Fixture()) {
            var old = f.CachedStaff();
            f.Controller.ModelState.AddModelError("FirstName", "Required");
            Check(f.Controller.Save(f.Model(), new Upload()) is PartialViewResult, "Invalid forms should retain validation behavior");
            Check(f.Employees.SaveCalls == 0 && ReferenceEquals(old, f.CachedStaff()) && File.Exists(f.OldPhoto), "Invalid forms must not write employee data or invalidate staff");
            Check(f.Controller.Delete(999) is HttpNotFoundResult && ReferenceEquals(old, f.CachedStaff()), "Missing employee deletion must not invalidate staff");
        }
        Console.WriteLine("PASS: actual employee create/edit/active/photo/delete actions invalidate only public staff after successful persistence; failed saves/deletes preserve cache and old portraits, unsuccessful uploads are removed, and invalid forms remain unchanged.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Success(ActionResult result) {
        var json = result as JsonResult;
        Check(json != null && (bool)json.Data.GetType().GetProperty("ok").GetValue(json.Data), "Successful employee changes must return ok=true");
    }
    private static void Failure(ActionResult result, Fixture f) {
        var json = result as JsonResult;
        Check(json != null && !(bool)json.Data.GetType().GetProperty("ok").GetValue(json.Data) && f.Context.ResultResponse.StatusCode == 500, "Failed employee persistence must not report success");
    }

    private sealed class Fixture : IDisposable {
        internal readonly string Root;
        internal readonly string OldPhoto;
        internal readonly Employees Employees = new Employees();
        internal readonly Logger Log = new Logger();
        internal readonly Context Context;
        internal readonly EmployeesController Controller;
        private readonly string otherKey = "employee-cache-unrelated:" + Guid.NewGuid();
        private readonly object otherValue = new object();
        internal Fixture() {
            Root = Path.Combine(Path.GetTempPath(), "gtx-employees-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "SiteImages", "staff"));
            OldPhoto = Path.Combine(Root, "SiteImages", "staff", "old.png");
            File.WriteAllText(OldPhoto, "existing offline photo fixture");
            Employees.Rows.Add(new Employee { Id = 1, FirstName = "Original", LastName = "Person", Position = "Demo", Email = "original@example.invalid", Phone = "5135550100", PhotoPath = "/SiteImages/staff/old.png", Active = true, CreatedDate = new DateTime(2026, 1, 1) });
            Context = new Context(Root);
            Controller = new EmployeesController(new Session(), Employees, null, null, Log);
            var routes = new RouteData(); routes.Values["controller"] = "Employees";
            Controller.ControllerContext = new ControllerContext(Context, routes, Controller);
            AppCache.Remove(Constants.EMPLOYERS_CACHE);
            AppCache.GetOrCreate(otherKey, () => otherValue);
        }
        internal EmployeeModel Model() { return EmployeeModel.FromEntity(Employees.GetEmployee(1)); }
        internal EmployeeModel[] CachedStaff() { return AppCache.GetOrCreate(Constants.EMPLOYERS_CACHE, () => Controller.GetEmployers(), minutes: 60); }
        internal void CheckUnrelatedCache() { Check(ReferenceEquals(otherValue, AppCache.GetOrCreate(otherKey, () => new object())), "Employee edits must not flush unrelated application caches"); }
        public void Dispose() {
            AppCache.Remove(Constants.EMPLOYERS_CACHE); AppCache.Remove(otherKey);
            var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Root);
            Check(path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("gtx-employees-files-", StringComparison.Ordinal), "Unexpected fixture directory");
            Directory.Delete(path, true);
            Controller.Dispose();
        }
    }
    private sealed class Employees : IEmployeesService {
        internal readonly List<Employee> Rows = new List<Employee>();
        internal int Reads, SaveCalls;
        internal bool FailSave, FailDelete;
        public Employee[] GetEmployees() { Reads++; return Rows.Select(Clone).ToArray(); }
        public Employee GetEmployee(int id) { return Clone(Rows.SingleOrDefault(e => e.Id == id)); }
        public bool SaveEmployee(Employee employee) {
            SaveCalls++;
            if (FailSave) return false;
            if (employee.Id == 0) { employee.Id = Rows.Max(e => e.Id) + 1; employee.CreatedDate = new DateTime(2026, 1, 2); }
            else Rows.RemoveAll(e => e.Id == employee.Id);
            Rows.Add(Clone(employee)); return true;
        }
        public bool DeleteEmployee(int id) { return !FailDelete && Rows.RemoveAll(e => e.Id == id) == 1; }
        private static Employee Clone(Employee e) { return e == null ? null : new Employee { Id=e.Id, FirstName=e.FirstName, LastName=e.LastName, Position=e.Position, Email=e.Email, Phone=e.Phone, PhotoPath=e.PhotoPath, Active=e.Active, CreatedDate=e.CreatedDate }; }
    }
    private sealed class Upload : HttpPostedFileBase {
        internal string SavedPath;
        public override int ContentLength => 12;
        public override string FileName => "replacement.png";
        public override void SaveAs(string filename) { SavedPath = filename; File.WriteAllText(filename, "new offline photo fixture"); }
    }
    private sealed class Context : HttpContextBase {
        internal readonly Response ResultResponse = new Response();
        private readonly Server server;
        internal Context(string root) { server = new Server(root); }
        public override HttpResponseBase Response => ResultResponse;
        public override HttpServerUtilityBase Server => server;
    }
    private sealed class Response : HttpResponseBase { public override int StatusCode { get; set; } = 200; }
    private sealed class Server : HttpServerUtilityBase {
        private readonly string root;
        internal Server(string root) { this.root = root; }
        public override string MapPath(string virtualPath) {
            Check(virtualPath.StartsWith("~/SiteImages/staff/", StringComparison.Ordinal), "Unexpected upload fixture path");
            var path = Path.GetFullPath(Path.Combine(root, virtualPath.Substring(2).Replace('/', Path.DirectorySeparatorChar)));
            Check(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Upload left fixture directory");
            return path;
        }
    }
    private sealed class Logger : ILogService {
        internal int Calls;
        public void Log(Log header, string action) { Calls++; }
        public void Log(Log header, CommonUnit.LogType type) { Calls++; }
        public void Log(Log header, Exception error) { Calls++; }
        public Log[] GetLogs() { return new Log[0]; }
    }
    private sealed class Session : ISessionData {
        public CommonUnit.Environment Environment { get; set; }
        public CommonUnit.Responsibility Responsibility { get; set; }
        public bool? IsMajordome { get; set; }
        public Log LogHeader { get; set; } = new Log();
        public T GetSession<T>(string key) { return default(T); }
        public T GetSession<T>(string key, T fallback) { return fallback; }
        public void SetSession(string key, object value) { }
    }
}
