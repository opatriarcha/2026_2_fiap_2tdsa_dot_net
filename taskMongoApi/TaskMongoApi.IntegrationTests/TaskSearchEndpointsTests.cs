using System.Net;
using System.Net.Http.Json;
using TaskMongoApi.Models;
using Xunit;

namespace TaskMongoApi.IntegrationTests;

/// <summary>
/// Testes das buscas sobre a massa de dados do SeededTaskApiFactory (6 tarefas).
/// Os testes só leem, então podem compartilhar o mesmo banco.
/// </summary>
public class TaskSearchEndpointsTests(SeededTaskApiFactory factory) : IClassFixture<SeededTaskApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---------------------------------------------------------------- /tasks/search

    [Fact]
    public async Task Search_SemFiltros_DeveRetornarTodasPaginadasComDefaults()
    {
        var result = await SearchAsync("");

        Assert.Equal(6, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(6, result.Items.Count);
    }

    [Fact]
    public async Task Search_ComPaginacao_DeveRetornarPaginaCorreta()
    {
        var result = await SearchAsync("?pageSize=2&page=3");

        Assert.Equal(6, result.Total);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Search_PaginaAlemDoFim_DeveRetornarListaVazia()
    {
        var result = await SearchAsync("?pageSize=2&page=99");

        Assert.Equal(6, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_PorTitulo_DeveIgnorarMaiusculas()
    {
        var result = await SearchAsync("?title=MONGODB");

        var task = Assert.Single(result.Items);
        Assert.Equal("Modelar coleção no MongoDB", task.Title);
    }

    [Fact]
    public async Task Search_PorTituloComCaracteresDeRegex_DeveTratarComoTextoLiteral()
    {
        var result = await SearchAsync("?title=" + Uri.EscapeDataString("(.*"));

        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Search_PorVariosStatus_DeveCombinarComOr()
    {
        var result = await SearchAsync("?status=Pending&status=InProgress");

        Assert.Equal(4, result.Total);
        Assert.All(result.Items, t => Assert.Contains(t.Status, new[] { TaskItemStatus.Pending, TaskItemStatus.InProgress }));
    }

    [Fact]
    public async Task Search_PorPrioridade_DeveFiltrar()
    {
        var result = await SearchAsync("?priority=Critical");

        Assert.Equal(TaskItemPriority.Critical, Assert.Single(result.Items).Priority);
    }

    [Fact]
    public async Task Search_PorTags_DeveRetornarTarefasComQualquerUmaDasTags()
    {
        var result = await SearchAsync("?tags=devops&tags=docs");

        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task Search_PorTagsComMatchAllTags_DeveExigirTodasAsTags()
    {
        var result = await SearchAsync("?tags=devops&tags=ci&matchAllTags=true");

        Assert.Equal("Configurar pipeline de CI", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Search_PorResponsavel_DeveSerExatoEIgnorarMaiusculas()
    {
        var exact = await SearchAsync("?assignee=ANA");
        var partial = await SearchAsync("?assignee=an");

        Assert.Equal(2, exact.Total);
        Assert.All(exact.Items, t => Assert.Equal("ana", t.Assignee));
        Assert.Equal(0, partial.Total);
    }

    [Fact]
    public async Task Search_PorIntervaloDePrazo_DeveFiltrar()
    {
        var from = DateTime.UtcNow.ToString("O");
        var to = DateTime.UtcNow.AddDays(7).ToString("O");

        var result = await SearchAsync($"?dueFrom={Uri.EscapeDataString(from)}&dueTo={Uri.EscapeDataString(to)}");

        Assert.Equal("Escrever testes de integração da API", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Search_ComFiltrosCombinados_DeveAplicarAnd()
    {
        var result = await SearchAsync("?assignee=bruno&status=Pending");

        Assert.Equal("Escrever testes de integração da API", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Search_OrdenadoPorTituloAsc_DeveRetornarEmOrdem()
    {
        var result = await SearchAsync("?sortBy=title&desc=false");

        var titles = result.Items.Select(t => t.Title).ToList();
        Assert.Equal(titles.Order(StringComparer.Ordinal), titles);
    }

    [Fact]
    public async Task Search_OrdenadoPorCriacaoDesc_DeveTrazerMaisRecentePrimeiro()
    {
        var result = await SearchAsync("");

        Assert.Equal("Tarefa sem responsável", result.Items[0].Title);
        Assert.Equal("Modelar coleção no MongoDB", result.Items[^1].Title);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?dueFrom=2030-01-01&dueTo=2020-01-01")]
    [InlineData("?createdFrom=2030-01-01&createdTo=2020-01-01")]
    public async Task Search_ComParametrosInvalidos_DeveRetornar400ComMensagem(string query)
    {
        var response = await _client.GetAsync("/tasks/search" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(TaskApiFactory.Json);
        Assert.False(string.IsNullOrWhiteSpace(error!.Error));
    }

    // ---------------------------------------------------------------- /tasks/text

    [Theory]
    [InlineData("testes", "Escrever testes de integração da API")]
    [InlineData("swagger", "Revisar documentação do Swagger")]
    public async Task FullTextSearch_DeveEncontrarPorPalavra(string q, string expectedTitle)
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>($"/tasks/text?q={q}", TaskApiFactory.Json);

        Assert.Equal(expectedTitle, tasks![0].Title);
    }

    [Fact]
    public async Task FullTextSearch_DeveRespeitarLimit()
    {
        // "descrição" aparece na descrição de todas as tarefas do seed
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/tasks/text?q=descrição&limit=2", TaskApiFactory.Json);

        Assert.Equal(2, tasks!.Count);
    }

    [Fact]
    public async Task FullTextSearch_SemQ_DeveRetornar400()
    {
        var response = await _client.GetAsync("/tasks/text");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------- /tasks/overdue e /tasks/due-soon

    [Fact]
    public async Task Overdue_DeveRetornarSoAbertasVencidasOrdenadasPorPrazo()
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/tasks/overdue", TaskApiFactory.Json);

        // A tarefa Done vencida ("Modelar coleção") não entra
        Assert.Equal(new[] { "Configurar pipeline de CI", "Revisar documentação do Swagger" }, tasks!.Select(t => t.Title));
    }

    [Fact]
    public async Task DueSoon_DeveRetornarSoAbertasDentroDoPeriodo()
    {
        var week = await _client.GetFromJsonAsync<List<TaskItem>>("/tasks/due-soon?days=7", TaskApiFactory.Json);
        var fifteenDays = await _client.GetFromJsonAsync<List<TaskItem>>("/tasks/due-soon?days=15", TaskApiFactory.Json);

        Assert.Equal("Escrever testes de integração da API", Assert.Single(week!).Title);
        // O deploy vence em 10 dias, mas está Cancelled
        Assert.Single(fifteenDays!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task DueSoon_ComDiasForaDoIntervalo_DeveRetornar400(int days)
    {
        var response = await _client.GetAsync($"/tasks/due-soon?days={days}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<PagedResult<TaskItem>> SearchAsync(string query) =>
        (await _client.GetFromJsonAsync<PagedResult<TaskItem>>("/tasks/search" + query, TaskApiFactory.Json))!;
}
