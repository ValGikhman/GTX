using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Transactions;
using System.Web.Mvc;
using System.Xml.Linq;
using GTX;
using GTX.Common;
using GTX.Models;

internal static class SpecialsTests
{
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(args[1]); return 0; }
        catch (Exception ex) { Console.WriteLine(ex.Message); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string settingsPath)
    {
        foreach (var name in new[] { "Index", "ListPartial", "Create", "Edit", "Save", "Delete", "UploadImage" })
        {
            var method = typeof(SpecialsController).GetMethod(name);
            Check(method.IsDefined(typeof(RequireAdminRole), true), name + " must require admin access");
            if (new[] { "Save", "Delete", "UploadImage" }.Contains(name))
            {
                Check(method.IsDefined(typeof(HttpPostAttribute), true), name + " must require POST");
                Check(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), name + " must require CSRF protection");
            }
        }

        var settings = XDocument.Load(settingsPath);
        var dev = settings.Descendants().Single(e => e.Name.LocalName == "Setting" && (string)e.Attribute("Name") == "SiteDEV")
            .Elements().Single(e => e.Name.LocalName == "Value").Value.Trim();
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(dev);
        Check(builder.DataSource.Equals(@"localhost\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase), "Tests require the local development SQL instance");
        var service = new SpecialsService { connectionString = dev };
        // All fixture changes roll back, including published offers. Never alter existing records.
        using (var transaction = new TransactionScope())
        {
            var title = "Special test " + Guid.NewGuid().ToString("N");
            var special = new SpecialModel {
                Title = title, CardContent = "<p><strong>Offer</strong></p><script>alert(1)</script><img src='/offer.png' onerror='alert(1)'>"
            };
            special.Sanitize();
            Check(!special.CardContent.Contains("script") && !special.CardContent.Contains("onerror"), "Unsafe content must be removed");
            Check(special.CardContent.Contains("<strong>"), "Formatting must survive sanitization");
            Check(service.Save(special), "Create failed");
            special = service.GetAll(true).Single(s => s.Title == title);
            Check(!service.GetAll().Any(s => s.Id == special.Id), "Draft leaked in public list");
            var controller = new SpecialsController(null, null, null, null, null, service);
            Check(controller.Special(special.Id) is HttpNotFoundResult, "Draft page must return 404");
            var created = special.CreatedAt;
            special.IsPublished = true;
            Check(service.Save(special), "Publish failed");
            Check(service.GetAll().Any(s => s.Id == special.Id), "Published card missing");
            var page = controller.Special(special.Id) as ViewResult;
            Check(page != null && ((SpecialModel)page.Model).CardContent == special.CardContent, "Page must use the card content");
            special.CreatedAt = DateTime.MinValue;
            Check(service.Save(special), "Card save failed");
            Check(service.GetById(special.Id).CreatedAt == created, "Update changed creation date");
            special.IsPublished = false;
            Check(service.Save(special), "Unpublish failed");
            Check(!service.GetAll().Any(s => s.Id == special.Id), "Unpublished card is still public");
            Check(controller.Special(special.Id) is HttpNotFoundResult, "Unpublished page is still public");
            Check(service.Delete(special.Id) && service.GetById(special.Id) == null, "Delete failed");
            Check(!service.Delete(special.Id), "Missing delete should report failure");
        }
        Console.WriteLine("PASS: admin/CSRF protection, sanitization, SQL CRUD, draft visibility, shared card/page content, unpublishing and creation date preservation (fixtures rolled back).");
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
