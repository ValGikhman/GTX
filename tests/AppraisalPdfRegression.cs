using System;
using System.IO;
using System.Linq;
using GTX.Common;
public class AppraisalPdfRegression {
    public static int Main(string[] args) {
        try {
            if (args.Length != 1) throw new Exception("Pass the original Downloads/all.pdf watch list as the first argument.");
            var vins = new[] {"58ABZ1B11KU026567","1FTBR3X81MKA59855","JH4CU2F68DC006060","5J8TB4H53FL011596","19UUB3F56HA001127","WAU2FAFC8HN123717","1GNSKBKCXJR285896","3VW5T7BU9KM148523","58ADA1C11NU019993","ZASPAKBN9P7D56780"};
            var miles = new[] {116589,89675,179230,125598,91778,69578,93537,79014,34972,26124};
            using (var stream = File.OpenRead(args[0])) {
                var rows = AppraisalPdfParser.Parse(stream);
                if (rows.Count != 10) throw new Exception("Expected ten unique vehicles.");
                if (rows[0].Run != "A-0029" || rows[1].Run != "GL-0058" || rows[2].Run != "CC-0087")
                    throw new Exception("Run number was not mapped to the correct VIN.");
                for (var i=0;i<10;i++) {
                    if (rows[i].Vin != vins[i] || rows[i].Miles != miles[i] || !string.IsNullOrEmpty(rows[i].Warning) || string.IsNullOrWhiteSpace(rows[i].Vehicle))
                        throw new Exception("VIN, mileage, vehicle or warning mismatch at row " + (i+1));
                }
            }
            var rejected = false;
            try { using(var invalid = new MemoryStream(new byte[] {1,2,3})) AppraisalPdfParser.Parse(invalid); }
            catch (Exception) { rejected = true; }
            if (!rejected) throw new Exception("Invalid file was accepted.");
            Console.WriteLine("PASS: ten exact VIN/mileage pairs, vehicle labels, unique rows and malformed PDF rejection.");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
