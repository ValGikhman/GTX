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

public static class Constants {

    #region Public Fields

    public const String SESSION = "SessionData";
    public const String SESSION_CUSTOMER = "SessionCustomer";
    public const String SESSION_EXCEPTION = "SessionException";
    public const String SESSION_FLOOR = "SessionFloor";
    public const String SESSION_ITEM = "SessionDataItem";
    public const String SESSION_LANGUAGE = "SessionLanguage";
    public const String SESSION_MENU = "SessionMenu";
    public const String SESSION_MODULE_INVENTORY = "SessionModuleInventory";
    public const String SESSION_MODULE_PRINT = "SessionModulePrint";
    public const String SESSION_MODULE_REPORTS = "SessionModuleReports";
    public const String SESSION_PRINTABLE = "SessionPrintable";
    public const String SESSION_PRINTER_KITCHEN_WIDTH = "SessionPrinterKitchenWidth";
    public const String SESSION_PRINTER_POS_WIDTH = "SessionDataPOSWidth";
    public const String SESSION_PRINTERS = "SessionPrinters";
    public const String SESSION_PRODUCTION = "SessionProduction";
    public const String SESSION_ROUTE = "SessionDataRoute";
    public const String SESSION_USER = "SessionUser";

    #endregion Public Fields
}