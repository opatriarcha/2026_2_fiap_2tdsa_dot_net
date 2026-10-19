using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskMongoApi.Models;

public enum TaskItemStatus
{
    Pending,
    InProgress,
    Done,
    Cancelled
}

public enum TaskItemPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Documento armazenado na coleção "tasks". O Id é um ObjectId no Mongo,
/// exposto como string na API. Os enums são gravados como string para
/// deixar os documentos legíveis e as consultas mais simples.
/// </summary>
public class TaskItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    [BsonRepresentation(BsonType.String)]
    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;

    public List<string> Tags { get; set; } = [];
    public string? Assignee { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
