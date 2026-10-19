using System.Net.Http.Json;
using TaskMongoApi.Models;
using Xunit;

namespace TaskMongoApi.IntegrationTests;

/// <summary>
/// Testes das agregações sobre a massa de dados do SeededTaskApiFactory:
/// 3 Pending, 1 InProgress, 1 Done, 1 Cancelled; 2 vencidas abertas; 1 vencendo na semana.
/// </summary>
public class TaskStatsEndpointsTests(SeededTaskApiFactory factory) : IClassFixture<SeededTaskApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Summary_DeveConsolidarTotais()
    {
        var summary = await GetAsync<TaskSummary>("/tasks/stats/summary");

        Assert.Equal(6, summary.Total);
        Assert.Equal(2, summary.Overdue);
        Assert.Equal(1, summary.DueThisWeek);
        Assert.Equal(16.67, summary.CompletionRate);
        Assert.Equal(6, summary.ByStatus.Sum(s => s.Count));
        Assert.Equal(6, summary.ByPriority.Sum(p => p.Count));
    }

    [Fact]
    public async Task ByStatus_DeveContarPorStatusEmOrdemDecrescente()
    {
        var counts = await GetAsync<List<StatusCount>>("/tasks/stats/by-status");

        Assert.Equal(new StatusCount("Pending", 3), counts[0]);
        Assert.Contains(new StatusCount("InProgress", 1), counts);
        Assert.Contains(new StatusCount("Done", 1), counts);
        Assert.Contains(new StatusCount("Cancelled", 1), counts);
    }

    [Fact]
    public async Task ByAssignee_DeveCalcularIndicadoresPorResponsavel()
    {
        var stats = await GetAsync<List<AssigneeStats>>("/tasks/stats/by-assignee");

        Assert.Contains(new AssigneeStats("ana", Total: 2, Done: 1, Pending: 0, InProgress: 1, Overdue: 1), stats);
        Assert.Contains(new AssigneeStats("bruno", Total: 2, Done: 0, Pending: 1, InProgress: 0, Overdue: 0), stats);
        Assert.Contains(new AssigneeStats("carla", Total: 1, Done: 0, Pending: 1, InProgress: 0, Overdue: 1), stats);
        Assert.Contains(new AssigneeStats("(sem responsável)", Total: 1, Done: 0, Pending: 1, InProgress: 0, Overdue: 0), stats);
    }

    [Fact]
    public async Task Tags_DeveOrdenarPorContagemEDepoisPorNome()
    {
        var tags = await GetAsync<List<TagCount>>("/tasks/stats/tags");

        Assert.Equal(new TagCount("api", 2), tags[0]);
        Assert.Equal(new TagCount("devops", 2), tags[1]);
        Assert.Equal(7, tags.Count);
    }

    [Fact]
    public async Task Tags_DeveRespeitarLimit()
    {
        var tags = await GetAsync<List<TagCount>>("/tasks/stats/tags?limit=3");

        Assert.Equal(3, tags.Count);
    }

    private async Task<T> GetAsync<T>(string url) =>
        (await _client.GetFromJsonAsync<T>(url, TaskApiFactory.Json))!;
}
