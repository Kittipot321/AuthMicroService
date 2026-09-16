using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var password = options.Value.Identity.Password;

        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(password.RequiredLength);
    }
}
