using System;

public static class Constants {

    #region Public Fields

    public const string SESSION = "SessionData";
    public const string SESSION_LOG_HEADER = "SessionLogHeader";
    public const string SESSION_ENVIRONMENT = "SessionENVIRONMENT";
    public const string SESSION_MAJORDOME = "SessionMAJORDOME";


    // Cache keys - you can include tenant/store id etc. if needed
    public const string INVENTORY_CACHE = "GTX:Inventory";
    public const string EMPLOYERS_CACHE = "GTX:Employers";
    public const string OPENHOURS_CACHE = "GTX:OpenHours";
    public const string FILTERS_CACHE = "GTX:Filters";
    public const string CATEGORIES_CACHE = "GTX:Categories";
    public const string PASSWORDS_CACHE = "GTX:Passwords";
    public const string ROLE_CACHE = "GTX:Role";

    #endregion Public Fields
}