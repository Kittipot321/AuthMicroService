using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class Enable2FaConfirmRequestValidator : AbstractValidator<Enable2FaConfirmRequest>
{
    public Enable2FaConfirmRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var length = options.Value.Otp.CodeLength;

        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(length, length)
            .Matches("^[0-9]+$")
                .WithMessage("Code must contain digits only.");
    }
}
