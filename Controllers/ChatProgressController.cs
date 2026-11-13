using System;
using System.Runtime.Caching;
using System.Web.Mvc;
using System.Web.SessionState;

namespace GTX.Controllers
{
    [SessionState(SessionStateBehavior.Disabled)]
    public sealed class ChatProgressController : Controller
    {
        private const string Prefix = "GTX:ChatMarketProgress:";

        [HttpGet]
        public ActionResult Market(string id)
        {
            Response.Cache.SetNoStore();
            var progress = Guid.TryParseExact(id, "N", out var requestId)
                ? MemoryCache.Default.Get(Prefix + requestId.ToString("N"))
                : null;
            return Json(progress ?? false, JsonRequestBehavior.AllowGet);
        }

        internal static void StartMarket(Guid? requestId)
        {
            Start(requestId, true);
        }

        internal static void StartCarGurus(Guid? requestId)
        {
            Start(requestId, "cargurus");
        }

        internal static void StartDataOne(Guid? requestId)
        {
            Start(requestId, "dataone");
        }

        private static void Start(Guid? requestId, object progress)
        {
            if (requestId.HasValue)
                MemoryCache.Default.Set(Prefix + requestId.Value.ToString("N"), progress, DateTimeOffset.UtcNow.AddMinutes(1));
        }

        internal static void Clear(Guid? requestId)
        {
            if (requestId.HasValue)
                MemoryCache.Default.Remove(Prefix + requestId.Value.ToString("N"));
        }
    }
}
