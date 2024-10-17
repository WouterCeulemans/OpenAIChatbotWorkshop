namespace OpenAIChatbot.Web.Persistence.Entities
{
    public class Conversation
    {
        public required Guid Id { get; set; }

        public required string ThreadId { get; set; }

        public required string AssistantId { get; set; }

        public required DateTimeOffset CreatedOn { get; set; }

        public string? Title { get; set; }
    }
}
