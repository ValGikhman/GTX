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
            Public("all_inventory", "all inventory", "Open the public All Inventory page.", "Inventory", "All"),
            Public("suv_inventory", "SUV inventory", "Open the public SUV inventory page.", "Inventory", "Suvs"),
            Public("truck_inventory", "truck inventory", "Open the public truck inventory page.", "Inventory", "Trucks"),
            Public("sedan_inventory", "sedan inventory", "Open the public sedan inventory page.", "Inventory", "Sedans"),
            Public("van_inventory", "van inventory", "Open the public van inventory page.", "Inventory", "Vans"),
            Public("wagon_inventory", "wagon inventory", "Open the public wagon inventory page.", "Inventory", "Wagons"),
            Public("coupe_inventory", "coupe inventory", "Open the public coupe inventory page.", "Inventory", "Coupes"),
            Public("hatchback_inventory", "hatchback inventory", "Open the public hatchback inventory page.", "Inventory", "Hatchbacks"),
            Public("convertible_inventory", "convertible inventory", "Open the public convertible inventory page.", "Inventory", "Convertibles"),
            Public("financing_application", "financing application", "Open the customer financing application.", "Home", "Application"),
            Public("staff_page", "staff page", "Open the public GTX team page.", "Home", "Staff"),
            Public("about_page", "About Us page", "Open the public About Us page.", "Home", "About"),
            Public("contact_page", "contact page", "Open the public contact page.", "Home", "Contact"),
            Public("test_drive_page", "test-drive page", "Open the public test-drive request page.", "Home", "Contact"),
            Public("testimonials", "testimonials", "Open the customer testimonials page.", "Home", "Testimonials"),
            Public("privacy_policy", "privacy policy", "Open the privacy policy.", "Home", "PrivacyPolicy"),
            Public("terms", "terms and conditions", "Open the terms and conditions.", "Home", "TermsAndConditions"),
            Public("blog", "customer blog", "Open the public customer blog.", "Blogs", "List"),
            Authenticated("majordome_inventory", "Majordome inventory", "Open the internal Majordome vehicle-management page.", "Majordome", "Inventory"),
            Authenticated("vin_decoder", "VIN decoder", "Open the internal VIN decoder.", "VinDecoder", "Index"),
            Authenticated("announcements", "announcement management", "Open announcement management.", "Announcements", "Index"),
            Authenticated("blog_management", "blog management", "Open internal blog management.", "Blogs", "Index"),
            Authenticated("employee_management", "employee management", "Open employee management.", "Employees", "Index"),
            Authenticated("health", "system health", "Open the internal system-health page.", "Health", "Index"),
            Owner("inventory_dashboard", "inventory dashboard", "Open the owner inventory dashboard.", "InventoryManagement", "Dashboard"),
            Owner("inventory_management", "inventory management", "Open the owner inventory upload and management page.", "InventoryManagement", "Index")
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
