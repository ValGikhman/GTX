using Services;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Data.Linq.Mapping;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

internal static class VinDecoderStorageTests {
    private const string Vin = "1HGCM82633A004352";
    private const string NeoVin = "1FTBR3X81MKA59855";
    private const string RaceVin = "58ABZ1B11KU026567";
    private const string Legacy = "<decoded_data><query_responses><query_response identifier=\"legacy\"><us_market_data /></query_response></query_responses></decoded_data>";
    private static Type envelope;
    private static int Main(string[] args) {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(args[1]); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string server) {
        envelope = typeof(InventoryService).Assembly.GetType("Services.VinDecoderReportEnvelope", true);
        PureEnvelope();
        Database(server);
    }
    private static void PureEnvelope() {
        var old = (VinDecoderReportDTO)Call("Read", Legacy, "dataone", Vin);
        Check(old.Provider == "dataone" && old.PayloadFormat == "xml" && old.FormatterKey == "dataone-v1" && XNode.DeepEquals(XElement.Parse(old.RawPayload), XElement.Parse(Legacy)), "Legacy XML must remain a DataOne report");
        Check(Call("Read", Legacy, "neovin", Vin) == null, "Legacy XML must not be presented as NeoVIN");
        var report = Report("neovin", Vin);
        var merged = (string)Call("Merge", Legacy, report, Vin);
        var neo = (VinDecoderReportDTO)Call("Read", merged, "neovin", null);
        Check(neo.RawPayload == report.RawPayload && neo.Html == report.Html && neo.DecodedUtc == report.DecodedUtc, "JSON, HTML, XML-significant text and timestamps must round-trip exactly");
        Check(((VinDecoderReportDTO)Call("Read", merged, "dataone", null)).RawPayload == old.RawPayload, "Wrapping legacy XML must retain its DataOne payload");
        var document = XElement.Parse(merged);
        var future = XElement.Parse("<result provider=\"future\" format=\"future\"><payload>opaque &amp; retained</payload></result>");
        document.Add(future);
        var changed = Report("dataone", Vin);
        changed.RawPayload = Legacy.Replace("legacy", "changed");
        merged = (string)Call("Merge", document.ToString(), changed, Vin);
        Check(XNode.DeepEquals(XElement.Parse(merged).Elements("result").Single(r => (string)r.Attribute("provider") == "future"), future), "Merging a known provider must preserve future-provider entries");
        Check(((VinDecoderReportDTO)Call("Read", merged, "neovin", null)).RawPayload == report.RawPayload, "Updating DataOne must preserve NeoVIN");
        var removed = (string)Call("Remove", merged, "dataone");
        Check(Call("Read", removed, "dataone", null) == null && Call("Read", removed, "neovin", null) != null, "DataOne deletion must preserve NeoVIN");
        Check(Call("Remove", Legacy, "dataone") == null && (string)Call("Remove", Legacy, "neovin") == Legacy, "Legacy deletion must affect only DataOne");
        foreach (var operation in new[] { "Read", "Merge", "Remove" }) {
            var unknown = "<vin_decoder version=\"2\"><result provider=\"future\" /></vin_decoder>";
            Reject<InvalidOperationException>(() => {
                if (operation == "Read") Call(operation, unknown, "dataone", null);
                else if (operation == "Merge") Call(operation, unknown, report, Vin);
                else Call(operation, unknown, "dataone");
            }, "Unknown envelope versions must not be discarded");
        }
        Check(Call("DataOnePayload", "<vin_decoder version=\"2\"><result provider=\"future\" /></vin_decoder>") == null,
            "Unsupported envelope versions must not break compatibility inventory reads");
        var compatible = XElement.Parse(merged);
        compatible.Elements("result").Single(node => (string)node.Attribute("provider") == "dataone").SetAttributeValue("decodedUtc", "irrelevant-invalid-timestamp");
        Check(XNode.DeepEquals(XElement.Parse((string)Call("DataOnePayload", compatible.ToString())), XElement.Parse(changed.RawPayload)), "Compatibility reads need only native DataOne XML, not report timestamp metadata");
        compatible.Elements("result").Single(node => (string)node.Attribute("provider") == "dataone").SetAttributeValue("formatter", "future-formatter");
        Check(Call("DataOnePayload", compatible.ToString()) == null, "Unsupported formatter metadata must not enter the existing DataOne parser");
        var invalid = Report("neovin", Vin);
        invalid.FormatterKey = "dataone-v1";
        Reject<ArgumentException>(() => Call("Merge", null, invalid, Vin), "Provider/formatter mismatches must be rejected");
        invalid = Report("neovin", Vin); invalid.RawPayload = "not-json";
        Reject<ArgumentException>(() => Call("Merge", null, invalid, Vin), "Malformed NeoVIN JSON must be rejected");
        invalid = Report("dataone", Vin); invalid.RawPayload = "<!DOCTYPE decoded_data [<!ENTITY x 'unsafe'>]><decoded_data>&x;</decoded_data>";
        Reject<InvalidOperationException>(() => Call("Merge", null, invalid, Vin), "DTD-bearing XML must be rejected");
        Console.WriteLine("PASS: legacy compatibility, independent provider merges/deletes, raw JSON/HTML preservation, formatter allowlist, future-provider retention and unknown-version/DTD rejection");
    }
    private static void Database(string server) {
        var local = @"^(?:\.|localhost|127\.0\.0\.1|" + Regex.Escape(Environment.MachineName) + @")(?:\\[A-Za-z0-9_.$-]+)?$";
        Check(Regex.IsMatch(server, local, RegexOptions.IgnoreCase) || Regex.IsMatch(server, @"^\(localdb\)\\[A-Za-z0-9_.$-]+$", RegexOptions.IgnoreCase), "Only local SQL Server fixtures are allowed");
        var name = "GTX_Vin_Storage_Test_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = "master", IntegratedSecurity = true, ConnectTimeout = 10 }.ConnectionString;
        var fixture = new SqlConnectionStringBuilder(master) { InitialCatalog = name }.ConnectionString;
        var created = false;
        try {
            Sql(master, "CREATE DATABASE [" + name + "]"); created = true;
            CreateSchema(fixture);
            foreach (var item in new[] { new[] { "LEGACY1", Vin }, new[] { "ONLYNEO", NeoVin }, new[] { "RACE1", RaceVin }, new[] { "AMBIG1", "4T1BE46K19U342407" }, new[] { "AMBIG2", "4T1BE46K19U342407" } })
                Sql(fixture, "INSERT dbo.Inventory(Stock,VIN,DateCreated,SetToUpload,Year,Make,Model) VALUES(@stock,@vin,GETDATE(),'Y',2021,'Fixture','Vehicle')", new SqlParameter("@stock", item[0]), new SqlParameter("@vin", item[1]));
            var legacyId = Guid.NewGuid();
            Sql(fixture, "INSERT dbo.Details(Id,Stock,DataOneContent,DateCreated) VALUES(@id,'LEGACY1',@xml,GETDATE())", new SqlParameter("@id", legacyId), new SqlParameter("@xml", SqlDbType.Xml) { Value = Legacy });
            var inventory = Service(fixture);
            Check(inventory.GetInventoryStockByVin(" " + Vin.ToLowerInvariant() + " ") == "LEGACY1", "Inventory lookup must match a normalized exact VIN");
            Check(inventory.GetInventoryStockByVin("2HGCM82633A004352") == null, "A missing VIN must not select an unrelated stock");
            Reject<InvalidOperationException>(() => inventory.GetInventoryStockByVin("4T1BE46K19U342407"), "Ambiguous inventory VINs must be rejected");
            var neo = Report("neovin", Vin);
            inventory.SaveVinDecoderReport("LEGACY1", neo);
            Check(inventory.GetDataOneDetails("LEGACY1") == Legacy, "Existing DataOne reads must unwrap legacy data after a NeoVIN save");
            Check(inventory.GetVinDecoderReport("LEGACY1", "neovin").RawPayload == neo.RawPayload, "NeoVIN must be saved independently in the same row");
            Check((Guid)Scalar(fixture, "SELECT Id FROM dbo.Details WHERE Stock='LEGACY1'") == legacyId, "Merging providers must preserve the existing row identity");
            Reject<InvalidOperationException>(() => inventory.SaveVinDecoderReport("LEGACY1", Report("neovin", NeoVin)), "A result for another VIN must not overwrite an inventory stock");
            Reject<InvalidOperationException>(() => inventory.SaveVinDecoderReport("MISSING", neo), "Reports may only be saved for current inventory");
            inventory.SaveVinDecoderReport("ONLYNEO", Report("neovin", NeoVin));
            Check(inventory.GetDataOneDetails("ONLYNEO") == null, "A NeoVIN-only row must not appear as DataOne");
            var standard = inventory.GetInventory(true, false).vehicles;
            Check(standard.Single(v => v.Stock.Trim() == "LEGACY1").HasDataOne && !standard.Single(v => v.Stock.Trim() == "ONLYNEO").HasDataOne && standard.All(v => v.DataOne == null), "Standard inventory must use provider-specific flags without loading payload DTOs");
            var full = inventory.GetInventory(true, true).vehicles;
            Check(full.Single(v => v.Stock.Trim() == "LEGACY1").DataOne.DataOneContent == Legacy && full.Single(v => v.Stock.Trim() == "ONLYNEO").DataOne == null, "Full inventory must expose only native DataOne XML through existing DTOs");
            inventory.SaveDataOneDetails("LEGACY1", Legacy.Replace("legacy", "updated"));
            Check(inventory.GetVinDecoderReport("LEGACY1", "neovin").RawPayload == neo.RawPayload, "Legacy DataOne saves must preserve NeoVIN");
            var decoder = (VinDecoderService)FormatterServices.GetUninitializedObject(typeof(VinDecoderService)); decoder.connectionString = fixture;
            decoder.DeleteDataOneDetails("LEGACY1");
            Check(inventory.GetDataOneDetails("LEGACY1") == null && inventory.GetVinDecoderReport("LEGACY1", "neovin") != null, "Legacy DataOne deletion must leave the other provider intact");
            Task.WhenAll(Task.Run(() => Service(fixture).SaveDataOneDetails("RACE1", Legacy)), Task.Run(() => Service(fixture).SaveVinDecoderReport("RACE1", Report("neovin", RaceVin)))).GetAwaiter().GetResult();
            Check(inventory.GetDataOneDetails("RACE1") == Legacy && inventory.GetVinDecoderReport("RACE1", "neovin") != null && (int)Scalar(fixture, "SELECT COUNT(*) FROM dbo.Details WHERE Stock='RACE1'") == 1, "Concurrent first saves must retain both providers in one row");
            var next = Report("neovin", RaceVin); next.RawPayload = "{\"updated\":true}";
            Task.WhenAll(Task.Run(() => decoder.DeleteDataOneDetails("RACE1")), Task.Run(() => Service(fixture).SaveVinDecoderReport("RACE1", next))).GetAwaiter().GetResult();
            Check(inventory.GetDataOneDetails("RACE1") == null && inventory.GetVinDecoderReport("RACE1", "neovin").RawPayload == next.RawPayload, "Concurrent delete/save must preserve the surviving provider update");
            var unknown = "<vin_decoder version=\"2\"><result provider=\"future\" /></vin_decoder>";
            Sql(fixture, "UPDATE dbo.Details SET DataOneContent=@xml WHERE Stock='ONLYNEO'", new SqlParameter("@xml", SqlDbType.Xml) { Value = unknown });
            var storedUnknown = (string)Scalar(fixture, "SELECT CONVERT(nvarchar(max),DataOneContent) FROM dbo.Details WHERE Stock='ONLYNEO'");
            Reject<InvalidOperationException>(() => inventory.SaveVinDecoderReport("ONLYNEO", Report("neovin", NeoVin)), "Unknown persisted versions must not be overwritten");
            Check((string)Scalar(fixture, "SELECT CONVERT(nvarchar(max),DataOneContent) FROM dbo.Details WHERE Stock='ONLYNEO'") == storedUnknown, "A rejected save changed an unsupported envelope");
            inventory.SaveDataOneDetails("LEGACY1", Legacy);
            Reject<InvalidOperationException>(() => inventory.GetDataOneDetails("ONLYNEO"), "Explicit DataOne reads must reject unknown versions rather than signal a missing paid decode");
            Reject<InvalidOperationException>(() => inventory.GetVinDecoderReport("ONLYNEO", "neovin"), "Explicit provider report reads must reject unknown versions");
            var mixed = inventory.GetInventory(true, true).vehicles;
            Check(mixed.Single(v => v.Stock.Trim() == "LEGACY1").DataOne.DataOneContent == Legacy && mixed.Single(v => v.Stock.Trim() == "ONLYNEO").DataOne == null,
                "One unsupported record must not prevent valid DataOne inventory payloads from loading");
            var unsupportedFormatter = "<vin_decoder version=\"1\"><result provider=\"dataone\" format=\"xml\" formatter=\"future\"><payload>" + Legacy + "</payload></result></vin_decoder>";
            Sql(fixture, "INSERT dbo.Details(Id,Stock,DataOneContent,DateCreated) VALUES(NEWID(),'AMBIG1',@xml,GETDATE())", new SqlParameter("@xml", SqlDbType.Xml) { Value = unsupportedFormatter });
            mixed = inventory.GetInventory(true, true).vehicles;
            var flags = inventory.GetInventory(true, false).vehicles;
            Check(mixed.Single(v => v.Stock.Trim() == "AMBIG1").DataOne == null && !flags.Single(v => v.Stock.Trim() == "AMBIG1").HasDataOne,
                "Unsupported provider formatter metadata must not break inventory or advertise DataOne availability");
            Console.WriteLine("PASS: isolated SQL legacy/provider merges, row identity, inventory-only/VIN validation, lightweight flags/full XML DTOs, provider-scoped delete, concurrent save/delete and unknown-version preservation");
        }
        finally {
            if (created) {
                Check(Regex.IsMatch(name, @"^GTX_Vin_Storage_Test_[a-f0-9]{32}$"), "Unexpected fixture database name");
                SqlConnection.ClearAllPools();
                Sql(master, "ALTER DATABASE [" + name + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + name + "]");
            }
        }
    }
    private static void CreateSchema(string connection) {
        var columns = typeof(CurrentInventory).GetProperties().Select(property => new { Property = property, Column = property.GetCustomAttributes(typeof(ColumnAttribute), true).Cast<ColumnAttribute>().FirstOrDefault() }).Where(item => item.Column != null).Select(item => {
            var name = string.IsNullOrEmpty(item.Column.Name) ? item.Property.Name : item.Column.Name;
            return "[" + name.Replace("]", "]]") + "] " + item.Column.DbType + (item.Property.PropertyType == typeof(Guid) && item.Column.IsPrimaryKey ? " DEFAULT NEWID()" : "");
        });
        Sql(connection, "CREATE TABLE dbo.Inventory(" + string.Join(",", columns) + ",CONSTRAINT PK_Inventory PRIMARY KEY(id),CONSTRAINT UQ_Inventory_Stock UNIQUE(Stock));"
            + "CREATE TABLE dbo.Details(Id uniqueidentifier NOT NULL PRIMARY KEY,Stock nchar(10) NOT NULL UNIQUE REFERENCES dbo.Inventory(Stock),DataOneContent xml NOT NULL,DateCreated datetime NOT NULL);"
            + "CREATE TABLE dbo.Stories(Id uniqueidentifier NOT NULL PRIMARY KEY,Stock nchar(10) NOT NULL UNIQUE REFERENCES dbo.Inventory(Stock),Title nvarchar(255) NOT NULL,HtmlContent nvarchar(max) NOT NULL,DateCreated datetime NOT NULL);"
            + "CREATE TABLE dbo.InventoryLog(Id bigint IDENTITY PRIMARY KEY,DateCreated datetime NOT NULL);");
    }
    private static InventoryService Service(string connection) { var service = (InventoryService)FormatterServices.GetUninitializedObject(typeof(InventoryService)); service.connectionString = connection; return service; }
    private static VinDecoderReportDTO Report(string provider, string vin) {
        return new VinDecoderReportDTO { Vin = vin, Provider = provider, PayloadFormat = provider == "dataone" ? "xml" : "json", FormatterKey = provider == "dataone" ? "dataone-v1" : "neovin-v1", RawPayload = provider == "dataone" ? Legacy : "{\"note\":\"XML <& ]]> remains text\",\"items\":[1,2]}", Html = "<style>.fixture{color:red}</style><details>Native <& ]]> report</details>", DecodedUtc = new DateTime(2026, 11, 12, 12, 30, 0, DateTimeKind.Utc) };
    }
    private static object Call(string name, params object[] arguments) { try { return envelope.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments); } catch (TargetInvocationException ex) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; } }
    private static void Sql(string connection, string sql, params SqlParameter[] parameters) { using (var db = new SqlConnection(connection)) { db.Open(); using (var command = new SqlCommand(sql, db)) { command.Parameters.AddRange(parameters); command.ExecuteNonQuery(); } } }
    private static object Scalar(string connection, string sql) { using (var db = new SqlConnection(connection)) { db.Open(); using (var command = new SqlCommand(sql, db)) return command.ExecuteScalar(); } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException(message); }
}
