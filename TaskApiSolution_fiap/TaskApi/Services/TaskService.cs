using TaskApi.Models;
using TaskApi.Repositories;

namespace TaskApi.Services;

public class TaskService : ITaskService
{
    private const int TitleMaxLength = 100;
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<TaskItem> GetAll() => _repository.GetAll();

    public TaskItem? GetById(Guid id) => _repository.GetById(id);

    public TaskItem Create(CreateTaskDto dto)
    {
        ValidateTitle(dto.Title);

        var task = new TaskItem
        {
            Title = dto.Title.Trim(),
            IsDone = false
        };

        return _repository.Add(task);
    }

    public bool Update(Guid id, UpdateTaskDto dto)
    {
        ValidateTitle(dto.Title);

        var existing = _repository.GetById(id);
        if (existing is null)
            return false;

        existing.Title = dto.Title.Trim();
        existing.IsDone = dto.IsDone;

        return _repository.Update(existing);
    }

    public bool Delete(Guid id) => _repository.Delete(id);

    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new TaskValidationException("O título da tarefa é obrigatório.");

        if (title.Trim().Length > TitleMaxLength)
            throw new TaskValidationException($"O título não pode exceder {TitleMaxLength} caracteres.");
    }
}
