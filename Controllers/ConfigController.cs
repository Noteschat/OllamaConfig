using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using OllamaConfig.Managers;

namespace OllamaConfig.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class ConfigController : Controller
    {
        private readonly ConfigManager _configs;
        private readonly RegistrationManager _registrations;

        public ConfigController(ConfigManager configs, RegistrationManager registrations)
        {
            _configs = configs;
            _registrations = registrations;
        }

        [HttpGet]
        public async Task<dynamic> GetAll()
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            var result = await _configs.GetAll(user);
            return result.Match<ActionResult>(
                configs => {
                    var res = configs.Select(config => new ConfigAll
                    {
                        ConfigId = config.ConfigId,
                        Name = config.Name,
                    }).ToList();
                    return StatusCode(200, new { configs = res });
                },
                error =>
                {
                    switch (error)
                    {
                        case ConfigError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        case ConfigError.WrongFormatInDatabase:
                            return StatusCode(500, new { cause = "conversion failed" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpPost]
        public async Task<dynamic> CreateNew()
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            NewConfigBody data;

            try
            {
                data = JsonSerializer.Deserialize<NewConfigBody>(requestBody);
            }
            catch
            {
                data = new NewConfigBody();
            }

            var result = await _configs.CreateNew(user, data);

            return result.Match<ActionResult>(
                config => {
                    Task.Run(() => _registrations.SendNewConfig(config));
                    return StatusCode(200, new { id = config.ConfigId });
                },
                error =>
                {
                    switch (error)
                    {
                        case ConfigError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        case ConfigError.MissingProperty:
                            return StatusCode(400, new { cause = "missing property" });
                        case ConfigError.IdentityCreationError:
                            return StatusCode(400, new { cause = "couldn't create user" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpGet("{id}")]
        public async Task<dynamic> GetSpecific(string id)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            var result = await _configs.FindOne(user, id);

            return result.Match(
                config => StatusCode(200, config),
                error =>
                {
                    switch (error)
                    {
                        case ConfigError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        case ConfigError.NotFound:
                            return StatusCode(404, new { cause = "config not found" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpPut("{id}")]
        public async Task<dynamic> ChangeConfig(string id)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            ChangeConfigBody data;

            try
            {
                data = JsonSerializer.Deserialize<ChangeConfigBody>(requestBody);
            }
            catch
            {
                return StatusCode(400, new { cause = "wrong format" });
            }

            var result = await _configs.ChangeOne(user, id, data);
            return result.Match(
                config => StatusCode(200, config),
                error =>
                {
                    switch (error)
                    {
                        case ConfigError.Unauthorized:
                            return StatusCode(403, new { cause = "not logged in" });
                        case ConfigError.MissingProperty:
                            return StatusCode(400, new { cause = "missing property" });
                        case ConfigError.NotFound:
                            return StatusCode(404, new { cause = "config not found" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpDelete("{id}")]
        public async Task<dynamic> DeleteSpecific(string id)
        {
            if (!HttpContext.Items.TryGetValue("user", out var userObj) ||
            userObj is not User user)
            {
                return StatusCode(500, new { cause = "invalid user" });
            }

            switch (await _configs.DeleteOne(user, id))
            {
                case ConfigError.None:
                    Task.Run(() => _registrations.DeleteByConfig(id));
                    return StatusCode(200);
                case ConfigError.Unauthorized:
                    return StatusCode(403, new { cause = "not logged in" });
                case ConfigError.NotFound:
                    return StatusCode(404, new { cause = "config doesn't exist" });
                case ConfigError.IdentityDeletionError:
                    return StatusCode(500, new { cause = "config-user couldn't be deleted" });
                default:
                    return StatusCode(500, new { cause = "deletion failed" });
            }
        }
    }
}
