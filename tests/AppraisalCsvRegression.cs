using System;
using System.IO;
using System.Text;
using GTX.Common;
class AppraisalCsvRegression {
    static System.Collections.Generic.List<AppraisalImportVehicle> Parse(string text) { using(var s=new MemoryStream(Encoding.UTF8.GetBytes(text))) return AppraisalCsvParser.Parse(s); }
    static void Reject(string text) {var rejected=false;try{Parse(text);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Invalid CSV accepted.");}
    static int Main() { try {
        var rows=Parse("\uFEFFVIN#,Odometer,Year,Make,Model\r\n58ABZ1B11KU026567,\"116,589\",2019,Lexus,ES 350\r\n1FTBR3X81MKA59855,89675,2021,Ford,T250\r\n");
        if(rows.Count!=2 || rows[0].Miles!=116589 || rows[0].Vehicle!="2019 Lexus ES 350" || rows[1].Miles!=89675)throw new Exception("VIN and miles mapping mismatch.");
        rows=Parse("VIN;Mileage;Vehicle\n58ABZ1B11KU026567;116589 miles;\"Lexus, ES 350\"\n");
        if(rows[0].Miles!=116589 || rows[0].Vehicle!="Lexus, ES 350")throw new Exception("Quoted semicolon CSV failed.");
        rows=Parse("VIN\tMiles\tVehicle\n58ABZ1B11KU026567\t116589\t\"Lexus\nES 350\"\n");
        if(rows[0].Miles!=116589 || !rows[0].Vehicle.Contains("ES 350"))throw new Exception("Tab-delimited or multiline field failed.");
        rows=Parse("VIN,Miles\n58ABZ1B11KU026567,116589\n58ABZ1B11KU026567,116589\n");if(rows.Count!=1)throw new Exception("Duplicate VIN not deduplicated.");
        rows=Parse("VIN,Miles\n58ABZ1B11KU026567,116589\n58ABZ1B11KU026567,120000\n");if(rows.Count!=1 || rows[0].Miles!=null || rows[0].Warning==null)throw new Exception("Conflicting odometer readings guessed.");
        rows=Parse("VIN,Miles\n58ABZ1B11KU026567,\"107,00\"\n");if(rows[0].Miles!=null)throw new Exception("Malformed mileage guessed.");
        rows=Parse("VIN,Miles,RUN #,LINE\n58ABZ1B11KU026567,116589,A-0029,001\n1FTBR3X81MKA59855,89675,,0058\n");
        if(rows[0].Run!="A-0029" || rows[1].Run!="0058")throw new Exception("RUN / LINE mapping or leading zeros lost.");
        Reject("VIN,Price\n58ABZ1B11KU026567,10000");Reject("VIN,Odometer (km)\n58ABZ1B11KU026567,100000");Reject("VIN,Miles\nINVALID,10000");Reject("VIN,Miles\n58ABZ1B11KU026567,10000,extra");
        var many="VIN,Miles\n";for(var i=0;i<101;i++)many+="1FTBR3X8"+i.ToString("D9")+",100000\n";Reject(many);
        Console.WriteLine("PASS: CSV header aliases, BOM, quoted commas, semicolon/tab delimiters, multiline fields, exact mileage pairing, duplicates, conflicts and invalid-file limits.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
