using TaskMongoApi.Models;

namespace TaskMongoApi.Services;

public class TaskValidationException(string message) : Exception(message);

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<TaskItem> CreateAsync(CreateTaskDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(string id, UpdateTaskDto dto, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(string id, TaskItemStatus status, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    Task<PagedResult<TaskItem>> SearchAsync(TaskSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> FullTextSearchAsync(string text, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetDueWithinAsync(int days, CancellationToken ct = default);

    Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssigneeStats>> StatsByAssigneeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TagCount>> TopTagsAsync(int limit, CancellationToken ct = default);
    Task<TaskSummary> GetSummaryAsync(CancellationToken ct = default);
}
