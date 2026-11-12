using System;
using System.Collections.Generic;
using System.Linq;

namespace GTX.Common {
    public enum MajordomeItem { InventoryDashboard, InventoryManagement, Inventory, VinDecoder, Appraisal, Announcements, Blogs, Specials, Employees, Health, Sitemap, ChatBotCommands, Roles, Profile, Services }
    public sealed class MajordomeMenuEntry {
        public MajordomeItem Item { get; set; }
        public string Title { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
    }
    public static class MajordomeMenu {
        public static readonly IReadOnlyList<MajordomeMenuEntry> Items = Array.AsReadOnly(new[] {
            Entry(MajordomeItem.InventoryDashboard, "Dashboard", Enums.Controllers.InventoryManagement, "Dashboard", icon: "bi-speedometer2", description: "Track inventory activity and trends"),
            Entry(MajordomeItem.InventoryManagement, "Management", Enums.Controllers.InventoryManagement, icon: "bi-database-gear", description: "Manage inventory imports and records"),
            Entry(MajordomeItem.Inventory, "Majordome", Enums.Controllers.Majordome, "Inventory", icon: "bi-car-front", description: "Browse and manage your vehicles"),
            Entry(MajordomeItem.VinDecoder, "Vindecoder", Enums.Controllers.VinDecoder, icon: "bi-upc-scan", description: "Decode vehicle details from a VIN"),
            Entry(MajordomeItem.Appraisal, "Appraisal", Enums.Controllers.Majordome, "Appraisal", icon: "bi-graph-up-arrow", description: "Research vehicle values and auction bids"),
            Entry(MajordomeItem.Announcements, "Alerts", Enums.Controllers.Announcements, icon: "bi-megaphone", description: "Manage website alerts and notices"),
            Entry(MajordomeItem.Blogs, "Blogs", Enums.Controllers.Blogs, icon: "bi-journal-richtext", description: "Write and manage blog posts"), Entry(MajordomeItem.Specials, "Specials", Enums.Controllers.Specials, icon: "bi-tags", description: "Manage featured offers and specials"),
            Entry(MajordomeItem.Employees, "Employees", Enums.Controllers.Employees, icon: "bi-person-badge", description: "Manage your team and staff profiles"), Entry(MajordomeItem.Health, "Health", Enums.Controllers.Health, icon: "bi-activity", description: "Check website health and diagnostics"),
            Entry(MajordomeItem.Sitemap, "Sitemap", Enums.Controllers.Sitemap, icon: "bi-diagram-3", description: "Review indexed pages and vehicle links"), Entry(MajordomeItem.ChatBotCommands, "Chat Bot", Enums.Controllers.ChatBotCommands, icon: "bi-chat-dots", description: "Manage chatbot navigation commands"),
            Entry(MajordomeItem.Roles, "Roles", Enums.Controllers.Roles, icon: "bi-shield-lock", description: "Manage passwords and menu permissions"),
            Entry(MajordomeItem.Profile, "Profile", Enums.Controllers.Profile, icon: "bi-building-gear", description: "Manage company details and research defaults"),
            Entry(MajordomeItem.Services, "Services", Enums.Controllers.Services, icon: "bi-plug", description: "Configure connected services and credentials") });
        private static MajordomeMenuEntry Entry(MajordomeItem item, string title, Enums.Controllers controller, string action = "Index", string icon = "bi-grid", string description = "") {
            return new MajordomeMenuEntry { Item = item, Title = title, Controller = controller.ToString(), Action = action, Icon = icon, Description = description };
        }
        public static string[] Defaults(string role) {
            return Items.Where(x => role == MajordomeAccount.Name
                || (x.Item != MajordomeItem.Roles && x.Item != MajordomeItem.Profile && x.Item != MajordomeItem.Services))
                .Select(x => x.Item.ToString()).ToArray();
        }
        public static MajordomeItem ForRequest(string controller, string action) {
            if (controller.Equals(Enums.Controllers.Majordome.ToString(), StringComparison.OrdinalIgnoreCase))
                return action.StartsWith("Appraisal", StringComparison.OrdinalIgnoreCase) || action.Equals("Apprisal", StringComparison.OrdinalIgnoreCase) ? MajordomeItem.Appraisal : MajordomeItem.Inventory;
            if (controller.Equals(Enums.Controllers.InventoryManagement.ToString(), StringComparison.OrdinalIgnoreCase))
                return action.IndexOf("Dashboard", StringComparison.OrdinalIgnoreCase) >= 0 ? MajordomeItem.InventoryDashboard : MajordomeItem.InventoryManagement;
            return Items.First(x => x.Controller.Equals(controller, StringComparison.OrdinalIgnoreCase)).Item;
        }
    }
}
