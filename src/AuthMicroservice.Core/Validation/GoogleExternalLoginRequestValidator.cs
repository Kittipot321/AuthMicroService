using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class GoogleExternalLoginRequestValidator : AbstractValidator<GoogleExternalLoginRequest>
{
    public GoogleExternalLoginRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(2048);
    }
}
