using Microsoft.AspNetCore.Http.HttpResults;
using TaskMongoApi.Models;
using TaskMongoApi.Services;

namespace TaskMongoApi.Endpoints;

/// <summary>
/// Handlers em métodos estáticos nomeados (em vez de lambdas inline) para
/// permitir testes unitários com o ITaskService mockado.
/// </summary>
public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var tasks = app.MapGroup("/tasks").WithTags("Tasks");

        // CRUD
        tasks.MapGet("/", GetAll);
        tasks.MapGet("/{id}", GetById);
        tasks.MapPost("/", Create);
        tasks.MapPut("/{id}", Update);
        tasks.MapPatch("/{id}/status", UpdateStatus);
        tasks.MapDelete("/{id}", Delete);

        // Buscas aprofundadas
        var search = app.MapGroup("/tasks").WithTags("Search");
        search.MapGet("/search", Search);          // filtros combinados + paginação + ordenação
        search.MapGet("/text", FullTextSearch);    // full-text ($text) por relevância
        search.MapGet("/overdue", GetOverdue);     // vencidas e ainda abertas
        search.MapGet("/due-soon", GetDueSoon);    // vencem nos próximos N dias

        // Estatísticas (aggregation pipeline)
        var stats = app.MapGroup("/tasks/stats").WithTags("Stats");
        stats.MapGet("/summary", GetSummary);
        stats.MapGet("/by-status", CountByStatus);
        stats.MapGet("/by-assignee", StatsByAssignee);
        stats.MapGet("/tags", TopTags);

        return app;
    }

    // ---------------------------------------------------------------- CRUD

    public static async Task<Ok<IReadOnlyList<TaskItem>>> GetAll(ITaskService service, CancellationToken ct) =>
        TypedResults.Ok(await service.GetAllAsync(ct));

    public static async Task<Results<Ok<TaskItem>, NotFound>> GetById(string id, ITaskService service, CancellationToken ct)
    {
        var task = await service.GetByIdAsync(id, ct);
        return task is not null ? TypedResults.Ok(task) : TypedResults.NotFound();
    }

    public static async Task<Results<Created<TaskItem>, BadRequest<ErrorResponse>>> Create(
        CreateTaskDto dto, ITaskService service, CancellationToken ct)
    {
        try
        {
            var created = await service.CreateAsync(dto, ct);
            return TypedResults.Created($"/tasks/{created.Id}", created);
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static async Task<Results<NoContent, NotFound, BadRequest<ErrorResponse>>> Update(
        string id, UpdateTaskDto dto, ITaskService service, CancellationToken ct)
    {
        try
        {
            return await service.UpdateAsync(id, dto, ct) ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static async Task<Results<NoContent, NotFound>> UpdateStatus(
        string id, UpdateStatusDto dto, ITaskService service, CancellationToken ct) =>
        await service.UpdateStatusAsync(id, dto.Status, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    public static async Task<Results<NoContent, NotFound>> Delete(string id, ITaskService service, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();

    // ---------------------------------------------------------------- buscas

    public static async Task<Results<Ok<PagedResult<TaskItem>>, BadRequest<ErrorResponse>>> Search(
        [AsParameters] TaskSearchQuery query, ITaskService service, CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await service.SearchAsync(query, ct));
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static async Task<Results<Ok<IReadOnlyList<TaskItem>>, BadRequest<ErrorResponse>>> FullTextSearch(
        string? q, ITaskService service, CancellationToken ct, int limit = 20)
    {
        try
        {
            return TypedResults.Ok(await service.FullTextSearchAsync(q ?? string.Empty, limit, ct));
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static async Task<Ok<IReadOnlyList<TaskItem>>> GetOverdue(ITaskService service, CancellationToken ct) =>
        TypedResults.Ok(await service.GetOverdueAsync(ct));

    public static async Task<Results<Ok<IReadOnlyList<TaskItem>>, BadRequest<ErrorResponse>>> GetDueSoon(
        ITaskService service, CancellationToken ct, int days = 7)
    {
        try
        {
            return TypedResults.Ok(await service.GetDueWithinAsync(days, ct));
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    // ---------------------------------------------------------------- estatísticas

    public static async Task<Ok<TaskSummary>> GetSummary(ITaskService service, CancellationToken ct) =>
        TypedResults.Ok(await service.GetSummaryAsync(ct));

    public static async Task<Ok<IReadOnlyList<StatusCount>>> CountByStatus(ITaskService service, CancellationToken ct) =>
        TypedResults.Ok(await service.CountByStatusAsync(ct));

    public static async Task<Ok<IReadOnlyList<AssigneeStats>>> StatsByAssignee(ITaskService service, CancellationToken ct) =>
        TypedResults.Ok(await service.StatsByAssigneeAsync(ct));

    public static async Task<Ok<IReadOnlyList<TagCount>>> TopTags(ITaskService service, CancellationToken ct, int limit = 10) =>
        TypedResults.Ok(await service.TopTagsAsync(limit, ct));
}
