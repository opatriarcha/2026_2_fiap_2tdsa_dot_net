using TaskMongoApi.Models;

namespace TaskMongoApi.Repositories;

public interface ITaskRepository
{
    // CRUD
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default);
    Task<bool> ReplaceAsync(TaskItem task, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(string id, TaskItemStatus status, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    // Buscas aprofundadas
    Task<PagedResult<TaskItem>> SearchAsync(TaskSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> FullTextSearchAsync(string text, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetDueWithinAsync(int days, CancellationToken ct = default);

    // Agregações
    Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssigneeStats>> StatsByAssigneeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TagCount>> TopTagsAsync(int limit, CancellationToken ct = default);
    Task<TaskSummary> GetSummaryAsync(CancellationToken ct = default);
}
