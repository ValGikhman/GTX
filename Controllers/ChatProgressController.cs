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
            var checkingMarket = Guid.TryParseExact(id, "N", out var requestId)
                && MemoryCache.Default.Contains(Prefix + requestId.ToString("N"));
            return Json(checkingMarket, JsonRequestBehavior.AllowGet);
        }

        internal static void StartMarket(Guid? requestId)
        {
            if (requestId.HasValue)
                MemoryCache.Default.Set(Prefix + requestId.Value.ToString("N"), true, DateTimeOffset.UtcNow.AddMinutes(1));
        }

        internal static void Clear(Guid? requestId)
        {
            if (requestId.HasValue)
                MemoryCache.Default.Remove(Prefix + requestId.Value.ToString("N"));
        }
    }
}
