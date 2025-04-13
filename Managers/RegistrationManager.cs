using MongoDB.Driver;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OllamaConfig.Managers
{
    public class RegistrationManager
    {
        public IMongoCollection<DBRegistration> _registrations;
        ConfigManager _configs;

        public RegistrationManager(ConfigManager configs)
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("NotesChat");
            try
            {
                database.DropCollection("configRegistrations");
            }
            catch (Exception e)
            {
                Logger.Warn("Couldn't delete ConfigRegistration Collection | " + e.Message);
            }
            _registrations = database.GetCollection<DBRegistration>("configRegistrations");
            _configs = configs;
        }

        public async Task<Either<Registration, RegistrationError>> CreateNew (RegistrationBody data)
        {
            string id = Guid.NewGuid().ToString();
            DBRegistration registration;
            if (data.Callback != null)
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Cookie", "callbackId=" + data.Callback.Value.Id);

                    var callbackResult = await client.GetAsync(data.Callback.Value.Uri);
                    if(!callbackResult.IsSuccessStatusCode)
                    {
                        return new Either<Registration, RegistrationError>(RegistrationError.CallbackError);
                    }
                }
                catch
                {
                    return new Either<Registration, RegistrationError>(RegistrationError.CallbackError);
                }

                registration = new DBRegistration
                {
                    Id = id,
                    Accepted = false,
                    Callback = data.Callback.Value.Uri,
                };

                Logger.Info("Registration with Callback: " + registration.Callback);
            }
            else
            {
                registration = new DBRegistration
                {
                    Id = id,
                    Accepted = false
                };

                Logger.Info("Registration without Callback");
            }
            
            try
            {
                await _registrations.InsertOneAsync(registration);
                return new Either<Registration, RegistrationError>(new Registration
                {
                    Id = id,
                });
            }
            catch
            {
                return new Either<Registration, RegistrationError>(RegistrationError.NoDatabaseConnection);
            }
        }

        public async Task<RegistrationError> AcceptOne (string id)
        {
            try
            {
                var update = Builders<DBRegistration>.Update.Set("Accepted", true);
                UpdateResult result = await _registrations.UpdateOneAsync(registration => registration.Id == id, update);
                if (result.ModifiedCount < 0)
                {
                    return RegistrationError.NotFound;
                }

                DBRegistration registration = (await _registrations.FindAsync(registration => registration.Id == id)).ToList()[0];

                if (registration.Callback == null || registration.Callback.Length <= 0)
                {
                    return RegistrationError.None;
                }

                Task.Run(() =>
                {
                    _configs.SendAll(registration.Id, registration.Callback);
                });

                return RegistrationError.None;
            }
            catch
            {
                return RegistrationError.NoDatabaseConnection;
            }
        }

        public async Task<Either<bool, RegistrationError>> IsAccepted(string id)
        {
            try
            {
                var result = await _registrations.FindAsync(registration => registration.Id == id);
                var list = result.ToList();
                if(list.Count <= 0)
                {
                    return new Either<bool, RegistrationError>(false);
                }
                return new Either<bool, RegistrationError>(list[0].Accepted);
            }
            catch
            {
                return new Either<bool, RegistrationError>(RegistrationError.NoDatabaseConnection);
            }
        }
    
        public async Task DeleteByConfig(string configId)
        {
            var registrations = await _registrations.FindAsync(registration => registration.Accepted && registration.Callback != null && registration.Callback.Length > 0);
            List<Task> tasks = new List<Task>();
            foreach (var registration in registrations.ToList())
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Add("Cookie", "registrationId=" + registration.Id);
                        await client.DeleteAsync(registration.Callback + "/" + configId);
                    }
                    catch
                    {
                        try
                        {
                            _registrations.DeleteOne(registration.Id);
                        }
                        catch
                        {
                            Logger.Warn("Dead Registration: " + registration.Id);
                        }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }

        public async Task SendNewConfig(Config config)
        {
            var registrations = await _registrations.FindAsync(registration => registration.Accepted && registration.Callback != null && registration.Callback.Length > 0);
            List<Task> tasks = new List<Task>();
            foreach (var registration in registrations.ToList())
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Add("Cookie", "registrationId=" + registration.Id);
                        Logger.Info("Sending to: " + registration.Callback);
                        await client.PostAsync(registration.Callback, new StringContent(JsonSerializer.Serialize(config), Encoding.UTF8, "application/json"));
                    }
                    catch (Exception e1)
                    {
                        Logger.Warn("Error while sending: " + e1.Message);
                        try
                        {
                            _registrations.DeleteOne(db => db.Id == registration.Id);
                        }
                        catch (Exception e2)
                        {
                            Logger.Warn("Dead Registration: " + registration.Id);
                            Logger.Warn("Error: " + e2.Message);
                        }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }
    }

    public struct RegistrationBody
    {
        [JsonPropertyName("callback")]
        public RegistrationBodyCallback? Callback { get; set; }
    }

    public struct RegistrationBodyCallback
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public struct DBRegistration
    {
        public string Id { get; set; }
        public bool Accepted { get; set; }
        public string? Callback { get; set; }
    }

    public struct Registration
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public enum RegistrationError
    {
        None,
        NoDatabaseConnection,
        NotFound,
        CallbackError
    }
}
