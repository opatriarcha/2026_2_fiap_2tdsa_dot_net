using MongoDB.Bson;
using TaskMongoApi.Models;
using TaskMongoApi.Repositories;

namespace TaskMongoApi.Services;

/// <summary>
/// Regras de negócio: validação, normalização de tags e controle de CompletedAt.
/// Ids inválidos (que não são ObjectId) são tratados como "não encontrado".
/// </summary>
public class TaskService(ITaskRepository repository) : ITaskService
{
    private const int TitleMaxLength = 200;
    private const int MaxPageSize = 100;

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default) =>
        repository.GetAllAsync(ct);

    public async Task<TaskItem?> GetByIdAsync(string id, CancellationToken ct = default) =>
        IsValidId(id) ? await repository.GetByIdAsync(id, ct) : null;

    public Task<TaskItem> CreateAsync(CreateTaskDto dto, CancellationToken ct = default)
    {
        ValidateTitle(dto.Title);

        var now = DateTime.UtcNow;
        var task = new TaskItem
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Priority = dto.Priority,
            Tags = NormalizeTags(dto.Tags),
            Assignee = dto.Assignee?.Trim(),
            DueDate = dto.DueDate?.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };

        return repository.CreateAsync(task, ct);
    }

    public async Task<bool> UpdateAsync(string id, UpdateTaskDto dto, CancellationToken ct = default)
    {
        ValidateTitle(dto.Title);

        var existing = await GetByIdAsync(id, ct);
        if (existing is null) return false;

        existing.Title = dto.Title.Trim();
        existing.Description = dto.Description?.Trim();
        existing.Priority = dto.Priority;
        existing.Tags = NormalizeTags(dto.Tags);
        existing.Assignee = dto.Assignee?.Trim();
        existing.DueDate = dto.DueDate?.ToUniversalTime();
        existing.UpdatedAt = DateTime.UtcNow;

        if (existing.Status != dto.Status)
        {
            existing.Status = dto.Status;
            existing.CompletedAt = dto.Status == TaskItemStatus.Done ? existing.UpdatedAt : null;
        }

        return await repository.ReplaceAsync(existing, ct);
    }

    public async Task<bool> UpdateStatusAsync(string id, TaskItemStatus status, CancellationToken ct = default) =>
        IsValidId(id) && await repository.UpdateStatusAsync(id, status, ct);

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        IsValidId(id) && await repository.DeleteAsync(id, ct);

    public Task<PagedResult<TaskItem>> SearchAsync(TaskSearchQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1) throw new TaskValidationException("page deve ser maior ou igual a 1.");
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new TaskValidationException($"pageSize deve estar entre 1 e {MaxPageSize}.");
        if (query.DueFrom > query.DueTo)
            throw new TaskValidationException("dueFrom não pode ser maior que dueTo.");
        if (query.CreatedFrom > query.CreatedTo)
            throw new TaskValidationException("createdFrom não pode ser maior que createdTo.");

        return repository.SearchAsync(query, ct);
    }

    public Task<IReadOnlyList<TaskItem>> FullTextSearchAsync(string text, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new TaskValidationException("O parâmetro 'q' é obrigatório.");

        return repository.FullTextSearchAsync(text.Trim(), Math.Clamp(limit, 1, MaxPageSize), ct);
    }

    public Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default) =>
        repository.GetOverdueAsync(ct);

    public Task<IReadOnlyList<TaskItem>> GetDueWithinAsync(int days, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) throw new TaskValidationException("days deve estar entre 1 e 365.");
        return repository.GetDueWithinAsync(days, ct);
    }

    public Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct = default) =>
        repository.CountByStatusAsync(ct);

    public Task<IReadOnlyList<AssigneeStats>> StatsByAssigneeAsync(CancellationToken ct = default) =>
        repository.StatsByAssigneeAsync(ct);

    public Task<IReadOnlyList<TagCount>> TopTagsAsync(int limit, CancellationToken ct = default) =>
        repository.TopTagsAsync(Math.Clamp(limit, 1, MaxPageSize), ct);

    public Task<TaskSummary> GetSummaryAsync(CancellationToken ct = default) =>
        repository.GetSummaryAsync(ct);

    private static bool IsValidId(string id) => ObjectId.TryParse(id, out _);

    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new TaskValidationException("O título é obrigatório.");
        if (title.Trim().Length > TitleMaxLength)
            throw new TaskValidationException($"O título deve ter no máximo {TitleMaxLength} caracteres.");
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags) =>
        tags?.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? [];
}
