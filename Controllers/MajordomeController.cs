using GTX.Models;
using Newtonsoft.Json;
using Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace GTX.Controllers {

    public class MajordomeController : BaseController {
        private readonly string openAiApiKey = ConfigurationManager.AppSettings["OpenAI:ApiKey"];

        public MajordomeController(ISessionData sessionData, IInventoryService inventoryService, ILogService logService)
            : base(sessionData, inventoryService, logService) {
            if (Model == null) {
                Model = new BaseModel();
                Model.Inventory = new Inventory();
            }
        }

        public ActionResult Index(BaseModel model) {
            Model.Inventory.Vehicles = Model.Inventory.All;
            return View(Model);
        }

        public ActionResult Inventory(BaseModel model) {
            Model.Inventory.Vehicles = Model.Inventory.All;
            foreach (var vehicle in Model.Inventory.Vehicles) {
                vehicle.Story = InventoryService.GetStory(vehicle.Stock);
            }

            return View(Model);
        }

        public ActionResult Logs() {
            return View(LogService.GetLogs());
        }

        [HttpPost]
        public async Task<ActionResult> CreateStory(string stock) {
            try {
                var vehicle = Model.Inventory.Vehicles.FirstOrDefault(m => m.Stock == stock);
                var story = await GetChatGptResponse(GetPrompt(vehicle));
                var response = SplitResponse(story);
                InventoryService.SaveStory(vehicle.Stock, response.story, response.title);
                return Json(new { success = true, message = "Story created successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> ReStoryAll() {
            try {
                Model.Inventory.Vehicles = Model.Inventory.All;
                foreach (var vehicle in Model.Inventory.Vehicles) {
                    vehicle.Story = InventoryService.GetStory(vehicle.Stock);
                    if (vehicle.Story == null) {
                        var story = await GetChatGptResponse(GetPrompt(vehicle));
                        var response = SplitResponse(story);
                        InventoryService.SaveStory(vehicle.Stock, response.story, response.title);
                    }
                }
                return Json(new { success = true, message = "Story created successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUpdatedItems() {
            var items = Model.Inventory.All; // updated list
            foreach (var vehicle in Model.Inventory.Vehicles) {
                vehicle.Story = InventoryService.GetStory(vehicle.Stock); // Attach stories
            }

            return Json(items, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult Upload(List<HttpPostedFileBase> files, string stock) {
            if (files == null || files.Count == 0) {
                return new HttpStatusCodeResult(400, "No files uploaded.");
            }

            var uploadPath = Server.MapPath("~/GTXImages/Inventory/" + stock);
            if (!Directory.Exists(uploadPath)) {
                Directory.CreateDirectory(uploadPath);
            }

            foreach (var file in files) {
                if (file != null && file.ContentLength > 0) {
                    var filePath = Path.Combine(uploadPath, Path.GetFileName(file.FileName));
                    file.SaveAs(filePath);
                }
            }

            Model.Inventory.Vehicles = ApplyImages(Model.Inventory.Vehicles);
            return Json(new { Message = "Upload successful", FileCount = files.Count });
        }

        [HttpPost]
        public JsonResult ApplyTerm(string term) {
            term = term.Trim().ToUpper();
            Log($"Applying term: {term}");
            Model.CurrentFilter = null;
            Model.Inventory.Vehicles = ApplyTerms(term);
            Model.Inventory.Title = "Search";
            return Json(new { redirectUrl = Url.Action("Inventory") });
        }

        [HttpPost]
        public ActionResult DeleteImages(string stock) {
            string path = $"~/GTXImages/Inventory/{stock}";
            path = Server.MapPath(path);

            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };

            if (!Directory.Exists(path)) {
                return Json(new { success = false, message = "Directory not found." });
            }

            try {
                string[] imageFiles = Directory.GetFiles(path).Where(file => extensions.Contains(Path.GetExtension(file).ToLower())).ToArray();

                foreach (string file in imageFiles) {
                    System.IO.File.Delete(file);
                }


                Model.Inventory.Vehicles = ApplyImages(Model.Inventory.Vehicles);
                return Json(new { success = true, message = "All files deleted successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }

        }

        private async Task<string> GetChatGptResponse(string prompt) {
            var apiUrl = "https://api.openai.com/v1/chat/completions";

            using (var httpClient = new HttpClient()) {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiApiKey}");

                var requestBody = new {
/*                    model = "gpt-3.5-turbo",   // or "gpt-4o" if your account supports it */
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 500,
                    temperature = 0.9
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode) {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                    return result.choices[0].message.content.ToString();
                }
                else {
                    return $"Error: {response.StatusCode}";
                }
            }
        }

        private string GetPrompt(Models.GTX vehicle) {
            var reps = string.Empty;
            var representatives = Model.Employers.Where(m => m.Position.Contains("Sales")).Select(m => m.Name).ToArray();
            if (representatives != null) {
                reps = string.Join(", ", representatives);
            }
            string car = $"{vehicle.Year} {vehicle.Make} {vehicle.Model} {vehicle.VehicleStyle}";
            var features = $"{vehicle.Features}";
            string prompt = $@"
You are an expert automotive storyteller. Write a short captivating, imaginative, vivid, and engaging story in HTML format for the following car:
Car: {car}  
Features: {features}
Genral: {car} is being sold by the GTX Autogroup here in Cincinnati Ohio area.
Our sales crew: {reps} will help you to start your thrilling journey through scenic Ohio valley roads.

Your response must:
1. Start with a catchy **title inside <title> tags** (for example: <title>The Electric Dream</title>).
2. Write a minimum of **10 sentences**, each inside a separate <p> tag.
3. Use poetic language.
4. Mention at least **5 car features** from the provided list and wrap each feature in <strong> tags as well as the car.
5. Do **not use double quotes** anywhere in the story.
6. End the story with a sense of joy, adventure, opportunity.

The output should be **only the HTML story** without any extra text before or after.
Please do not place any other characters like **` and **html text in front of the output.
";
            return prompt;
        }

        private (string story, string title) SplitResponse(string response) {
            string story = string.Empty;
            string title = string.Empty;

            // Гамно remover
            story = response.Replace("```html", "").Replace("```", "").Replace("\"", "");
            string pattern = @"<title>(.*?)</title>";
            Match match = Regex.Match(story, pattern, RegexOptions.IgnoreCase);

            if (match.Success) {
                title = match.Groups[1].Value;  // Extracted inner text

                // Remove the entire <h5>...</h5> from the HTML
                story = Regex.Replace(story, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            return (story, title);
        }
    }
}