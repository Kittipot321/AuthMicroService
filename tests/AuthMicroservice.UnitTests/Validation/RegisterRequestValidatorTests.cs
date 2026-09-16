using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.UnitTests.Validation;

public class RegisterRequestValidatorTests
{
    private static RegisterRequestValidator BuildValidator()
    {
        var opts = Options.Create(new AuthMicroserviceOptions());
        return new RegisterRequestValidator(opts);
    }

    [Fact]
    public async Task Empty_Email_Fails()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new RegisterRequest { Email = "", Password = "P@ssw0rd!" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("short1!")]       // too short
    [InlineData("lowercase1!")]    // no uppercase
    [InlineData("UPPERCASE1!")]    // no lowercase
    [InlineData("NoDigits!")]      // no digit
    [InlineData("NoSymbol1")]      // no non-alnum
    public async Task Weak_Password_Fails(string password)
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new RegisterRequest { Email = "a@b.com", Password = password });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task Valid_Request_Passes()
    {
        var v = BuildValidator();
        var result = await v.ValidateAsync(new RegisterRequest
        {
            Email = "alice@example.com",
            Password = "P@ssw0rd!",
            FullName = "Alice"
        });
        result.IsValid.Should().BeTrue();
    }
}
