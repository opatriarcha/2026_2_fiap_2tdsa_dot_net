using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskMongoApi.Configuration;
using TaskMongoApi.Data;
using TaskMongoApi.Endpoints;
using TaskMongoApi.Repositories;
using TaskMongoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// MongoDB: MongoClient é thread-safe e deve ser singleton.
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(MongoDbSettings.SectionName));
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>()
        .GetDatabase(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.DatabaseName));

// Injeção de dependência
builder.Services.AddSingleton<MongoTaskRepository>();
builder.Services.AddSingleton<ITaskRepository>(sp => sp.GetRequiredService<MongoTaskRepository>());
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddSingleton<DatabaseSeeder>();

// Enums como string no JSON ("Pending" em vez de 0)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await app.Services.GetRequiredService<MongoTaskRepository>().EnsureIndexesAsync();

// Dados de exemplo em qualquer ambiente, menos nos testes (que usam massa própria).
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", async (IMongoDatabase db) =>
{
    await db.RunCommandAsync((Command<MongoDB.Bson.BsonDocument>)"{ ping: 1 }");
    return Results.Ok(new { status = "healthy" });
}).WithTags("Health");

app.MapTaskEndpoints();
app.Run();

public partial class Program { }
