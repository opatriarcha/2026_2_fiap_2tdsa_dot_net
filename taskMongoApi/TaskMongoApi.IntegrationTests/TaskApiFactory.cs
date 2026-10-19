using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TaskMongoApi.Models;
using Xunit;

namespace TaskMongoApi.IntegrationTests;

/// <summary>
/// Sobe a API em memória apontando para o MongoDB do docker-compose, mas em um
/// banco com nome único por instância (TaskDb_test_xxx), apagado no DisposeAsync.
/// Assim os testes não tocam no TaskDb e as classes podem rodar em paralelo.
///
/// A connection string pode ser trocada pela variável de ambiente
/// MongoDb__ConnectionString (ex.: em CI).
/// </summary>
public class TaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string DatabaseName { get; } = $"TaskDb_test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" desliga o DatabaseSeeder: cada teste controla a própria massa.
        builder.UseEnvironment("Testing");
        builder.UseSetting("MongoDb:DatabaseName", DatabaseName);
    }

    public IMongoCollection<TaskItem> Tasks =>
        Services.GetRequiredService<IMongoDatabase>().GetCollection<TaskItem>("tasks");

    public virtual Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.GetRequiredService<IMongoClient>().DropDatabaseAsync(DatabaseName);
        await base.DisposeAsync();
    }
}

/// <summary>
/// Factory com uma massa de dados conhecida, usada pelos testes de busca e
/// estatísticas (que só leem). As datas são relativas ao momento da carga.
/// </summary>
public class SeededTaskApiFactory : TaskApiFactory
{
    public override async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        await Tasks.InsertManyAsync(
        [
            Seed("Configurar pipeline de CI", TaskItemStatus.InProgress, TaskItemPriority.High,
                ["devops", "ci"], "ana", now.AddDays(-2), now.AddDays(-10)),
            Seed("Escrever testes de integração da API", TaskItemStatus.Pending, TaskItemPriority.Medium,
                ["testes", "api"], "bruno", now.AddDays(5), now.AddDays(-3)),
            Seed("Modelar coleção no MongoDB", TaskItemStatus.Done, TaskItemPriority.Critical,
                ["mongodb", "api"], "ana", now.AddDays(-5), now.AddDays(-15), completedAt: now.AddDays(-6)),
            Seed("Revisar documentação do Swagger", TaskItemStatus.Pending, TaskItemPriority.Low,
                ["docs"], "carla", now.AddDays(-1), now.AddDays(-7)),
            Seed("Deploy em homologação", TaskItemStatus.Cancelled, TaskItemPriority.Medium,
                ["devops", "docker"], "bruno", now.AddDays(10), now.AddDays(-4)),
            Seed("Tarefa sem responsável", TaskItemStatus.Pending, TaskItemPriority.Low,
                [], null, null, now.AddDays(-1))
        ]);
    }

    private static TaskItem Seed(string title, TaskItemStatus status, TaskItemPriority priority,
        List<string> tags, string? assignee, DateTime? dueDate, DateTime createdAt, DateTime? completedAt = null) =>
        new()
        {
            Title = title,
            Description = $"Descrição de {title.ToLowerInvariant()}",
            Status = status,
            Priority = priority,
            Tags = tags,
            Assignee = assignee,
            DueDate = dueDate,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CompletedAt = completedAt
        };
}
