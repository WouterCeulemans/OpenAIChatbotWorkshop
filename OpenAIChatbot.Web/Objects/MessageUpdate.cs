namespace OpenAIChatbot.Web.Objects
{
    public class MessageUpdate
    {
        public string Role { get; set; }

        public string? Text { get; set; }

        public DateTimeOffset CreatedOn { get; set; }
    }
}
