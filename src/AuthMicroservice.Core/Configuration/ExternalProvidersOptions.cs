namespace AuthMicroservice.Core.Configuration;

public sealed class ExternalProvidersOptions
{
    public GoogleProviderOptions Google { get; set; } = new();

    public MicrosoftProviderOptions Microsoft { get; set; } = new();

    public FacebookProviderOptions Facebook { get; set; } = new();

    public LineProviderOptions Line { get; set; } = new();

    public ThaIdProviderOptions ThaId { get; set; } = new();
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

    // Channel secret — required for authorization-code exchange (challenge/callback flow).
    public string ChannelSecret { get; set; } = string.Empty;

    // Authorize base URL — LINE production is https://access.line.me.
    public string Authority { get; set; } = "https://access.line.me";

    public string TokenEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/token";

    public string VerifyEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/verify";

    // Absolute URL of this API's callback endpoint — must match the redirect_uri registered with LINE.
    public string RedirectUri { get; set; } = string.Empty;

    // Whitelist of allowed prefixes for the frontend returnUrl (open-redirect guard).
    public List<string> AllowedReturnUrlPrefixes { get; set; } = new();

    public string Scopes { get; set; } = "openid profile email";

    public int StateLifetimeMinutes { get; set; } = 10;
}

public sealed class ThaIdProviderOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    // Sandbox default; override to https://imauth.bora.dopa.go.th/api/v2/oauth2 for production.
    public string Authority { get; set; } = "https://imauthtestc.bora.dopa.go.th/api/v2/oauth2";

    // Absolute URL of this API's callback endpoint — must match the redirect_uri registered with ThaID.
    public string RedirectUri { get; set; } = string.Empty;

    // Whitelist of allowed prefixes for the frontend returnUrl (open-redirect guard).
    public List<string> AllowedReturnUrlPrefixes { get; set; } = new();

    public string Scopes { get; set; } = "openid pid given_name family_name email birthdate address";

    public int StateLifetimeMinutes { get; set; } = 10;
}
