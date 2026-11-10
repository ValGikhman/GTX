using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTX.Common {
    public static class AppraisalCsvParser {
        private static string Header(string value) => Regex.Replace((value ?? "").Trim('\uFEFF').ToLowerInvariant(), "[^a-z0-9]", "");
        public static List<AppraisalImportVehicle> Parse(Stream stream) {
            string text;
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 4096, true)) text = reader.ReadToEnd();
            if (text.IndexOf('\0') >= 0) throw new InvalidOperationException("Use a text CSV file with VIN and odometer mileage columns.");
            var firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var delimiter = new[] { ",", ";", "\t" }.OrderByDescending(d => CountDelimiters(firstLine, d[0])).First();
            var result = new List<AppraisalImportVehicle>();
            using (var parser = new TextFieldParser(new StringReader(text)) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true }) {
                parser.SetDelimiters(delimiter);
                if (parser.EndOfData) throw new InvalidOperationException("The CSV is empty.");
                var headers = parser.ReadFields().Select(Header).ToArray();
                var vinColumns = headers.Select((name,index) => new {name,index}).Where(h => new[] {"vin","vinnumber","vehiclevin","vehicleidentificationnumber","serialnumber"}.Contains(h.name)).ToArray();
                var mileColumns = headers.Select((name,index) => new {name,index}).Where(h => new[] {"miles","mileage","odometer","odometermiles","odometerreading","odomiles","odo","actualmiles","odometermi","mileagemiles"}.Contains(h.name)).ToArray();
                if (vinColumns.Length != 1 || mileColumns.Length != 1) throw new InvalidOperationException("CSV must have one VIN column and one Miles, Mileage or Odometer column in miles. Kilometers are not converted.");
                var vinIndex = vinColumns[0].index; var milesIndex = mileColumns[0].index;
                Func<string[],string,string> field = (fields,name) => { var index = Array.IndexOf(headers,name); return index >= 0 && index < fields.Length ? fields[index].Trim() : ""; };
                var runHeaders = new[] { "run", "runno", "runnumber", "line", "lineno", "linenumber" };
                while (!parser.EndOfData) {
                    var line = parser.LineNumber;
                    if (line > 10000) throw new InvalidOperationException("CSV has too many rows. Upload a smaller watch list.");
                    string[] fields;
                    try { fields = parser.ReadFields(); } catch (MalformedLineException) { throw new InvalidOperationException("CSV contains an invalid quoted row near line " + line + "."); }
                    if (fields == null || fields.All(string.IsNullOrWhiteSpace)) continue;
                    if (fields.Length != headers.Length) throw new InvalidOperationException("CSV row " + line + " has a different number of columns than the header.");
                    var vin = fields[vinIndex].Trim().ToUpperInvariant();
                    if (vin.Length == 0) continue;
                    if (!Regex.IsMatch(vin, "^[A-HJ-NPR-Z0-9]{17}$")) throw new InvalidOperationException("Invalid 17-character VIN in CSV row " + line + ".");
                    var mileage = Regex.Replace(fields[milesIndex].Trim(), @"\s*(mi|miles)$", "", RegexOptions.IgnoreCase);
                    int parsed;
                    int? miles = Regex.IsMatch(mileage, @"^(\d{1,3}(,\d{3})+|\d+)$") && int.TryParse(mileage.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed >= 0 && parsed <= 2000000 ? (int?)parsed : null;
                    var vehicle = field(fields,"vehicle");
                    if (vehicle.Length == 0) vehicle = string.Join(" ", new[] {field(fields,"year"),field(fields,"make"),field(fields,"model"),field(fields,"trim")}.Where(v => v.Length > 0));
                    var run = runHeaders.Select(name => field(fields, name)).FirstOrDefault(value => value.Length > 0);
                    var item = new AppraisalImportVehicle { Vin=vin, Run=run, Vehicle=vehicle.Length > 0 ? vehicle : "Vehicle", Miles=miles, Warning=miles.HasValue ? null : "Mileage missing or invalid in CSV; skipped." };
                    var existing = result.FirstOrDefault(row => row.Vin == vin);
                    if (existing == null) result.Add(item);
                    else {
                        if (existing.Run == null) existing.Run = item.Run;
                        if (existing.Miles != miles) {existing.Miles=null;existing.Warning="Conflicting mileages for this VIN in CSV; skipped.";}
                    }
                    if (result.Count > 100) throw new InvalidOperationException("Upload no more than 100 vehicles at a time.");
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("No vehicles with VINs were found in this CSV.");
            return result;
        }
        private static int CountDelimiters(string line, char delimiter) {
            var quoted=false;var count=0;
            for(var i=0;i<line.Length;i++) {if(line[i]=='"')quoted=!quoted;else if(!quoted && line[i]==delimiter)count++;}
            return count;
        }
    }
}
