using Microsoft.AspNetCore.Mvc;

namespace TaskApi.UnitTests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TaskApi.Endpoints;
using TaskApi.Models;
using TaskApi.Services;
using Xunit;

public class TaskEndpointsTests
{
    private readonly Mock<ITaskService> _serviceMock = new(MockBehavior.Strict);

    [Fact]
    public void GetAll_QuandoNaoHaTarefas_DeveRetornarOkComListaVazia()
    {
        _serviceMock.Setup(s => s.GetAll()).Returns(Array.Empty<TaskItem>());

        var result = TaskEndpoints.GetAll(_serviceMock.Object);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Empty(result.Value!);
        _serviceMock.Verify(s => s.GetAll(), Times.Once);

    }

    [Fact]
    public void GetAll_DeveRetornarOkComAsTarefasDoServico()
    {
        var tarefas = new List<TaskItem>
        {
            new() { Title = "Tarefa A" },
            new() { Title = "Tarefa B" }
        };
        
        _serviceMock.Setup(s => s.GetAll()).Returns(tarefas);
        
        var result = TaskEndpoints.GetAll(_serviceMock.Object);
        
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Same(tarefas, result.Value);
    }

    [Fact]
    public void GetById_QuandoExiste_DeveRetornarOkComATarefa()
    {
        var id = Guid.NewGuid();
        var task = new TaskItem{ Id = id, Title = "Ler um Manga" };
       
        _serviceMock.Setup(s => s.GetById(id)).Returns(task);
        
        var result = TaskEndpoints.GetById(id, _serviceMock.Object);

        var ok = Assert.IsType<Ok<TaskItem>>(result.Result);

        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Same(task, ok.Value);
    }
    
    [Fact]
    public void GetById_QuandoNaoExiste_DeveRetornarNotFound()
    {
        
    }
}