using System;

public static class Constants {

    #region Public Fields

    public const string SESSION = "SessionData";
    public const string SESSION_LOG_HEADER = "SessionLogHeader";
    public const string SESSION_ENVIRONMENT = "SessionENVIRONMENT";
    public const string SESSION_RESPONSIBILITY = "SessionRESPONSIBILITY";
    public const string SESSION_MAJORDOME = "SessionMAJORDOME";

    public const int DOCUMENTARY_FEE = 398;


    // Cache keys - you can include tenant/store id etc. if needed
    public const string INVENTORY_CACHE = "GTX:Inventory";
    public const string CHAT_INVENTORY_CACHE = "GTX:Chat:PublicInventory";
    public const string DATAONE_INVENTORY_CACHE = CHAT_INVENTORY_CACHE + ":DataOne:FullSnapshot";
    public const string MAJORDOME_INVENTORY_CACHE = "GTX:MajordomeInventory";
    public const string MAJORDOME_DASHBOARD_CACHE = "GTX:MajordomeDashboard:7";
    public const string INVENTORY_MANAGEMENT_LOGS_CACHE_PREFIX = "GTX:InventoryManagement:Logs:";
    public const string INVENTORY_MANAGEMENT_VEHICLES_CACHE_PREFIX = "GTX:InventoryManagement:Vehicles:";
    public const string INVENTORY_MANAGEMENT_DASHBOARD_CACHE_PREFIX = "GTX:InventoryManagement:Dashboard:";
    public const string INVENTORY_MANAGEMENT_HISTORY_CACHE_PREFIX = "GTX:InventoryManagement:History:";
    public const string EMPLOYERS_CACHE = "GTX:Employers";
    public const string OPENHOURS_CACHE = "GTX:OpenHours";
    public const string FILTERS_CACHE = "GTX:Filters";
    public const string CATEGORIES_CACHE = "GTX:Categories";
    public const string PASSWORDS_CACHE = "GTX:Passwords";
    public const string ROLE_CACHE = "GTX:Role";

    #endregion Public Fields
}
