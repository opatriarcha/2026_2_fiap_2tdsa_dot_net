using Microsoft.AspNetCore.Http.HttpResults;
using TaskApi.Models;
using TaskApi.Services;

namespace TaskApi.Endpoints;

/// <summary>
/// Handlers dos endpoints de tarefas. Ficam em métodos estáticos nomeados
/// (em vez de lambdas inline no Program.cs) para permitir testes unitários
/// da camada de endpoints com o ITaskService mockado.
/// </summary>
public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var tasks = app.MapGroup("/tasks").WithTags("Tasks");

        // GET /tasks - lista todas as tarefas
        tasks.MapGet("/", GetAll);

        // GET /tasks/{id} - obtém uma tarefa por id
        tasks.MapGet("/{id:guid}", GetById);

        // POST /tasks - cria uma nova tarefa
        tasks.MapPost("/", Create);

        // PUT /tasks/{id} - atualiza uma tarefa existente
        tasks.MapPut("/{id:guid}", Update);

        // DELETE /tasks/{id} - remove uma tarefa
        tasks.MapDelete("/{id:guid}", Delete);

        return app;
    }

    public static Ok<IEnumerable<TaskItem>> GetAll(ITaskService service) =>
        TypedResults.Ok(service.GetAll());

    public static Results<Ok<TaskItem>, NotFound> GetById(Guid id, ITaskService service)
    {
        var task = service.GetById(id);
        return task is not null ? TypedResults.Ok(task) : TypedResults.NotFound();
    }

    public static Results<Created<TaskItem>, BadRequest<ErrorResponse>> Create(
        CreateTaskDto dto,
        ITaskService service)
    {
        try
        {
            var created = service.Create(dto);
            return TypedResults.Created($"/tasks/{created.Id}", created);
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static Results<NoContent, NotFound, BadRequest<ErrorResponse>> Update(
        Guid id,
        UpdateTaskDto dto,
        ITaskService service)
    {
        try
        {
            var updated = service.Update(id, dto);
            return updated ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (TaskValidationException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static Results<NoContent, NotFound> Delete(Guid id, ITaskService service) =>
        service.Delete(id) ? TypedResults.NoContent() : TypedResults.NotFound();
}  