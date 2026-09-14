namespace TaskApi.IntegrationTests;
using System.Net;
using System.Net.Http.Json;
using TaskApi.Models;
using Xunit;

public class TaskEndpointsTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly TaskApiFactory _factory;

    public TaskEndpointsTests()
    {
        _factory = new TaskApiFactory();
        _client = _factory.CreateClient();
    }
    
    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTasks_QuandoNaoExistemTarefas_DeveRetornarStatusCode200_e_lista_vazia()
    {
        var response = await _client.GetAsync("/tasks");
        response.EnsureSuccessStatusCode();
        
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(tasks);
        Assert.Empty(tasks!);
    }

    [Fact]
    public async System.Threading.Tasks.Task PostTask_ComDadosValidos_DeveCriarERetornar_201()
    {
        var text = "Escrever os testes de Integracao";
        var dto = new CreateTaskDto(text);
        var response = await _client.PostAsJsonAsync("/tasks", dto);
        
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(created);
        Assert.Equal(text, created!.Title);
        Assert.False(created.IsDone);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal($"/tasks/{created.Id}", response.Headers.Location?.OriginalString);
        
    }


}