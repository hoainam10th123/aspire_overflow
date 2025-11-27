using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SearchService.Data;
using SearchService.MessageHandlers;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;
using Typesense.Setup;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.AddServiceDefaults();

// services__typesense__typesense__0 open searchService khi app host chay xem 
var typesenseUri = builder.Configuration["services:typesense:typesense:0"];

var typesenseApiKey = builder.Configuration["typesense-api-key"];

if (string.IsNullOrEmpty(typesenseApiKey))
    throw new InvalidOperationException("typesense api key not found");

if (string.IsNullOrEmpty(typesenseUri))
    throw new InvalidOperationException("typesense uri not found");

var uri = new Uri(typesenseUri);

builder.Services.AddTypesenseClient(options =>
{
    options.ApiKey = typesenseApiKey;
    options.Nodes = new List<Node>
    {
        new Node(uri.Host, uri.Port.ToString(), uri.Scheme)
    };
});

builder.AddRabbitMQClient(connectionName: "rabbitmq");

builder.Services.AddOpenTelemetry().WithTracing(tracing =>
{
    tracing.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(builder.Environment.ApplicationName));
});

builder.Services.AddHostedService<QuestionDeletedHandler>();
builder.Services.AddHostedService<QuestionUpdatedHandler>();
builder.Services.AddHostedService<QuestionCreateHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

app.MapGet("/search", async (string query, ITypesenseClient client) =>
{
    // [aspire]something
    string? tag = null;
    var tagMatch = Regex.Match(query, @"\[(.*?)\]");
    if (tagMatch.Success)
    {
        tag = tagMatch.Groups[1].Value;
        query = query.Replace(tagMatch.Value, "").Trim();
    }

    var searchParams = new SearchParameters(query, "title,content");

    if (!string.IsNullOrWhiteSpace(tag))
    {
        searchParams.FilterBy = $"tags:=[{tag}]";
    }

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense search failed", e.Message);
    }

});

app.MapGet("/search/similar-titles", async (string query, ITypesenseClient client) =>
{
    var searchParams = new SearchParameters(query, "title");

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense search failed", e.Message);
    }
});

using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<ITypesenseClient>();
await SearchInitializer.EnsureIndexExists(client);

app.Run();
