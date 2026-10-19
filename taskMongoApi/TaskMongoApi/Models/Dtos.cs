namespace TaskMongoApi.Models;

public record CreateTaskDto(
    string Title,
    string? Description,
    TaskItemPriority Priority = TaskItemPriority.Medium,
    List<string>? Tags = null,
    string? Assignee = null,
    DateTime? DueDate = null);

public record UpdateTaskDto(
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskItemPriority Priority,
    List<string>? Tags,
    string? Assignee,
    DateTime? DueDate);

public record UpdateStatusDto(TaskItemStatus Status);

/// <summary>
/// Filtros da busca avançada (GET /tasks/search). Todos são opcionais e combinados com AND.
/// É um record com construtor porque, com [AsParameters], só os defaults de parâmetros
/// de construtor tornam o parâmetro opcional (inicializadores de propriedade não contam).
/// </summary>
public record TaskSearchQuery(
    string? Title = null,
    TaskItemStatus[]? Status = null,
    TaskItemPriority[]? Priority = null,
    string[]? Tags = null,
    bool MatchAllTags = false,
    string? Assignee = null,
    DateTime? DueFrom = null,
    DateTime? DueTo = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string SortBy = "createdAt",
    bool Desc = true,
    int Page = 1,
    int PageSize = 10);

public record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public record StatusCount(string Status, int Count);

public record AssigneeStats(string Assignee, int Total, int Done, int Pending, int InProgress, int Overdue);

public record TagCount(string Tag, int Count);

public record TaskSummary(
    long Total,
    long Overdue,
    long DueThisWeek,
    double CompletionRate,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<StatusCount> ByPriority);

public record ErrorResponse(string Error);
