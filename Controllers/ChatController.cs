using Common;
using GTX.Common;
using GTX.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GTX.Controllers
{
    public sealed class ChatController : Controller
    {
        private const string ResponsesUrl = "https://api.openai.com/v1/responses";
        private const string RequestTokenSessionKey = "GTX:ChatRequestToken";
        private const string PromptCacheKey = "gtx-dealership-chat-v1";
        private const string DefaultChatModel = "gpt-4.1-mini";
        private const int MaxToolRounds = 2;
        private static readonly HttpClient OpenAiClient = CreateOpenAiClient();
        private static readonly Regex ResponseIdPattern = new Regex(@"^resp_[A-Za-z0-9_-]+$", RegexOptions.Compiled);
        private static readonly JArray AssistantTools = BuildTools();
        private static readonly MemoryCache RateLimitCache = MemoryCache.Default;

        private readonly IInventoryService _inventoryService;
        private readonly IContactService _contactService;
        private readonly IEmployeesService _employeesService;
        private readonly IChatBotTeachingService _teachingService;

        public ChatController(
            IInventoryService inventoryService,
            IContactService contactService,
            IEmployeesService employeesService,
            IChatBotTeachingService teachingService)
        {
            _inventoryService = inventoryService;
            _contactService = contactService;
            _employeesService = employeesService;
            _teachingService = teachingService;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!ChatBotSettings.Enabled)
            {
                filterContext.Result = HttpNotFound();
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Salespeople()
        {
            var employees = _employeesService.GetEmployees() ?? Array.Empty<Employee>();
            var salespeople = employees
                .Where(IsActiveSalesperson)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new
                {
                    id = employee.Id,
                    name = Join(employee.FirstName, employee.LastName)
                })
                .ToArray();

            return Json(salespeople, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> Message(ChatBotRequest request)
        {
            if (request == null || !HasValidRequestToken(request.ChatRequestToken))
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(new ChatBotResponse { Success = false, Reply = "Please refresh the page and try again." });
            }

            if (!AllowRequest("chat", 20, TimeSpan.FromMinutes(1)))
            {
                Response.StatusCode = 429;
                return Json(new ChatBotResponse { Success = false, Reply = "Please wait a moment before sending another message." });
            }

            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(new ChatBotResponse { Success = false, Reply = "Please enter a message." });
            }

            var previousResponseId = NormalizeResponseId(request.PreviousResponseId);
            var message = request.Message.Trim();
            var currentRole = RoleCookie.GetCurrentRole(Request, Session);

            try
            {
                var commandNavigation = ResolveCommandNavigation(message, currentRole);
                if (commandNavigation != null)
                {
                    return Json(new ChatBotResponse
                    {
                        Success = true,
                        Reply = commandNavigation.RequiresLogin
                            ? "Please log in to open " + commandNavigation.Label + "."
                            : "Opening " + commandNavigation.Label + "...",
                        Navigation = commandNavigation
                    });
                }

                var apiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    return Json(new ChatBotResponse { Success = false, Reply = "The assistant is temporarily unavailable. Please call (513) 489-2886." });
                }

                var result = await GetAssistantReplyAsync(apiKey, message, previousResponseId);
                return Json(new ChatBotResponse
                {
                    Success = true,
                    Reply = result.Reply,
                    ResponseId = result.ResponseId,
                    TotalVehicleMatches = result.TotalVehicleMatches,
                    InventoryUrl = result.InventoryUrl,
                    Vehicles = result.Vehicles.ToArray()
                });
            }
            catch (OpenAiRequestException)
            {
                Response.StatusCode = (int)HttpStatusCode.BadGateway;
                return Json(new ChatBotResponse { Success = false, Reply = "I could not reach the assistant. Please try again or call (513) 489-2886." });
            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new ChatBotResponse { Success = false, Reply = "Something went wrong. Please try again or contact our sales team." });
            }
        }

        [HttpPost]
        public async Task<ActionResult> SubmitLead(ChatLeadRequest request)
        {
            if (request == null || !HasValidRequestToken(request.ChatRequestToken))
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(new { success = false, message = "Please refresh the page and try again." });
            }

            if (!AllowRequest("lead", 5, TimeSpan.FromMinutes(10)))
            {
                Response.StatusCode = 429;
                return Json(new { success = false, message = "Please wait before submitting another request." });
            }

            if (request == null || !ModelState.IsValid)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(new { success = false, message = FirstModelError("Please check the contact information and try again.") });
            }

            try
            {
                var salesperson = request.EmployerId > 0
                    ? (_employeesService.GetEmployees() ?? Array.Empty<Employee>())
                        .FirstOrDefault(employee => employee.Id == request.EmployerId && IsActiveSalesperson(employee))
                    : null;
                if (request.EmployerId > 0 && salesperson == null)
                {
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return Json(new { success = false, message = "Please select an active salesperson." });
                }

                var stock = NormalizeStock(request.VehicleStock);
                var vehicle = FindVehicle(stock);
                var comment = BuildLeadComment(request, vehicle, salesperson);
                var contact = new Contact
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Phone = request.Phone.Trim(),
                    Email = request.Email.Trim(),
                    Comment = comment
                };

                var contactId = _contactService.SaveContact(contact);
                if (!contactId.HasValue)
                {
                    throw new InvalidOperationException("The lead could not be saved.");
                }

                var contactModel = new ContactModel
                {
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    Phone = contact.Phone,
                    Email = contact.Email,
                    Comment = contact.Comment,
                    EmployerId = salesperson == null ? 0 : salesperson.Id
                };
                contactModel.CurrentVehicle.VehicleDetails = vehicle;
                var delivery = await Utility.XMLHelpers.XmlRepository.SendAdfLeadAsync(contactModel);
                if (!delivery.Success)
                {
                    System.Diagnostics.Trace.TraceError(
                        "Chat lead {0} was saved but AutoRaptor delivery failed. {1}",
                        contactId.Value,
                        delivery.ErrorMessage);
                    return Json(new
                    {
                        success = false,
                        saved = true,
                        crmDelivered = false,
                        message = "Your request was saved, but AutoRaptor delivery failed. Please call (513) 489-2886 so we can follow up promptly."
                    });
                }

                return Json(new
                {
                    success = true,
                    saved = true,
                    crmDelivered = true,
                    message = salesperson == null
                        ? "Thanks! Your request was delivered to GTX Auto Group."
                        : "Thanks! Your request was delivered for the attention of " + Join(salesperson.FirstName, salesperson.LastName) + "."
                });
            }
            catch (Exception)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { success = false, message = "We could not submit your request. Please call (513) 489-2886." });
            }
        }

        private ChatNavigationResult ResolveCommandNavigation(string message, CommonUnit.Roles currentRole)
        {
            var normalizedPhrase = NormalizeCommandPhrase(message);
            if (string.IsNullOrWhiteSpace(normalizedPhrase)) return null;

            ChatBotNavigationLesson lesson = null;
            foreach (var lookupPhrase in NavigationLookupPhrases(normalizedPhrase))
            {
                lesson = _teachingService.FindActiveLesson(lookupPhrase);
                if (lesson != null) break;
            }
            if (lesson == null) return null;

            var definition = ChatBotNavigationCatalog.Find(lesson.ActionKey);
            return definition == null ? null : BuildNavigationResult(definition, currentRole);
        }

        private static IEnumerable<string> NavigationLookupPhrases(string normalizedPhrase)
        {
            yield return normalizedPhrase;

            var withoutRequestWords = Regex.Replace(
                normalizedPhrase,
                @"^(?:please\s+)?(?:open|go\s+to|navigate\s+to|take\s+me\s+to|show\s+me|show|visit|view)\s+(?:the\s+)?",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            withoutRequestWords = Regex.Replace(
                withoutRequestWords,
                @"\s+please$",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

            if (withoutRequestWords.Length > 0
                && !string.Equals(withoutRequestWords, normalizedPhrase, StringComparison.Ordinal))
            {
                yield return withoutRequestWords;
            }
        }

        private ChatNavigationResult BuildNavigationResult(
            ChatBotNavigationDefinition definition,
            CommonUnit.Roles currentRole)
        {
            var hasAccess = !definition.RequiresAuthentication
                || (definition.OwnerOnly
                    ? currentRole == CommonUnit.Roles.Owner
                    : currentRole != CommonUnit.Roles.User);
            var url = string.Equals(definition.ActionKey, "test_drive_page", StringComparison.OrdinalIgnoreCase)
                ? Url.RouteUrl("TestDriveContact")
                : Url.Action(definition.Action, definition.Controller);

            return new ChatNavigationResult
            {
                Url = url,
                Label = definition.Label,
                RequiresLogin = !hasAccess,
                RequiredRole = definition.OwnerOnly
                    ? "owner"
                    : definition.RequiresAuthentication ? "admin" : null
            };
        }

        private static string NormalizeCommandPhrase(string value)
        {
            value = (value ?? string.Empty).ToLowerInvariant();
            value = Regex.Replace(value, @"[^a-z0-9]+", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return value.Length <= 300 ? value : value.Substring(0, 300).Trim();
        }

        private async Task<AssistantResult> GetAssistantReplyAsync(string apiKey, string message, string previousResponseId)
        {
            JArray input = new JArray(new JObject
            {
                ["role"] = "user",
                ["content"] = message
            });

            var responseId = previousResponseId;
            var safetyIdentifier = BuildSafetyIdentifier();
            var vehicleResults = new List<ChatVehicleResult>();
            int? totalVehicleMatches = null;
            string inventoryUrl = null;
            for (var round = 0; round < MaxToolRounds; round++)
            {
                var payload = BuildOpenAiPayload(input, responseId, safetyIdentifier, includeTools: round == 0);
                var response = await PostOpenAiAsync(apiKey, payload);
                responseId = (string)response["id"];

                var functionCalls = (response["output"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Where(item => string.Equals((string)item["type"], "function_call", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (functionCalls.Length == 0)
                {
                    var reply = ExtractOutputText(response);
                    if (string.IsNullOrWhiteSpace(reply))
                    {
                        throw new OpenAiRequestException();
                    }

                    return new AssistantResult
                    {
                        Reply = reply.Trim(),
                        ResponseId = responseId,
                        TotalVehicleMatches = totalVehicleMatches,
                        InventoryUrl = inventoryUrl,
                        Vehicles = vehicleResults
                    };
                }

                input = new JArray();
                foreach (var call in functionCalls)
                {
                    var toolName = (string)call["name"];
                    var toolOutput = ExecuteTool(toolName, (string)call["arguments"]);
                    CaptureVehicleResults(
                        toolName,
                        toolOutput,
                        vehicleResults,
                        ref totalVehicleMatches,
                        ref inventoryUrl);
                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = (string)call["call_id"],
                        ["output"] = toolOutput
                    });
                }
            }

            throw new OpenAiRequestException();
        }

        private JObject BuildOpenAiPayload(
            JArray input,
            string previousResponseId,
            string safetyIdentifier,
            bool includeTools)
        {
            var model = ConfigurationManager.AppSettings["OpenAI:ChatModel"];
            if (string.IsNullOrWhiteSpace(model)) model = DefaultChatModel;
            else model = model.Trim();

            var payload = new JObject
            {
                ["model"] = model,
                ["instructions"] = AssistantInstructions,
                ["input"] = input,
                ["max_output_tokens"] = BoundedAppSetting("OpenAI:ChatMaxOutputTokens", 250, 100, 1000),
                ["store"] = true,
                ["prompt_cache_key"] = PromptCacheKey,
                ["safety_identifier"] = safetyIdentifier
            };

            if (includeTools)
            {
                payload["tools"] = AssistantTools.DeepClone();
                payload["parallel_tool_calls"] = true;
            }

            if (!string.IsNullOrWhiteSpace(previousResponseId))
            {
                payload["previous_response_id"] = previousResponseId;
            }

            return payload;
        }

        private async Task<JObject> PostOpenAiAsync(string apiKey, JObject payload)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, ResponsesUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await OpenAiClient.SendAsync(request))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new OpenAiRequestException();
                    }

                    return JObject.Parse(body);
                }
            }
        }

        private string ExecuteTool(string name, string argumentsJson)
        {
            JObject arguments;
            try
            {
                arguments = string.IsNullOrWhiteSpace(argumentsJson) ? new JObject() : JObject.Parse(argumentsJson);
            }
            catch (JsonException)
            {
                return JsonConvert.SerializeObject(new { error = "Invalid tool arguments." });
            }

            switch (name)
            {
                case "search_inventory":
                    return SearchInventory(arguments);
                case "get_vehicle":
                    return GetVehicle(arguments);
                case "get_dealership_hours":
                    return GetDealershipHours();
                default:
                    return JsonConvert.SerializeObject(new { error = "Unknown tool." });
            }
        }

        private string SearchInventory(JObject arguments)
        {
            var snapshot = GetChatInventorySnapshot();
            var inventory = snapshot.Vehicles;
            IEnumerable<GTXDTO> query = inventory;
            var freeText = (string)arguments["query"];
            var bodyType = NormalizeBodyTypeFilter((string)arguments["body_type"]);
            if (string.IsNullOrWhiteSpace(bodyType)) bodyType = InferBodyType(freeText);

            query = FilterContains(query, (string)arguments["make"], vehicle => vehicle.Make);
            query = FilterContains(query, (string)arguments["model"], vehicle => vehicle.Model);
            query = FilterContains(query, (string)arguments["color"], vehicle => Join(vehicle.Color, vehicle.Color2));
            query = FilterContains(query, bodyType, vehicle => Join(vehicle.VehicleType, vehicle.Body, vehicle.VehicleStyle));
            query = FilterTransmission(query, (string)arguments["transmission"]);
            query = FilterFuelType(query, (string)arguments["fuel_type"]);
            var cylinders = PositiveInt(arguments["cylinders"]);
            if (cylinders.HasValue) query = query.Where(vehicle => vehicle.Cylinders == cylinders.Value);

            var doors = PositiveInt(arguments["doors"]);
            var minimumHorsepower = PositiveInt(arguments["minimum_horsepower"]);
            var minimumCityMpg = PositiveInt(arguments["minimum_city_mpg"]);
            var minimumHighwayMpg = PositiveInt(arguments["minimum_highway_mpg"]);
            var minimumSeating = PositiveInt(arguments["minimum_seating"]);
            if (doors.HasValue) query = query.Where(vehicle => DataOneMatches(snapshot, vehicle, data => data.Doors.Contains(doors.Value)));
            if (minimumHorsepower.HasValue) query = query.Where(vehicle => DataOneMatches(snapshot, vehicle, data => data.Horsepower.Any(value => value >= minimumHorsepower.Value)));
            if (minimumCityMpg.HasValue) query = query.Where(vehicle => DataOneMatches(snapshot, vehicle, data => data.CityMpg.Any(value => value >= minimumCityMpg.Value)));
            if (minimumHighwayMpg.HasValue) query = query.Where(vehicle => DataOneMatches(snapshot, vehicle, data => data.HighwayMpg.Any(value => value >= minimumHighwayMpg.Value)));
            if (minimumSeating.HasValue) query = query.Where(vehicle => DataOneMatches(snapshot, vehicle, data => data.Seating.Any(value => value >= minimumSeating.Value)));
            query = FilterDataOneText(query, (string)arguments["features"], snapshot);
            freeText = StripStructuredDataOnePhrases(
                freeText,
                doors.HasValue,
                minimumHorsepower.HasValue,
                minimumCityMpg.HasValue || minimumHighwayMpg.HasValue,
                minimumSeating.HasValue);
            query = FilterFreeText(query, freeText, snapshot);

            var maximumPrice = PositiveInt(arguments["maximum_price"]);
            var maximumMileage = PositiveInt(arguments["maximum_mileage"]);
            if (maximumPrice.HasValue) query = query.Where(vehicle => EffectivePrice(vehicle) <= maximumPrice.Value);
            if (maximumMileage.HasValue) query = query.Where(vehicle => vehicle.Mileage <= maximumMileage.Value);

            var limit = Math.Max(1, Math.Min(5, PositiveInt(arguments["limit"]) ?? 5));
            var matchingVehicles = query
                .OrderBy(vehicle => EffectivePrice(vehicle))
                .ThenBy(vehicle => vehicle.Mileage)
                .ToArray();
            var totalMatches = matchingVehicles.Length;
            var matches = matchingVehicles
                .Take(limit)
                .Select(vehicle => ToVehicleSummary(vehicle, GetDataOneSearch(snapshot, vehicle.Stock)))
                .ToArray();
            var inventoryUrl = totalMatches > 0
                ? MatchingInventoryUrl(matchingVehicles)
                : null;

            return JsonConvert.SerializeObject(new
            {
                count = totalMatches,
                displayed_count = matches.Length,
                inventory_url = inventoryUrl,
                vehicles = matches
            });
        }

        private string MatchingInventoryUrl(IEnumerable<GTXDTO> vehicles)
        {
            var stocks = vehicles
                .Where(vehicle => vehicle != null && !string.IsNullOrWhiteSpace(vehicle.Stock))
                .Select(vehicle => vehicle.Stock.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (stocks.Length == 0) return null;

            return Url.Action("All", "Inventory", new { stocks = string.Join(",", stocks) });
        }

        private static void CaptureVehicleResults(
            string toolName,
            string toolOutput,
            ICollection<ChatVehicleResult> results,
            ref int? totalMatches,
            ref string inventoryUrl)
        {
            if (!string.Equals(toolName, "search_inventory", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(toolName, "get_vehicle", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            JObject payload;
            try
            {
                payload = JObject.Parse(toolOutput);
            }
            catch (JsonException)
            {
                return;
            }

            results.Clear();
            if (string.Equals(toolName, "search_inventory", StringComparison.OrdinalIgnoreCase))
            {
                totalMatches = (int?)payload["count"] ?? 0;
                inventoryUrl = (string)payload["inventory_url"];
                foreach (var vehicle in (payload["vehicles"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    results.Add(ToChatVehicleResult(vehicle));
                }
                return;
            }

            var found = (bool?)payload["found"] == true;
            totalMatches = found ? 1 : 0;
            inventoryUrl = null;
            if (found) results.Add(ToChatVehicleResult(payload));
        }

        private static ChatVehicleResult ToChatVehicleResult(JObject vehicle)
        {
            var titleParts = new[]
            {
                (string)vehicle["Year"],
                (string)vehicle["Make"],
                (string)vehicle["Model"],
                (string)vehicle["trim"],
                (string)vehicle["type"]
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

            return new ChatVehicleResult
            {
                Stock = (string)vehicle["Stock"],
                Title = string.Join(" ", titleParts),
                Mileage = (int?)vehicle["Mileage"] ?? 0,
                Cylinders = (int?)vehicle["Cylinders"] ?? 0,
                AdvertisedPrice = (int?)vehicle["advertised_price"] ?? 0,
                DocumentaryFee = (int?)vehicle["documentary_fee"] ?? 0,
                PriceWithDocumentaryFee = (int?)vehicle["price_with_documentary_fee"] ?? 0,
                Url = (string)vehicle["url"]
            };
        }

        private string GetVehicle(JObject arguments)
        {
            var vehicle = FindVehicle(NormalizeStock((string)arguments["stock"]));
            if (vehicle == null)
            {
                return JsonConvert.SerializeObject(new { found = false });
            }

            var dataOne = GetDataOneSearch(GetChatInventorySnapshot(), vehicle.Stock);
            return JsonConvert.SerializeObject(new
            {
                found = true,
                vehicle.Stock,
                vehicle.Year,
                vehicle.Make,
                vehicle.Model,
                trim = vehicle.VehicleStyle,
                type = vehicle.VehicleType,
                body = vehicle.Body,
                vehicle.Mileage,
                vehicle.Cylinders,
                advertised_price = EffectivePrice(vehicle),
                documentary_fee = DocumentaryFee(vehicle),
                price_with_documentary_fee = EffectivePrice(vehicle) + DocumentaryFee(vehicle),
                vehicle.Color,
                vehicle.DriveTrain,
                vehicle.Engine,
                transmission = TransmissionDescription(vehicle.Transmission),
                vehicle.FuelType,
                features = Crop(vehicle.Features, 1200),
                data_one = DataOneSummary(dataOne),
                url = VehicleUrl(vehicle.Stock)
            });
        }

        private static string GetDealershipHours()
        {
            var hours = AppCache.GetOrCreate(
                Constants.OPENHOURS_CACHE,
                () => Utility.XMLHelpers.XmlRepository.GetOpenHours() ?? Array.Empty<OpenHours>(),
                minutes: 60) ?? Array.Empty<OpenHours>();
            DateTime localNow;
            try
            {
                var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, eastern);
            }
            catch (TimeZoneNotFoundException)
            {
                localNow = DateTime.Now;
            }
            catch (InvalidTimeZoneException)
            {
                localNow = DateTime.Now;
            }

            var today = hours.FirstOrDefault(item => string.Equals(
                item.Day,
                localNow.DayOfWeek.ToString(),
                StringComparison.OrdinalIgnoreCase));
            var isOpen = today != null && localNow.Hour >= today.From && localNow.Hour < today.To;

            return JsonConvert.SerializeObject(new
            {
                timezone = "America/New_York (Eastern Time)",
                local_time = localNow.ToString("yyyy-MM-dd h:mm tt"),
                currently_open = isOpen,
                today = today == null ? "Closed" : today.Description,
                schedule = hours.Select(item => new { day = item.Day, hours = item.Description }).ToArray(),
                note = "Holiday hours may vary. Call (513) 489-2886 to confirm."
            });
        }

        private object ToVehicleSummary(GTXDTO vehicle, ChatDataOneSearch dataOne)
        {
            return new
            {
                vehicle.Stock,
                vehicle.Year,
                vehicle.Make,
                vehicle.Model,
                trim = vehicle.VehicleStyle,
                type = vehicle.VehicleType,
                vehicle.Mileage,
                vehicle.Cylinders,
                advertised_price = EffectivePrice(vehicle),
                documentary_fee = DocumentaryFee(vehicle),
                price_with_documentary_fee = EffectivePrice(vehicle) + DocumentaryFee(vehicle),
                transmission = TransmissionDescription(vehicle.Transmission),
                data_one = DataOneSummary(dataOne),
                url = VehicleUrl(vehicle.Stock)
            };
        }

        private GTX.Models.GTX FindVehicle(string stock)
        {
            if (string.IsNullOrWhiteSpace(stock)) return null;

            var dto = GetPublicInventory()
                .FirstOrDefault(vehicle => string.Equals(vehicle.Stock, stock, StringComparison.OrdinalIgnoreCase));
            return dto == null ? null : GTX.Models.GTX.ToGTX(new[] { dto }).FirstOrDefault();
        }

        private GTXDTO[] GetPublicInventory()
        {
            return GetChatInventorySnapshot().Vehicles;
        }

        private ChatInventorySnapshot GetChatInventorySnapshot()
        {
            var includeDataOne = ChatDataOneEnabled();
            var cacheKey = Constants.CHAT_INVENTORY_CACHE + (includeDataOne ? ":DataOne" : ":Standard");
            return AppCache.GetOrCreate(
                cacheKey,
                () => BuildChatInventorySnapshot(includeDataOne),
                minutes: BoundedAppSetting("OpenAI:ChatInventoryCacheMinutes", 1, 1, 60))
                ?? new ChatInventorySnapshot();
        }

        private ChatInventorySnapshot BuildChatInventorySnapshot(bool includeDataOne)
        {
            var vehicles = _inventoryService.GetInventory(
                includeHiddenInventory: false,
                includeDataOneContent: includeDataOne).vehicles ?? Array.Empty<GTXDTO>();
            var dataOneByStock = new Dictionary<string, ChatDataOneSearch>(StringComparer.OrdinalIgnoreCase);

            if (includeDataOne)
            {
                foreach (var vehicle in vehicles)
                {
                    var content = vehicle.DataOne == null ? null : vehicle.DataOne.DataOneContent;
                    var search = BuildDataOneSearch(vehicle, content);
                    if (search != null && !string.IsNullOrWhiteSpace(vehicle.Stock))
                    {
                        dataOneByStock[vehicle.Stock.Trim()] = search;
                    }

                    // Retain only the flattened, approved search fields in the chatbot cache.
                    vehicle.DataOne = null;
                }
            }

            return new ChatInventorySnapshot
            {
                Vehicles = vehicles,
                DataOneByStock = dataOneByStock
            };
        }

        private string BuildLeadComment(ChatLeadRequest request, GTX.Models.GTX vehicle, Employee salesperson)
        {
            var parts = new List<string> { "Website AI assistant lead." };
            if (salesperson != null)
            {
                parts.Add("Attn: " + Join(salesperson.FirstName, salesperson.LastName) + ".");
            }
            if (vehicle != null)
            {
                parts.Add(string.Format("Vehicle: {0} {1} {2} (stock {3}).", vehicle.Year, vehicle.Make, vehicle.Model, vehicle.Stock));
            }
            else if (!string.IsNullOrWhiteSpace(request.VehicleStock))
            {
                parts.Add("Requested stock: " + Crop(request.VehicleStock.Trim(), 20) + ".");
            }
            else
            {
                parts.Add("Vehicle: None selected - general sales inquiry.");
            }

            if (!string.IsNullOrWhiteSpace(request.Message)) parts.Add(request.Message.Trim());
            return Crop(string.Join(Environment.NewLine, parts), 1200);
        }

        private static bool IsActiveSalesperson(Employee employee)
        {
            return employee != null
                && employee.Active
                && !string.IsNullOrWhiteSpace(employee.Position)
                && employee.Position.IndexOf("Sales", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static JArray BuildTools()
        {
            return new JArray
            {
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "search_inventory",
                    ["description"] = "Search the live public GTX Auto Group inventory. Use this before claiming that a vehicle is available.",
                    ["strict"] = false,
                    ["parameters"] = JObject.Parse(@"{
                        'type':'object',
                        'properties':{
                            'query':{'type':'string'},
                            'make':{'type':'string'},
                            'model':{'type':'string'},
                            'color':{'type':'string','description':'Exterior or interior vehicle color, such as red, black, white, or silver.'},
                            'body_type':{'type':'string'},
                            'transmission':{'type':'string','description':'Transmission type such as manual, automatic, or CVT.'},
                            'fuel_type':{'type':'string','description':'Fuel type such as electric, hybrid, gasoline, diesel, or flex-fuel.'},
                            'cylinders':{'type':'integer','description':'Exact engine cylinder count, such as 4, 6, or 8.'},
                            'doors':{'type':'integer','description':'Exact door count from DataOne specifications, such as 2 or 4.'},
                            'minimum_horsepower':{'type':'integer','description':'Minimum horsepower from DataOne engine specifications.'},
                            'minimum_city_mpg':{'type':'integer','description':'Minimum EPA city MPG from DataOne specifications.'},
                            'minimum_highway_mpg':{'type':'integer','description':'Minimum EPA highway MPG from DataOne specifications.'},
                            'minimum_seating':{'type':'integer','description':'Minimum seating or passenger capacity from DataOne specifications.'},
                            'features':{'type':'string','description':'A standard equipment or specification phrase from DataOne, such as heated seats or blind spot monitoring.'},
                            'maximum_price':{'type':'integer'},
                            'maximum_mileage':{'type':'integer'},
                            'limit':{'type':'integer','minimum':1,'maximum':5}
                        },
                        'additionalProperties':false
                    }")
                },
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "get_vehicle",
                    ["description"] = "Get live details for one public vehicle using its stock number.",
                    ["strict"] = false,
                    ["parameters"] = JObject.Parse(@"{
                        'type':'object',
                        'properties':{'stock':{'type':'string'}},
                        'required':['stock'],
                        'additionalProperties':false
                    }")
                },
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "get_dealership_hours",
                    ["description"] = "Get the dealership's current weekly working hours and open/closed status from the website schedule.",
                    ["strict"] = false,
                    ["parameters"] = JObject.Parse(@"{
                        'type':'object',
                        'properties':{},
                        'additionalProperties':false
                    }")
                }
            };
        }

        private static bool ChatDataOneEnabled()
        {
            bool enabled;
            return bool.TryParse(ConfigurationManager.AppSettings["DataOne"], out enabled) && enabled;
        }

        private static ChatDataOneSearch BuildDataOneSearch(GTXDTO vehicle, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            var decoded = GTX.Models.GTX.SetDecodedData(content);
            var queryResponses = decoded == null || decoded.QueryResponses == null
                ? null
                : decoded.QueryResponses.Items;
            if (queryResponses == null) return null;

            var styles = queryResponses
                .Where(response => response != null
                    && response.UsMarketData != null
                    && response.UsMarketData.UsStyles != null
                    && response.UsMarketData.UsStyles.Styles != null)
                .SelectMany(response => response.UsMarketData.UsStyles.Styles)
                .Where(style => style != null)
                .ToArray();
            if (styles.Length == 0) return null;

            if (styles.Length > 1)
            {
                var styleTerm = (vehicle.VehicleStyle ?? string.Empty).Trim();
                if (styleTerm.Length > 0)
                {
                    var matchingStyles = styles.Where(style => Join(
                            style.Name,
                            style.BasicData == null ? null : style.BasicData.Trim,
                            style.BasicData == null ? null : style.BasicData.OemBodyStyle)
                        .IndexOf(styleTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    if (matchingStyles.Length == 1) styles = matchingStyles;
                }
            }

            if (styles.Length > 1 && !string.IsNullOrWhiteSpace(vehicle.Transmission))
            {
                var transmissionCode = vehicle.Transmission.Trim()[0];
                var matchingStyles = styles.Where(style => style.Transmissions != null
                        && style.Transmissions.Items != null
                        && style.Transmissions.Items.Any(transmission => transmission != null
                            && !string.IsNullOrWhiteSpace(transmission.Type)
                            && char.ToUpperInvariant(transmission.Type[0]) == char.ToUpperInvariant(transmissionCode)))
                    .ToArray();
                if (matchingStyles.Length == 1) styles = matchingStyles;
            }

            // Do not combine specifications from multiple trims and present them as one vehicle.
            if (styles.Length != 1) return null;

            var doors = new HashSet<int>();
            var horsepower = new HashSet<int>();
            var cityMpg = new HashSet<int>();
            var highwayMpg = new HashSet<int>();
            var seating = new HashSet<int>();
            var equipment = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var searchValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var style in styles)
            {
                AddSearchValues(searchValues, style.Name);
                var basic = style.BasicData;
                if (basic != null)
                {
                    AddSearchValues(searchValues,
                        basic.Trim,
                        basic.VehicleType,
                        basic.BodyType,
                        basic.BodySubtype,
                        basic.OemBodyStyle,
                        basic.PackageSummary,
                        basic.DriveType,
                        basic.BrakeSystem,
                        basic.CountryOfManufacture,
                        basic.Doors == null ? null : basic.Doors + " doors",
                        basic.OemDoors == null ? null : basic.OemDoors + " doors");
                    AddPositiveInts(doors, basic.Doors, basic.OemDoors);
                }

                foreach (var engine in style.Engines == null || style.Engines.Items == null
                    ? Enumerable.Empty<Engine>()
                    : style.Engines.Items.Where(item => item != null))
                {
                    AddSearchValues(searchValues,
                        engine.Name,
                        engine.MarketingName,
                        engine.EngineType,
                        engine.IceAspiration,
                        engine.IceBlockType,
                        engine.IceDisplacement,
                        engine.FuelType,
                        engine.TotalMaxHp == null ? null : engine.TotalMaxHp + " horsepower hp",
                        engine.IceMaxHp == null ? null : engine.IceMaxHp + " horsepower hp",
                        engine.ElectricMaxHp == null ? null : engine.ElectricMaxHp + " horsepower hp");
                    AddPositiveInts(horsepower, engine.TotalMaxHp, engine.IceMaxHp, engine.ElectricMaxHp);
                }

                foreach (var record in style.EpaFuelEfficiency == null || style.EpaFuelEfficiency.Records == null
                    ? Enumerable.Empty<EpaMpgRecord>()
                    : style.EpaFuelEfficiency.Records.Where(item => item != null))
                {
                    AddSearchValues(searchValues,
                        record.City == null ? null : record.City + " city mpg",
                        record.Highway == null ? null : record.Highway + " highway mpg",
                        record.Combined == null ? null : record.Combined + " combined mpg",
                        record.FuelType);
                    AddPositiveInts(cityMpg, record.City);
                    AddPositiveInts(highwayMpg, record.Highway);
                }

                foreach (var category in style.StandardSpecifications == null || style.StandardSpecifications.Categories == null
                    ? Enumerable.Empty<SpecificationCategory>()
                    : style.StandardSpecifications.Categories.Where(item => item != null))
                {
                    foreach (var value in category.Values == null
                        ? Enumerable.Empty<SpecificationValue>()
                        : category.Values.Where(item => item != null))
                    {
                        AddSearchValues(searchValues, category.Name, value.Name, value.Value, Join(category.Name, value.Name, value.Value));
                        if (Regex.IsMatch(Join(category.Name, value.Name), @"\b(seats?|seating|passengers?|occupants?|capacity)\b", RegexOptions.IgnoreCase))
                        {
                            AddPositiveInts(seating, value.Value);
                        }
                    }
                }

                AddStandardEquipment(style.StandardGenericEquipment, equipment, searchValues);
            }

            return new ChatDataOneSearch
            {
                Doors = doors.ToArray(),
                Horsepower = horsepower.ToArray(),
                CityMpg = cityMpg.ToArray(),
                HighwayMpg = highwayMpg.ToArray(),
                Seating = seating.ToArray(),
                Equipment = equipment.OrderBy(value => value).Take(100).ToArray(),
                SearchText = Crop(string.Join(" ", searchValues), 30000)
            };
        }

        private static void AddStandardEquipment(
            GenericEquipmentGroups groups,
            ISet<string> equipment,
            ISet<string> searchValues)
        {
            if (groups == null || groups.Groups == null) return;

            foreach (var group in groups.Groups.Where(item => item != null))
            {
                foreach (var category in group.Categories == null
                    ? Enumerable.Empty<GenericEquipmentCategory>()
                    : group.Categories.Where(item => item != null))
                {
                    foreach (var item in category.Equipments == null
                        ? Enumerable.Empty<GenericEquipment>()
                        : category.Equipments.Where(value => value != null))
                    {
                        var values = item.Values == null
                            ? Array.Empty<string>()
                            : item.Values.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Value))
                                .Select(value => value.Value.Trim())
                                .ToArray();
                        var description = Join(group.Name, category.Name, item.Name, string.Join(" ", values));
                        AddSearchValues(searchValues, description);
                        if (!string.IsNullOrWhiteSpace(item.Name)) equipment.Add(item.Name.Trim());
                    }
                }
            }
        }

        private static void AddSearchValues(ISet<string> target, params string[] values)
        {
            foreach (var value in values.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                target.Add(value.Trim());
            }
        }

        private static void AddPositiveInts(ISet<int> target, params string[] values)
        {
            foreach (var value in values.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                foreach (Match match in Regex.Matches(value, @"\d+"))
                {
                    int parsed;
                    if (int.TryParse(match.Value, out parsed) && parsed > 0) target.Add(parsed);
                }
            }
        }

        private static ChatDataOneSearch GetDataOneSearch(ChatInventorySnapshot snapshot, string stock)
        {
            ChatDataOneSearch search;
            return snapshot != null
                && !string.IsNullOrWhiteSpace(stock)
                && snapshot.DataOneByStock.TryGetValue(stock.Trim(), out search)
                ? search
                : null;
        }

        private static bool DataOneMatches(
            ChatInventorySnapshot snapshot,
            GTXDTO vehicle,
            Func<ChatDataOneSearch, bool> predicate)
        {
            var search = GetDataOneSearch(snapshot, vehicle == null ? null : vehicle.Stock);
            return search != null && predicate(search);
        }

        private static IEnumerable<GTXDTO> FilterDataOneText(
            IEnumerable<GTXDTO> source,
            string value,
            ChatInventorySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(value)) return source;
            var terms = Regex.Matches(value, @"[A-Za-z0-9-]+")
                .Cast<Match>()
                .Select(match => match.Value)
                .Where(term => term.Length > 1)
                .ToArray();
            return source.Where(vehicle =>
            {
                var dataOne = GetDataOneSearch(snapshot, vehicle.Stock);
                var text = Join(vehicle.Features, dataOne == null ? null : dataOne.SearchText);
                return terms.All(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        private static object DataOneSummary(ChatDataOneSearch dataOne)
        {
            if (dataOne == null) return null;
            return new
            {
                doors = dataOne.Doors,
                horsepower_max = MaximumOrNull(dataOne.Horsepower),
                city_mpg_max = MaximumOrNull(dataOne.CityMpg),
                highway_mpg_max = MaximumOrNull(dataOne.HighwayMpg),
                seating_max = MaximumOrNull(dataOne.Seating),
                standard_equipment = dataOne.Equipment.Take(25).ToArray()
            };
        }

        private static int? MaximumOrNull(IEnumerable<int> values)
        {
            var items = values == null ? Array.Empty<int>() : values.ToArray();
            return items.Length == 0 ? (int?)null : items.Max();
        }

        private static string StripStructuredDataOnePhrases(
            string value,
            bool hasDoors,
            bool hasHorsepower,
            bool hasMpg,
            bool hasSeating)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            if (hasDoors)
            {
                value = Regex.Replace(value, @"\b(?:\d+|one|two|three|four|five|six)[ -]?doors?\b", " ", RegexOptions.IgnoreCase);
            }
            if (hasHorsepower)
            {
                value = Regex.Replace(value, @"\b(?:(?:at\s+least|minimum(?:\s+of)?|over|more\s+than)\s+)?\d+\s*(?:horsepower|hp)\b", " ", RegexOptions.IgnoreCase);
            }
            if (hasMpg)
            {
                value = Regex.Replace(value, @"\b(?:(?:at\s+least|minimum(?:\s+of)?|over|more\s+than)\s+)?\d+\s*(?:(?:city|highway)\s+)?mpg\b", " ", RegexOptions.IgnoreCase);
            }
            if (hasSeating)
            {
                value = Regex.Replace(value, @"\b(?:seats?|seating)\s+(?:(?:at\s+least|minimum(?:\s+of)?)\s+)?\d+(?:\s+(?:people|passengers?))?\b", " ", RegexOptions.IgnoreCase);
                value = Regex.Replace(value, @"\b(?:(?:at\s+least|minimum(?:\s+of)?)\s+)?\d+\s+(?:seats?|people|passengers?)\b", " ", RegexOptions.IgnoreCase);
            }

            return value;
        }

        private static IEnumerable<GTXDTO> FilterContains(IEnumerable<GTXDTO> source, string value, Func<GTXDTO, string> selector)
        {
            if (string.IsNullOrWhiteSpace(value)) return source;
            var term = value.Trim();
            return source.Where(item => (selector(item) ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string InferBodyType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Regex.IsMatch(value, @"\b(?:suvs?|sport utility vehicles?)\b", RegexOptions.IgnoreCase)) return "SUV";
            if (Regex.IsMatch(value, @"\b(?:trucks?|pickups?)\b", RegexOptions.IgnoreCase)) return "TRUCK";
            if (Regex.IsMatch(value, @"\b(?:vans?|minivans?)\b", RegexOptions.IgnoreCase)) return "VAN";
            if (Regex.IsMatch(value, @"\bhatchbacks?\b", RegexOptions.IgnoreCase)) return "HATCHBACK";
            if (Regex.IsMatch(value, @"\bcoupes?\b", RegexOptions.IgnoreCase)) return "COUPE";
            if (Regex.IsMatch(value, @"\bsedans?\b", RegexOptions.IgnoreCase)) return "SEDAN";
            if (Regex.IsMatch(value, @"\bconvertibles?\b", RegexOptions.IgnoreCase)) return "CONVERTIBLE";
            if (Regex.IsMatch(value, @"\bwagons?\b", RegexOptions.IgnoreCase)) return "WAGON";
            return null;
        }

        private static string NormalizeBodyTypeFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var term = value.Trim();
            return Regex.IsMatch(
                term,
                @"^(?:cars?|vehicles?|automobiles?|autos?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                ? null
                : term;
        }

        private static IEnumerable<GTXDTO> FilterTransmission(IEnumerable<GTXDTO> source, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return source;

            var term = value.Trim().ToLowerInvariant();
            if (term.Contains("manual"))
            {
                return source.Where(vehicle => IsTransmissionCode(vehicle.Transmission, "M"));
            }
            if (term.Contains("automatic"))
            {
                return source.Where(vehicle => IsTransmissionCode(vehicle.Transmission, "A"));
            }
            if (term.Contains("cvt") || term.Contains("continuously variable"))
            {
                return source.Where(vehicle => IsTransmissionCode(vehicle.Transmission, "C"));
            }

            return source.Where(vehicle => TransmissionDescription(vehicle.Transmission)
                .IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IEnumerable<GTXDTO> FilterFreeText(
            IEnumerable<GTXDTO> source,
            string value,
            ChatInventorySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(value)) return source;

            var transmissionTerm = Regex.Match(
                value,
                @"\b(manuals?|automatics?|cvt|continuously\s+variable)\b",
                RegexOptions.IgnoreCase);
            if (transmissionTerm.Success)
            {
                source = FilterTransmission(source, transmissionTerm.Value);
            }

            var fuelTerm = Regex.Match(
                value,
                @"\b(electric|evs?|bevs?|hybrids?|phevs?|diesel|gasoline|gas|flex[ -]?fuel)\b",
                RegexOptions.IgnoreCase);
            if (fuelTerm.Success)
            {
                source = FilterFuelType(source, fuelTerm.Value);
            }

            var cylinderTerm = Regex.Match(
                value,
                @"\b(?:v\s*-?\s*)?(\d{1,2})\s*(?:-?\s*cyl(?:inder)?s?)?\b",
                RegexOptions.IgnoreCase);
            if (cylinderTerm.Success && Regex.IsMatch(
                cylinderTerm.Value,
                @"(?:\bv\s*-?\s*\d|cyl)",
                RegexOptions.IgnoreCase))
            {
                int cylinderCount;
                if (int.TryParse(cylinderTerm.Groups[1].Value, out cylinderCount))
                {
                    source = source.Where(vehicle => vehicle.Cylinders == cylinderCount);
                }
            }

            var residual = Regex.Replace(
                value,
                @"\b(?:v\s*-?\s*)?\d{1,2}\s*(?:-?\s*cyl(?:inder)?s?)\b|\bv\s*-?\s*\d{1,2}\b|\b(manuals?|automatics?|cvt|continuously|variable|transmissions?|gearboxes?|electric|evs?|bevs?|hybrids?|phevs?|diesel|gasoline|gas|flex[ -]?fuel|fuel|powered|suvs?|sport|utility|trucks?|pickups?|vans?|minivans?|hatchbacks?|coupes?|sedans?|convertibles?|wagons?|available|availability|current|currently|inventory|what|which|are|is|list|please|looking|for|any|do|you|have|show|find|me|with|vehicles?|cars?)\b",
                " ",
                RegexOptions.IgnoreCase);
            var terms = Regex.Matches(residual, @"[A-Za-z0-9-]+")
                .Cast<Match>()
                .Select(match => match.Value)
                .Where(term => term.Length > 1)
                .ToArray();

            foreach (var term in terms)
            {
                var searchTerm = term;
                source = source.Where(vehicle => SearchableVehicleText(
                        vehicle,
                        GetDataOneSearch(snapshot, vehicle.Stock))
                    .IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return source;
        }

        private static string SearchableVehicleText(GTXDTO vehicle, ChatDataOneSearch dataOne)
        {
            return Join(
                vehicle.Stock,
                vehicle.Year.ToString(),
                vehicle.Make,
                vehicle.Model,
                vehicle.VehicleStyle,
                vehicle.VehicleType,
                vehicle.Body,
                vehicle.Color,
                vehicle.Color2,
                vehicle.Features,
                vehicle.Transmission,
                TransmissionDescription(vehicle.Transmission),
                vehicle.FuelType,
                dataOne == null ? null : dataOne.SearchText);
        }

        private static IEnumerable<GTXDTO> FilterFuelType(IEnumerable<GTXDTO> source, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return source;

            var term = value.Trim().ToLowerInvariant();
            if (Regex.IsMatch(term, @"\b(electric|ev|evs|bev|bevs)\b"))
            {
                term = "Electric";
            }
            else if (Regex.IsMatch(term, @"\b(hybrid|hybrids|phev|phevs)\b"))
            {
                term = "Hybrid";
            }
            else if (term == "gas")
            {
                term = "Gasoline";
            }
            else if (term.Contains("flex"))
            {
                term = "Flex-Fuel";
            }

            return source.Where(vehicle => string.Equals(
                (vehicle.FuelType ?? string.Empty).Trim(),
                term,
                StringComparison.OrdinalIgnoreCase));
        }

        private static string TransmissionDescription(string value)
        {
            var code = (value ?? string.Empty).Trim();
            if (IsTransmissionCode(code, "M")) return "Manual" + TransmissionSuffix(code);
            if (IsTransmissionCode(code, "A")) return "Automatic" + TransmissionSuffix(code);
            if (IsTransmissionCode(code, "C")) return "Continuously variable (CVT)" + TransmissionSuffix(code);
            return string.IsNullOrWhiteSpace(code) ? "Not specified" : code;
        }

        private static bool IsTransmissionCode(string value, string expectedCode)
        {
            var code = (value ?? string.Empty).Trim();
            return code.Equals(expectedCode, StringComparison.OrdinalIgnoreCase)
                || code.StartsWith(expectedCode + " ", StringComparison.OrdinalIgnoreCase);
        }

        private static string TransmissionSuffix(string value)
        {
            var code = (value ?? string.Empty).Trim();
            return code.Length > 1 ? " " + code.Substring(1).Trim() : string.Empty;
        }

        private static int EffectivePrice(GTXDTO vehicle)
        {
            return vehicle.InternetPrice > 0 ? vehicle.InternetPrice : vehicle.RetailPrice;
        }

        private static int DocumentaryFee(GTXDTO vehicle)
        {
            return vehicle.InternetPrice > 0 ? Constants.DOCUMENTARY_FEE : 0;
        }

        private static int EffectivePrice(GTX.Models.GTX vehicle)
        {
            return vehicle.InternetPrice > 0 ? vehicle.InternetPrice : vehicle.RetailPrice;
        }

        private static int DocumentaryFee(GTX.Models.GTX vehicle)
        {
            return vehicle.InternetPrice > 0 ? Constants.DOCUMENTARY_FEE : 0;
        }

        private string VehicleUrl(string stock)
        {
            return Url.Action("Details", "Inventory", new { stock }, Request.Url == null ? "https" : Request.Url.Scheme);
        }

        private static string ExtractOutputText(JObject response)
        {
            var texts = new List<string>();
            foreach (var item in response["output"] as JArray ?? new JArray())
            {
                if (!string.Equals((string)item["type"], "message", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var content in item["content"] as JArray ?? new JArray())
                {
                    if (string.Equals((string)content["type"], "output_text", StringComparison.OrdinalIgnoreCase))
                    {
                        texts.Add((string)content["text"]);
                    }
                }
            }
            return string.Join(Environment.NewLine, texts.Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        private bool AllowRequest(string action, int limit, TimeSpan window)
        {
            var key = "GTX:ChatRate:" + action + ":" + BuildSafetyIdentifier();
            var counter = RateLimitCache.Get(key) as RateCounter;
            if (counter == null)
            {
                counter = new RateCounter();
                counter = (RateLimitCache.AddOrGetExisting(key, counter, DateTimeOffset.UtcNow.Add(window)) as RateCounter) ?? counter;
            }

            lock (counter)
            {
                counter.Count++;
                return counter.Count <= limit;
            }
        }

        public static string GetOrCreateRequestToken(System.Web.HttpSessionStateBase session)
        {
            if (session == null) return string.Empty;

            var token = session[RequestTokenSessionKey] as string;
            if (!string.IsNullOrWhiteSpace(token)) return token;

            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            token = Convert.ToBase64String(bytes);
            session[RequestTokenSessionKey] = token;
            return token;
        }

        private bool HasValidRequestToken(string suppliedToken)
        {
            var expectedToken = Session == null ? null : Session[RequestTokenSessionKey] as string;
            if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(suppliedToken)) return false;

            var expected = Encoding.UTF8.GetBytes(expectedToken);
            var supplied = Encoding.UTF8.GetBytes(suppliedToken);
            if (expected.Length != supplied.Length) return false;

            var difference = 0;
            for (var index = 0; index < expected.Length; index++)
            {
                difference |= expected[index] ^ supplied[index];
            }

            return difference == 0;
        }

        private string BuildSafetyIdentifier()
        {
            var sessionId = Session == null ? string.Empty : Session.SessionID;
            var value = string.Join("|", sessionId, Request.UserHostAddress ?? "unknown", Request.UserAgent ?? string.Empty);
            using (var sha = SHA256.Create())
            {
                return "visitor_" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).Substring(0, 24).ToLowerInvariant();
            }
        }

        private string FirstModelError(string fallback)
        {
            return ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message)) ?? fallback;
        }

        private static string NormalizeResponseId(string value)
        {
            value = (value ?? string.Empty).Trim();
            return value.Length <= 200 && ResponseIdPattern.IsMatch(value) ? value : null;
        }

        private static string NormalizeStock(string value)
        {
            value = (value ?? string.Empty).Trim();
            return value.Length <= 20 ? value : null;
        }

        private static int? PositiveInt(JToken token)
        {
            int value;
            return token != null && int.TryParse(token.ToString(), out value) && value >= 0 ? value : (int?)null;
        }

        private static int BoundedAppSetting(string key, int fallback, int minimum, int maximum)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value)
                && value >= minimum
                && value <= maximum
                ? value
                : fallback;
        }

        private static HttpClient CreateOpenAiClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(
                    BoundedAppSetting("OpenAI:ChatTimeoutSeconds", 30, 5, 120))
            };
        }

        private static string Join(params string[] values)
        {
            return string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string Crop(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maximumLength) return value;
            return value.Substring(0, maximumLength);
        }

        private const string AssistantInstructions = @"You are the GTX Auto Group website AI assistant for a used-car dealership in Cincinnati, Ohio.
Be concise, friendly, factual, and respond in the language used by the shopper.
Use the inventory tools for every question about current availability, price, mileage, features, or vehicle details. Never invent inventory or claim a vehicle is available without a tool result.
Use get_dealership_hours for every question about business hours, working hours, opening or closing times, or whether the dealership is open. Never guess the schedule.
Never claim that you changed or reset controls, filters, or other state on the shopper's page.
Do not claim that you can create, edit, or delete chatbot commands from the chat window.
For transmission questions, pass manual, automatic, or CVT in the search_inventory transmission parameter.
For fuel or powertrain questions, pass electric, hybrid, gasoline, diesel, or flex-fuel in the search_inventory fuel_type parameter. Treat EV and BEV as electric and PHEV as hybrid.
For vehicle-color questions, pass the requested exterior or interior color in the search_inventory color parameter.
Treat car, cars, vehicle, and vehicles as generic inventory words; never pass them as the body_type. Only use body_type for a specific category such as SUV, truck, van, sedan, coupe, hatchback, convertible, or wagon.
For engine-cylinder questions, pass the exact cylinder count in the search_inventory cylinders parameter. Treat V8 as 8 cylinders, V6 as 6 cylinders, and similar V-number requests accordingly.
For door-count questions, pass the exact count in the search_inventory doors parameter.
For minimum horsepower, city MPG, highway MPG, or seating requests, use the corresponding minimum_horsepower, minimum_city_mpg, minimum_highway_mpg, or minimum_seating parameter.
For requested equipment or features, pass the shopper's concise feature phrase in the search_inventory features parameter.
DataOne specifications are included only when available for the current vehicle. Treat returned DataOne equipment as verified standard equipment only, never as optional equipment that is installed. If no vehicles match or DataOne is unavailable, say so without guessing.
The website renders inventory tool results as standardized vehicle cards. When a vehicle tool returns one or more vehicles, reply with one short introductory or request-specific sentence only; do not enumerate vehicles, repeat vehicle facts, include prices, or include vehicle links in your text. The cards display the advertised price + documentary fee = price with documentary fee using the exact tool values. Say that price and availability can change and should be confirmed with the dealership.
You may explain general shopping, trade-in, and financing concepts, but never guarantee credit approval, quote binding loan terms, appraise a trade, negotiate a price, or request SSNs, bank details, driver's-license numbers, or credit-card information.
Invite interested shoppers to use the Contact sales form in the chat window or call (513) 489-2886. Do not claim that you submitted a lead yourself.
Do not output HTML. Keep normal answers under 120 words unless the shopper asks for more detail.";

        private sealed class AssistantResult
        {
            public string Reply { get; set; }
            public string ResponseId { get; set; }
            public int? TotalVehicleMatches { get; set; }
            public string InventoryUrl { get; set; }
            public List<ChatVehicleResult> Vehicles { get; set; } = new List<ChatVehicleResult>();
        }

        private sealed class ChatInventorySnapshot
        {
            public GTXDTO[] Vehicles { get; set; } = Array.Empty<GTXDTO>();
            public Dictionary<string, ChatDataOneSearch> DataOneByStock { get; set; }
                = new Dictionary<string, ChatDataOneSearch>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ChatDataOneSearch
        {
            public int[] Doors { get; set; } = Array.Empty<int>();
            public int[] Horsepower { get; set; } = Array.Empty<int>();
            public int[] CityMpg { get; set; } = Array.Empty<int>();
            public int[] HighwayMpg { get; set; } = Array.Empty<int>();
            public int[] Seating { get; set; } = Array.Empty<int>();
            public string[] Equipment { get; set; } = Array.Empty<string>();
            public string SearchText { get; set; }
        }

        private sealed class RateCounter
        {
            public int Count { get; set; }
        }

        private sealed class OpenAiRequestException : Exception
        {
        }
    }
}
