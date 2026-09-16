using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Requests;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IOptions<AuthMicroserviceOptions> options)
    {
        var password = options.Value.Identity.Password;

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(password.RequiredLength)
            .Must(p => !password.RequireDigit || p.Any(char.IsDigit))
                .WithMessage("Password must contain at least one digit.")
            .Must(p => !password.RequireLowercase || p.Any(char.IsLower))
                .WithMessage("Password must contain at least one lowercase letter.")
            .Must(p => !password.RequireUppercase || p.Any(char.IsUpper))
                .WithMessage("Password must contain at least one uppercase letter.")
            .Must(p => !password.RequireNonAlphanumeric || p.Any(c => !char.IsLetterOrDigit(c)))
                .WithMessage("Password must contain at least one non-alphanumeric character.");

        RuleFor(x => x.FullName).MaximumLength(200);
    }
}
