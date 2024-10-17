using Microsoft.Azure.Cosmos;
using OpenAIChatbot.Web.Persistence.Entities;

namespace OpenAIChatbot.Web.Persistence
{
    public class CosmosService
    {
        private const string DatabaseName = "openai-chatbot";
        private const string ConversationsContainerName = "conversations";
        private const string ConversationsPartitionKeyPath = "/id";

        private readonly CosmosClient _client;
        private Database? _database;
        private Container? _conversations;

        public CosmosService(CosmosClient client)
        {
            _client = client;
        }

        private Database Database
        {
            get
            {
                return _database ??= _client.GetDatabase(DatabaseName);
            }
        }

        private Container Conversations
        {
            get
            {
                return _conversations ??= Database.GetContainer(ConversationsContainerName);
            }
        }

        public async Task EnsureCreated()
        {
            var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            await databaseResponse.Database.CreateContainerIfNotExistsAsync(ConversationsContainerName, ConversationsPartitionKeyPath);
        }

        public async Task<Conversation> GetConversation(Guid id)
        {
            string idString = id.ToString();
            var response = await Conversations.ReadItemAsync<Conversation>(idString, new PartitionKey(idString));
            return response.Resource;
        }

        public Task SaveConversation(Conversation conversation)
        {
            return Conversations.UpsertItemAsync(conversation);
        }
    }
}