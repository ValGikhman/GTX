using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GTX.Common {
    public sealed class AppraisalImportVehicle {
        public string Vin { get; set; }
        public string Vehicle { get; set; }
        public string Run { get; set; }
        public int? Miles { get; set; }
        public int Page { get; set; }
        public string Warning { get; set; }
    }
    public static class AppraisalPdfParser {
        private static readonly Regex VinPattern = new Regex("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.Compiled);
        public static List<AppraisalImportVehicle> Parse(Stream stream) {
            var result = new List<AppraisalImportVehicle>();
            using (var pdf = PdfDocument.Open(stream)) {
                if (pdf.NumberOfPages > 50) throw new InvalidOperationException("Upload a PDF with no more than 50 pages.");
                for (var pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++) {
                    var words = pdf.GetPage(pageNumber).GetWords().OrderByDescending(w => w.BoundingBox.Bottom).ToArray();
                    var lines = new List<List<Word>>();
                    foreach (var word in words) {
                        var line = lines.LastOrDefault();
                        if (line == null || Math.Abs(line[0].BoundingBox.Bottom - word.BoundingBox.Bottom) > 3) { line = new List<Word>(); lines.Add(line); }
                        line.Add(word);
                    }
                    foreach (var line in lines) line.Sort((a,b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
                    var header = lines.FirstOrDefault(line => line.Any(w => w.Text.Equals("Odometer", StringComparison.OrdinalIgnoreCase)) && line.Any(w => w.Text == "Model"));
                    var runColumn = FindRunColumn(header);
                    foreach (var vinLine in lines.Select((line,index) => new {line,index})) {
                        foreach (var vin in vinLine.line.Where(w => VinPattern.IsMatch(w.Text.ToUpperInvariant()))) {
                            var item = new AppraisalImportVehicle { Vin = vin.Text.ToUpperInvariant(), Page = pageNumber };
                            if (header != null) {
                                var odometer = header.First(w => w.Text.Equals("Odometer", StringComparison.OrdinalIgnoreCase)).BoundingBox.Left;
                                var color = header.FirstOrDefault(w => w.Text.Equals("Color", StringComparison.OrdinalIgnoreCase))?.BoundingBox.Left ?? double.MaxValue;
                                var row = vinLine.line.Any(w => Regex.IsMatch(w.Text, "^(19|20)[0-9]{2}$")) ? vinLine.line : vinLine.index > 0 ? lines[vinLine.index - 1] : null;
                                if (row != null && Math.Abs(row[0].BoundingBox.Bottom - vin.BoundingBox.Bottom) < 35) {
                                    if (runColumn.HasValue) {
                                        var run = string.Join(" ", row.Where(w => {
                                            var center = (w.BoundingBox.Left + w.BoundingBox.Right) / 2;
                                            return center >= runColumn.Value.Left && center < runColumn.Value.Right;
                                        }).Select(w => w.Text));
                                        if (run.Length > 0) item.Run = run;
                                    }
                                    var mileWords = row.Where(w => w.BoundingBox.Left >= odometer - 5 && w.BoundingBox.Left < color - 2 && Regex.IsMatch(w.Text, "^[0-9,]+$")).ToArray();
                                    int miles;
                                    if (mileWords.Length == 1 && int.TryParse(mileWords[0].Text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out miles) && miles >= 0 && miles <= 2000000) item.Miles = miles;
                                    var year = row.FirstOrDefault(w => Regex.IsMatch(w.Text, "^(19|20)[0-9]{2}$"));
                                    if (year != null) item.Vehicle = string.Join(" ", row.Where(w => w.BoundingBox.Left >= year.BoundingBox.Left && w.BoundingBox.Left < odometer - 5).Select(w => w.Text));
                                }
                            }
                            if (!item.Miles.HasValue) item.Warning = "Mileage could not be verified. Enter the odometer reading before appraisal.";
                            var existing = result.FirstOrDefault(v => v.Vin == item.Vin);
                            if (existing == null) result.Add(item);
                            else {
                                if (existing.Run == null) existing.Run = item.Run;
                                if (existing.Miles != item.Miles) { existing.Miles = null; existing.Warning = "This VIN has conflicting mileage readings. Verify and enter the correct reading."; }
                            }
                            if (result.Count > 100) throw new InvalidOperationException("Upload no more than 100 vehicles at a time.");
                        }
                    }
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("No readable VINs found. Use a text-based auction PDF; scanned images need OCR first.");
            return result;
        }

        private static (double Left, double Right)? FindRunColumn(List<Word> header) {
            if (header == null) return null;
            var index = header.FindIndex(w => Regex.IsMatch(w.Text, @"^run#?$", RegexOptions.IgnoreCase));
            if (index < 0) index = header.FindIndex(w => Regex.IsMatch(w.Text, @"^line#?$", RegexOptions.IgnoreCase));
            if (index < 0) return null;

            var last = index;
            if (last + 1 < header.Count && Regex.IsMatch(header[last + 1].Text, @"^(?:#|no\.?|number)$", RegexOptions.IgnoreCase)) last++;
            var left = index == 0 ? double.NegativeInfinity
                : (header[index - 1].BoundingBox.Right + header[index].BoundingBox.Left) / 2;
            var right = last + 1 == header.Count ? double.PositiveInfinity
                : (header[last].BoundingBox.Right + header[last + 1].BoundingBox.Left) / 2;
            return (left, right);
        }
    }
}
