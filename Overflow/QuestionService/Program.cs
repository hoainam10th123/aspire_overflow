using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using QuestionService.Data;
using QuestionService.services;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.AddServiceDefaults();// dong nay

//Nó giúp service này hiểu và xác thực token phát hành từ Keycloak
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "overflow", opt =>
    {
        opt.Audience = "overflow";
        opt.RequireHttpsMetadata = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuers =
                    [
                        "http://localhost:6001/realms/overflow",
                        "http://keycloak/realms/overflow",
                        "http://keycloak:8080/realms/overflow",
                        "http://id.overflow.local/realms/overflow",
                        "https://id.overflow.local/realms/overflow",
                        "https://overflow-id.trycatchlearn.com/realms/overflow",
                    ],
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.AddNpgsqlDbContext<QuestionDbContext>("questiondb");

var retryPolicy = Policy.Handle<BrokerUnreachableException>()
    .Or<SocketException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        (exception, timespan, retryCount) =>
        {
            Console.WriteLine($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timespan} before next retry.");
        }
    );

await retryPolicy.ExecuteAsync(async () =>
{
    var enpoint = builder.Configuration.GetConnectionString("rabbitmq")
    ?? throw new InvalidOperationException("RabbitMQ connection string is not configured.");
    var factory = new RabbitMQ.Client.ConnectionFactory()
    {
        Uri = new Uri(enpoint)
    };
    using var connection = factory.CreateConnection();
});

builder.AddRabbitMQClient(connectionName: "rabbitmq");

builder.Services.AddOpenTelemetry().WithTracing(tracing =>
{
    tracing.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(builder.Environment.ApplicationName));
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints(); // endpoint = alive, health

using var scope = app.Services.CreateScope();

try
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
    await dbContext.Database.MigrateAsync();
}
catch (Exception ex)
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
}


app.Run();
