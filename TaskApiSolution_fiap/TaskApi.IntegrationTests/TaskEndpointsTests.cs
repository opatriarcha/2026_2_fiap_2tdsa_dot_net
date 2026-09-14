namespace TaskApi.IntegrationTests;
using System.Net;
using System.Net.Http.Json;
using TaskApi.Models;
using Xunit;

public class TaskEndpointsTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly TaskApiFactory _factory;

    public TaskEndpointsTests()
    {
        _factory = new TaskApiFactory();
        _client = _factory.CreateClient();
    }
    
    public void Dispose()
    {
        _factory.Dispose();
        _client.Dispose();
    }
    
    
}