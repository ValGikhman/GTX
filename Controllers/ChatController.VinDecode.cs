using GTX.Common;
using GTX.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public sealed partial class ChatController
    {
        private static readonly Regex VinDecodeCommand = new Regex(@"\A\s*decode\b(?<arguments>[\s\S]*)\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex VinDecodePrefix = new Regex(@"\Avin\b\s*[:#]?\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static bool TryParseVinDecodeCommand(string message, out string vin)
        {
            vin = null;
            var command = VinDecodeCommand.Match(message ?? string.Empty);
            if (!command.Success) return false;

            var argument = VinDecodePrefix.Replace(command.Groups["arguments"].Value.Trim(), string.Empty, 1);
            vin = VinDecoderController.NormalizeVin(argument);
            return true;
        }

        private async Task<ActionResult> DecodeVinCommandAsync(string vin, Guid? progressId)
        {
            Response.Cache.SetNoStore();
            if (vin == null)
                return VinDecodeError(400);

            if (!SiteAccess.Can(SiteAccess.Current(Request), MajordomeItem.VinDecoder))
                return VinDecodeError(403);

            ChatProgressController.StartDataOne(progressId);
            try
            {
                return CreateVinDecodeReply(vin, await VinDecoderController.DecodeHtmlAsync(vin));
            }
            catch (Exception)
            {
                return VinDecodeError(502);
            }
        }

        private ActionResult CreateVinDecodeReply(string vin, ActionResult result)
        {
            var content = result as ContentResult;
            if (content != null && content.ContentType == "text/html" && !string.IsNullOrWhiteSpace(content.Content))
            {
                return Json(new ChatBotResponse
                {
                    Success = true,
                    PrivateReply = true,
                    Reply = "Your VIN report is ready to show,\nClick View VIN report in the button below.\nExpand each section for your inspection,\nNu, the car's details in one collection.",
                    VinDecode = new ChatVinDecodeResult { Vin = vin, Html = content.Content }
                });
            }

            return VinDecodeError((result as HttpStatusCodeResult)?.StatusCode ?? 502);
        }

        private ActionResult VinDecodeError(int statusCode)
        {
            string reply;
            switch (statusCode)
            {
                case 400:
                    reply = "Send a 17-character VIN, just right,\nLeave I, O, and Q out of sight.\nTry decode VIN, then the code in a row,\nNu, one VIN at a time and we're ready to go.";
                    break;
                case 403:
                    reply = "VIN Decoder access unlocks this view,\nSign in with a role that grants it to you.\nAsk Owner for access to make it so,\nNu, then send your VIN and we're ready to go.";
                    break;
                case 429:
                    reply = "The VIN decoding limit says no,\nWait a little before another go.\nKeep that VIN handy, let the service rest,\nNu, try again later; patience is best.";
                    break;
                case 504:
                    reply = "The VIN decoder took too long today,\nPlease try that same VIN after a delay.\nNo report arrived, so give it a beat,\nNu, try again shortly and we'll repeat.";
                    break;
                default:
                    statusCode = 502;
                    reply = "The VIN report couldn't reach us today,\nPlease try that same VIN again, oy vey.\nThe decoder needs a moment of rest,\nNu, come back shortly; a retry is best.";
                    break;
            }

            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new ChatBotResponse { Success = false, PrivateReply = true, Reply = reply });
        }
    }
}
