namespace AuthMicroservice.Core.Configuration;

public sealed class ExternalProvidersOptions
{
    public GoogleProviderOptions Google { get; set; } = new();

    public MicrosoftProviderOptions Microsoft { get; set; } = new();
}

public sealed class GoogleProviderOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }
}

public sealed class MicrosoftProviderOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    // "common" | "organizations" | "consumers" | specific tenant GUID
    public string TenantId { get; set; } = "common";
}
