using System.Net;
using System.Net.Http.Json;
using MongoDB.Driver;
using TaskMongoApi.Models;
using Xunit;

namespace TaskMongoApi.IntegrationTests;

/// <summary>
/// Testes do CRUD. O banco é compartilhado entre os testes da classe, então
/// cada teste cria as próprias tarefas e não depende da ordem de execução.
/// </summary>
public class TaskCrudEndpointsTests(TaskApiFactory factory) : IClassFixture<TaskApiFactory>
{
    private const string MissingId = "000000000000000000000000";
    private readonly HttpClient _client = factory.CreateClient();

    // ---------------------------------------------------------------- POST

    [Fact]
    public async Task PostTask_ComDadosValidos_DeveCriarERetornar201()
    {
        var dto = new CreateTaskDto("  Escrever os testes de integração  ", "com xUnit",
            TaskItemPriority.High, ["MongoDB", "testes", "mongodb"], "ana", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var response = await _client.PostAsJsonAsync("/tasks", dto, TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>(TaskApiFactory.Json);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrEmpty(created!.Id));
        Assert.Equal($"/tasks/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Escrever os testes de integração", created.Title);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.Equal(TaskItemPriority.High, created.Priority);
        Assert.Equal(new[] { "mongodb", "testes" }, created.Tags);
        Assert.Null(created.CompletedAt);
    }

    [Fact]
    public async Task PostTask_DevePersistirNoMongo()
    {
        var created = await CreateTaskAsync("Persistida no banco");

        var count = await factory.Tasks.CountDocumentsAsync(t => t.Id == created.Id);

        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostTask_SemTitulo_DeveRetornar400ComMensagem(string title)
    {
        var response = await _client.PostAsJsonAsync("/tasks", new CreateTaskDto(title, null), TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TaskApiFactory.Json);
        Assert.Equal("O título é obrigatório.", error!.Error);
    }

    [Fact]
    public async Task PostTask_ComTituloMaiorQue200_DeveRetornar400()
    {
        var response = await _client.PostAsJsonAsync("/tasks",
            new CreateTaskDto(new string('a', 201), null), TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTask_ComPrioridadeInvalida_DeveRetornar400()
    {
        var response = await _client.PostAsync("/tasks",
            JsonContent.Create(new { title = "x", priority = "Urgente" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------- GET

    [Fact]
    public async Task GetTasks_DeveRetornar200ComTarefaCriada()
    {
        var created = await CreateTaskAsync("Aparece na listagem");

        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/tasks", TaskApiFactory.Json);

        Assert.Contains(tasks!, t => t.Id == created.Id);
    }

    [Fact]
    public async Task GetTaskById_QuandoExiste_DeveRetornar200()
    {
        var created = await CreateTaskAsync("Buscar por id");

        var task = await _client.GetFromJsonAsync<TaskItem>($"/tasks/{created.Id}", TaskApiFactory.Json);

        Assert.Equal(created.Id, task!.Id);
        Assert.Equal("Buscar por id", task.Title);
    }

    [Theory]
    [InlineData(MissingId)]
    [InlineData("id-que-nao-e-objectid")]
    public async Task GetTaskById_QuandoNaoExisteOuIdInvalido_DeveRetornar404(string id)
    {
        var response = await _client.GetAsync($"/tasks/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- PUT

    [Fact]
    public async Task PutTask_ComDadosValidos_DeveAtualizarERetornar204()
    {
        var created = await CreateTaskAsync("Antes");
        var dto = new UpdateTaskDto("Depois", "nova descrição", TaskItemStatus.InProgress,
            TaskItemPriority.Critical, ["api"], "bruno", null);

        var response = await _client.PutAsJsonAsync($"/tasks/{created.Id}", dto, TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updated = await GetTaskAsync(created.Id!);
        Assert.Equal("Depois", updated.Title);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
        Assert.Equal(TaskItemPriority.Critical, updated.Priority);
        Assert.Equal("bruno", updated.Assignee);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task PutTask_ParaDone_DevePreencherCompletedAt()
    {
        var created = await CreateTaskAsync("Concluir via PUT");
        var dto = new UpdateTaskDto("Concluir via PUT", null, TaskItemStatus.Done, TaskItemPriority.Medium, null, null, null);

        await _client.PutAsJsonAsync($"/tasks/{created.Id}", dto, TaskApiFactory.Json);

        Assert.NotNull((await GetTaskAsync(created.Id!)).CompletedAt);
    }

    [Fact]
    public async Task PutTask_QuandoNaoExiste_DeveRetornar404()
    {
        var dto = new UpdateTaskDto("x", null, TaskItemStatus.Pending, TaskItemPriority.Low, null, null, null);

        var response = await _client.PutAsJsonAsync($"/tasks/{MissingId}", dto, TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutTask_SemTitulo_DeveRetornar400()
    {
        var created = await CreateTaskAsync("Título válido");
        var dto = new UpdateTaskDto("", null, TaskItemStatus.Pending, TaskItemPriority.Low, null, null, null);

        var response = await _client.PutAsJsonAsync($"/tasks/{created.Id}", dto, TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------- PATCH status

    [Fact]
    public async Task PatchStatus_ParaDone_DevePreencherCompletedAt_EVoltarParaPendingDeveLimpar()
    {
        var created = await CreateTaskAsync("Mudar status");

        var done = await _client.PatchAsJsonAsync($"/tasks/{created.Id}/status",
            new UpdateStatusDto(TaskItemStatus.Done), TaskApiFactory.Json);
        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        var afterDone = await GetTaskAsync(created.Id!);
        Assert.Equal(TaskItemStatus.Done, afterDone.Status);
        Assert.NotNull(afterDone.CompletedAt);

        await _client.PatchAsJsonAsync($"/tasks/{created.Id}/status",
            new UpdateStatusDto(TaskItemStatus.Pending), TaskApiFactory.Json);
        var afterReopen = await GetTaskAsync(created.Id!);
        Assert.Equal(TaskItemStatus.Pending, afterReopen.Status);
        Assert.Null(afterReopen.CompletedAt);
    }

    [Fact]
    public async Task PatchStatus_QuandoNaoExiste_DeveRetornar404()
    {
        var response = await _client.PatchAsJsonAsync($"/tasks/{MissingId}/status",
            new UpdateStatusDto(TaskItemStatus.Done), TaskApiFactory.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- DELETE

    [Fact]
    public async Task DeleteTask_QuandoExiste_DeveRemoverERetornar204()
    {
        var created = await CreateTaskAsync("Para apagar");

        var response = await _client.DeleteAsync($"/tasks/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/tasks/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task DeleteTask_QuandoNaoExiste_DeveRetornar404()
    {
        var response = await _client.DeleteAsync($"/tasks/{MissingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<TaskItem> CreateTaskAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/tasks", new CreateTaskDto(title, null), TaskApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskItem>(TaskApiFactory.Json))!;
    }

    private async Task<TaskItem> GetTaskAsync(string id) =>
        (await _client.GetFromJsonAsync<TaskItem>($"/tasks/{id}", TaskApiFactory.Json))!;
}
