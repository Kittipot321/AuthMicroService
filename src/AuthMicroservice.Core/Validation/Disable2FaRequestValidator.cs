using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;

namespace AuthMicroservice.Core.Validation;

public sealed class Disable2FaRequestValidator : AbstractValidator<Disable2FaRequest>
{
    public Disable2FaRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
