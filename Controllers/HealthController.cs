using GTX;
using GTX.Common;
using GTX.Controllers;
using Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Web.Mvc;

[RequireAdminRole]
public class HealthController : BaseController
{
    private static readonly object _cpuLock = new object();
    private static readonly Dictionary<int, (DateTime utc, TimeSpan cpu)> _lastCpu = new Dictionary<int, (DateTime, TimeSpan)>();
    private static readonly object _totalCpuCacheLock = new object();
    private static DateTime _totalCpuCacheUtc = DateTime.MinValue;
    private static double _totalCpuCacheValue = 0.0;


    public HealthController(
        ISessionData sessionData,
        IInventoryService inventoryService,
        IVinDecoderService vinDecoderService,
        ILogService logService, 
        IEmployeesService employeesService)
    : base(sessionData, inventoryService, vinDecoderService, logService, employeesService)  { }

    public ActionResult ActiveSessions()
    {
        var active = (int)(HttpContext.Application["TotalSessions"] ?? 0);
        return Content(active.ToString());
    }
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
        var now = DateTime.UtcNow;
        lock (_totalCpuCacheLock)
        {
            if ((now - _totalCpuCacheUtc).TotalMilliseconds < 1500)
            {
                return _totalCpuCacheValue;
            }
        }

        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor"))
            using (var results = searcher.Get())
            {
                var sum = 0.0;
                var count = 0;

                foreach (ManagementObject mo in results)
                {
                    sum += Convert.ToDouble(mo["LoadPercentage"]);
                    count++;
                }

                var value = count > 0 ? Math.Round(sum / count, 1) : 0.0;
                lock (_totalCpuCacheLock)
                {
                    _totalCpuCacheUtc = now;
                    _totalCpuCacheValue = value;
                }

                return value;
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
    public ActionResult Index()
    {
        return View(); // Views/Health/Index.cshtml
    }

    // ---- JSON endpoint (NO PerformanceCounter) ----
    [HttpGet]
    public ActionResult HealthJson()
    {
        var (totalPhys, availPhys, memLoadPct) = GetMemory();

        var iisProc = Process.GetCurrentProcess();
        var iisWs = SafeWorkingSet(iisProc);
        var iisPriv = SafePrivateBytes(iisProc);

        var iisObj = new
        {
            Name = iisProc.ProcessName,
            Pid = iisProc.Id,
            CpuPercent = GetProcessCpuPercent(iisProc),
            WorkingSetBytes = iisWs,
            PrivateBytes = iisPriv,
            MemoryPercent = totalPhys > 0 ? Math.Round((iisWs / (double)totalPhys) * 100.0, 1) : 0.0
        };

        var totals = new
        {
            CpuPercentTotal = GetTotalCpuPercent(),
            TotalPhysBytes = totalPhys,
            AvailPhysBytes = availPhys,
            MemoryPercentTotal = totalPhys > 0 ? Math.Round(((totalPhys - availPhys) / (double)totalPhys) * 100.0, 1) : 0.0,
            MemoryLoadPercent = memLoadPct
        };

        var activeSessions = Convert.ToInt32(HttpContext.Application["TotalSessions"] ?? 0);

        return Json(new
        {
            ServerTime = DateTimeOffset.Now,
            Totals = totals,
            IIS = iisObj,
            ActiveSessions = activeSessions
        }, JsonRequestBehavior.AllowGet);
    }

    [HttpPost]
    public ActionResult SetOffline(bool isOffline)
    {
        MaintenanceFlag.SetOffline(isOffline);
        return Json(new { ok = true, isOffline });
    }

    // Optional: to load current state
    [HttpGet]
    public ActionResult GetOffline()
    {
        return Json(new { isOffline = MaintenanceFlag.IsOffline() }, JsonRequestBehavior.AllowGet);
    }

}
