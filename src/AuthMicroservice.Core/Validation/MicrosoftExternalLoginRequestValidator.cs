using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class MicrosoftExternalLoginRequestValidator : AbstractValidator<MicrosoftExternalLoginRequest>
{
    public MicrosoftExternalLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(8192);
    }
}
