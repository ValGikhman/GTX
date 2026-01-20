using GTX.Models;
using System;
using System.Diagnostics;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public class HealthController : Controller
    {

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            var p = Process.GetCurrentProcess();

            long procWorkingSet = p.WorkingSet64;
            long procPrivateBytes = p.PrivateMemorySize64;
            long procPaged = p.PagedMemorySize64;

            long? committedBytes = null;
            long? availBytes = null;

            try
            {
                using (var committedCounter = new PerformanceCounter("Memory", "Committed Bytes"))
                using (var availCounter = new PerformanceCounter("Memory", "Available Bytes"))
                {
                    availBytes = Convert.ToInt64(availCounter.NextValue());
                    committedBytes = Convert.ToInt64(committedCounter.NextValue());
                }
            }
            catch { /* ignore */ }

            long managed = GC.GetTotalMemory(false);

            var model = new HealthModel
            {
                ServerTime = DateTimeOffset.Now,
                ProcessName = p.ProcessName,
                Pid = p.Id,
                WorkingSetBytes = procWorkingSet,
                PrivateBytes = procPrivateBytes,
                PagedBytes = procPaged,
                ManagedBytes = managed,
                AvailableBytes = availBytes,
                CommittedBytes = committedBytes
            };

            return View("Index", model);
        }
    }
}