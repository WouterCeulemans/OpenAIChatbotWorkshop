#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Azure.AI.OpenAI;
using Microsoft.AspNetCore.SignalR;
using OpenAI.Assistants;
using System.ClientModel;
using OpenAIChatbot.Web.Objects;

namespace OpenAIChatbot.Web.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly string _assistantId;

        public ChatHub(AzureOpenAIClient azureClient, IConfiguration configuration)
        {
            _azureClient = azureClient;
            _assistantId = configuration.GetValue<string>("AzureOpenAI:AssistantId") ?? throw new InvalidOperationException("AssistantId not provided");
        }

        public async Task<string?> SendMessage(string? threadId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            AssistantClient assistantClient = _azureClient.GetAssistantClient();
            if (string.IsNullOrEmpty(threadId))
            {
                var thread = await CreateNewThread(assistantClient, message);
                threadId = thread.Id;
            }
            else
            {
                await AddMessageToThread(assistantClient, threadId, message);
            }

            MessageUpdate? messageUpdate = new();
            AsyncCollectionResult<StreamingUpdate> streamingUpdates = assistantClient.CreateRunStreamingAsync(threadId, _assistantId);
            await foreach (var update in streamingUpdates)
            {
                if (update is MessageStatusUpdate messageStatusUpdate)
                {
                    if (messageStatusUpdate.UpdateKind == StreamingUpdateReason.MessageCreated)
                    {
                        messageUpdate.CreatedOn = messageStatusUpdate.Value.CreatedAt;
                        messageUpdate.Role = nameof(MessageRole.Assistant).ToLowerInvariant();
                    }
                }
                else if (update is MessageContentUpdate messageContentUpdate)
                {
                    messageUpdate.Text += messageContentUpdate.Text;
                    await Clients.Client(Context.ConnectionId).SendAsync("ReceiveMessageUpdate", messageUpdate);
                }
            }

            return threadId;
        }

        private async Task<AssistantThread> CreateNewThread(AssistantClient assistantClient, string message)
        {
            ThreadInitializationMessage initializationMessage = new(MessageRole.User, [message]);
            ThreadCreationOptions options = new()
            {
                InitialMessages = { initializationMessage }
            };

            ClientResult<AssistantThread> result = await assistantClient.CreateThreadAsync(options);
            return result.Value;
        }

        private async Task AddMessageToThread(AssistantClient assistantClient, string threadId, string message)
        {
            ThreadMessage threadMessage = await assistantClient.CreateMessageAsync(threadId, MessageRole.User, [message]);
        }
    }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.