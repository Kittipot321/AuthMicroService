namespace AuthMicroservice.Core.Configuration;

public sealed class DataProtectionOptions
{
    public string ApplicationName { get; set; } = "AuthMicroservice";

    public string? KeyRingPath { get; set; }
}
