using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
