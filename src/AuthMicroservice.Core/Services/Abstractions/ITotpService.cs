namespace AuthMicroservice.Core.Services.Abstractions;

public interface ITotpService
{
    string GenerateSecretBase32();

    string BuildOtpauthUri(string issuer, string accountName, string secretBase32);

    string BuildQrCodePngBase64(string otpauthUri);

    bool VerifyCode(string secretBase32, string code);

    string ProtectSecret(string secretBase32);

    string UnprotectSecret(string protectedSecret);
}
