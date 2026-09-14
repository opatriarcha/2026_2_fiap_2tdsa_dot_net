using TaskApi.Models;
using TaskApi.Repositories;
using TaskApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Injeção de dependência
builder.Services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var tasks = app.MapGroup("/tasks").WithTags("Tasks");

// GET /tasks - lista todas as tarefas
tasks.MapGet("/", (ITaskService service) =>
    Results.Ok(service.GetAll()));

// GET /tasks/{id} - obtém uma tarefa por id
tasks.MapGet("/{id:guid}", (Guid id, ITaskService service) =>
{
    var task = service.GetById(id);
    return task is not null ? Results.Ok(task) : Results.NotFound();
});

// POST /tasks - cria uma nova tarefa
tasks.MapPost("/", (CreateTaskDto dto, ITaskService service) =>
{
    try
    {
        var created = service.Create(dto);
        return Results.Created($"/tasks/{created.Id}", created);
    }
    catch (TaskValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// PUT /tasks/{id} - atualiza uma tarefa existente
tasks.MapPut("/{id:guid}", (Guid id, UpdateTaskDto dto, ITaskService service) =>
{
    try
    {
        var updated = service.Update(id, dto);
        return updated ? Results.NoContent() : Results.NotFound();
    }
    catch (TaskValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// DELETE /tasks/{id} - remove uma tarefa
tasks.MapDelete("/{id:guid}", (Guid id, ITaskService service) =>
    service.Delete(id) ? Results.NoContent() : Results.NotFound());

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithTags("Health");

app.Run();

// Classe parcial exposta para permitir o uso de WebApplicationFactory<Program>
// nos testes de integração.
public partial class Program { }
