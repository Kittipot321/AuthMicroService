using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class TotpEnableConfirmRequestValidator : AbstractValidator<TotpEnableConfirmRequest>
{
    public TotpEnableConfirmRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var digits = options.Value.Totp.Digits;

        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(digits, digits)
            .Matches("^[0-9]+$")
                .WithMessage("Code must contain digits only.");
    }
}
