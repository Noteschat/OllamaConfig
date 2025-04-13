using Microsoft.AspNetCore.Mvc;
using OllamaConfig.Managers;
using System.Text.Json;

namespace OllamaConfig.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class RegisterController : Controller
    {
        private readonly RegistrationManager _registrations;

        public RegisterController(RegistrationManager registrations)
        {
            _registrations = registrations;
        }

        [HttpPost]
        public async Task<dynamic> GetNew()
        {
            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            RegistrationBody data;

            try
            {
                data = JsonSerializer.Deserialize<RegistrationBody>(requestBody);
            }
            catch
            {
                data = new RegistrationBody();
            }

            var result = await _registrations.CreateNew(data);
            return result.Match<ActionResult>(
                id => StatusCode(200, new { id = id.Id }),
                err => StatusCode(500, new { cause = "creation failed" })
            );
        }
    }
}
