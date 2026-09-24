using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class SendEmailVerificationOtpRequestValidator : AbstractValidator<SendEmailVerificationOtpRequest>
{
    public SendEmailVerificationOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}
