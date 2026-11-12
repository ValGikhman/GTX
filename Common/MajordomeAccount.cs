using GTX.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Hosting;
using System.Xml;

namespace GTX.Common {
    // This account is deliberately separate from the editable database roles.
    public static class MajordomeAccount {
        public const int Id = -1;
        public const string Name = "Majordome";
        public const string PasswordHashEnvironmentVariable = "GTX_MAJORDOME_PASSWORD_HASH";

        public static bool IsReservedName(string name) {
            return string.Equals(name?.Trim(), Name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMajordome(SiteRoleModel role) {
            return role != null && role.Id == Id && role.Name == Name;
        }

        public static SiteRoleModel Current() {
            // Read on every request so changing the hash revokes existing logins.
            var hash = ReadPasswordHash();
            if (string.IsNullOrWhiteSpace(hash)) return null;
            try {
                // System.Web.Helpers.Crypto's PBKDF2 format: version, salt, subkey.
                var bytes = Convert.FromBase64String(hash);
                if (bytes.Length != 49 || bytes[0] != 0) return null;
            }
            catch (FormatException) { return null; }

            string revision;
            using (var sha = SHA256.Create())
                revision = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(hash))).Replace("-", "").ToLowerInvariant();
            return new SiteRoleModel {
                Id = Id, Name = Name, PasswordHash = hash, Revision = revision,
                Permissions = MajordomeMenu.Defaults(Name)
            };
        }

        private static string ReadPasswordHash() {
            var configured = Environment.GetEnvironmentVariable(PasswordHashEnvironmentVariable);
            if (configured != null) return configured.Trim();
            var path = HostingEnvironment.MapPath("~/App_Data/Majordome.config")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Majordome.config");
            try {
                if (!File.Exists(path)) return null;
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                using (var reader = XmlReader.Create(stream, settings)) {
                    var document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                    return document.DocumentElement?.Name == "majordome"
                        ? document.DocumentElement.GetAttribute("passwordHash").Trim() : null;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (XmlException) { return null; }
        }
    }
}
