using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public static class CommonUnit {

    #region Public Enums

    public enum Alert {

        [Display(Name = "The dish is ready")]
        DishIsReady = 1,

        [Display(Name = "Order is ready")]
        OrderIsReady = 2
    }

    public enum CategoryType {

        [Display(Name = "Menu")]
        Menu = 1,

        [Display(Name = "Inventory")]
        Inventory = 2,

        [Display(Name = "Product")]
        Product = 4
    }

    public enum CheckStatus {

        [Display(Name = "Active")]
        Active = 1,

        [Display(Name = "Ordered")]
        Ordered = 2,

        [Display(Name = "Ready")]
        Ready = 3,

        [Display(Name = "Paid")]
        Paid = 4,

        [Display(Name = "Cancelled")]
        Cancelled = 5
    }

    public enum CheckType {
        Guest = 1,
        ToGo = 2
    }

    public enum CommentType {
        Check = 1,
        MenuItem = 2,
        InventoryRegistry = 4
    }

    public enum InventoryType {
        In = 2,
        Out = 4,
    }

    public enum Languages {

        [Display(Name = "EN")]
        English = 1,

        [Display(Name = "UK")]
        Ukrainian = 2,

        [Display(Name = "RU")]
        Russain = 3
    }

    public enum LogType {

        [Display(Name = "Exception")]
        Exception = 0,

        [Display(Name = "LogIn")]
        LogIn = 1,

        [Display(Name = "LogOut")]
        LogOut = 2,

        [Display(Name = "Get")]
        Get = 3,

        [Display(Name = "Post")]
        Post = 4,

        [Display(Name = "Service")]
        Service = 5,

        [Display(Name = "Add Floor")]
        AddFloor = 6,

        [Display(Name = "Add Menu")]
        AddMenu = 7,

        [Display(Name = "Add Category")]
        AddCategory = 8,

        [Display(Name = "Add Item")]
        AddItem = 9,

        [Display(Name = "Add ItemProduct")]
        AddItemProduct = 10,

        [Display(Name = "Add ItemPrice")]
        AddItemPrice = 12,

        [Display(Name = "Delete Floor")]
        DeleteFloor = 13,

        [Display(Name = "Delete Menu")]
        DeleteMenu = 14,

        [Display(Name = "Delete Category")]
        DeleteCategory = 15,

        [Display(Name = "Delete Item")]
        DeleteItem = 16,

        [Display(Name = "Delete ItemAssociation")]
        DeleteItemAssociation = 17,

        [Display(Name = "Delete ItemPrice")]
        DeleteItemPrice = 18,

        [Display(Name = "Activity")]
        Activity = 36,
    }

    public enum MenuItemStatus {
        Active = 1,
        Ordered = 2
    }

    public enum Modules {
        Print,
        Inventory,
        Reports
    }

    public enum Payments {
        Cash = 0,
        Mastercard = 2,
        Visa = 4,
        Discover = 8,
        Generic = 16
    }

    public enum PrinterWidth {

        [Display(Name = "58mm")]
        Printer58mm = 165,

        [Display(Name = "80mm")]
        Printer80mm = 235
    }

    public enum PrintStatus {
        Printed = 1,
        Queued = 2,
        Cancelled = 3
    }

    public enum PrintType {
        KitchenOrder = 1,
        Check = 2
    }

    public enum ProductType {

        [Display(Name = "Alternatives")]
        Alternatives = 1,

        [Display(Name = "Addons")]
        Addons = 2
    }

    public enum Settings {

        [Display(Name = "Wall Background")]
        WallBackground = 1,

        [Display(Name = "Page Background")]
        PageBackground = 2,

        [Display(Name = "Category Color")]
        CategoryColor = 3,

        [Display(Name = "Category Description Color")]
        CategoryDescriptionColor = 4,

        [Display(Name = "Item Color")]
        ItemColor = 5,

        [Display(Name = "Item Description Color")]
        ItemDescriptionColor = 6,

        [Display(Name = "Price  Color")]
        PriceColor = 7,

        [Display(Name = "Category Font Size")]
        CategoryFontSize = 8,

        [Display(Name = "Category Description FontSize")]
        CategoryDescriptionFontSize = 9,

        [Display(Name = "Item FontSize")]
        ItemFontSize = 10,

        [Display(Name = "Item Description FontSize")]
        ItemDescriptionFontSize = 11,

        [Display(Name = "Price FontSize")]
        PriceFontSize = 12,

        [Display(Name = "Show hidden menu items")]
        ShowHiddenItems = 13,

        [Display(Name = "Kitchen Printer")]
        PrinterKitchen = 14,

        [Display(Name = "POS Printer")]
        PrinterPOS = 15,

        [Display(Name = "Kitchen Printer Width")]
        PrinterKitchenWidth = 16,

        [Display(Name = "POS Printer Width")]
        PrinterPOSWidth = 17,

        [Display(Name = "Language")]
        Language = 18
    }

    public enum SettingsItems {

        [Display(Name = "Category")]
        Category = 1,

        [Display(Name = "Category Description")]
        CategoryDescription = 2,

        [Display(Name = "Item")]
        Item = 3,

        [Display(Name = "Item Description")]
        ItemDescription = 4,

        [Display(Name = "Price")]
        Price = 5
    }

    public enum Status {

        [Display(Name = "Active")]
        Active = 1,

        [Display(Name = "Inctive")]
        NotActive = 0
    }

    public enum TableOrderStatus {

        [Display(Name = "Open")]
        Open = 1,

        [Display(Name = "Served")]
        Served = 2,

        [Display(Name = "Closed")]
        Closed = 3
    }

    public enum UOM {
        Each = 1,
        Lbl = 2,
        Oz = 3,
        Box = 4,
        Pack = 5,
        Liter = 6,
        Gramm = 7
    }

    public enum UserType {

        [Display(Name = "Administrator")]
        Administrator = 1,

        [Display(Name = "Manager")]
        Manager = 2,

        [Display(Name = "Waiter")]
        Waiter = 4
    }

    #endregion Public Enums

    #region Public Methods

    public static String GetIP() {
        String strHostName = "";
        strHostName = System.Net.Dns.GetHostName();
        IPHostEntry ipEntry = System.Net.Dns.GetHostEntry(strHostName);
        IPAddress[] addr = ipEntry.AddressList;
        return addr[addr.Length - 2].ToString();
    }

    #endregion Public Methods
}

public static class EnumHelper<T> {

    #region Public Methods

    public static string GetDisplayValue(T value) {
        var fieldInfo = value.GetType().GetField(value.ToString());

        var descriptionAttributes = fieldInfo.GetCustomAttributes(
            typeof(DisplayAttribute), false) as DisplayAttribute[];

        if (descriptionAttributes == null) return string.Empty;
        return (descriptionAttributes.Length > 0) ? descriptionAttributes[0].Name : value.ToString();
    }

    public static IList<string> GetDisplayValues(Enum value) {
        return GetNames(value).Select(obj => GetDisplayValue(Parse(obj))).ToList();
    }

    public static IList<string> GetNames(Enum value) {
        return value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public).Select(fi => fi.Name).ToList();
    }

    public static IList<T> GetValues(Enum value) {
        var enumValues = new List<T>();

        foreach (FieldInfo fi in value.GetType().GetFields(BindingFlags.Static | BindingFlags.Public)) {
            enumValues.Add((T)Enum.Parse(value.GetType(), fi.Name, false));
        }
        return enumValues;
    }

    public static T Parse(String value) {
        return (T)Enum.Parse(typeof(T), value, true);
    }

    #endregion Public Methods
}