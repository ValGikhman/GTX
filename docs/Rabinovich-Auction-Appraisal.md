# Rabinovich dealer research and appraisals

Staff with Appraisal access can ask for a named vehicle appraisal and up to ten
active comparables, without supplying a VIN:

> Appraise a 2021 Honda Accord EX-L with 42k miles — show me 10 comparables.

Rabinovich uses MarketCheck MCP to find a matching reference VIN, get the price
prediction, and retrieve a bounded active-listing sample. A supplied subject VIN
is decoded instead. Prediction and active search run together. The reply shows
the estimate, available MSRP, fetched-sample statistics, and comparable listings
with dealer, VIN, mileage, asking price, distance, days listed and available links.
Results retain missing prices rather than inventing them. A reference VIN is
identified as a reference; it is not presented as the user's vehicle.

The chat displays **Checking MarketCheck AI…** after reading the request and
immediately before sending the research lookup. It clears the indicator when the
reply completes or fails. The command guide contains appraisal, dealer research,
VIN, named-vehicle, mileage, ZIP/radius and auction examples.

Counts reported by the provider are separate from the fetched sample used for
statistics. The search uses the subject mileage plus or minus 10,000 miles and
the configured ZIP/radius, with explicit user values taking precedence. Up to 50
listings are sampled and up to ten are shown, ranked by mileage proximity and
then distance. Equal VINs are deduplicated; a supplied subject VIN is excluded.
Clean Carfax, condition and history claims are not verified or given an invented
premium. Trade-in/private-party estimates and confirmed sale prices are not
inferred from active asking prices.

Only a small extraction request goes to the language model. Raw MarketCheck
responses stay on the research server; the bounded result is rendered directly,
without sending the provider payload back through the model.
Rabinovich uses OpenAI `gpt-5-mini` with low reasoning effort for chat and request
extraction. Its bounded output budget reserves room for reasoning as well as the
visible reply. Other OpenAI features retain their existing Services model setting.

## Existing price and auction requests

GTX owners and managers can ask Rabinovich for the MarketCheck suggested retail price, a nearby listing count with matching dealers, or a full auction appraisal. Include the VIN and supply the odometer mileage when known. AutoDealer.dev reads omitted ZIP, radius, and mileage from its private `Web.config`.

> Appraise VIN 4T1BE46K19U342407 with 73,929 miles.

That request returns only the MarketCheck suggested price. Adding “for auction” also returns nearby comparable asking prices and the maximum hammer bid. The auction appraisal defaults to a 100-mile comparable search and accepts a different radius, resale assumption or current hammer bid.

“List market for 2017 Acura MDX Advance Package” or “check market for 2017 Acura MDX Advance Package” shows matching dealers in separate current and recent sections. The same commands accept a VIN. Recent means the past 90 days and uses a radius of up to 100 miles. The default odometer target of 50,000 miles searches from 40,000 to 60,000 miles.

Plain VIN price requests retain the existing direct price-research path. Rich
appraisals and the normal auction path use MarketCheck MCP's independent-dealer
prediction. Auction appraisals also use NeoVIN and active inventory; the existing
bid calculator uses the resale assumption, configured auction fee, transport and
target profit. The rich retail appraisal does not require auction-fee settings.

## Server setup

On Windows, the bundled starter can install an isolated local runtime and start
the server without changing the user's PATH, Python registry entries or application
configuration. Git must already be available on PATH. Both startup scripts are
included in the AutoDealer.dev publish output. From its website directory on
the API server, install the runtime and start MCP:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/Start-MarketCheckMcp.ps1 -Setup
```

Installing the dependencies is a one-time step per server. Later starts omit
`-Setup`. The starter reuses a healthy server, otherwise starts
it in the background with a hidden window and waits for its health endpoint.
`-Foreground` runs it in the current terminal when diagnosing startup. The default
runtime, pinned upstream checkout, process metadata and logs live under
`%LOCALAPPDATA%\GTX\MarketCheckMcp`; `-RuntimeDirectory` can select another location.
The launcher reads the provider key directly from the private Web.config, keeping
it out of process arguments and startup output. No billable lookup is made by the
health check. This starts a local process, not a scheduled or automatically
restarting Windows service.

Run the official [MarketCheck MCP server](https://github.com/MarketcheckHub/marketcheck-api-mcp/tree/86e645c9b927ec99ccae2a8e9cff8408e129a5b1) on the AutoDealer.dev host. Install the upstream dependencies with Python 3.12 or later. The AutoDealer.dev launcher reads the MarketCheck key and loopback URL from its private `Web.config`.

```powershell
uv sync --frozen
uv run --no-sync python C:\path\to\autodealer.dev\Scripts\Start-MarketCheckMcp.py .
```

Configure these settings in the private `Web.config` for each app.

| Application | App setting | Value |
| --- | --- | --- |
| AutoDealer.dev | `MarketCheck:ApiKey` | Existing MarketCheck API key |
| AutoDealer.dev | `MarketCheck:McpUrl` | `http://127.0.0.1:8765/mcp` |
| AutoDealer.dev | `Research:RabinovichKeyId` | The key ID before the dot in the registered AutoDealer.dev key assigned to GTX |
| AutoDealer.dev | `Research:DefaultZip` | `45040` |
| AutoDealer.dev | `Research:DefaultRadius` | `100` miles |
| AutoDealer.dev | `Research:DefaultMileage` | `50000` odometer miles |
| GTX | `AutoDealer:AppraisalBaseUrl` | HTTPS AutoDealer.dev origin, such as `https://localhost:44398` |
| GTX | `AutoDealer:ApiKey` | The registered AutoDealer.dev key also used by GTX VIN Decoder |

Enable API security on AutoDealer.dev and set its existing `AuctionBid:AuctionFeeUsd`, `AuctionBid:TransportUsd`, and `AuctionBid:MinimumProfitUsd` settings. Restart both applications after configuring them.

The new staff-only `POST /api/service/price-research/market-appraisal` endpoint
uses the same registered Rabinovich key restriction, `vin:read` permission,
defaults and quota protections as the existing research tools. Production
configuration is unchanged by these source edits.

## Production readiness

Deploy the updated AutoDealer.dev backend together with the GTX chatbot changes.
The MCP service must run on the AutoDealer.dev server: `127.0.0.1` refers to that
server, not the developer's computer or a separate GTX web host. Starting IIS or
publishing the .NET application does not install or start the Python service.
The Windows helper starts a process but does not register a service. Production
needs a persistent service supervisor that starts MCP after reboot and restarts
it on failure. Hosting that cannot run a persistent Python process needs a
different MCP hosting arrangement before this loopback configuration can work.

Before enabling the chatbot in production, verify `/health` on the configured
MCP host, complete an MCP initialize/tools-list check, and test one authenticated
appraisal against the production AutoDealer.dev origin. Check the registered
Rabinovich key, `vin:read` access, ZIP/radius defaults and provider quota on that
host. Local success alone does not verify these production dependencies.
Sandbox and Demo can share the same MCP process when both use the same
AutoDealer.dev API origin. An `ok` quota health response with empty quota values
only verifies the API connection; it does not verify that MCP is running.

Vehicle searches request `include_build_object=true` for both the reference-VIN
and comparable lookups. The official MCP tool otherwise strips the build object,
which would prevent the application from validating returned vehicle matches.
If MCP is stopped, the API now returns an actionable 503 instead of a generic
502; the chat identifies the unavailable MCP service.

## Offline validation

After building GTX, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/ChatMarketAppraisal.Tests.ps1
node tests/chat-market-appraisal.js
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ../autodealer.dev/Scripts/Test-PriceResearch.ps1
```

These checks simulate providers and make no paid requests.
