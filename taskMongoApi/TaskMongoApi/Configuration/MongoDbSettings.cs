namespace TaskMongoApi.Configuration;

/// <summary>
/// Mapeia a seção "MongoDb" do appsettings.json.
/// </summary>
public class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string TasksCollectionName { get; set; } = "tasks";
}
