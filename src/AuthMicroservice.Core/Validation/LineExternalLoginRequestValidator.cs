using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class LineExternalLoginRequestValidator : AbstractValidator<LineExternalLoginRequest>
{
    public LineExternalLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(8192);
    }
}
