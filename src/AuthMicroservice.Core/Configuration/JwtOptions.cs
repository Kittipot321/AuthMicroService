namespace AuthMicroservice.Core.Configuration;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "AuthMicroservice";

    public string Audience { get; set; } = "AuthMicroservice.Clients";

    public string Key { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public int RefreshTokenLifetimeDays { get; set; } = 7;

    public int ClockSkewSeconds { get; set; } = 30;
}
