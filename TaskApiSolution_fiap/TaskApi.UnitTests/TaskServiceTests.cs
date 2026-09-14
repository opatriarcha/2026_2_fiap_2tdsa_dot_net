using Moq;
using TaskApi.Models;
using TaskApi.Repositories;
using TaskApi.Services;
using Xunit;

namespace TaskApi.UnitTests;

public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _repositoryMock;
    private readonly ITaskService _service;

    public TaskServiceTests()
    {
        _repositoryMock = new Mock<ITaskRepository>();
        _service = new TaskService(_repositoryMock.Object);
    }

    [Fact]
    public void Create_ComTituloValido_DeveChamarRepositorioEDevolverTarefa()
    {
        //ARRANGE
        var dto = new CreateTaskDto("Comprar Café");
        _repositoryMock
            .Setup(r => r.Add(It.IsAny<TaskItem>()))
            .Returns((TaskItem t) => t);
        
        //ACT
        var result = _service.Create(dto);
        
        //ASSERT
        Assert.Equal("Comprar Café", result.Title);
        Assert.False(result.IsDone);
        
        _repositoryMock.Verify(r => r.Add(
            It.Is<TaskItem>( t => t.Title == "Comprar Café")), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_ComTituloInvalido_DeveLancarTaskValidationException(string? titulo)
    {
        var dto = new CreateTaskDto(titulo!);

        var ex = Assert.Throws<TaskValidationException>(() => _service.Create(dto));

        Assert.Equal("O título da tarefa é obrigatório.", ex.Message);
        _repositoryMock.Verify(r => r.Add(It.IsAny<TaskItem>()), Times.Never);
        
    }
    
    [Fact]
    public void Create_ComTituloMuitoLongo_DeveLancarTaskValidationException()
    {
        var tituloLongo = new string('a', 101);
        var dto = new CreateTaskDto(tituloLongo);

        var ex = Assert.Throws<TaskValidationException>(() => _service.Create(dto));

        Assert.Contains("não pode exceder", ex.Message);
    }

    [Fact]
    public void Create_DeveRemoverEspacosEmBrancoNasExtremidadesDoTitulo()
    {
        var dto = new CreateTaskDto("  Lavar o carro  ");
        _repositoryMock.Setup(r => r.Add(It.IsAny<TaskItem>())).Returns((TaskItem t) => t);

        var result = _service.Create(dto);

        Assert.Equal("Lavar o carro", result.Title);
    }

    [Fact]
    public void GetById_QuandoExiste_DeveRetornarTarefa()
    {
        var id = Guid.NewGuid();
        var task = new TaskItem { Id = id, Title = "Estudar" };
        _repositoryMock.Setup(r => r.GetById(id)).Returns(task);

        var result = _service.GetById(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public void GetById_QuandoNaoExiste_DeveRetornarNull()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<Guid>())).Returns((TaskItem?)null);

        var result = _service.GetById(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void Update_QuandoTarefaNaoExiste_DeveRetornarFalse()
    {
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetById(id)).Returns((TaskItem?)null);

        var result = _service.Update(id, new UpdateTaskDto("Novo título", true));

        Assert.False(result);
        _repositoryMock.Verify(r => r.Update(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public void Update_QuandoTarefaExiste_DeveAtualizarTituloEStatus()
    {
        var id = Guid.NewGuid();
        var existing = new TaskItem { Id = id, Title = "Antigo", IsDone = false };
        _repositoryMock.Setup(r => r.GetById(id)).Returns(existing);
        _repositoryMock.Setup(r => r.Update(It.IsAny<TaskItem>())).Returns(true);

        var result = _service.Update(id, new UpdateTaskDto("Atualizado", true));

        Assert.True(result);
        _repositoryMock.Verify(r => r.Update(It.Is<TaskItem>(
            t => t.Title == "Atualizado" && t.IsDone)), Times.Once);
    }

    [Fact]
    public void Update_ComTituloInvalido_DeveLancarExceptionSemConsultarRepositorio()
    {
        var id = Guid.NewGuid();

        Assert.Throws<TaskValidationException>(() => _service.Update(id, new UpdateTaskDto("", true)));

        _repositoryMock.Verify(r => r.GetById(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void Delete_DeveDelegarParaRepositorioERetornarSeuResultado()
    {
        var id = Guid.NewGuid();
        _repositoryMock.Setup(r => r.Delete(id)).Returns(true);

        var result = _service.Delete(id);

        Assert.True(result);
        _repositoryMock.Verify(r => r.Delete(id), Times.Once);
    }

    [Fact]
    public void GetAll_DeveRetornarTodasAsTarefasDoRepositorio()
    {
        var lista = new List<TaskItem>
        {
            new() { Title = "A" },
            new() { Title = "B" }
        };
        _repositoryMock.Setup(r => r.GetAll()).Returns(lista);

        var result = _service.GetAll();

        Assert.Equal(2, result.Count());
    }
    
}