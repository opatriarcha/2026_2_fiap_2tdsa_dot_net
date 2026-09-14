using TaskApi.Models;

namespace TaskApi.Repositories;

public interface ITaskRepository
{
    IEnumerable<TaskItem> GetAll();
    TaskItem? GetById(Guid id);
    TaskItem Add(TaskItem task);
    bool Update(TaskItem task);
    bool Delete(Guid id);
}
