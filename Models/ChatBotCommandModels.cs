using GTX.Common;
using System;
using System.Collections.Generic;

namespace GTX.Models
{
    public sealed class ChatBotCommandPageModel
    {
        public IReadOnlyList<ChatBotCommandRowModel> Commands { get; set; } = new List<ChatBotCommandRowModel>();
        public IReadOnlyList<ChatBotNavigationDefinition> Actions { get; set; } = new List<ChatBotNavigationDefinition>();
    }

    public sealed class ChatBotCommandRowModel
    {
        public int Id { get; set; }
        public string Phrase { get; set; }
        public string NormalizedPhrase { get; set; }
        public string ActionKey { get; set; }
        public string ActionLabel { get; set; }
        public string ActionDescription { get; set; }
        public string CreatedByRole { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public sealed class ChatBotCommandRequest
    {
        public int Id { get; set; }
        public string RequestToken { get; set; }
        public string Phrase { get; set; }
        public string ActionKey { get; set; }
    }
}
