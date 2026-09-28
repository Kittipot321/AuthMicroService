using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class LoginTotpVerifyRequestValidator : AbstractValidator<LoginTotpVerifyRequest>
{
    public LoginTotpVerifyRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var digits = options.Value.Totp.Digits;

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(digits, digits)
            .Matches("^[0-9]+$")
                .WithMessage("Code must contain digits only.");
    }
}
