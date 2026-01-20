using GTX;
using GTX.Controllers;
using Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using static CpuSampler;

public class HealthController : BaseController
{
    public HealthController(
        ISessionData sessionData,
        IInventoryService inventoryService,
        IVinDecoderService vinDecoderService,
        IEZ360Service _ez360Service,
        ILogService logService,
        IBlogPostService blogPostService)
    : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, blogPostService)
    {
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult HealthJson()
    {
        var (totalPhys, availPhys) = MemoryInfo.Get();

        // IIS (current app pool worker)
        var iisProc = Process.GetCurrentProcess();
        var iis = BuildProc(iisProc, totalPhys, "IIS (w3wp)");

        // SQL Server (local)
        ProcMetrics sql = null;
        try
        {
            var sqlProc = Process.GetProcessesByName("sqlservr").FirstOrDefault();
            if (sqlProc != null)
                sql = BuildProc(sqlProc, totalPhys, "SQL Server (sqlservr)");
        }
        catch
        {
            // might require permissions; leave null
        }

        // Totals
        var totals = new HealthTotals
        {
            TotalPhysBytes = totalPhys,
            AvailPhysBytes = availPhys,
            MemoryPercentTotal = totalPhys > 0 ? ((totalPhys - availPhys) / (double)totalPhys) * 100.0 : 0,
            CpuPercentTotal = GetTotalCpuPercent()
        };

        var model = new HealthModel
        {
            ServerTime = DateTimeOffset.Now.ToString("o"),
            IIS = iis,
            SqlServer = sql,
            Totals = totals
        };

        return Json(model, JsonRequestBehavior.AllowGet);
    }

    private static ProcMetrics BuildProc(Process p, long totalPhys, string label)
    {
        // WorkingSet can throw if access denied; keep it safe
        long ws = 0;
        try { ws = p.WorkingSet64; } catch { }

        var cpu = 0.0;
        try { cpu = CpuSampler.GetCpuPercent(p); } catch { }

        var memPct = totalPhys > 0 ? (ws / (double)totalPhys) * 100.0 : 0;

        return new ProcMetrics
        {
            Name = label,
            Pid = p.Id,
            CpuPercent = cpu,
            WorkingSetBytes = ws,
            MemoryPercent = memPct
        };
    }

    // Uses a perf counter for overall CPU. First call may be 0; polling fixes that.
    private static readonly PerformanceCounter _cpuTotal =
        new PerformanceCounter("Processor", "% Processor Time", "_Total");

    private static double GetTotalCpuPercent()
    {
        try
        {
            var v = _cpuTotal.NextValue();
            if (v < 0) v = 0;
            if (v > 100) v = 100;
            return Math.Round(v, 1);
        }
        catch { return 0; }
    }
}
