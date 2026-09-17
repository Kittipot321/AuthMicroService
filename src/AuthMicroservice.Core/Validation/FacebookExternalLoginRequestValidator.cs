using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class FacebookExternalLoginRequestValidator : AbstractValidator<FacebookExternalLoginRequest>
{
    public FacebookExternalLoginRequestValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .MaximumLength(8192);
    }
}
