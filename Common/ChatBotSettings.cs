public static class ChatBotSettings
{
    public static bool Enabled
    {
        get
        {
            return GTX.Common.ServiceConfiguration.Enabled("OpenAI:ChatBot");
        }
    }
}
