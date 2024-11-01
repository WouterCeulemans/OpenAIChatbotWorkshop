﻿#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Azure.AI.OpenAI;
using Microsoft.AspNetCore.SignalR;
using OpenAI.Assistants;
using System.ClientModel;
using OpenAIChatbot.Web.Objects;
using OpenAIChatbot.Web.Persistence.Entities;
using OpenAIChatbot.Web.Persistence;
using OpenAI.Chat;
using OpenAIChatbot.Web.Services;
using System.Text.Json;

namespace OpenAIChatbot.Web.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly CosmosService _cosmosService;
        private readonly string _assistantId;
        private readonly string _defaultModel;
        private const string GenerateTitlePrompt = """
            Generate a conversation name based on the following messages. Words length should be around 3 to 10.
            The name must be in the same languages as the messages. You only answer with the name of the conversation.
            {0}
        """;

        public ChatHub(AzureOpenAIClient azureClient, CosmosService cosmosService, IConfiguration configuration)
        {
            _azureClient = azureClient;
            _cosmosService = cosmosService;
            _assistantId = configuration.GetValue<string>("AzureOpenAI:AssistantId") ?? throw new InvalidOperationException("AssistantId not provided");
            _defaultModel = configuration.GetValue<string>("AzureOpenAI:DefaultModel") ?? throw new InvalidOperationException("AssistantId not provided");
        }

        public async Task<Conversation?> SendMessage(Guid? conversationId, string message, string[] fileIds)
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
                var thread = await CreateNewThread(assistantClient, message, fileIds);
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
                await AddMessageToThread(assistantClient, conversation.ThreadId, message, fileIds);
            }

            MessageUpdate? messageUpdate = new();
            AsyncCollectionResult<StreamingUpdate> streamingUpdates = assistantClient.CreateRunStreamingAsync(conversation.ThreadId, conversation.AssistantId);
            ThreadRun? currentRun;
            do
            {
                currentRun = null;
                List<ToolOutput> outputsToSubmit = [];
            await foreach (var update in streamingUpdates)
            {
                    if (update is RequiredActionUpdate requiredActionUpdate)
                {
                        if (requiredActionUpdate.FunctionName == "get_weather")
                        {
                            var args = JsonSerializer.Deserialize<WeatherForecastInputData>(requiredActionUpdate.FunctionArguments);
                            string output = string.Empty;
                            if (args is not null)
                            {
                                var weatherForecast = WeatherForecastService.GetWeatherForecast(args.Location);
                                output = JsonSerializer.Serialize(weatherForecast);
                            }

                            var toolOutput = new ToolOutput
                            {
                                Output = output,
                                ToolCallId = requiredActionUpdate.ToolCallId
                            };
                            outputsToSubmit.Add(toolOutput);
                        }
                    }
                    else if (update is RunUpdate runUpdate)
                    {
                        currentRun = runUpdate;
                    if (runUpdate.UpdateKind == StreamingUpdateReason.RunCompleted)
                    {
                            List<Message> messages =
                            [
                            new()
                            {
                                Role = nameof(MessageRole.User),
                                Text = message
                            },
                            new()
                            {
                                Role = nameof(MessageRole.Assistant),
                                Text = messageUpdate.Text ?? string.Empty
                            }
                        ];
                        conversation.Title = await GenerateConversationTitle(messages);
                        await _cosmosService.SaveConversation(conversation);
                    }
                }
                else if (update is MessageStatusUpdate messageStatusUpdate)
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

                if (outputsToSubmit.Count > 0)
                {
                    streamingUpdates = assistantClient.SubmitToolOutputsToRunStreamingAsync(currentRun!.ThreadId, currentRun.Id, outputsToSubmit);
                }
            }
            while (currentRun?.Status.IsTerminal == false);

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

        private async Task<AssistantThread> CreateNewThread(AssistantClient assistantClient, string message, string[] fileIds)
        {
            ThreadInitializationMessage initializationMessage = new(MessageRole.User, [message]);
            AddAttachments(initializationMessage.Attachments, fileIds);
            ThreadCreationOptions options = new()
            {
                InitialMessages = { initializationMessage }
            };

            ClientResult<AssistantThread> result = await assistantClient.CreateThreadAsync(options);
            return result.Value;
        }

        private async Task AddMessageToThread(AssistantClient assistantClient, string threadId, string message, string[] fileIds)
        {
            MessageCreationOptions options = new();
            AddAttachments(options.Attachments, fileIds);
            ThreadMessage threadMessage = await assistantClient.CreateMessageAsync(threadId, MessageRole.User, [message], options);
        }

        private async Task<string?> GenerateConversationTitle(List<Message> messages)
        {
            ChatClient chatClient = _azureClient.GetChatClient(_defaultModel);
            var conversation = string.Join("/n", messages.Select(x => $"{x.Role}: {x.Text}"));
            var prompt = string.Format(GenerateTitlePrompt, conversation);
            var userMessage = ChatMessage.CreateUserMessage(prompt);
            ClientResult<ChatCompletion> result = await chatClient.CompleteChatAsync(userMessage);

            return result.Value.Content.FirstOrDefault()?.Text;
        }

        private static void AddAttachments(IList<MessageCreationAttachment> attachments, string[] fileIds)
        {
            if (fileIds.Length == 0)
            {
                return;
            }

            foreach (var fileId in fileIds)
            {
                attachments.Add(new MessageCreationAttachment(fileId, [ToolDefinition.CreateFileSearch()]));
            }
        }
    }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.