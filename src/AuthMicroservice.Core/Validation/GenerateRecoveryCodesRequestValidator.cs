using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class GenerateRecoveryCodesRequestValidator : AbstractValidator<GenerateRecoveryCodesRequest>
{
    public GenerateRecoveryCodesRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
