using banzai_server.Handlers;
using banzai_server.Models;
using banzai_server.Services;
using Microsoft.EntityFrameworkCore;

DotNetEnv.Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

var connectionString = $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB_DATABASE")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB_USERNAME")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")}";

builder.Services.AddDbContext<BanzaiDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<ISessionStore>(_ =>
    new RedisSessionStore(
        Environment.GetEnvironmentVariable("REDIS_HOST") ?? "127.0.0.1",
        int.Parse(Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379")
    ));

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<ChatHandler>();
builder.Services.AddScoped<BanchoHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/", () => "running osu-banzai v0.0.0");

app.MapPost("/", (HttpContext ctx, BanchoHandler handler) => handler.Handle(ctx));

app.Urls.Add("http://0.0.0.0:10000");
app.Run();
