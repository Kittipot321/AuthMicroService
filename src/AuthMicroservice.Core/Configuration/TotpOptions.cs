namespace AuthMicroservice.Core.Configuration;

public sealed class TotpOptions
{
    public bool Enabled { get; set; } = true;

    public string Issuer { get; set; } = "AuthMicroservice";

    public int Digits { get; set; } = 6;

    public int PeriodSeconds { get; set; } = 30;

    public int VerificationWindowSteps { get; set; } = 1;
}
