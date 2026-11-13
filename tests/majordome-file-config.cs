using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Web.Helpers;
using System.Web.Security;
using GTX.Common;

internal static class MajordomeFileConfigTests
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        // The executable lives in its own temporary directory, so this cannot read
        // or overwrite the web application's real private credential file.
        Environment.SetEnvironmentVariable("GTX_MAJORDOME_PASSWORD_HASH", null);
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
        var path = Path.Combine(directory, "Majordome.config");
        Directory.CreateDirectory(directory);
        Check(!File.Exists(path), "The isolated credential path must start empty");
        Check(MajordomeAccount.Current() == null, "A missing file must disable the built-in account");

        var firstHash = Crypto.HashPassword("Isolated-File-Credential42!");
        File.WriteAllText(path, "<majordome passwordHash=\"" + firstHash + "\" />");
        var first = MajordomeAccount.Current();
        Check(SiteAccess.IsMajordome(first) && first.PasswordHash == firstHash,
            "The private XML file must supply the administrator credential");
        var request = AccessRequest(first.Id + ":" + first.Revision);
        Check(SiteAccess.IsMajordome(SiteAccess.Current(request)), "A file-backed current cookie must be accepted");

        var secondHash = Crypto.HashPassword("Rotated-File-Credential43!");
        File.WriteAllText(path, "<majordome passwordHash=\"" + secondHash + "\" />");
        var second = MajordomeAccount.Current();
        Check(second != null && second.PasswordHash == secondHash && second.Revision != first.Revision,
            "Editing the private file must change the credential revision immediately");
        Check(SiteAccess.Current(request) == null, "Editing the file must revoke previously issued cookies");

        foreach (var invalid in new[] {
            "<majordome", "<other passwordHash=\"" + secondHash + "\" />",
            "<majordome />", "<majordome passwordHash=\"invalid\" />",
            "<!DOCTYPE majordome [<!ENTITY credential '" + secondHash + "'>]><majordome passwordHash=\"&credential;\" />"
        })
        {
            File.WriteAllText(path, invalid);
            Check(MajordomeAccount.Current() == null, "Malformed, wrong-root, invalid-hash and DTD-bearing XML must fail closed");
        }
        File.WriteAllText(path, "<majordome passwordHash=\"" + secondHash + "\" />");
        Environment.SetEnvironmentVariable("GTX_MAJORDOME_PASSWORD_HASH", "invalid-override");
        Check(MajordomeAccount.Current() == null, "An invalid environment override must not fall back to a valid private file");
        Environment.SetEnvironmentVariable("GTX_MAJORDOME_PASSWORD_HASH", firstHash);
        Check(MajordomeAccount.Current().PasswordHash == firstHash, "The environment must take precedence over the private file");
        Environment.SetEnvironmentVariable("GTX_MAJORDOME_PASSWORD_HASH", null);
        File.Delete(path);
        Check(MajordomeAccount.Current() == null, "Removing the private file must disable the built-in account");
        Console.WriteLine("PASS: isolated private XML credential loading, missing-file denial, live file rotation/cookie revocation, malformed XML/DTD denial and environment precedence");
    }

    private static HttpRequestBase AccessRequest(string ticket)
    {
        return new CookieRequest(Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(ticket), "GTX.SiteRole")));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class CookieRequest : HttpRequestBase
    {
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();
        public CookieRequest(string value) { cookies.Add(new HttpCookie("gtx_access", value)); }
        public override HttpCookieCollection Cookies { get { return cookies; } }
    }
}
