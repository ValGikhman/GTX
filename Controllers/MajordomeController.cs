using GTX.Models;
using Newtonsoft.Json;
using Services;
using System;
using System.Collections.Generic;
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
                return SaveStory(vehicle.Stock, response.story, response.title);
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SaveStory(string stock, string story, string title) {
            try {
                InventoryService.SaveStory(stock, story, title);
                return Json(new { success = true, message = "Story saved successfully.", Title = title, Story = story });
            }

            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult> ReStoryAll() {
            try {
                Model.Inventory.Vehicles = Model.Inventory.All;
                Parallel.ForEach(Model.Inventory.Vehicles, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async vehicle => {
                    if (string.IsNullOrEmpty(vehicle.Story.Title)) {
                        var story = await GetChatGptResponse(GetPrompt(vehicle));
                        var response = SplitResponse(story);
                        InventoryService.SaveStory(vehicle.Stock, response.story, response.title);
                    }
                });

                return Json(new { success = true, message = "Story created successfully." });
            }

            catch (Exception ex) {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUpdatedItems() {
            Model.Inventory.Vehicles = ApplyImagesAndStories(Model.Inventory.Vehicles);
            return Json(Model.Inventory.Vehicles, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> Upload(IEnumerable<HttpPostedFileBase> files, string stock) {
            if (files != null && files.Any()) {
                var uploadPath = Server.MapPath(Path.Combine(imageFolder, stock));
                string url = Path.Combine(imageFolder, stock);

                Directory.CreateDirectory(uploadPath);
                var tasks = files
                    .Where(f => f != null && f.ContentLength > 0)
                    .Select(async f => {
                        string fullPath = Path.Combine(uploadPath, f.FileName);

                        using var memoryStream = new MemoryStream(); await f.InputStream.CopyToAsync(memoryStream);
                        byte[] imageBytes = memoryStream.ToArray();
                        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
                            await fs.WriteAsync(imageBytes, 0, imageBytes.Length);
                        }

                        InventoryService.SaveImage(stock, $"{url}/{f.FileName}");
                    });

                await Task.WhenAll(tasks);
            }

            return Json(new {
                Message = "Upload successful"
            });
        }

        public void SetDetails(string stock) {
            stock = stock?.Trim().ToUpper();
            Model.CurrentVehicle.VehicleDetails = Model.Inventory.All.FirstOrDefault(m => m.Stock == stock);
            Model.CurrentVehicle.VehicleImages = GetImages(stock);
            SessionData.CurrentVehicle = Model.CurrentVehicle;
        }

        [HttpPost]
        public JsonResult SaveOrder(string[] sorted, string stock) {
            InventoryService.UpdateOrder(sorted, stock);
            return Json(new { success = true });
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
        public JsonResult DeleteImage(string file, string stock) {
            string filePath = Server.MapPath(file);

            if (System.IO.File.Exists(filePath)) {
                System.IO.File.Delete(filePath);
                InventoryService.DeleteImage(stock, file);
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public ActionResult DeleteImages(string stock) {
            string path = $"{imageFolder}{stock}";
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

                InventoryService.DeleteImages(stock);
                Model.Inventory.Vehicles = ApplyImagesAndStories(Model.Inventory.Vehicles);
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
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 700,
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
2. Write a minimum of **10 sentences**, each inside a separate <p class='p-story'> tag.
3. Write in a poetic yet mysterious and persuasive tone with a touch of futuristic imagery to make story vivid, rich, and atmospheric.
4. Mention at least **5 car features** from the provided list and wrap each feature in <strong class='strong-story'> tags as well as the car.
5. Do **not use double quotes** anywhere in the story.
6. End the story with a sense of joy, adventure, opportunity.

The output should be **only the HTML story** without any extra text before or after.
Please do not place any other characters like **``` and **```html text in front of the output.
Do not place any **<html>**, **<body>** and **<head>** tags
";
            return prompt;
        }

        private (string story, string title) SplitResponse(string response) {
            string story;
            string title = string.Empty;

            // Гамно remover
            story = response.Replace("```html", "").Replace("```", "").Replace("\"", "");
            story = story.Replace("<html>", "").Replace("</html>", "");
            story = story.Replace("<head>", "").Replace("</head>", "");
            story = story.Replace("<body>", "").Replace("</body>", "");

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