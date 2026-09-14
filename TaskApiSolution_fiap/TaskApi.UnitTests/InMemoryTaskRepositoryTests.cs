using TaskApi.Models;
using TaskApi.Repositories;
using Xunit;

namespace TaskApi.UnitTests;

public class InMemoryTaskRepositoryTests
{
    private readonly InMemoryTaskRepository _repository = new();

    [Fact]
    public void Add_DeveArmazenarTarefaEPermitirBuscaPorId()
    {
        var task = new TaskItem { Title = "Nova Tarefa" };
        _repository.Add(task);
        
        var result = _repository.GetById(task.Id);
        
        Assert.NotNull(result);
        Assert.Equal("Nova Tarefa", result.Title);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public void GetById_QuantoNaoExiste_DeveRetornarNull()
    {
        var result = _repository.GetById(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact] //TEstes AAA - Arrage -> Act -> Assert
    public void Update_QuantoTarefaExiste_DeveRetornarTrueEAtualizarValores()
    {
        //ARRANGE
        var task = new TaskItem { Title = "Original" };
        _repository.Add(task);
        
        //ACT
        task.Title = "Modified Title";
        var updated = _repository.Update(task);
        
        //ASSERT
        Assert.True(updated);
        Assert.Equal("Modified Title", _repository.GetById(task.Id).Title);
        
    }
    
    [Fact]
    public void Delete_QuandoTarefaExiste_DeveRemoverERetornarTrue()
    {
        //ARRANGE
        var task = new TaskItem { Title = "Para remover" };
        _repository.Add(task);

        
        //ACT
        var deleted = _repository.Delete(task.Id);

        
        //ASSERT
        Assert.True(deleted);
        Assert.Null(_repository.GetById(task.Id));
    }

    [Fact]
    public void Delete_QuandoTarefaNaoExiste_DeveRetornarFalse()
    {
        var deleted = _repository.Delete(Guid.NewGuid());

        Assert.False(deleted);
    }
    
    [Fact]
    public void GetAll_DeveRetornarTarefasOrdenadasPorDataDeCriacao()
    {
        
        //ARRANGE
        var primeira = new TaskItem { Title = "Primeira", CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
        var segunda = new TaskItem { Title = "Segunda", CreatedAt = DateTime.UtcNow };

        _repository.Add(segunda);
        _repository.Add(primeira);

        //ACT
        var result = _repository.GetAll().ToList();

        
       //ASSERT
        Assert.Equal("Primeira", result[0].Title);
        Assert.Equal("Segunda", result[1].Title);
    }
}