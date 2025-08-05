using GTX.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
            ViewBag.Message = "Inventory management";
            ViewBag.Title = "Inventory management";
            var vehicles = Model.Inventory.All ?? Array.Empty<Models.GTX>();

            Parallel.ForEach(vehicles, vehicle =>
            {
                vehicle.Story = InventoryService.GetStory(vehicle.Stock);
            });

            Model.Inventory.Vehicles = vehicles;

            return View(Model);
        }

        public ActionResult Logs() {
            ViewBag.Message = "Logs";
            ViewBag.Title = "Logs";
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
        public void ShowAdmin() {
            SessionData.Majordome = true;
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

                        InventoryService.SaveImage(stock, $"{f.FileName}");
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

        public ActionResult OverlayModal(Guid id) {
            Services.Image image = InventoryService.GetImage(id);
            return PartialView("_OverlayModal", image);
        }


        [HttpPost]
        public JsonResult SaveOrder(Guid[] sorted) {
            InventoryService.UpdateOrder(sorted);
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult SaveOverlay(Guid id, string stock, string overlay, string imagePath) {
            InventoryService.SaveOverlay(id, overlay);
            CreateImageWithOverlay(stock, imagePath, overlay);
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
        public JsonResult DeleteImage(Guid id, string file, string stock) {
            string path = $"{imageFolder}{stock}/{file}";
            path = Server.MapPath(path);

            if (System.IO.File.Exists(path)) {
                System.IO.File.Delete(path);
                InventoryService.DeleteImage(id);
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

        public void CreateImageWithOverlay(string stock, string baseImagePath,  string overlayJson) {
            string baseImage = Server.MapPath(baseImagePath);
            string outputImage = Server.MapPath(baseImagePath);

            using (var image = new Bitmap(baseImage))
            using (var graphics = Graphics.FromImage(image)) {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Parse JSON
                JObject overlayObj = JObject.Parse(overlayJson);
                var overlay = overlayObj["overlay"];
                var bgColor = GetColorFromStyle((string)overlay["style"]);

                // Draw semi-transparent overlay rectangle (opacity 0.6)
                float overlayOpacity = 0.6f;
                int alpha = (int)(overlayOpacity * 255);
                Color bgColorWithAlpha = Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B);

                int overlayHeight = image.Height / 8;
                var overlayRect = new Rectangle(0, image.Height - overlayHeight, image.Width, overlayHeight); // bottom banner

                using (Brush brush = new SolidBrush(bgColorWithAlpha)) {
                    graphics.FillRectangle(brush, overlayRect);
                }

                // Draw text
                foreach (var child in overlay["children"]) {
                    string text = (string)child["text"];
                    string style = (string)child["style"];
                    var (font, color) = GetFontAndColorFromStyle(image.Width, style);

                    using (var brush = new SolidBrush(color)) // text has full opacity
                    {
                        RectangleF textRect = new RectangleF(
                            20, // padding-left
                            image.Height - overlayHeight, // Y: start at bottom overlay
                            image.Width - 20, // width with horizontal padding
                            overlayHeight // height
                        );

                        // Setup string formatting for centered text and wrapping
                        StringFormat format = new StringFormat() {
                            Alignment = StringAlignment.Center, // horizontal center
                            LineAlignment = StringAlignment.Center, // vertical center
                            Trimming = StringTrimming.EllipsisWord,
                            FormatFlags = StringFormatFlags.LineLimit
                        };

                        // Draw the text block
                        graphics.DrawString(text, font, brush, textRect, format);
                    }
                }
                string filename = $"{Path.GetDirectoryName(outputImage)}/{Path.GetFileNameWithoutExtension(outputImage)}-O{Path.GetExtension(outputImage)}";
                image.Save(filename, ImageFormat.Png);
                InventoryService.SaveImage(stock, Path.GetFileName(filename));
            }
        }

        [HttpPost]
        public async Task<ActionResult> RemoveBackground(string stock, string file) {

            var fileName = Server.MapPath(Path.Combine(imageFolder, stock, file));
            string originalExtension = Path.GetExtension(file).ToLower();

            if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(fileName)) {
                return Json(new { success = false, message = "File does not exist." });
            }

/*            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
                ModelState.AddModelError("", "Only PNG images are supported.");
                return View();
            }*/

            var apiUrl = "https://api.openai.com/v1/images/edits";

            // Read image into stream
            byte[] imageData;
            if (originalExtension == ".jpg" || originalExtension == ".jpeg") {
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read)) {
                    imageData = ConvertJpgToTransparentPng(fs, Color.White, 20);
                }
            }
            else if (originalExtension == ".png") {
                imageData = System.IO.File.ReadAllBytes(fileName);
            }
            else {
                return Json(new { success = false, message = "Only JPG or PNG files are supported." });
            }

            using (var client = new HttpClient())
            using (var form = new MultipartFormDataContent()) {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiApiKey);
                var imageContent = new ByteArrayContent(imageData);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                form.Add(imageContent, "image", fileName);
                form.Add(new StringContent("Remove the background"), "prompt");
                form.Add(new StringContent("1"), "n");
                form.Add(new StringContent("1024x1024"), "size");

                var response = await client.PostAsync(apiUrl, form);

                if (!response.IsSuccessStatusCode) {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"OpenAI API Error: {error}" });
                }

                var responseData = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseData);
                string imageUrl = result.data[0].url;

                byte[] finalImageData;
                using (var imageClient = new HttpClient()) {
                    finalImageData = await imageClient.GetByteArrayAsync(imageUrl);
                }

                var dirPath = Server.MapPath(imageFolder);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                System.IO.File.WriteAllBytes(fileName, finalImageData);

                return Json(new { success = true, message = "All good" });
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

        private Color GetColorFromStyle(string style) {
            // crude parser for background-color: lightskyblue;
            var match = System.Text.RegularExpressions.Regex.Match(style, @"background-color:\s*([^;]+)");
            return match.Success ? Color.FromName(match.Groups[1].Value.Trim()) : Color.Transparent;
        }

        private (Font font, Color color) GetFontAndColorFromStyle(int width, string style) {
            float size = 1.0f;
            FontStyle fontStyle = FontStyle.Regular;
            Color color = Color.Black;
            string fontFamily = "Arial";

            if (style.Contains("italic")) fontStyle |= FontStyle.Italic;
            if (style.Contains("bold")) fontStyle |= FontStyle.Bold;


            float vw = width / 100f;

            var sizeMatch = Regex.Match(style, @"font-size:\s*(\d+(\.\d+)?)vw");
            if (sizeMatch.Success) size = float.Parse(sizeMatch.Groups[1].Value) * vw * 1.5f;

            var colorMatch = System.Text.RegularExpressions.Regex.Match(style, @"color:\s*([^;]+)");
            if (colorMatch.Success) color = Color.FromName(colorMatch.Groups[1].Value.Trim());

            Font font = new Font(fontFamily, size, fontStyle);
            return (font, color);
        }

        private byte[] ConvertJpgToTransparentPng(Stream jpgStream, Color backgroundToRemove, int tolerance = 10) {

            using (var original = new Bitmap(jpgStream)) {
                Bitmap transparentBitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

                for (int y = 0; y < original.Height; y++) {
                    for (int x = 0; x < original.Width; x++) {
                        Color pixelColor = original.GetPixel(x, y);

                        if (IsColorClose(pixelColor, backgroundToRemove, tolerance)) {
                            transparentBitmap.SetPixel(x, y, Color.Transparent);
                        }
                        else {
                            transparentBitmap.SetPixel(x, y, pixelColor);
                        }
                    }
                }

                using (var ms = new MemoryStream()) {
                    transparentBitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private bool IsColorClose(Color a, Color b, int tolerance) {
            return Math.Abs(a.R - b.R) <= tolerance &&
                   Math.Abs(a.G - b.G) <= tolerance &&
                   Math.Abs(a.B - b.B) <= tolerance;
        }
    }
}