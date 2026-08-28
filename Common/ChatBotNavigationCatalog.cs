using System;
using System.Collections.Generic;
using System.Linq;

namespace GTX.Common
{
    public sealed class ChatBotNavigationDefinition
    {
        public string ActionKey { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public bool RequiresAuthentication { get; set; }
        public bool OwnerOnly { get; set; }
    }

    public static class ChatBotNavigationCatalog
    {
        private static readonly ChatBotNavigationDefinition[] Definitions =
        {
            Public("all_inventory", "All Inventory", "Open the public All Inventory page.", "Inventory", "All"),
            Public("suv_inventory", "SUV Inventory", "Open the public SUV inventory page.", "Inventory", "Suvs"),
            Public("truck_inventory", "Truck Inventory", "Open the public truck inventory page.", "Inventory", "Trucks"),
            Public("sedan_inventory", "Sedan Inventory", "Open the public sedan inventory page.", "Inventory", "Sedans"),
            Public("van_inventory", "Van Inventory", "Open the public van inventory page.", "Inventory", "Vans"),
            Public("wagon_inventory", "Wagon Inventory", "Open the public wagon inventory page.", "Inventory", "Wagons"),
            Public("coupe_inventory", "Coupe Inventory", "Open the public coupe inventory page.", "Inventory", "Coupes"),
            Public("hatchback_inventory", "Hatchback Inventory", "Open the public hatchback inventory page.", "Inventory", "Hatchbacks"),
            Public("convertible_inventory", "Convertible Inventory", "Open the public convertible inventory page.", "Inventory", "Convertibles"),
            Public("financing_application", "Financing Application", "Open the customer financing application.", "Home", "Application"),
            Public("staff_page", "Staff Page", "Open the public GTX team page.", "Home", "Staff"),
            Public("about_page", "About Us Page", "Open the public About Us page.", "Home", "About"),
            Public("contact_page", "Contact Page", "Open the public contact page.", "Home", "Contact"),
            Public("test_drive_page", "Test-Drive Page", "Open the public test-drive request page.", "Home", "Contact"),
            Public("testimonials", "Testimonials", "Open the customer testimonials page.", "Home", "Testimonials"),
            Public("privacy_policy", "Privacy Policy", "Open the privacy policy.", "Home", "PrivacyPolicy"),
            Public("terms", "Terms and Conditions", "Open the terms and conditions.", "Home", "TermsAndConditions"),
            Public("blog", "Customer Blog", "Open the public customer blog.", "Blogs", "List"),
            Authenticated("majordome_inventory", "Majordome Inventory", "Open the internal Majordome vehicle-management page.", "Majordome", "Inventory"),
            Authenticated("vin_decoder", "VIN Decoder", "Open the internal VIN decoder.", "VinDecoder", "Index"),
            Authenticated("announcements", "Announcement Management", "Open announcement management.", "Announcements", "Index"),
            Authenticated("blog_management", "Blog Management", "Open internal blog management.", "Blogs", "Index"),
            Authenticated("employee_management", "Employee Management", "Open employee management.", "Employees", "Index"),
            Authenticated("health", "System Health", "Open the internal system-health page.", "Health", "Index"),
            Owner("inventory_dashboard", "Inventory Dashboard", "Open the owner inventory dashboard.", "InventoryManagement", "Dashboard"),
            Owner("inventory_management", "Inventory Management", "Open the owner inventory upload and management page.", "InventoryManagement", "Index"),
            Owner("chat_bot_commands", "CB Commands", "Open the owner chatbot command-management page.", "ChatBotCommands", "Index")
        };

        public static IReadOnlyList<ChatBotNavigationDefinition> All => Definitions;

        public static ChatBotNavigationDefinition Find(string actionKey)
        {
            return Definitions.FirstOrDefault(item => string.Equals(
                item.ActionKey,
                actionKey,
                StringComparison.OrdinalIgnoreCase));
        }

        private static ChatBotNavigationDefinition Public(
            string key,
            string label,
            string description,
            string controller,
            string action)
        {
            return Create(key, label, description, controller, action, false, false);
        }

        private static ChatBotNavigationDefinition Authenticated(
            string key,
            string label,
            string description,
            string controller,
            string action)
        {
            return Create(key, label, description, controller, action, true, false);
        }

        private static ChatBotNavigationDefinition Owner(
            string key,
            string label,
            string description,
            string controller,
            string action)
        {
            return Create(key, label, description, controller, action, true, true);
        }

        private static ChatBotNavigationDefinition Create(
            string key,
            string label,
            string description,
            string controller,
            string action,
            bool requiresAuthentication,
            bool ownerOnly)
        {
            return new ChatBotNavigationDefinition
            {
                ActionKey = key,
                Label = label,
                Description = description,
                Controller = controller,
                Action = action,
                RequiresAuthentication = requiresAuthentication,
                OwnerOnly = ownerOnly
            };
        }
    }
}
