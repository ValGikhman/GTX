using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GTX.Helpers {
    // Private App_Data storage must be preserved across deployments and shared by all workers.
    // Only token hashes and token-keyed password verifiers are stored, never bearer tokens.
    public sealed class MobileSessionStore {
        private readonly string directory;
        public MobileSessionStore(string directory) { this.directory = directory; }
        private static byte[] TokenBytes(string token) {
            if (string.IsNullOrEmpty(token) || token.Length != 44) return null;
            try {
                var bytes = Convert.FromBase64String(token);
                return bytes.Length == 32 && Convert.ToBase64String(bytes) == token ? bytes : null;
            } catch (FormatException) { return null; }
        }
        private string PathFor(byte[] token) {
            using (var hash = SHA256.Create())
                return Path.Combine(directory, BitConverter.ToString(hash.ComputeHash(token)).Replace("-", "") + ".session");
        }
        private static byte[] Verifier(byte[] token, string password) {
            using (var hmac = new HMACSHA256(token))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes("GTX.MobileSession.v1\0" + password));
        }
        public string Create(string password) {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Owner password is required.", "password");
            var token = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(token);
            Directory.CreateDirectory(directory);
            using (var file = new FileStream(PathFor(token), FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                var verifier = Verifier(token, password);
                file.Write(verifier, 0, verifier.Length);
                file.Flush(true);
            }
            return Convert.ToBase64String(token);
        }
        public bool IsValid(string token, string password) {
            var bytes = TokenBytes(token);
            if (bytes == null || string.IsNullOrWhiteSpace(password)) return false;
            byte[] stored;
            try { stored = File.ReadAllBytes(PathFor(bytes)); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            var expected = Verifier(bytes, password);
            if (stored.Length != expected.Length) return false;
            var difference = 0;
            for (var i = 0; i < expected.Length; i++) difference |= stored[i] ^ expected[i];
            return difference == 0;
        }
        public void Revoke(string token) {
            var bytes = TokenBytes(token);
            if (bytes != null && Directory.Exists(directory)) File.Delete(PathFor(bytes));
        }
    }
}
