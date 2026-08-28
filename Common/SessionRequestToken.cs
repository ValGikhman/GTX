using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace GTX.Common
{
    public static class SessionRequestToken
    {
        private const string SessionKey = "GTX:ChatRequestToken";

        public static string GetOrCreate(HttpSessionStateBase session)
        {
            if (session == null) return string.Empty;

            var token = session[SessionKey] as string;
            if (!string.IsNullOrWhiteSpace(token)) return token;

            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            token = Convert.ToBase64String(bytes);
            session[SessionKey] = token;
            return token;
        }

        public static bool IsValid(HttpSessionStateBase session, string suppliedToken)
        {
            var expectedToken = session == null ? null : session[SessionKey] as string;
            if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(suppliedToken)) return false;

            var expected = Encoding.UTF8.GetBytes(expectedToken);
            var supplied = Encoding.UTF8.GetBytes(suppliedToken);
            if (expected.Length != supplied.Length) return false;

            var difference = 0;
            for (var index = 0; index < expected.Length; index++)
            {
                difference |= expected[index] ^ supplied[index];
            }

            return difference == 0;
        }
    }
}
