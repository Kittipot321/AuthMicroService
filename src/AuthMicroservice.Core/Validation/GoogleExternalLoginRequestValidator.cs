using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class GoogleExternalLoginRequestValidator : AbstractValidator<GoogleExternalLoginRequest>
{
    public GoogleExternalLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(8192);
    }
}
