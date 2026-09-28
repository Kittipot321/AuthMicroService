namespace AuthMicroservice.Core.Contracts.Responses;

public sealed class TotpSetupResponse
{
    public string OtpauthUri { get; set; } = string.Empty;
    public string QrCodePngBase64 { get; set; } = string.Empty;
    public string SecretBase32 { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public int Digits { get; set; }
    public int PeriodSeconds { get; set; }
}
