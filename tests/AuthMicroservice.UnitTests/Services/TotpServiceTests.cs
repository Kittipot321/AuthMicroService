using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using OtpNet;

namespace AuthMicroservice.UnitTests.Services;

public class TotpServiceTests
{
    private static TotpService Build(Action<TotpOptions>? configure = null)
    {
        var totpOptions = new TotpOptions
        {
            Enabled = true,
            Issuer = "TestIssuer",
            Digits = 6,
            PeriodSeconds = 30,
            VerificationWindowSteps = 1
        };
        configure?.Invoke(totpOptions);

        var authOptions = new AuthMicroserviceOptions { Totp = totpOptions };
        var monitor = new Mock<IOptionsMonitor<AuthMicroserviceOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(authOptions);

        var provider = new EphemeralDataProtectionProvider();
        return new TotpService(monitor.Object, provider);
    }

    [Fact]
    public void GenerateSecretBase32_ProducesUsableSecret()
    {
        var svc = Build();
        var secret = svc.GenerateSecretBase32();

        secret.Should().NotBeNullOrWhiteSpace();
        secret.Should().MatchRegex("^[A-Z2-7]+$", "Base32 alphabet");
        Base32Encoding.ToBytes(secret).Length.Should().Be(20);
    }

    [Fact]
    public void ProtectAndUnprotectSecret_Roundtrip()
    {
        var svc = Build();
        var secret = svc.GenerateSecretBase32();

        var protectedValue = svc.ProtectSecret(secret);
        protectedValue.Should().NotBe(secret);

        var unprotected = svc.UnprotectSecret(protectedValue);
        unprotected.Should().Be(secret);
    }

    [Fact]
    public void VerifyCode_AcceptsCurrentCode()
    {
        var svc = Build();
        var secret = svc.GenerateSecretBase32();

        var currentCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        svc.VerifyCode(secret, currentCode).Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_RejectsBogusCode()
    {
        var svc = Build();
        var secret = svc.GenerateSecretBase32();

        svc.VerifyCode(secret, "000000").Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_HandlesWhitespaceAndSpaces()
    {
        var svc = Build();
        var secret = svc.GenerateSecretBase32();
        var raw = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var padded = raw.Insert(3, " ");
        svc.VerifyCode(secret, "  " + padded + "  ").Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_EmptyInputs_ReturnsFalse()
    {
        var svc = Build();
        svc.VerifyCode(string.Empty, "123456").Should().BeFalse();
        svc.VerifyCode("JBSWY3DPEHPK3PXP", string.Empty).Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_InvalidBase32Secret_ReturnsFalse()
    {
        var svc = Build();
        svc.VerifyCode("!!!not-base32!!!", "123456").Should().BeFalse();
    }

    [Fact]
    public void BuildOtpauthUri_IncludesIssuerAccountAndParams()
    {
        var svc = Build();
        var uri = svc.BuildOtpauthUri("TestIssuer", "user@example.com", "JBSWY3DPEHPK3PXP");

        uri.Should().StartWith("otpauth://totp/TestIssuer:user%40example.com?");
        uri.Should().Contain("secret=JBSWY3DPEHPK3PXP");
        uri.Should().Contain("issuer=TestIssuer");
        uri.Should().Contain("digits=6");
        uri.Should().Contain("period=30");
    }

    [Fact]
    public void BuildQrCodePngBase64_ReturnsNonEmptyPngBase64()
    {
        var svc = Build();
        var uri = svc.BuildOtpauthUri("Test", "u@example.com", "JBSWY3DPEHPK3PXP");
        var qr = svc.BuildQrCodePngBase64(uri);

        qr.Should().NotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(qr);
        bytes.Length.Should().BeGreaterThan(50);
        // PNG signature
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x4E);
        bytes[3].Should().Be(0x47);
    }
}
