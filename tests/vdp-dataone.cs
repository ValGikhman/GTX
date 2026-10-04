using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using GTX.Controllers;
using GTX.Models;
using Services;

internal static class VdpDataOneTests
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        CheckCase("Saved XML overrides stale false flag", "<decoded_data />", false, false, false, 1, 0, 0, true);
        CheckCase("Saved XML with true flag", "<decoded_data />", true, false, false, 1, 0, 0, true);
        CheckCase("Missing row decodes and saves", null, false, false, false, 1, 1, 1, true);
        CheckCase("Missing row overrides stale true flag", null, true, false, false, 1, 1, 1, true);
        CheckCase("Empty content decodes and saves", " ", true, false, false, 1, 1, 1, true);
        CheckCase("Database failure does not call API", null, false, true, false, 1, 0, 0, false);
        CheckCase("Invalid saved XML does not call API", "invalid XML", false, false, false, 1, 0, 0, false);
        CheckCase("Memory cache skips database and API", null, true, false, true, 0, 0, 0, true);
        Console.WriteLine("PASS: 8 database-first DataOne scenarios, including repeated loads.");
    }

    private static void CheckCase(string name, string saved, bool flag, bool failRead, bool cached,
        int expectedReads, int expectedDecodes, int expectedSaves, bool hasResult)
    {
        int reads = 0, decodes = 0, saves = 0;
        var controller = (InventoryController)FormatterServices.GetUninitializedObject(typeof(InventoryController));
        controller.InventoryService = (IInventoryService)new Stub(typeof(IInventoryService), call => {
            if (call.MethodName == "GetDataOneDetails") {
                reads++;
                if (failRead) throw new InvalidOperationException("Database unavailable");
                return saved;
            }
            if (call.MethodName == "SaveDataOneDetails") {
                saves++;
                if ((string)call.Args[1] != "<decoded_data />") throw new Exception("Unexpected saved XML");
                return null;
            }
            throw new Exception("Unexpected inventory call: " + call.MethodName);
        }).GetTransparentProxy();
        controller.VinDecoderService = (IVinDecoderService)new Stub(typeof(IVinDecoderService), call => {
            if (call.MethodName != "DecodeVin") throw new Exception("Unexpected decoder call");
            decodes++;
            if (reads != 1) throw new Exception("API called before database lookup");
            return "<decoded_data />";
        }).GetTransparentProxy();
        var vehicle = new GTX.Models.GTX { Stock = "GTX123456", VIN = "1HGCM82633A004352", HasDataOne = flag,
            DataOne = cached ? new DecodedData() : null };
        var method = typeof(InventoryController).GetMethod("LoadVehicleDataOneDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        object result = null;
        bool failed = false;
        try { result = method.Invoke(controller, new object[] { vehicle }); }
        catch (TargetInvocationException ex) {
            if (!failRead || !(ex.InnerException is InvalidOperationException)) throw;
            failed = true;
        }
        if (failed != failRead || (result != null) != hasResult) throw new Exception(name + ": wrong result");
        if (hasResult && !ReferenceEquals(result, method.Invoke(controller, new object[] { vehicle })))
            throw new Exception(name + ": subsequent load did not reuse decoded data");
        if (reads != expectedReads || decodes != expectedDecodes || saves != expectedSaves)
            throw new Exception(name + ": unexpected database/API call counts");
        Console.WriteLine("PASS: " + name);
    }

    private sealed class Stub : RealProxy
    {
        private readonly Func<IMethodCallMessage, object> handle;
        internal Stub(Type type, Func<IMethodCallMessage, object> handler) : base(type) { handle = handler; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try { return new ReturnMessage(handle(call), null, 0, call.LogicalCallContext, call); }
            catch (Exception ex) { return new ReturnMessage(ex, call); }
        }
    }
}
