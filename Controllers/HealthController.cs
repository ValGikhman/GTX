using GTX;
using GTX.Controllers;
using Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Web.Mvc;

public class HealthController : BaseController
{
    private static readonly object _cpuLock = new object();
    private static readonly Dictionary<int, (DateTime utc, TimeSpan cpu)> _lastCpu = new Dictionary<int, (DateTime, TimeSpan)>();


    public HealthController(
        ISessionData sessionData,
        IInventoryService inventoryService,
        IVinDecoderService vinDecoderService,
        IEZ360Service _ez360Service,
        ILogService logService,
        IBlogPostService blogPostService)
    : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, blogPostService)  { }

    private static double GetProcessCpuPercent(Process p)
    {
        try
        {
            var now = DateTime.UtcNow;
            var cpu = p.TotalProcessorTime;

            lock (_cpuLock)
            {
                if (!_lastCpu.TryGetValue(p.Id, out var prev))
                {
                    _lastCpu[p.Id] = (now, cpu);
                    return 0.0; // first sample
                }

                var elapsedMs = (now - prev.utc).TotalMilliseconds;
                if (elapsedMs < 250)
                {
                    _lastCpu[p.Id] = (now, cpu);
                    return 0.0;
                }

                var cpuDeltaMs = (cpu - prev.cpu).TotalMilliseconds;
                _lastCpu[p.Id] = (now, cpu);

                var cores = Environment.ProcessorCount;
                var pct = (cpuDeltaMs / (elapsedMs * cores)) * 100.0;

                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;

                return Math.Round(pct, 1);
            }
        }
        catch
        {
            return 0.0;
        }
    }

    // ---- Total CPU% (system-wide) via WMI (no PerformanceCounter) ----
    private static double GetTotalCpuPercent()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor"))
            using (var results = searcher.Get())
            {
                var vals = results
                    .Cast<ManagementObject>()
                    .Select(mo => Convert.ToDouble(mo["LoadPercentage"]))
                    .ToList();

                return vals.Count > 0 ? Math.Round(vals.Average(), 1) : 0.0;
            }
        }
        catch
        {
            return 0.0;
        }
    }

    // ---- Physical memory totals via GlobalMemoryStatusEx (no PerformanceCounter) ----
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;     // physical memory load 0-100
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static (long totalPhys, long availPhys, uint memLoadPct) GetMemory()
    {
        try
        {
            var ms = new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX))
            };

            if (!GlobalMemoryStatusEx(ref ms))
                return (0, 0, 0);

            return ((long)ms.ullTotalPhys, (long)ms.ullAvailPhys, ms.dwMemoryLoad);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; }
        catch { return 0; }
    }

    private static long SafePrivateBytes(Process p)
    {
        try { return p.PrivateMemorySize64; }
        catch { return 0; }
    }

    // ---- Views ----
    [HttpGet]
    [AllowAnonymous]
    public ActionResult Index()
    {
        return View(); // Views/Health/Index.cshtml
    }

    // ---- JSON endpoint (NO PerformanceCounter) ----
    [HttpGet]
    [AllowAnonymous]
    public ActionResult HealthJson()
    {
        var (totalPhys, availPhys, memLoadPct) = GetMemory();

        // IIS (current app pool worker hosting this app)
        var iisProc = Process.GetCurrentProcess();
        var iisWs = SafeWorkingSet(iisProc);
        var iisPriv = SafePrivateBytes(iisProc);

        var iisObj = new
        {
            Name = iisProc.ProcessName,
            Pid = iisProc.Id,

            CpuPercent = GetProcessCpuPercent(iisProc),

            // memory "real values"
            WorkingSetBytes = iisWs,        // good for "RAM in use" display
            PrivateBytes = iisPriv,         // good for "private" display

            // percent of machine physical RAM (based on Working Set)
            MemoryPercent = totalPhys > 0 ? Math.Round((iisWs / (double)totalPhys) * 100.0, 1) : 0.0
        };

        // SQL Server (local) - optional
        object sqlObj = null;
        try
        {
            var sqlProc = Process.GetProcessesByName("sqlservr").FirstOrDefault();
            if (sqlProc != null)
            {
                var sqlWs = SafeWorkingSet(sqlProc);
                var sqlPriv = SafePrivateBytes(sqlProc);

                sqlObj = new
                {
                    Name = "SQL Server (sqlservr)",
                    Pid = sqlProc.Id,

                    CpuPercent = GetProcessCpuPercent(sqlProc),

                    WorkingSetBytes = sqlWs,
                    PrivateBytes = sqlPriv,

                    MemoryPercent = totalPhys > 0 ? Math.Round((sqlWs / (double)totalPhys) * 100.0, 1) : 0.0
                };
            }
        }
        catch
        {
            sqlObj = null; // access denied, etc.
        }

        var totals = new
        {
            CpuPercentTotal = GetTotalCpuPercent(),
            TotalPhysBytes = totalPhys,
            AvailPhysBytes = availPhys,
            MemoryPercentTotal = totalPhys > 0 ? Math.Round(((totalPhys - availPhys) / (double)totalPhys) * 100.0, 1) : 0.0,
            MemoryLoadPercent = memLoadPct
        };

        return Json(new
        {
            ServerTime = DateTimeOffset.Now, // MVC /Date(...)/
            Totals = totals,
            IIS = iisObj,
            SqlServer = sqlObj
        }, JsonRequestBehavior.AllowGet);
    }
}
