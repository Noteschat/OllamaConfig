using System.Text.Json;
using System.Net;
using OllamaConfig.Managers;

namespace OllamaConfig.Middlewares
{
    public class Authentication
    {
        private readonly RequestDelegate _next;
        private readonly IdentityCache<User> _idCache;
        private readonly RegistrationManager _registrations;

        public Authentication(RequestDelegate next, IdentityCache<User> idCache, RegistrationManager registrations)
        {
            _next = next;
            _idCache = idCache;
            _registrations = registrations;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.Value.EndsWith("/api/register"))
            {
                await _next(context);
                return;
            }

            var sessionId = context.Request.Cookies["sessionId"];
            var registrationId = context.Request.Cookies["registrationId"];
            if (sessionId == null && registrationId == null)
            {
                Logger.Warn("Unauthenticated Connection attempt.");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(JsonSerializer.Serialize(new { cause = "not logged in" }));
                return;
            }

            if (sessionId != null)
            {
                var cacheEntry = _idCache.Get(sessionId);
                if (!cacheEntry.IsSuccess)
                {
                    var cookies = new CookieContainer();
                    cookies.Add(new Uri("http://localhost/"), new Cookie("sessionId", sessionId));
                    var handler = new HttpClientHandler
                    {
                        CookieContainer = cookies
                    };

                    var _httpClient = new HttpClient(handler);
                    var apiResponse = await _httpClient.GetAsync($"http://localhost/api/identity/login/valid");

                    if (!apiResponse.IsSuccessStatusCode)
                    {
                        Logger.Warn("Unknown Session Connection attempt.");

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { cause = "not logged in" }));
                        return;
                    }

                    var jsonResponse = await apiResponse.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(jsonResponse);
                    context.Items["user"] = user;

                    _idCache.Add(sessionId, user);
                }
                else
                {
                    context.Items["user"] = cacheEntry.Success;
                }
            }
            else
            {
                var result = await _registrations.IsAccepted(registrationId);
                if (!result.IsSuccess || !result.Success)
                {
                    Logger.Warn("Unknown or Unaccepted Registration Connection attempt.");

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { cause = "not logged in" }));
                    return;
                }

                context.Items["registration"] = true;
            }
            context.Items["sessionId"] = sessionId;

            // Forward the request to the next middleware
            await _next(context);
        }
    }
}
