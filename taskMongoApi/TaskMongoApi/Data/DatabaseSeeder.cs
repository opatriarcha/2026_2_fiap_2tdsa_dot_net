using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskMongoApi.Configuration;
using TaskMongoApi.Models;

namespace TaskMongoApi.Data;

/// <summary>
/// Popula a coleção de tarefas com dados de exemplo na subida da aplicação.
/// Só insere se a coleção estiver vazia, então é seguro rodar a cada start.
/// Não roda no ambiente "Testing" (ver Program.cs): os testes controlam a própria massa.
/// </summary>
public class DatabaseSeeder(IMongoDatabase database, IOptions<MongoDbSettings> settings, ILogger<DatabaseSeeder> logger)
{
    private readonly IMongoCollection<TaskItem> _collection =
        database.GetCollection<TaskItem>(settings.Value.TasksCollectionName);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _collection.Find(FilterDefinition<TaskItem>.Empty).Limit(1).AnyAsync(ct))
        {
            logger.LogInformation("Seed ignorado: a coleção '{Collection}' já possui dados.",
                _collection.CollectionNamespace.CollectionName);
            return;
        }

        var tasks = BuildTasks(DateTime.UtcNow);
        await _collection.InsertManyAsync(tasks, cancellationToken: ct);

        logger.LogInformation("Seed concluído: {Count} tarefas inseridas em '{Collection}'.",
            tasks.Count, _collection.CollectionNamespace.CollectionName);
    }

    /// <summary>
    /// Massa variada para exercitar as buscas: todos os status e prioridades,
    /// tarefas vencidas, vencendo na semana, sem prazo e sem responsável.
    /// Datas relativas ao momento do seed.
    /// </summary>
    private static List<TaskItem> BuildTasks(DateTime now)
    {
        TaskItem NewTask(string title, string description, TaskItemStatus status, TaskItemPriority priority,
            string[] tags, string? assignee, int? dueInDays, int createdDaysAgo) =>
            new()
            {
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                Tags = [.. tags],
                Assignee = assignee,
                DueDate = dueInDays is null ? null : now.AddDays(dueInDays.Value),
                CreatedAt = now.AddDays(-createdDaysAgo),
                UpdatedAt = now.AddDays(-createdDaysAgo / 2),
                CompletedAt = status == TaskItemStatus.Done ? now.AddDays(-createdDaysAgo / 2) : null
            };

        return
        [
            NewTask("Configurar pipeline de CI", "Criar workflow de build e testes no GitHub Actions",
                TaskItemStatus.InProgress, TaskItemPriority.High, ["devops", "ci"], "ana", -2, 10),
            NewTask("Escrever testes de integração da API", "Cobrir endpoints de CRUD e buscas com xUnit",
                TaskItemStatus.Pending, TaskItemPriority.Medium, ["testes", "api"], "bruno", 5, 3),
            NewTask("Modelar coleção de tarefas no MongoDB", "Definir documento, índices e índice de texto",
                TaskItemStatus.Done, TaskItemPriority.Critical, ["mongodb", "api"], "ana", -5, 15),
            NewTask("Revisar documentação do Swagger", "Adicionar descrições e exemplos aos endpoints",
                TaskItemStatus.Pending, TaskItemPriority.Low, ["docs"], "carla", -1, 7),
            NewTask("Deploy em ambiente de homologação", "Publicar a API e o MongoDB em containers",
                TaskItemStatus.Cancelled, TaskItemPriority.Medium, ["devops", "docker"], "bruno", 10, 4),
            NewTask("Implementar autenticação JWT", "Proteger os endpoints de escrita com token JWT",
                TaskItemStatus.Pending, TaskItemPriority.High, ["seguranca", "api"], "diego", 3, 2),
            NewTask("Criar índice composto para relatórios", "Avaliar explain() das agregações por responsável",
                TaskItemStatus.InProgress, TaskItemPriority.Medium, ["mongodb", "performance"], "ana", 2, 5),
            NewTask("Corrigir bug de paginação na busca", "Parâmetros opcionais eram tratados como obrigatórios",
                TaskItemStatus.Done, TaskItemPriority.Critical, ["bug", "api"], "bruno", -3, 6),
            NewTask("Configurar logs estruturados", "Adicionar Serilog com saída em JSON",
                TaskItemStatus.Pending, TaskItemPriority.Low, ["observabilidade"], null, null, 1),
            NewTask("Atualizar dependências do projeto", "Revisar pacotes NuGet desatualizados",
                TaskItemStatus.Pending, TaskItemPriority.Low, ["manutencao"], "carla", 20, 8),
            NewTask("Adicionar health check do MongoDB", "Endpoint /health executando ping no banco",
                TaskItemStatus.Done, TaskItemPriority.Medium, ["devops", "mongodb"], "diego", -8, 12),
            NewTask("Planejar sprint de melhorias", "Levantar débitos técnicos e priorizar backlog",
                TaskItemStatus.Pending, TaskItemPriority.Medium, ["planejamento"], null, -4, 9)
        ];
    }
}
