using System.Security.Cryptography;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;

namespace AuthMicroservice.Core.Services;

internal sealed class TotpService : ITotpService
{
    internal const string DataProtectionPurpose = "AuthMicroservice.TotpSecret";

    private readonly IOptionsMonitor<AuthMicroserviceOptions> _authOptions;
    private readonly IDataProtector _protector;

    public TotpService(
        IOptionsMonitor<AuthMicroserviceOptions> authOptions,
        IDataProtectionProvider dataProtectionProvider)
    {
        _authOptions = authOptions;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    public string GenerateSecretBase32()
    {
        Span<byte> buffer = stackalloc byte[20];
        RandomNumberGenerator.Fill(buffer);
        return Base32Encoding.ToString(buffer.ToArray()).TrimEnd('=');
    }

    public string BuildOtpauthUri(string issuer, string accountName, string secretBase32)
    {
        var opts = _authOptions.CurrentValue.Totp;
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}" +
               $"?secret={secretBase32}" +
               $"&issuer={encodedIssuer}" +
               $"&algorithm=SHA1" +
               $"&digits={opts.Digits}" +
               $"&period={opts.PeriodSeconds}";
    }

    public string BuildQrCodePngBase64(string otpauthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(pixelsPerModule: 8);
        return Convert.ToBase64String(bytes);
    }

    public bool VerifyCode(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var trimmed = code.Trim().Replace(" ", string.Empty);
        var opts = _authOptions.CurrentValue.Totp;

        byte[] key;
        try
        {
            key = Base32Encoding.ToBytes(secretBase32);
        }
        catch
        {
            return false;
        }

        var totp = new Totp(key, step: opts.PeriodSeconds, totpSize: opts.Digits);
        var window = new VerificationWindow(previous: opts.VerificationWindowSteps, future: opts.VerificationWindowSteps);
        return totp.VerifyTotp(trimmed, out _, window);
    }

    public string ProtectSecret(string secretBase32) => _protector.Protect(secretBase32);

    public string UnprotectSecret(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
