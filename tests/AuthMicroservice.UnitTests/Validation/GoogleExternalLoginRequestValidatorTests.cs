using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Validation;
using FluentAssertions;

namespace AuthMicroservice.UnitTests.Validation;

public class GoogleExternalLoginRequestValidatorTests
{
    private static GoogleExternalLoginRequestValidator BuildValidator() => new();

    [Fact]
    public async Task Empty_Code_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { Code = string.Empty });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GoogleExternalLoginRequest.Code));
    }

    [Fact]
    public async Task Whitespace_Code_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { Code = "   " });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Non_Empty_Code_Passes()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { Code = "4/0AbCdEfGhIjKlMnOpQrStUvWxYz" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Overlong_Code_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new GoogleExternalLoginRequest { Code = new string('x', 2049) });
        result.IsValid.Should().BeFalse();
    }
}
