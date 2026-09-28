namespace AuthMicroservice.Core.Configuration;

public sealed class RecoveryCodeOptions
{
    public bool Enabled { get; set; } = true;

    public int Count { get; set; } = 10;

    public int Length { get; set; } = 10;
}
