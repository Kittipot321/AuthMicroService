using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var password = options.Value.Identity.Password;

        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(password.RequiredLength)
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must be different from the current password.");
    }
}
