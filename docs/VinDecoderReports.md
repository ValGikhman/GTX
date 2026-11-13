# Saved VIN decoder reports

The VIN Decoder page offers DataOne by default and MarketCheck NeoVIN. Decode
requests a fresh report and saves it when the VIN identifies exactly one inventory
vehicle. Load saved reopens the selected provider's last saved report without
contacting either provider. Non-inventory VINs remain available for decoding, but
are not saved. If a save fails, the decoded report remains visible and the page
reports that it was not saved.

## Existing database storage

No table rename, additional columns or SQL migration is needed. Reports use the
existing `dbo.Details` row for the inventory Stock and its XML `DataOneContent`
column. The column holds a versioned XML envelope:

```xml
<vin_decoder version="1">
  <result provider="dataone" format="xml" formatter="dataone-v1"
          vin="1HGCM82633A004352" decodedUtc="2026-11-12T15:30:00Z">
    <payload><decoded_data><!-- Native DataOne XML --></decoded_data></payload>
    <html>HTML report stored as XML text</html>
  </result>
  <result provider="neovin" format="json" formatter="neovin-v1"
          vin="1HGCM82633A004352" decodedUtc="2026-11-12T15:30:00Z">
    <payload>{"vin":"1HGCM82633A004352"}</payload>
    <html>HTML report stored as XML text</html>
  </result>
</vin_decoder>
```

The provider/format/formatter values are controlled by the application. Provider
responses stay inside their payloads; DataOne's existing `service_provider` value
is preserved. JSON and HTML are written with XML text APIs so special characters
round-trip safely. A save replaces only that provider's result, preserving the
other provider and the row Id. Saves and provider deletions merge the latest row
inside a transaction to prevent concurrent updates from losing another report.

DataOne and NeoVIN share the same report stylesheet, headers, disclosure panels
and tables. NeoVIN groups repeated category prefixes under a single parent, such
as Features → Safety → Standard, and omits generated field counters. Repeated
feature types and equipment item/attribute pairs form their own subgroups within
each category; locations such as front and rear, values and extra metadata remain
visible. All provider rows and the complete raw response remain available.
GTX formats both fresh and saved raw payloads through the selected,
allowlisted formatter. Saved HTML is a snapshot rather than the source used to
reopen reports, so styling updates apply without a new paid decode. NeoVIN keeps
its original decoded timestamp when a saved report is reformatted.

Legacy `<decoded_data>` rows are read as DataOne without rewriting them merely to
add metadata. The first multi-provider save wraps the existing XML. Inventory,
chat, comparison and mobile readers continue receiving only DataOne's XML, and a
NeoVIN-only row does not count as saved DataOne. Deleting DataOne removes only
that provider; the Details row remains while another result exists.

## Deployment

Deploy the updated Services library with GTX. AutoDealer.dev also needs its new
`GET /api/service/vin/{VIN}/report` endpoint, which returns raw DataOne XML and HTML
from one decode. NeoVIN uses its existing `POST /api/service/neovin` report
endpoint, which already returns raw JSON and HTML together. Saving does not make
a second paid decoder request. The configured vehicle-research origin and account
key in Services are used for inventory reports; the key needs `vin:read` access.

Deploy the updated AutoDealer backend before enabling the updated GTX workflow.
No production configuration or database schema is changed by these source edits.
GTX's project links the shared renderer/model source files from the sibling
`autodealer.dev/Models` directory; keep that checkout alongside GTX when building.
The compiled renderers are included in GTX.dll, so no source files are needed on
the production server.

## Offline checks

After building GTX, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/vin-decoder-api.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/VinDecoderStorage.Tests.ps1
node tests/vin-decoder-providers.js
```

The storage regression creates and removes its own database on local SQL Server.
The API and UI checks use simulated providers and make no paid requests.
