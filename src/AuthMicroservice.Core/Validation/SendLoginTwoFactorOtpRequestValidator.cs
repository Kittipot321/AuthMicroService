using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class SendLoginTwoFactorOtpRequestValidator : AbstractValidator<SendLoginTwoFactorOtpRequest>
{
    public SendLoginTwoFactorOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}
