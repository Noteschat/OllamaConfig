using MongoDB.Driver;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OllamaConfig.Managers
{
    public class ConfigManager
    {
        public IMongoCollection<DBConfig> _configs;

        public ConfigManager()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("NotesChat");
            _configs = database.GetCollection<DBConfig>("configs");
        }

        public async Task<Either<List<ConfigAll>, ConfigError>> GetAll(User user)
        {
            IAsyncCursor<DBConfig> result;
            try
            {
                result = await _configs.FindAsync(config => config.Owner == user.Id);
            }
            catch (Exception e)
            {
                Logger.Error("Couldn't retrieve all configs for user. | " + e.Message);
                return new Either<List<ConfigAll>, ConfigError>(ConfigError.NoDatabaseConnection);
            }

            try
            {
                var list = result.ToList().Select(config => new ConfigAll
                {
                    ConfigId = config.Id,
                    Name = config.Name,
                }).ToList();
                return new Either<List<ConfigAll>, ConfigError>(list);
            }
            catch (Exception e)
            {
                Logger.Error("Couldn't convert all configs for user. | " + e.Message);
                return new Either<List<ConfigAll>, ConfigError>(ConfigError.WrongFormatInDatabase);
            }
        }

        public async Task<Either<Config, ConfigError>> CreateNew(User user, NewConfigBody body)
        {
            if (body.Message == null || body.Message.Length == 0 || body.Name == null || body.Name.Length == 0 || body.Model == null || body.Model.Length == 0)
            {
                return new Either<Config, ConfigError>(ConfigError.MissingProperty);
            }

            try
            {
                var password = Guid.NewGuid().ToString();
                var configUser = new
                {
                    name = body.Name,
                    password,
                };
                HttpClient client = new HttpClient();
                var identityResponse = await client.PostAsync("http://localhost/api/identity/user", new StringContent(JsonSerializer.Serialize(configUser),Encoding.UTF8, "application/json"));
                if (!identityResponse.IsSuccessStatusCode)
                {
                    return new Either<Config, ConfigError>(ConfigError.IdentityCreationError);
                }
                var identityContent = JsonSerializer.Deserialize<IdentityUserCreationResponse>(await identityResponse.Content.ReadAsStringAsync());

                var id = Guid.NewGuid().ToString();
                var dbconfig = new DBConfig
                {
                    Owner = user.Id,
                    Id = id,
                    ConfigUserId = identityContent.Id,
                    Model = body.Model,
                    Name = body.Name,
                    Message = body.Message,
                    Password = password
                };
                await _configs.InsertOneAsync(dbconfig);

                var config = new Config
                {
                    ConfigId = id,
                    ConfigUserId = identityContent.Id,
                    Model = body.Model,
                    Name = body.Name,
                    Message = body.Message,
                    Password = password,
                };

                return new Either<Config, ConfigError>(config);
            }
            catch (Exception e)
            {
                Logger.Error("Couldn't establish Database Connection! | " + e.Message);
                return new Either<Config, ConfigError>(ConfigError.NoDatabaseConnection);
            }
        }

        public async Task<Either<Config, ConfigError>> FindOne(User user, string id)
        {

            IAsyncCursor<DBConfig> result;
            try
            {
                result = await _configs.FindAsync(config => config.Id == id && config.Owner == user.Id);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return new Either<Config, ConfigError>(ConfigError.NoDatabaseConnection);
            }

            var list = result.ToList();
            if (list.Count > 0)
            {
                var config = new Config
                {
                    ConfigId = list[0].Id,
                    ConfigUserId = list[0].ConfigUserId,
                    Name = list[0].Name,
                    Password = list[0].Password,
                    Model = list[0].Model,
                    Message = list[0].Message,
                };
                return new Either<Config, ConfigError>(config);
            }
            else
            {
                return new Either<Config, ConfigError>(ConfigError.NotFound);
            }
        }

        public async Task<Either<Config, ConfigError>> ChangeOne(User user, string id, ChangeConfigBody body)
        {
            if ((body.Message == null || body.Message.Length == 0) && (body.Model == null || body.Model.Length == 0))
            {
                return new Either<Config, ConfigError>(ConfigError.MissingProperty);
            }

            IAsyncCursor<DBConfig> result;
            try
            {
                result = await _configs.FindAsync(config => config.Id == id && config.Owner == user.Id);
            }
            catch
            {
                return new Either<Config, ConfigError>(ConfigError.NoDatabaseConnection);
            }

            var list = result.ToList();
            if (list.Count > 0)
            {
                var config = list[0];

                UpdateDefinition<DBConfig> update;
                body.Model = body.Model != null && body.Model.Length > 0 ? body.Model : config.Model;
                body.Message = body.Message != null && body.Message.Length > 0 ? body.Message : config.Message;
                if (body.Model != config.Model)
                {
                    update = Builders<DBConfig>.Update.Set("Model", body.Model);
                    await _configs.UpdateOneAsync(config => config.Id == id && config.Owner == user.Id, update);
                }
                if(body.Message != config.Message)
                {
                    update = Builders<DBConfig>.Update.Set("Message", body.Message);
                    await _configs.UpdateOneAsync(config => config.Id == id && config.Owner == user.Id, update);
                }

                return new Either<Config, ConfigError>(new Config
                {
                    ConfigId = config.Id,
                    ConfigUserId = config.ConfigUserId,
                    Name = config.Name,
                    Password = config.Password,
                    Model = body.Model,
                    Message = body.Message
                });
            }
            else
            {
                return new Either<Config, ConfigError>(ConfigError.NotFound);
            }
        }

        public async Task<ConfigError> DeleteOne(User user, string id)
        {
            try
            {
                var dbresult = (await _configs.FindAsync(config => config.Id == id && config.Owner == user.Id)).ToList();

                if (dbresult.Count <= 0)
                {
                    return ConfigError.NotFound;
                }

                var result = dbresult[0];

                HttpClient client = new HttpClient(new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                });
                
                var loginResult = await client.PostAsync("http://localhost/api/identity/login", new StringContent(JsonSerializer.Serialize(new { name = result.Name, password = result.Password }), Encoding.UTF8, "application/json"));
                if(!loginResult.IsSuccessStatusCode)
                {
                    Logger.Error("StatusCode on Login: " + loginResult.StatusCode);
                    return ConfigError.IdentityDeletionError;
                }

                var identityDeletionResult = await client.DeleteAsync("http://localhost/api/identity/user/current");
                if(!identityDeletionResult.IsSuccessStatusCode)
                {
                    Logger.Error("StatusCode on Deletion: " + identityDeletionResult.StatusCode);
                    return ConfigError.IdentityDeletionError;
                }

                await _configs.DeleteOneAsync(config => config.Id == id && config.Owner == user.Id);
                return ConfigError.None;
            }
            catch (Exception e)
            {
                Logger.Error("Couldn't delete config: " + e.Message);
                return ConfigError.NoDatabaseConnection;
            }
        }

        public async Task SendAll(string Id, string callback)
        {
            var configs = (await _configs.FindAsync(config => true)).ToList().Select(config =>
            {
                return new Config
                {
                    ConfigId = config.Id,
                    ConfigUserId = config.ConfigUserId,
                    Message = config.Message,
                    Model = config.Model,
                    Name = config.Name,
                    Password = config.Password,
                };
            });

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Cookie", "registrationId=" + Id);

            foreach (var config in configs.ToList())
            {
                try
                {
                    await client.PostAsync(callback, new StringContent(JsonSerializer.Serialize(config), Encoding.UTF8, "application/json"));
                }
                catch (Exception e)
                {
                    Logger.Error($"Couldn't send config: " + e.Message);
                }

                await Task.Delay(2000);
            }
        }
    }

    public struct DBConfig
    {
        [JsonPropertyName("owner")]
        public string Owner { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("configUserId")]
        public string ConfigUserId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public struct Config
    {
        [JsonPropertyName("configId")]
        public string ConfigId { get; set; }
        [JsonPropertyName("configUserId")]
        public string ConfigUserId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public struct ConfigAll
    {
        [JsonPropertyName("id")]
        public string ConfigId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public struct NewConfigBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public struct ChangeConfigBody
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public struct IdentityUserCreationResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public enum ConfigError
    {
        None,
        NoDatabaseConnection,
        NotFound,
        WrongFormatInDatabase,
        Unauthorized,
        MissingProperty,
        IdentityCreationError,
        IdentityDeletionError
    }
}
