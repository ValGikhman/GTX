# Services

Open **Majordome → Services** (`/Services`) to configure OpenAI, DataOne, CarGurus
and vehicle research, Cloudflare, and Remove Background. Only the built-in
Majordome account can see this page or submit changes. Regular roles cannot gain
access through saved permission values.

Every service has an on/off switch and editable settings on its own card, using
the same layout and styles as Health. Disabled cards remain visible so they can be
enabled again. Credentials are displayed and stored as raw values, as requested.
Settings live in the protected, Git-ignored `App_Data/Services.config` file, outside
the publishing package. Each card saves independently; stale forms cannot overwrite
newer edits. Grant the application pool identity write access to this App_Data folder.
No database migration or application restart is required for service edits.

OpenAI's Model dropdown loads the models available to the API key currently entered
on its card. Use Refresh after changing the key or to fetch a newer list. Loading or
refreshing never changes the current selection; a configured model stays available
even if it is absent from the returned list. Choose a model that supports the chatbot:
the model-list API also returns models for other tasks. Lookup errors leave the
current selection intact, and changing a selection takes effect only after Save.

OpenAI's existing switch controls the chatbot; configured comparison and story
features continue to use its API settings. The CarGurus switch controls CarGurus
browsing. Its card also contains the shared vehicle-research API URL/key used by
VIN decoding and appraisal. Disabling CarGurus does not disable those other tools.
The VIN Decoder page offers DataOne by default and MarketCheck NeoVIN in its
provider dropdown. NeoVIN uses the vehicle-research API URL and key on this card,
calling AutoDealer.dev's `/api/service/neovin/{VIN}/html` endpoint. The account key
must have `vin:read` access; AutoDealer.dev keeps its own MarketCheck provider key.
Changing the dropdown clears the previous report; press Decode to request a report
from the selected provider. Inventory VIN reports are saved automatically. Load
saved reopens the selected provider's report without another provider request;
VINs outside inventory can be decoded but are not saved. The auction shortlist
keeps its existing decoder. See [saved VIN reports](VinDecoderReports.md) for the
existing-table storage format and deployment requirements.
Turning off DataOne blocks direct DataOne calls and inventory enrichment. Turning
off Cloudflare uses native images and blocks R2/cache operations. Turning off Remove
Background hides both gallery and batch actions and rejects new removal/confirmation
requests; preview/cancellation remain available for cleanup.

## Upgrade an existing host

Before publishing the cleaned Web.config, export that host's current settings:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/Export-ServiceConfiguration.ps1 -WebConfigPath 'C:\site\Web.config' -Path 'C:\site\App_Data\Services.config'
```

Run the script from the built application checkout; the path arguments identify the
host's existing configuration. It preserves that host's credentials, URLs, flags,
and numeric settings and imports legacy R2 credential files if needed. It never
prints credential values or overwrites an existing Services.config. If no old
RemoveBg switch exists, its initial state follows whether an API key was configured.
After verifying the private file exists, deploy the new application and open Services.
Provision this private file separately on each host and preserve it during later deployments.

If the old Web.config is still present and no private file exists, runtime reads can
temporarily use its legacy values. The first successful card save snapshots all
settings to the private file. Once that file exists, it is authoritative: clearing
a key never falls back to Web.config or old credential text files.

To export and clean a local Web.config in one operation, add `-RemoveFromWebConfig`.
Infrastructure settings such as database selection, ASP.NET/IIS configuration and
the separate Majordome password remain outside this Services page.

## Validation

After building, run the offline service configuration and export regressions:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/ServiceConfiguration.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/ServiceConfigurationExport.Tests.ps1
node tests/service-switches.js
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OpenAiModels.Tests.ps1
node tests/service-models.js
```

These use synthetic credentials, temporary files and simulated requests. They do
not contact service providers or modify the configured application database.
