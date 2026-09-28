using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class LoginRecoveryCodeVerifyRequestValidator : AbstractValidator<LoginRecoveryCodeVerifyRequest>
{
    public LoginRecoveryCodeVerifyRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(32);
    }
}
