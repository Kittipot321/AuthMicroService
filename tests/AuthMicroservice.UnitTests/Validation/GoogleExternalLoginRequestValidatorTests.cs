using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Validation;
using FluentAssertions;

namespace AuthMicroservice.UnitTests.Validation;

public class GoogleExternalLoginRequestValidatorTests
{
    private static GoogleExternalLoginRequestValidator BuildValidator() => new();

    [Fact]
    public async Task Empty_IdToken_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { IdToken = string.Empty });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GoogleExternalLoginRequest.IdToken));
    }

    [Fact]
    public async Task Whitespace_IdToken_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { IdToken = "   " });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Non_Empty_IdToken_Passes()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { IdToken = "eyJhbGciOi..." });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Overlong_IdToken_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { IdToken = new string('x', 8193) });
        result.IsValid.Should().BeFalse();
    }
}
