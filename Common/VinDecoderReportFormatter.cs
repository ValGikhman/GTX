using autodealer.dev.Models;
using Services;
using System;

namespace GTX.Common {
    internal static class VinDecoderReportFormatter {
        internal static string Render(VinDecoderReportDTO report) {
            if (report == null) throw InvalidPayload();
            var dataOne = report.Provider == "dataone" && report.PayloadFormat == "xml" && report.FormatterKey == "dataone-v1";
            var neoVin = report.Provider == "neovin" && report.PayloadFormat == "json" && report.FormatterKey == "neovin-v1";
            if (!dataOne && !neoVin)
                throw new VinDecoderReportException(503, "The saved VIN report format is not supported.");

            var vin = Controllers.VinDecoderController.NormalizeVin(report.Vin);
            if (vin == null || string.IsNullOrWhiteSpace(report.RawPayload) || (neoVin && !report.DecodedUtc.HasValue))
                throw InvalidPayload();

            try {
                return dataOne
                    ? VinDecodeHtmlRenderer.Render(report.RawPayload, vin)
                    : NeoVinHtmlRenderer.Render(NeoVinResult.Create(vin, report.RawPayload, report.DecodedUtc.Value));
            } catch (Exception ex) when (ex is VinDecodeResponseException || ex is NeoVinException || ex is ArgumentException) {
                throw InvalidPayload();
            }
        }

        private static VinDecoderReportException InvalidPayload() {
            return new VinDecoderReportException(502, "The VIN report contains an invalid decoding response.");
        }
    }
}
