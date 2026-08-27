using System;
using System.Configuration;

public static class ChatBotSettings
{
    public static bool Enabled
    {
        get
        {
            return string.Equals(
                ConfigurationManager.AppSettings["OpenAI:ChatBot"],
                "true",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
