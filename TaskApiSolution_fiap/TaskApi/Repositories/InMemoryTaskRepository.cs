using System.Collections.Concurrent;
using TaskApi.Models;

namespace TaskApi.Repositories;

/// <summary>
/// Repositório em memória (thread-safe) usado como armazenamento padrão da API.
/// Registrado como Singleton no container de DI.
/// </summary>
public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    public IEnumerable<TaskItem> GetAll() => _tasks.Values.OrderBy(t => t.CreatedAt);

    public TaskItem? GetById(Guid id) =>
        _tasks.TryGetValue(id, out var task) ? task : null;

    public TaskItem Add(TaskItem task)
    {
        _tasks[task.Id] = task;
        return task;
    }

    public bool Update(TaskItem task)
    {
        if (!_tasks.ContainsKey(task.Id))
            return false;

        _tasks[task.Id] = task;
        return true;
    }

    public bool Delete(Guid id) => _tasks.TryRemove(id, out _);
}
