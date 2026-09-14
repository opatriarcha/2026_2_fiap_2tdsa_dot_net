using TaskApi.Models;

namespace TaskApi.Services;

public interface ITaskService
{
    IEnumerable<TaskItem> GetAll();
    TaskItem? GetById(Guid id);
    TaskItem Create(CreateTaskDto dto);
    bool Update(Guid id, UpdateTaskDto dto);
    bool Delete(Guid id);
}

/// <summary>
/// Lançada quando os dados de entrada violam alguma regra de negócio.
/// </summary>
public class TaskValidationException : Exception
{
    public TaskValidationException(string message) : base(message) { }
}
