#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Azure.AI.OpenAI;
using Microsoft.AspNetCore.SignalR;
using OpenAI.Assistants;
using System.ClientModel;
using OpenAIChatbot.Web.Objects;
using OpenAIChatbot.Web.Persistence.Entities;
using OpenAIChatbot.Web.Persistence;

namespace OpenAIChatbot.Web.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly CosmosService _cosmosService;
        private readonly string _assistantId;

        public ChatHub(AzureOpenAIClient azureClient, CosmosService cosmosService, IConfiguration configuration)
        {
            _azureClient = azureClient;
            _cosmosService = cosmosService;
            _assistantId = configuration.GetValue<string>("AzureOpenAI:AssistantId") ?? throw new InvalidOperationException("AssistantId not provided");
        }

        public async Task<Conversation?> SendMessage(Guid? conversationId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            Conversation? conversation = null;
            if (conversationId.HasValue)
            {
                conversation = await _cosmosService.GetConversation(conversationId.Value);
            }

            AssistantClient assistantClient = _azureClient.GetAssistantClient();
            if (conversation is null)
            {
                var thread = await CreateNewThread(assistantClient, message);
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    ThreadId = thread.Id,
                    AssistantId = _assistantId,
                    CreatedOn = thread.CreatedAt
                };

                await _cosmosService.SaveConversation(conversation);
            }
            else
            {
                await AddMessageToThread(assistantClient, conversation.ThreadId, message);
            }

            MessageUpdate? messageUpdate = new();
            AsyncCollectionResult<StreamingUpdate> streamingUpdates = assistantClient.CreateRunStreamingAsync(conversation.ThreadId, conversation.AssistantId);
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

            return conversation;
        }

        public async Task<Conversation[]> GetConversations()
        {
            return (await _cosmosService.GetConversations()).OrderByDescending(x => x.CreatedOn).ToArray();
        }

        public async Task<Message[]> GetConversationMessages(Guid conversationId)
        {
            var conversation = await _cosmosService.GetConversation(conversationId);
            AssistantClient assistantClient = _azureClient.GetAssistantClient();
            MessageCollectionOptions options = new() { Order = MessageCollectionOrder.Ascending };
            AsyncCollectionResult<ThreadMessage> threadMessages = assistantClient.GetMessagesAsync(conversation.ThreadId, options);
            List<Message> messages = [];
            await foreach (var message in threadMessages)
            {
                var messageText = string.Empty;
                foreach (var content in message.Content)
                {
                    messageText += content.Text;
                }

                messages.Add(new Message
                {
                    Role = message.Role.ToString().ToLowerInvariant(),
                    Text = messageText
                });
            }

            return [.. messages];
        }

        public async Task<bool> DeleteConversation(Guid conversationId)
        {
            var conversation = await _cosmosService.GetConversation(conversationId);
            AssistantClient assistantClient = _azureClient.GetAssistantClient();
            var result = await assistantClient.DeleteThreadAsync(conversation.ThreadId);
            if (!result.Value.Deleted)
            {
                return false;
            }

            await _cosmosService.DeleteConversation(conversationId);

            return true;
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