namespace AuthMicroservice.Core.Configuration;

public sealed class AuthMicroserviceOptions
{
    public const string SectionName = "AuthMicroservice";

    public DatabaseOptions Database { get; set; } = new();

    public JwtOptions Jwt { get; set; } = new();

    public EmailOptions Email { get; set; } = new();

    public IdentitySettings Identity { get; set; } = new();

    public TokenLinkOptions TokenLinks { get; set; } = new();

    public string RoutePrefix { get; set; } = "/auth";

    public bool EnableSwagger { get; set; } = true;

    public EndpointOptions Endpoints { get; set; } = new();

    public HealthCheckOptions HealthChecks { get; set; } = new();
}

public sealed class HealthCheckOptions
{
    public bool CheckDatabase { get; set; } = true;

    public bool CheckSmtp { get; set; }

    public int SmtpTimeoutSeconds { get; set; } = 3;
}
