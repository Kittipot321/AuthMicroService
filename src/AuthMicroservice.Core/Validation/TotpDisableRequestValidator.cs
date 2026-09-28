using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class TotpDisableRequestValidator : AbstractValidator<TotpDisableRequest>
{
    public TotpDisableRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
