using OllamaConfig.Managers;

namespace OllamaConfig
{
    public class CommandHandler
    {
        static bool running = true;
        static RegistrationManager manager;
        public static async Task Run(WebApplication app)
        {
            manager = app.Services.GetRequiredService<RegistrationManager>();
            while (running)
            {
                var command = Console.ReadLine();
                if (command == null || command.Length <= 0)
                {
                    continue;
                }
                switch (command.Substring(0, 3))
                {
                    case "ACK":
                        var acceptResult = await manager.AcceptOne(command.Substring(3));
                        Logger.Info(acceptResult == RegistrationError.None ? "Accepted Successfully" : "Denied with Error: " + acceptResult);
                        break;
                    case "END":
                        running = false;
                        break;
                    default:
                        Logger.Warn("Unknown Command");
                        break;
                }
            }
        }
    }
}
