namespace AuthMicroservice.Core.Configuration;

public sealed class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.InMemory;

    public string? ConnectionString { get; set; }

    public bool AutoMigrate { get; set; } = true;

    public bool SeedDefaults { get; set; } = true;
}
