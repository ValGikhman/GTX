using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Xml;

namespace GTX.Common {
    public sealed class ServiceFieldDefinition {
        public string Key { get; internal set; }
        public string Label { get; internal set; }
        public string Type { get; internal set; }
        public string DefaultValue { get; internal set; }
        public int? Minimum { get; internal set; }
        public int? Maximum { get; internal set; }
        public int MaxLength { get; internal set; } = 2048;
        public bool RequiredWhenEnabled { get; internal set; }
    }

    public sealed class ServiceDefinition {
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string Description { get; internal set; }
        public string Icon { get; internal set; }
        public string Style { get; internal set; }
        public string EnabledKey { get; internal set; }
        public IReadOnlyList<ServiceFieldDefinition> Fields { get; internal set; }
    }

    public sealed class ServiceConfigurationSnapshot {
        public IReadOnlyDictionary<string, string> Values { get; internal set; }
        public string Revision { get; internal set; }
    }

    // Raw values live only in protected App_Data, independently of application publishing.
    public static class ServiceConfiguration {
        private const string RequestCacheKey = "GTX.ServiceConfiguration";
        private static volatile bool hasPrivateFile;
        public static readonly IReadOnlyList<ServiceDefinition> Services = Array.AsReadOnly(new[] {
            Service("openai", "OpenAI", "Chatbot, vehicle comparisons and generated stories.", "bi-stars", "openai", "OpenAI:ChatBot",
                Field("OpenAI:ApiKey", "API key", required: true),
                Field("OpenAI:ChatModel", "Model", "gpt-4.1-mini", required: true),
                Field("OpenAI:ResponsesUrl", "Responses API URL", "https://api.openai.com/v1/responses", "url", true),
                Number("OpenAI:ChatMaxOutputTokens", "Maximum chat output tokens", 250, 100, 1000),
                Number("OpenAI:ChatInventoryCacheMinutes", "Inventory cache (minutes)", 1, 1, 60),
                Number("OpenAI:ChatTimeoutSeconds", "Request timeout (seconds)", 30, 5, 120)),
            Service("dataone", "DataOne", "VIN specifications and inventory enrichment.", "bi-car-front", "dataone", "DataOne",
                Field("DataOne:AccessKey", "Access key", required: true),
                Field("DataOne:SecretAccessKey", "Secret access key", required: true)),
            Service("cargurus", "CarGurus & vehicle research", "CarGurus search and the shared VIN and appraisal research API.", "bi-graph-up-arrow", "marketcheck", "CarGurus:Enabled",
                Field("CarGurus:McpServerUrl", "CarGurus server URL", "https://mcp-apps.api.cargurus.com/chat-mcp/v1/stream", "url", true),
                Field("AutoDealer:AppraisalBaseUrl", "Vehicle research API URL", "", "url"),
                Field("AutoDealer:ApiKey", "Vehicle research API key")),
            Service("cloudflare", "Cloudflare", "Image delivery, cache management and R2 storage.", "bi-cloud", "cdn", "Cloudflare",
                Field("Images:NativeBaseUrl", "Native image base URL", "https://photos.usedcarscincinnati.com/Images", "url"),
                Field("Images:CloudflareBaseUrl", "Cloudflare image base URL", "https://pub-879e4994e6a64354b38b0729cd0c184c.r2.dev", "url", true),
                Field("Cloudflare:Cache:ZoneId", "Cache zone ID"),
                Field("Cloudflare:Cache:ApiToken", "Cache API token"),
                Field("Cloudflare:R2:BucketName", "R2 bucket name", "gtx", required: true),
                Field("Cloudflare:R2:AccessKeyId", "R2 access key ID", required: true),
                Field("Cloudflare:R2:SecretAccessKey", "R2 secret access key", required: true),
                Field("Cloudflare:R2:Endpoint", "R2 endpoint URL", "", "url", true)),
            Service("removebg", "Remove Background", "Background removal and account balance.", "bi-magic", "remove-bg", "RemoveBg:Enabled",
                Field("RemoveBg:ApiKey", "API key", required: true),
                Field("RemoveBg:Endpoint", "Background removal URL", "https://api.remove.bg/v1.0/removebg", "url", true),
                Field("RemoveBg:AccountEndpoint", "Account URL", "https://api.remove.bg/v1.0/account", "url", true))
        });

        private static ServiceDefinition Service(string id, string title, string description, string icon, string style, string enabledKey, params ServiceFieldDefinition[] fields) {
            return new ServiceDefinition { Id = id, Title = title, Description = description, Icon = icon, Style = style, EnabledKey = enabledKey, Fields = Array.AsReadOnly(fields) };
        }
        private static ServiceFieldDefinition Field(string key, string label, string value = "", string type = "text", bool required = false) {
            return new ServiceFieldDefinition { Key = key, Label = label, DefaultValue = value, Type = type, RequiredWhenEnabled = required };
        }
        private static ServiceFieldDefinition Number(string key, string label, int value, int minimum, int maximum) {
            var field = Field(key, label, value.ToString(CultureInfo.InvariantCulture), "number", true);
            field.Minimum = minimum; field.Maximum = maximum; return field;
        }

        public static string Get(string key) {
            string value;
            return Read().Values.TryGetValue(key, out value) ? value : null;
        }
        public static bool Enabled(string key) {
            bool enabled;
            return bool.TryParse(Get(key), out enabled) && enabled;
        }
        public static ServiceConfigurationSnapshot Read() {
            var context = HttpContext.Current;
            var snapshot = context?.Items[RequestCacheKey] as ServiceConfigurationSnapshot;
            if (snapshot == null) {
                snapshot = Load(ConfigurationPath());
                if (context != null) context.Items[RequestCacheKey] = snapshot;
            }
            return snapshot;
        }

        public static void Save(string serviceId, bool enabled, IDictionary<string, string> fields, string expectedRevision) {
            var service = Services.FirstOrDefault(s => s.Id == serviceId);
            if (service == null) throw new ValidationException("Unknown service.");
            if (fields == null || fields.Count != service.Fields.Count || service.Fields.Any(f => !fields.ContainsKey(f.Key)))
                throw new ValidationException("Submit every setting on this service card.");
            foreach (var field in service.Fields) Validate(field, fields[field.Key] ?? "", enabled);

            var path = ConfigurationPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // An exclusive file handle also serializes overlapping IIS workers.
            using (var writeLock = AcquireWriteLock(path + ".lock")) {
                var current = Load(path, allowBootstrap: true);
                if (!string.Equals(current.Revision, expectedRevision, StringComparison.Ordinal))
                    throw new InvalidOperationException("Settings changed in another window. Review the current settings and save again.");
                var values = current.Values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                values[service.EnabledKey] = enabled ? "true" : "false";
                foreach (var field in service.Fields) values[field.Key] = fields[field.Key] ?? "";
                Write(path, values);
            }
            HttpContext.Current?.Items.Remove(RequestCacheKey);
        }

        private static void Validate(ServiceFieldDefinition field, string value, bool enabled) {
            if (value.Length > field.MaxLength || value.Any(c => char.IsControl(c)))
                throw new ValidationException(field.Label + " contains an invalid value.");
            if (string.IsNullOrWhiteSpace(value)) {
                if (enabled && field.RequiredWhenEnabled) throw new ValidationException(field.Label + " is required when this service is enabled.");
                return;
            }
            if (field.Type == "number") {
                int number;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < field.Minimum || number > field.Maximum)
                    throw new ValidationException(field.Label + " must be between " + field.Minimum + " and " + field.Maximum + ".");
            }
            if (field.Type == "url") {
                Uri url;
                if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out url) || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Fragment) || !string.IsNullOrEmpty(url.Query))
                    throw new ValidationException(field.Label + " must be an HTTPS URL without credentials, a query or a fragment.");
            }
        }

        private static string ConfigurationPath() {
            return HostingEnvironment.MapPath("~/App_Data/Services.config")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Services.config");
        }
        private static ServiceConfigurationSnapshot Load(string path, bool allowBootstrap = false) {
            var values = Defaults();
            using (var stream = OpenConfiguration(path, allowBootstrap)) {
                if (stream != null) {
                    try {
                        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 };
                        using (var reader = XmlReader.Create(stream, settings)) {
                            var document = new XmlDocument { XmlResolver = null };
                            document.Load(reader);
                            if (document.DocumentElement?.Name != "services") throw new ConfigurationErrorsException("The private service settings file is invalid.");
                            var seen = new HashSet<string>(StringComparer.Ordinal);
                            foreach (XmlNode node in document.DocumentElement.ChildNodes) {
                                if (node.NodeType == XmlNodeType.Comment || node.NodeType == XmlNodeType.Whitespace) continue;
                                var key = node.Attributes?["key"]?.Value;
                                if (node.Name != "add" || key == null || !values.ContainsKey(key) || !seen.Add(key) || node.Attributes?["value"] == null)
                                    throw new ConfigurationErrorsException("The private service settings file contains an invalid setting.");
                                values[key] = node.Attributes["value"].Value;
                            }
                        }
                    }
                    catch (XmlException) { throw new ConfigurationErrorsException("The private service settings file is invalid."); }
                    // No legacy fallback once saved: clearing a credential must remain authoritative.
                }
                else {
                    foreach (var key in values.Keys.ToArray()) {
                        var legacy = ConfigurationManager.AppSettings[key];
                        if (legacy != null) values[key] = legacy;
                    }
                    if (ConfigurationManager.AppSettings["RemoveBg:Enabled"] == null)
                        values["RemoveBg:Enabled"] = string.IsNullOrWhiteSpace(values["RemoveBg:ApiKey"]) ? "false" : "true";
                    ImportLegacyR2(values);
                }
            }
            return new ServiceConfigurationSnapshot { Values = new ReadOnlyDictionary<string, string>(values), Revision = Revision(values) };
        }
        private static FileStream OpenConfiguration(string path, bool allowBootstrap) {
            // Windows replacement can briefly hide the destination path. Never interpret
            // that transition as permission to reactivate legacy credentials.
            for (var attempt = 0; ; attempt++) {
                try {
                    return OpenPrivateFile(path);
                }
                catch (IOException ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException
                    || (ex.HResult & 0xffff) == 32 || (ex.HResult & 0xffff) == 33) {
                    var missing = ex is FileNotFoundException || ex is DirectoryNotFoundException;
                    // Save holds the exclusive writer lock, so a missing file here is
                    // an initial bootstrap rather than another writer's replacement.
                    if (missing && allowBootstrap) return null;
                    FileStream bootstrap;
                    if (missing && !hasPrivateFile && TryOpenBootstrap(path, out bootstrap)) return bootstrap;
                    if (attempt >= 9) throw new IOException("Service settings are temporarily unavailable.");
                    Thread.Sleep(5);
                }
            }
        }
        private static FileStream OpenPrivateFile(string path) {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            hasPrivateFile = true;
            return stream;
        }
        private static bool TryOpenBootstrap(string path, out FileStream stream) {
            stream = null;
            try {
                // Hold a shared lock while verifying absence: a writer cannot start a
                // replacement between this probe and the decision to use legacy settings.
                using (var guard = new FileStream(path + ".lock", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    try { stream = OpenPrivateFile(path); }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    return true;
                }
            }
            catch (FileNotFoundException) { return true; }
            catch (DirectoryNotFoundException) { return true; }
            catch (IOException ex) when ((ex.HResult & 0xffff) == 32 || (ex.HResult & 0xffff) == 33) { return false; }
        }
        private static Dictionary<string, string> Defaults() {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var service in Services) {
                values.Add(service.EnabledKey, "false");
                foreach (var field in service.Fields) values.Add(field.Key, field.DefaultValue);
            }
            return values;
        }
        private static string Revision(IDictionary<string, string> values) {
            var text = new StringBuilder();
            foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal)) {
                text.Append(pair.Key.Length).Append(':').Append(pair.Key).Append(pair.Value.Length).Append(':').Append(pair.Value);
            }
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static FileStream AcquireWriteLock(string path) {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new InvalidOperationException("Service settings are being saved. Please try again."); }
        }
        private static void Write(string path, IDictionary<string, string> values) {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), CheckCharacters = true };
                using (var writer = XmlWriter.Create(temporary, settings)) {
                    writer.WriteStartElement("services");
                    foreach (var pair in values) {
                        writer.WriteStartElement("add"); writer.WriteAttributeString("key", pair.Key); writer.WriteAttributeString("value", pair.Value); writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void ImportLegacyR2(IDictionary<string, string> values) {
            var keys = new[] { "Cloudflare:R2:AccessKeyId", "Cloudflare:R2:SecretAccessKey", "Cloudflare:R2:Endpoint" };
            if (keys.All(k => !string.IsNullOrWhiteSpace(values[k]))) return;
            var appRoot = Path.GetDirectoryName(Path.GetDirectoryName(ConfigurationPath()));
            var path = new[] { Path.Combine(appRoot, "App_Data", "R2Credentials.txt"), Path.Combine(appRoot, "Your API Token.txt"), Path.Combine(Environment.CurrentDirectory, "Your API Token.txt") }.FirstOrDefault(File.Exists);
            if (path == null) return;
            var lines = File.ReadAllLines(path);
            var labels = new[] { "Access Key ID", "Secret Access Key", "S3 API endpoint" };
            for (var keyIndex = 0; keyIndex < keys.Length; keyIndex++) {
                if (!string.IsNullOrWhiteSpace(values[keys[keyIndex]])) continue;
                for (var i = 0; i < lines.Length - 1; i++) {
                    if (string.Equals(lines[i].Trim(), labels[keyIndex], StringComparison.OrdinalIgnoreCase)) { values[keys[keyIndex]] = lines[i + 1].Trim(); break; }
                }
            }
        }
    }
}
