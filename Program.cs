using OllamaConfig;
using OllamaConfig.Managers;
using OllamaConfig.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddSingleton<RegistrationManager>();
builder.Services.AddSingleton<IdentityCache<User>>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.UseMiddleware<Authentication>();

List<Task> tasks = new List<Task>
{
    Task.Run(app.Run),
    Task.Run(() => CommandHandler.Run(app)),
};

Task.WaitAll(tasks.ToArray());
