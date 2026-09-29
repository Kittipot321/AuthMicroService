using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthMicroservice.Core.Configuration;

public sealed class EmailVerificationOptions
{
    public EmailVerificationMode Mode { get; set; } = EmailVerificationMode.Link;

    public string LinkBaseUrl { get; set; } = "https://app.example.com/verify-email";
}

[JsonConverter(typeof(EmailVerificationModeJsonConverter))]
public enum EmailVerificationMode
{
    Disabled = 0,
    Link = 1,
    Code = 2
}

internal sealed class EmailVerificationModeJsonConverter : JsonStringEnumConverter
{
    public EmailVerificationModeJsonConverter() : base(JsonNamingPolicy.CamelCase) { }
}
