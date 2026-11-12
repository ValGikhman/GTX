using GTX.Common;
using GTX.Controllers;
using GTX.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml;

internal static class ServiceConfigurationTests {
    private static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var assemblyPath = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        };
        try { Run(args[1]); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string mode) {
        // Both the provider's App_Data fallback and legacy-file lookup stay inside this fixture.
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
        var path = Path.Combine(directory, "Services.config");
        Directory.CreateDirectory(directory);
        Check(!File.Exists(path), "The isolated service file must start empty");
        Check(ServiceConfiguration.Services.Count == 5, "There must be exactly five service cards");
        Check(ServiceConfiguration.Services.Select(s => s.Id).Distinct().Count() == 5, "Service identifiers must be unique");

        var original = ServiceConfiguration.Read();
        if (mode == "defaults") {
            foreach (var service in ServiceConfiguration.Services) {
                Check(!ServiceConfiguration.Enabled(service.EnabledKey), "An unconfigured service must start disabled");
                foreach (var field in service.Fields)
                    Check(ServiceConfiguration.Get(field.Key) == field.DefaultValue, "An unconfigured field must retain its documented default");
            }
            Check(!File.Exists(path), "Reading defaults must not create a credential file");
            Check(ServiceConfiguration.Get("unregistered-setting") == null, "Unknown setting lookups must not reveal arbitrary app settings");
            CheckControllerProtection();
            Console.WriteLine("PASS: isolated five-service defaults and administrator/POST/anti-forgery controller protection");
            return;
        }

        Check(ServiceConfiguration.Enabled("OpenAI:ChatBot"), "Legacy enabled state was not imported");
        Check(ServiceConfiguration.Get("OpenAI:ApiKey") == "legacy-openai-test-key", "Legacy API key was not imported");
        Check(ServiceConfiguration.Enabled("RemoveBg:Enabled"), "Existing remove.bg credentials must preserve the legacy enabled behavior");
        Check(!ServiceConfiguration.Enabled("DataOne"), "A legacy disabled service must remain disabled");
        Check(!File.Exists(path), "Legacy bootstrap reads must not silently persist credentials");

        var raw = " raw<&>\"'`$()=+ ";
        var openai = Fields("openai");
        openai["OpenAI:ApiKey"] = raw;
        ServiceConfiguration.Save("openai", true, openai, original.Revision);
        var firstSaved = ServiceConfiguration.Read();
        Check(firstSaved.Revision != original.Revision, "Editing a setting must change the nonsecret revision");
        Check(firstSaved.Revision == ServiceConfiguration.Read().Revision, "Unchanged settings must have a stable revision");
        Check(ServiceConfiguration.Get("OpenAI:ApiKey") == raw, "Raw credentials must round-trip without encryption or trimming");
        foreach (var pair in original.Values.Where(p => !openai.ContainsKey(p.Key) && p.Key != "OpenAI:ChatBot"))
            Check(firstSaved.Values[pair.Key] == pair.Value, "Saving one service card must preserve every other card");
        var document = new XmlDocument { XmlResolver = null };
        document.Load(path);
        var stored = document.DocumentElement.ChildNodes.Cast<XmlNode>().Single(n => n.Attributes["key"].Value == "OpenAI:ApiKey");
        Check(stored.Attributes["value"].Value == raw, "XML escaping must preserve the exact raw stored credential");
        CheckNoTemporaryFiles(directory);

        var unchanged = File.ReadAllText(path);
        Throws<InvalidOperationException>(() => ServiceConfiguration.Save("dataone", false, Fields("dataone"), original.Revision), "A stale card revision must not overwrite newer settings");
        Check(File.ReadAllText(path) == unchanged, "A stale save changed the private file");
        var injected = Fields("openai");
        injected.Remove("OpenAI:ChatModel");
        injected.Add("DataOne:AccessKey", "cross-card-test-key");
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", false, injected, firstSaved.Revision), "Cross-card setting injection must be rejected");
        Throws<ValidationException>(() => ServiceConfiguration.Save("missing-service", false, new Dictionary<string, string>(), firstSaved.Revision), "Unknown service writes must be rejected");
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", false, new Dictionary<string, string>(), firstSaved.Revision), "Incomplete service cards must be rejected");
        Check(File.ReadAllText(path) == unchanged, "Invalid service payloads changed the private file");

        openai = Fields("openai");
        openai["OpenAI:ApiKey"] = "";
        ServiceConfiguration.Save("openai", false, openai, firstSaved.Revision);
        Check(ServiceConfiguration.Get("OpenAI:ApiKey") == "", "Cleared credentials must not fall back to a legacy API key");
        Check(!ServiceConfiguration.Enabled("OpenAI:ChatBot"), "A saved disabled service must remain disabled");
        var cleared = ServiceConfiguration.Read();
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", true, openai, cleared.Revision), "Enabling a service without required credentials must be rejected");
        openai["OpenAI:ApiKey"] = "replacement-test-key";
        openai["OpenAI:ResponsesUrl"] = "https://example.test/responses?credential=invalid";
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", true, openai, cleared.Revision), "Credential-bearing URL queries must be rejected");
        openai["OpenAI:ResponsesUrl"] = "https://example.test/responses";
        openai["OpenAI:ChatTimeoutSeconds"] = "121";
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", true, openai, cleared.Revision), "Out-of-range timeouts must be rejected");
        openai["OpenAI:ChatTimeoutSeconds"] = "30";
        openai["OpenAI:ApiKey"] = "invalid\ncredential";
        Throws<ValidationException>(() => ServiceConfiguration.Save("openai", true, openai, cleared.Revision), "Control characters in credentials must be rejected");

        HttpContext.Current = Context();
        var requestSnapshot = ServiceConfiguration.Read();
        Check(ServiceConfiguration.Enabled("RemoveBg:Enabled"), "The fixture's remove.bg service must initially be enabled");
        SetStoredValue(path, "RemoveBg:Enabled", "false");
        Check(object.ReferenceEquals(requestSnapshot, ServiceConfiguration.Read()), "A request must use one consistent configuration snapshot");
        Check(ServiceConfiguration.Enabled("RemoveBg:Enabled"), "External edits must not change a request already using its snapshot");
        HttpContext.Current = Context();
        Check(!ServiceConfiguration.Enabled("RemoveBg:Enabled"), "A new request must observe an external switch change");
        var removebg = Fields("removebg");
        ServiceConfiguration.Save("removebg", true, removebg, ServiceConfiguration.Read().Revision);
        Check(ServiceConfiguration.Enabled("RemoveBg:Enabled"), "A save must refresh the saving request's snapshot");
        HttpContext.Current = null;

        var validXml = File.ReadAllText(path);
        var revision = ServiceConfiguration.Read().Revision;
        using (var locked = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Throws<InvalidOperationException>(() => ServiceConfiguration.Save("dataone", false, Fields("dataone"), revision), "Overlapping writers must be rejected without overwriting data");
        Check(File.ReadAllText(path) == validXml, "A rejected concurrent writer changed settings");
        CheckNoTemporaryFiles(directory);

        foreach (var invalidXml in new[] {
            "<services", "<other />",
            "<services><add key=\"OpenAI:ApiKey\" value=\"a\"/><add key=\"OpenAI:ApiKey\" value=\"b\"/></services>",
            "<services><add key=\"unknown-setting\" value=\"a\"/></services>",
            "<services><add key=\"OpenAI:ApiKey\"/></services>",
            "<!DOCTYPE services [<!ENTITY value 'test'>]><services><add key=\"OpenAI:ApiKey\" value=\"&value;\"/></services>"
        }) {
            File.WriteAllText(path, invalidXml);
            Throws<ConfigurationErrorsException>(() => ServiceConfiguration.Read(), "Malformed, duplicate, unknown and DTD-bearing XML must fail closed");
        }
        File.WriteAllText(path, "<services><add key=\"OpenAI:ChatBot\" value=\"false\" /></services>");
        Check(ServiceConfiguration.Get("OpenAI:ApiKey") == "", "A partial private configuration must not reactivate legacy credentials");
        File.WriteAllText(path, validXml);
        Check(ServiceConfiguration.Read().Revision == revision, "Restoring the exact settings must restore their revision");
        CheckInvalidConfigurationController(path);
        CheckControllerPost(path, raw);
        CheckConcurrentReadersAndSaves(path);
        CheckNoTemporaryFiles(directory);
        Console.WriteLine("PASS: isolated legacy bootstrap, exact raw credential persistence, per-card merge, authoritative clearing, live switches, conflict/injection/validation rejection, malformed XML/DTD denial, safe controller construction and 503 handling, concurrent atomic reads/saves, atomic-file cleanup and actual MVC dictionary binding/controller saves");
    }

    private static void CheckInvalidConfigurationController(string path) {
        var originalXml = File.ReadAllText(path);
        var previousContext = HttpContext.Current;
        const string credential = "synthetic-credential-must-not-reach-the-error";
        try {
            File.WriteAllText(path, "<services><add key=\"OpenAI:ApiKey\" value=\"" + credential + "\"/>");
            var output = new StringWriter();
            var context = new HttpContext(new HttpRequest("", "https://services.example.test/Services", ""), new HttpResponse(output));
            HttpContext.Current = context;

            // Exercise the real constructor: malformed settings must be handled by MVC, not field initializers.
            var controller = new ServicesController(null, null, null, null, null);
            var route = new RouteData();
            route.Values["controller"] = "Services";
            route.Values["action"] = "Index";
            controller.ControllerContext = new ControllerContext(new HttpContextWrapper(context), route, controller);
            var descriptor = new ReflectedActionDescriptor(typeof(ServicesController).GetMethod("Index"), "Index",
                new ReflectedControllerDescriptor(typeof(ServicesController)));
            var executing = new ActionExecutingContext(controller.ControllerContext, descriptor, new Dictionary<string, object>());
            typeof(BaseController).GetMethod("OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { executing });

            var preflightResult = executing.Result as HttpStatusCodeResult;
            Check(preflightResult != null && preflightResult.StatusCode == 503,
                "Malformed settings must produce a controlled 503 before controller initialization");
            Check(controller.Model == null, "Configuration failure must stop initialization before inventory or session dependencies are used");
            preflightResult.ExecuteResult(controller.ControllerContext);
            Check(context.Response.StatusCode == 503 && !context.Response.StatusDescription.Contains(credential) && !output.ToString().Contains(credential),
                "The action preflight's HTTP error must not reveal the malformed credential");

            var directResult = controller.Index() as HttpStatusCodeResult;
            Check(directResult != null && directResult.StatusCode == 503,
                "The Services Index action must handle malformed settings when invoked directly");
            Check(!directResult.StatusDescription.Contains(credential), "The direct action's error must not reveal a credential");
        }
        finally {
            HttpContext.Current = previousContext;
            File.WriteAllText(path, originalXml);
        }
    }

    private static void CheckConcurrentReadersAndSaves(string path) {
        var original = ServiceConfiguration.Read();
        var originalFields = Fields("dataone");
        var originalEnabled = original.Values["DataOne"] == "true";
        var concurrentFields = new Dictionary<string, string>(originalFields, StringComparer.Ordinal);
        var failures = new List<Exception>();
        var readCount = 0;
        var readers = new List<Thread>();
        using (var stop = new ManualResetEventSlim(false))
        using (var firstReads = new CountdownEvent(4)) {
            try {
                var initial = "parallel-generation-0-" + new string('x', 2000);
                concurrentFields["DataOne:AccessKey"] = initial;
                concurrentFields["DataOne:SecretAccessKey"] = initial;
                ServiceConfiguration.Save("dataone", false, concurrentFields, original.Revision);
                for (var readerIndex = 0; readerIndex < 4; readerIndex++) {
                    var reader = new Thread(() => {
                        HttpContext.Current = null;
                        var firstRead = true;
                        try {
                            while (!stop.IsSet) {
                                var snapshot = ServiceConfiguration.Read();
                                Check(snapshot.Values["DataOne:AccessKey"] == snapshot.Values["DataOne:SecretAccessKey"],
                                    "An atomic read observed fields from different saved generations (access length "
                                    + snapshot.Values["DataOne:AccessKey"].Length + ", secret length "
                                    + snapshot.Values["DataOne:SecretAccessKey"].Length + ", legacy bootstrap "
                                    + (snapshot.Values["DataOne:AccessKey"] == "legacy-dataone-test-key"
                                        && snapshot.Values["DataOne:SecretAccessKey"] == "legacy-dataone-test-secret") + ")");
                                Interlocked.Increment(ref readCount);
                                if (firstRead) { firstRead = false; firstReads.Signal(); }
                            }
                        }
                        catch (Exception ex) { lock (failures) failures.Add(ex); }
                    }) { IsBackground = true };
                    readers.Add(reader);
                    reader.Start();
                }
                Check(firstReads.Wait(TimeSpan.FromSeconds(5)), "Concurrent configuration readers did not start within the bounded fixture");
                var timer = Stopwatch.StartNew();
                for (var generation = 1; generation <= 64; generation++) {
                    Check(timer.Elapsed < TimeSpan.FromSeconds(10), "Concurrent atomic-save regression exceeded its time budget");
                    var value = "parallel-generation-" + generation + "-" + new string('x', 2000);
                    concurrentFields["DataOne:AccessKey"] = value;
                    concurrentFields["DataOne:SecretAccessKey"] = value;
                    ServiceConfiguration.Save("dataone", false, concurrentFields, ServiceConfiguration.Read().Revision);
                }
            }
            finally {
                stop.Set();
                foreach (var reader in readers)
                    Check(reader.Join(TimeSpan.FromSeconds(5)), "A configuration reader did not stop within the bounded fixture");
                ServiceConfiguration.Save("dataone", originalEnabled, originalFields, ServiceConfiguration.Read().Revision);
            }
        }
        Check(readCount >= 4, "Concurrent service saves must overlap actual configuration reads");
        if (failures.Count != 0)
            throw new InvalidOperationException("A concurrent reader failed during atomic service replacement.", failures[0]);
        Check(ServiceConfiguration.Read().Revision == original.Revision, "The concurrent fixture did not restore the original settings");
    }

    private static void CheckControllerProtection() {
        var controller = typeof(ServicesController);
        var permission = controller.GetCustomAttributes(typeof(RequireAdminRole), true).Cast<RequireAdminRole>().Single();
        Check(permission.RequireMajordome, "The Services controller must require the hidden administrator");
        var save = controller.GetMethod("Save");
        Check(save.IsDefined(typeof(HttpPostAttribute), true), "Saving credentials must require POST");
        Check(save.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), "Saving credentials must require an anti-forgery token");
        var input = save.GetCustomAttributes(typeof(ValidateInputAttribute), true).Cast<ValidateInputAttribute>().Single();
        Check(!input.EnableValidation, "Raw HTML-significant credential characters must reach encoded rendering and persistence");
    }

    private static void CheckControllerPost(string path, string raw) {
        var dataone = Fields("dataone");
        dataone["DataOne:SecretAccessKey"] = raw;
        var snapshot = ServiceConfiguration.Read();
        ModelBindingContext binding;
        var controller = Controller();
        var model = Bind(controller, "dataone", false, dataone, snapshot.Revision, out binding);
        Check(binding.ModelState.IsValid, "The actual MVC binder rejected a valid service-card payload");
        Check(model.Fields.Count == dataone.Count && model.Fields["DataOne:SecretAccessKey"] == raw,
            "Indexed key/value inputs must bind colon-bearing setting keys and raw values exactly");
        Check(!model.Enabled, "An unchecked service switch's hidden false value must bind as disabled");
        controller.ModelState.Merge(binding.ModelState);
        var result = controller.Save(model) as RedirectToRouteResult;
        Check(result != null && (string)result.RouteValues["action"] == "Index", "A valid controller save must redirect to the service page");
        Check((string)controller.TempData["SavedServiceId"] == "dataone", "A controller save must identify the saved card");
        var changed = ServiceConfiguration.Read();
        Check(changed.Values["DataOne:SecretAccessKey"] == raw, "A bound raw credential did not persist through the actual controller action");
        foreach (var pair in snapshot.Values.Where(p => !dataone.ContainsKey(p.Key) && p.Key != "DataOne"))
            Check(changed.Values[pair.Key] == pair.Value, "A controller save modified another service card");

        var unchanged = File.ReadAllText(path);
        controller = Controller();
        model = Bind(controller, "dataone", true, dataone, changed.Revision, out binding);
        Check(model.Enabled, "The checked checkbox plus hidden false value must bind as enabled");
        var injected = new Dictionary<string, string>(dataone) { ["OpenAI:ApiKey"] = "injected-key" };
        model = Bind(controller, "dataone", false, injected, changed.Revision, out binding);
        controller.ModelState.Merge(binding.ModelState);
        var rejected = controller.Save(model) as ViewResult;
        Check(rejected != null && !controller.ModelState.IsValid, "Cross-card fields must be rejected by the actual controller");
        Check(File.ReadAllText(path) == unchanged, "Rejected controller fields changed the private file");
        Check(controller.ModelState.Values.SelectMany(v => v.Errors).All(e => !e.ErrorMessage.Contains(raw)),
            "Controller validation messages must not contain raw credentials");

        controller = Controller();
        model = Bind(controller, "dataone", false, dataone, snapshot.Revision, out binding);
        controller.ModelState.Merge(binding.ModelState);
        Check(controller.Save(model) is ViewResult && !controller.ModelState.IsValid,
            "A stale controller save must return a reviewable validation error");
        Check(File.ReadAllText(path) == unchanged, "A stale controller save changed the private file");
    }

    private static ServicesController Controller() {
        // Bypass the web-host constructor: these tests must never load a configured SQL connection.
        var controller = (ServicesController)FormatterServices.GetUninitializedObject(typeof(ServicesController));
        controller.ControllerContext = new ControllerContext(new HttpContextWrapper(Context()), new RouteData(), controller);
        return controller;
    }

    private static ServiceSettingsSaveModel Bind(ServicesController controller, string serviceId, bool enabled,
        Dictionary<string, string> fields, string revision, out ModelBindingContext binding) {
        var form = new NameValueCollection { ["ServiceId"] = serviceId, ["Revision"] = revision };
        if (enabled) form.Add("Enabled", "true");
        form.Add("Enabled", "false");
        var index = 0;
        foreach (var pair in fields) {
            form.Add("Fields[" + index + "].Key", pair.Key);
            form.Add("Fields[" + index + "].Value", pair.Value);
            index++;
        }
        binding = new ModelBindingContext {
            ModelMetadata = ModelMetadataProviders.Current.GetMetadataForType(null, typeof(ServiceSettingsSaveModel)),
            ModelName = "", ModelState = new ModelStateDictionary(), FallbackToEmptyPrefix = true,
            ValueProvider = new NameValueCollectionValueProvider(form, CultureInfo.InvariantCulture)
        };
        return (ServiceSettingsSaveModel)new DefaultModelBinder().BindModel(controller.ControllerContext, binding);
    }

    private static Dictionary<string, string> Fields(string serviceId) {
        var snapshot = ServiceConfiguration.Read();
        return ServiceConfiguration.Services.Single(s => s.Id == serviceId).Fields
            .ToDictionary(f => f.Key, f => snapshot.Values[f.Key], StringComparer.Ordinal);
    }
    private static HttpContext Context() {
        return new HttpContext(new HttpRequest("", "https://services.example.test/", ""), new HttpResponse(new StringWriter()));
    }
    private static void SetStoredValue(string path, string key, string value) {
        var document = new XmlDocument { XmlResolver = null };
        document.Load(path);
        document.DocumentElement.ChildNodes.Cast<XmlNode>().Single(n => n.Attributes["key"].Value == key).Attributes["value"].Value = value;
        document.Save(path);
    }
    private static void CheckNoTemporaryFiles(string directory) {
        Check(Directory.GetFiles(directory, "Services.config.*.tmp").Length == 0, "An atomic save left a temporary credential file behind");
    }
    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
