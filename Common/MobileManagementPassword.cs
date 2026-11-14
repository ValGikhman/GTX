using System;
using System.IO;
using System.Xml;

namespace GTX.Common {
    // The mobile credential is independent of website roles and administrator settings.
    public static class MobileManagementPassword {
        public static string Read(string path) {
            try {
                if (!File.Exists(path)) return null;
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                using (var reader = XmlReader.Create(stream, settings)) {
                    var document = new XmlDocument { XmlResolver = null };
                    document.Load(reader);
                    if (document.DocumentElement?.Name != "mobileManagement") return null;
                    var hash = document.DocumentElement.GetAttribute("passwordHash").Trim();
                    var bytes = Convert.FromBase64String(hash);
                    return bytes.Length == 49 && bytes[0] == 0 ? hash : null;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (XmlException) { return null; }
            catch (FormatException) { return null; }
        }
    }
}
