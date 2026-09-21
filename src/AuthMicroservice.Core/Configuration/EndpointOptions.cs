using System.ComponentModel;
using System.Globalization;

namespace AuthMicroservice.Core.Configuration;

[TypeConverter(typeof(EndpointToggleTypeConverter))]
public sealed class EndpointToggle
{
    public bool Enabled { get; set; } = true;
    public bool ShowInSwagger { get; set; } = true;
}

internal sealed class EndpointToggleTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(bool) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is bool b)
        {
            return new EndpointToggle { Enabled = b };
        }

        if (value is string s && bool.TryParse(s, out var parsed))
        {
            return new EndpointToggle { Enabled = parsed };
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public sealed class EndpointOptions
{
    public bool Enabled { get; set; } = true;

    public EndpointToggle Register { get; set; } = new();
    public EndpointToggle Login { get; set; } = new();
    public EndpointToggle Refresh { get; set; } = new();
    public EndpointToggle Logout { get; set; } = new();
    public EndpointToggle LogoutAll { get; set; } = new();
    public EndpointToggle VerifyEmail { get; set; } = new();
    public EndpointToggle ResendVerification { get; set; } = new();
    public EndpointToggle ForgotPassword { get; set; } = new();
    public EndpointToggle ResetPassword { get; set; } = new();
    public EndpointToggle ChangePassword { get; set; } = new();
    public EndpointToggle Me { get; set; } = new();
    public EndpointToggle Health { get; set; } = new();
    public EndpointToggle ExternalGoogle { get; set; } = new();
    public EndpointToggle ExternalMicrosoft { get; set; } = new();
    public EndpointToggle ExternalFacebook { get; set; } = new();
    public EndpointToggle ExternalLine { get; set; } = new();
    public EndpointToggle ExternalThaId { get; set; } = new();
}
