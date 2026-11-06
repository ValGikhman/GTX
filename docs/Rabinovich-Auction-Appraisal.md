# Rabinovich auction appraisal

GTX owners and managers can ask Rabinovich for the MarketCheck suggested retail price, a nearby listing count with matching dealers, or a full auction appraisal. Include the VIN and supply the odometer mileage when known. AutoDealer.dev reads omitted ZIP, radius, and mileage from its private `Web.config`.

> Appraise VIN 4T1BE46K19U342407 with 73,929 miles.

That request returns only the MarketCheck suggested price. Adding “for auction” also returns nearby comparable asking prices and the maximum hammer bid. The auction appraisal defaults to a 100-mile comparable search and accepts a different radius, resale assumption or current hammer bid.

“List market for 2017 Acura MDX Advance Package” or “check market for 2017 Acura MDX Advance Package” shows matching dealers in separate current and recent sections. The same commands accept a VIN. Recent means the past 90 days and uses a radius of up to 100 miles. The default odometer target of 50,000 miles searches from 40,000 to 60,000 miles.

AutoDealer.dev calls MarketCheck's independent dealer price prediction tool for both requests. Auction appraisals also call NeoVIN and active inventory. The existing bid calculator uses the resale assumption, configured auction fee, transport and target profit.

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
