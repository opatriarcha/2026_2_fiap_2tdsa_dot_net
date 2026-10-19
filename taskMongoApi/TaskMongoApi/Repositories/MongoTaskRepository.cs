using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TaskMongoApi.Configuration;
using TaskMongoApi.Models;

namespace TaskMongoApi.Repositories;

public class MongoTaskRepository : ITaskRepository
{
    private static readonly string[] OpenStatuses =
        [nameof(TaskItemStatus.Pending), nameof(TaskItemStatus.InProgress)];

    private readonly IMongoCollection<TaskItem> _collection;
    private readonly FilterDefinitionBuilder<TaskItem> _filter = Builders<TaskItem>.Filter;

    public MongoTaskRepository(IMongoDatabase database, IOptions<MongoDbSettings> settings)
    {
        _collection = database.GetCollection<TaskItem>(settings.Value.TasksCollectionName);
    }

    // ---------------------------------------------------------------- índices

    /// <summary>
    /// Cria os índices usados pelas buscas. CreateMany é idempotente, então
    /// pode ser chamado em toda subida da aplicação.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var keys = Builders<TaskItem>.IndexKeys;
        var indexes = new[]
        {
            new CreateIndexModel<TaskItem>(
                keys.Ascending(t => t.Status).Ascending(t => t.Priority),
                new CreateIndexOptions { Name = "ix_status_priority" }),
            new CreateIndexModel<TaskItem>(
                keys.Ascending(t => t.Assignee),
                new CreateIndexOptions { Name = "ix_assignee" }),
            new CreateIndexModel<TaskItem>(
                keys.Ascending(t => t.Tags),
                new CreateIndexOptions { Name = "ix_tags" }),
            new CreateIndexModel<TaskItem>(
                keys.Ascending(t => t.DueDate),
                new CreateIndexOptions { Name = "ix_due_date" }),
            new CreateIndexModel<TaskItem>(
                keys.Text(t => t.Title).Text(t => t.Description),
                new CreateIndexOptions
                {
                    Name = "ix_text_title_description",
                    DefaultLanguage = "portuguese",
                    Weights = new BsonDocument { { "Title", 3 }, { "Description", 1 } }
                })
        };

        await _collection.Indexes.CreateManyAsync(indexes, ct);
    }

    // ---------------------------------------------------------------- CRUD

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken ct = default) =>
        await _collection.Find(_filter.Empty)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<TaskItem?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await _collection.Find(t => t.Id == id).FirstOrDefaultAsync(ct);

    public async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(task, cancellationToken: ct);
        return task;
    }

    public async Task<bool> ReplaceAsync(TaskItem task, CancellationToken ct = default)
    {
        var result = await _collection.ReplaceOneAsync(t => t.Id == task.Id, task, cancellationToken: ct);
        return result.MatchedCount > 0;
    }

    public async Task<bool> UpdateStatusAsync(string id, TaskItemStatus status, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var update = Builders<TaskItem>.Update
            .Set(t => t.Status, status)
            .Set(t => t.UpdatedAt, now)
            .Set(t => t.CompletedAt, status == TaskItemStatus.Done ? now : null);

        var result = await _collection.UpdateOneAsync(t => t.Id == id, update, cancellationToken: ct);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var result = await _collection.DeleteOneAsync(t => t.Id == id, ct);
        return result.DeletedCount > 0;
    }

    // ---------------------------------------------------------------- buscas

    public async Task<PagedResult<TaskItem>> SearchAsync(TaskSearchQuery query, CancellationToken ct = default)
    {
        var filter = BuildSearchFilter(query);
        var sort = BuildSort(query.SortBy, query.Desc);

        var totalTask = _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = _collection.Find(filter)
            .Sort(sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(ct);

        await Task.WhenAll(totalTask, itemsTask);
        return new PagedResult<TaskItem>(itemsTask.Result, totalTask.Result, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<TaskItem>> FullTextSearchAsync(string text, int limit, CancellationToken ct = default)
    {
        // $text usa o índice de texto; ordena pela relevância (textScore).
        var projection = Builders<TaskItem>.Projection.MetaTextScore("Score");
        var sort = Builders<TaskItem>.Sort.MetaTextScore("Score");

        var docs = await _collection.Find(_filter.Text(text))
            .Project<BsonDocument>(projection)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);

        return docs.Select(d =>
        {
            d.Remove("Score");
            return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<TaskItem>(d);
        }).ToList();
    }

    public async Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default)
    {
        var filter = _filter.Lt(t => t.DueDate, DateTime.UtcNow)
                     & _filter.In("Status", OpenStatuses);

        return await _collection.Find(filter).SortBy(t => t.DueDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetDueWithinAsync(int days, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var filter = _filter.Gte(t => t.DueDate, now)
                     & _filter.Lte(t => t.DueDate, now.AddDays(days))
                     & _filter.In("Status", OpenStatuses);

        return await _collection.Find(filter).SortBy(t => t.DueDate).ToListAsync(ct);
    }

    // ---------------------------------------------------------------- agregações

    public async Task<IReadOnlyList<StatusCount>> CountByStatusAsync(CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument { { "_id", "$Status" }, { "count", new BsonDocument("$sum", 1) } }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        var docs = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return docs.Select(d => new StatusCount(d["_id"].AsString, d["count"].AsInt32)).ToList();
    }

    public async Task<IReadOnlyList<AssigneeStats>> StatsByAssigneeAsync(CancellationToken ct = default)
    {
        BsonDocument CountWhen(BsonDocument condition) =>
            new("$sum", new BsonDocument("$cond", new BsonArray { condition, 1, 0 }));

        BsonDocument StatusEq(TaskItemStatus status) =>
            new("$eq", new BsonArray { "$Status", status.ToString() });

        var overdueCondition = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$ne", new BsonArray { "$DueDate", BsonNull.Value }),
            new BsonDocument("$lt", new BsonArray { "$DueDate", DateTime.UtcNow }),
            new BsonDocument("$in", new BsonArray { "$Status", new BsonArray(OpenStatuses) })
        });

        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$ifNull", new BsonArray { "$Assignee", "(sem responsável)" }) },
                { "total", new BsonDocument("$sum", 1) },
                { "done", CountWhen(StatusEq(TaskItemStatus.Done)) },
                { "pending", CountWhen(StatusEq(TaskItemStatus.Pending)) },
                { "inProgress", CountWhen(StatusEq(TaskItemStatus.InProgress)) },
                { "overdue", CountWhen(overdueCondition) }
            }),
            new BsonDocument("$sort", new BsonDocument("total", -1))
        };

        var docs = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return docs.Select(d => new AssigneeStats(
            d["_id"].AsString,
            d["total"].AsInt32,
            d["done"].AsInt32,
            d["pending"].AsInt32,
            d["inProgress"].AsInt32,
            d["overdue"].AsInt32)).ToList();
    }

    public async Task<IReadOnlyList<TagCount>> TopTagsAsync(int limit, CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$unwind", "$Tags"),
            new BsonDocument("$group", new BsonDocument { { "_id", "$Tags" }, { "count", new BsonDocument("$sum", 1) } }),
            new BsonDocument("$sort", new BsonDocument { { "count", -1 }, { "_id", 1 } }),
            new BsonDocument("$limit", limit)
        };

        var docs = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);
        return docs.Select(d => new TagCount(d["_id"].AsString, d["count"].AsInt32)).ToList();
    }

    public async Task<TaskSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var openFilter = new BsonDocument("Status", new BsonDocument("$in", new BsonArray(OpenStatuses)));

        // $facet executa várias sub-agregações em uma única ida ao banco.
        var facet = new BsonDocument("$facet", new BsonDocument
        {
            { "total", new BsonArray { new BsonDocument("$count", "n") } },
            { "done", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("Status", nameof(TaskItemStatus.Done))),
                    new BsonDocument("$count", "n")
                }
            },
            { "overdue", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument(openFilter) { { "DueDate", new BsonDocument("$lt", now) } }),
                    new BsonDocument("$count", "n")
                }
            },
            { "dueThisWeek", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument(openFilter)
                    {
                        { "DueDate", new BsonDocument { { "$gte", now }, { "$lte", now.AddDays(7) } } }
                    }),
                    new BsonDocument("$count", "n")
                }
            },
            { "byStatus", new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument { { "_id", "$Status" }, { "count", new BsonDocument("$sum", 1) } }),
                    new BsonDocument("$sort", new BsonDocument("count", -1))
                }
            },
            { "byPriority", new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument { { "_id", "$Priority" }, { "count", new BsonDocument("$sum", 1) } }),
                    new BsonDocument("$sort", new BsonDocument("count", -1))
                }
            }
        });

        var result = await _collection.Aggregate<BsonDocument>(new[] { facet }, cancellationToken: ct).FirstAsync(ct);

        long Count(string field) =>
            result[field].AsBsonArray.FirstOrDefault()?.AsBsonDocument["n"].ToInt64() ?? 0;

        IReadOnlyList<StatusCount> Groups(string field) =>
            result[field].AsBsonArray
                .Select(g => new StatusCount(g["_id"].AsString, g["count"].AsInt32))
                .ToList();

        var total = Count("total");
        var done = Count("done");
        var completionRate = total == 0 ? 0 : Math.Round(done * 100.0 / total, 2);

        return new TaskSummary(total, Count("overdue"), Count("dueThisWeek"), completionRate,
            Groups("byStatus"), Groups("byPriority"));
    }

    // ---------------------------------------------------------------- helpers

    private FilterDefinition<TaskItem> BuildSearchFilter(TaskSearchQuery q)
    {
        var filters = new List<FilterDefinition<TaskItem>>();

        if (!string.IsNullOrWhiteSpace(q.Title))
            filters.Add(_filter.Regex(t => t.Title,
                new BsonRegularExpression(Regex.Escape(q.Title.Trim()), "i")));

        if (q.Status is { Length: > 0 })
            filters.Add(_filter.In("Status", q.Status.Select(s => s.ToString())));

        if (q.Priority is { Length: > 0 })
            filters.Add(_filter.In("Priority", q.Priority.Select(p => p.ToString())));

        if (q.Tags is { Length: > 0 })
        {
            var tags = q.Tags.Select(t => t.Trim().ToLowerInvariant());
            filters.Add(q.MatchAllTags
                ? _filter.All(t => t.Tags, tags)
                : _filter.AnyIn(t => t.Tags, tags));
        }

        if (!string.IsNullOrWhiteSpace(q.Assignee))
            filters.Add(_filter.Regex(t => t.Assignee,
                new BsonRegularExpression($"^{Regex.Escape(q.Assignee.Trim())}$", "i")));

        if (q.DueFrom.HasValue) filters.Add(_filter.Gte(t => t.DueDate, q.DueFrom.Value.ToUniversalTime()));
        if (q.DueTo.HasValue) filters.Add(_filter.Lte(t => t.DueDate, q.DueTo.Value.ToUniversalTime()));
        if (q.CreatedFrom.HasValue) filters.Add(_filter.Gte(t => t.CreatedAt, q.CreatedFrom.Value.ToUniversalTime()));
        if (q.CreatedTo.HasValue) filters.Add(_filter.Lte(t => t.CreatedAt, q.CreatedTo.Value.ToUniversalTime()));

        return filters.Count == 0 ? _filter.Empty : _filter.And(filters);
    }

    private static SortDefinition<TaskItem> BuildSort(string sortBy, bool desc)
    {
        var field = sortBy.ToLowerInvariant() switch
        {
            "title" => "Title",
            "duedate" => "DueDate",
            "status" => "Status",
            "updatedat" => "UpdatedAt",
            _ => "CreatedAt"
        };

        var sort = Builders<TaskItem>.Sort;
        return desc ? sort.Descending(field) : sort.Ascending(field);
    }
}
