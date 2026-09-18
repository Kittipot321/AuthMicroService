namespace AuthMicroservice.Core.Configuration;

public sealed class ExternalProvidersOptions
{
    public GoogleProviderOptions Google { get; set; } = new();

    public MicrosoftProviderOptions Microsoft { get; set; } = new();

    public FacebookProviderOptions Facebook { get; set; } = new();

    public LineProviderOptions Line { get; set; } = new();
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

public sealed class FacebookProviderOptions
{
    public bool Enabled { get; set; }

    public string AppId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    public string GraphApiVersion { get; set; } = "v18.0";
}

public sealed class LineProviderOptions
{
    public bool Enabled { get; set; }

    // LINE Login channel ID — used both as verify-endpoint client_id and as expected aud claim.
    public string ChannelId { get; set; } = string.Empty;

    public string VerifyEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/verify";
}
