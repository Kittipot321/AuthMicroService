using System.Security.Claims;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.HealthChecks;
using AuthMicroservice.Core.Services.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AuthMicroservice.Core.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthMicroservice(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
        var toggles = options.Endpoints;

        if (!toggles.Enabled)
        {
            return endpoints;
        }

        var group = endpoints.MapGroup(options.RoutePrefix).WithTags("Auth");
        var swaggerEnabled = options.EnableSwagger;

        MapIf(toggles.Register, swaggerEnabled, () => group.MapPost("/register", RegisterAsync)
            .WithName("AuthRegister")
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem());

        MapIf(toggles.Login, swaggerEnabled, () => group.MapPost("/login", LoginAsync)
            .WithName("AuthLogin")
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked));

        MapIf(toggles.Refresh, swaggerEnabled, () => group.MapPost("/refresh", RefreshAsync)
            .WithName("AuthRefresh")
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized));

        MapIf(toggles.Logout, swaggerEnabled, () => group.MapPost("/logout", LogoutAsync)
            .WithName("AuthLogout")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent));

        MapIf(toggles.LogoutAll, swaggerEnabled, () => group.MapPost("/logout-all", LogoutAllAsync)
            .WithName("AuthLogoutAll")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent));

        MapIf(toggles.VerifyEmail, swaggerEnabled, () => group.MapPost("/verify-email", VerifyEmailAsync)
            .WithName("AuthVerifyEmail")
            .AllowAnonymous()
            .Produces<MessageResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem());

        MapIf(toggles.ResendVerification, swaggerEnabled, () => group.MapPost("/resend-verification", ResendVerificationAsync)
            .WithName("AuthResendVerification")
            .AllowAnonymous()
            .Produces<MessageResponse>(StatusCodes.Status200OK));

        MapIf(toggles.ForgotPassword, swaggerEnabled, () => group.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("AuthForgotPassword")
            .AllowAnonymous()
            .Produces<MessageResponse>(StatusCodes.Status200OK));

        MapIf(toggles.ResetPassword, swaggerEnabled, () => group.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("AuthResetPassword")
            .AllowAnonymous()
            .Produces<MessageResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem());

        MapIf(toggles.ChangePassword, swaggerEnabled, () => group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("AuthChangePassword")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem());

        MapIf(toggles.SendEmailVerificationOtp, swaggerEnabled, () => group.MapPost("/otp/email/send", SendEmailVerificationOtpAsync)
            .WithName("AuthSendEmailVerificationOtp")
            .AllowAnonymous()
            .Produces<TwoFactorRequiredResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem());

        MapIf(toggles.VerifyEmailOtp, swaggerEnabled, () => group.MapPost("/otp/email/verify", VerifyEmailWithOtpAsync)
            .WithName("AuthVerifyEmailWithOtp")
            .AllowAnonymous()
            .Produces<MessageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem());

        MapIf(toggles.LoginTwoFactorVerify, swaggerEnabled, () => group.MapPost("/login/2fa/verify", LoginTwoFactorVerifyAsync)
            .WithName("AuthLoginTwoFactorVerify")
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem());

        MapIf(toggles.TwoFactorEnableRequest, swaggerEnabled, () => group.MapPost("/2fa/enable-request", TwoFactorEnableRequestAsync)
            .WithName("AuthTwoFactorEnableRequest")
            .RequireAuthorization()
            .Produces<TwoFactorRequiredResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));

        MapIf(toggles.TwoFactorEnableConfirm, swaggerEnabled, () => group.MapPost("/2fa/enable-confirm", TwoFactorEnableConfirmAsync)
            .WithName("AuthTwoFactorEnableConfirm")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem());

        MapIf(toggles.TwoFactorDisable, swaggerEnabled, () => group.MapPost("/2fa/disable", TwoFactorDisableAsync)
            .WithName("AuthTwoFactorDisable")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem());

        MapIf(toggles.Me, swaggerEnabled, () => group.MapGet("/me", GetCurrentUserAsync)
            .WithName("AuthMe")
            .RequireAuthorization()
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound));

        MapIf(toggles.Health, swaggerEnabled, () => group.MapGet("/health", HealthAsync)
            .WithName("AuthHealth")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable));

        if (toggles.ExternalGoogle.Enabled && options.ExternalProviders.Google.Enabled)
        {
            var googleBuilder = group.MapPost("/external/google", ExternalGoogleAsync)
                .WithName("AuthExternalGoogle")
                .AllowAnonymous()
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status409Conflict);

            if (!toggles.ExternalGoogle.ShowInSwagger || !swaggerEnabled)
            {
                googleBuilder.ExcludeFromDescription();
            }
        }

        if (toggles.ExternalMicrosoft.Enabled && options.ExternalProviders.Microsoft.Enabled)
        {
            var microsoftBuilder = group.MapPost("/external/microsoft", ExternalMicrosoftAsync)
                .WithName("AuthExternalMicrosoft")
                .AllowAnonymous()
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status409Conflict);

            if (!toggles.ExternalMicrosoft.ShowInSwagger || !swaggerEnabled)
            {
                microsoftBuilder.ExcludeFromDescription();
            }
        }

        if (toggles.ExternalFacebook.Enabled && options.ExternalProviders.Facebook.Enabled)
        {
            var facebookBuilder = group.MapPost("/external/facebook", ExternalFacebookAsync)
                .WithName("AuthExternalFacebook")
                .AllowAnonymous()
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status409Conflict);

            if (!toggles.ExternalFacebook.ShowInSwagger || !swaggerEnabled)
            {
                facebookBuilder.ExcludeFromDescription();
            }
        }

        if (toggles.ExternalLine.Enabled && options.ExternalProviders.Line.Enabled)
        {
            var lineBuilder = group.MapPost("/external/line", ExternalLineAsync)
                .WithName("AuthExternalLine")
                .AllowAnonymous()
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status409Conflict);

            if (!toggles.ExternalLine.ShowInSwagger || !swaggerEnabled)
            {
                lineBuilder.ExcludeFromDescription();
            }
        }

        if (toggles.ExternalThaId.Enabled && options.ExternalProviders.ThaId.Enabled)
        {
            var challengeBuilder = group.MapGet("/external/thaid/challenge", ExternalThaIdChallengeAsync)
                .WithName("AuthExternalThaIdChallenge")
                .AllowAnonymous()
                .Produces(StatusCodes.Status302Found)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound);

            var callbackBuilder = group.MapGet("/external/thaid/callback", ExternalThaIdCallbackAsync)
                .WithName("AuthExternalThaIdCallback")
                .AllowAnonymous()
                .Produces(StatusCodes.Status302Found)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            if (!toggles.ExternalThaId.ShowInSwagger || !swaggerEnabled)
            {
                challengeBuilder.ExcludeFromDescription();
                callbackBuilder.ExcludeFromDescription();
            }
        }

        return endpoints;
    }

    private static void MapIf(EndpointToggle toggle, bool swaggerEnabled, Func<RouteHandlerBuilder> map)
    {
        if (!toggle.Enabled)
        {
            return;
        }

        var builder = map();
        if (!toggle.ShowInSwagger || !swaggerEnabled)
        {
            builder.ExcludeFromDescription();
        }
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        IValidator<RegisterRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.RegisterAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded
            ? Results.Created($"{ResolvePrefix(http)}/me", result.Value)
            : ToProblem(result);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        IValidator<LoginRequest> validator,
        IOptions<AuthMicroserviceOptions> authOptions,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginAsync(request, ResolveIp(http), cancellationToken);
        if (result.Succeeded)
        {
            return Results.Ok(result.Value);
        }

        if (result.ErrorCode == AuthErrorCodes.TwoFactorRequired)
        {
            var otp = authOptions.Value.Otp;
            return Results.Json(new TwoFactorRequiredResponse
            {
                Email = request.Email,
                ExpiresAt = DateTime.UtcNow.AddMinutes(otp.ExpirationMinutes),
                Message = result.ErrorMessage ?? "Two-factor verification required."
            }, statusCode: StatusCodes.Status202Accepted);
        }

        return ToProblem(result);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IAuthService authService,
        IValidator<RefreshRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.RefreshAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        IValidator<LogoutRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        await authService.LogoutAsync(request, ResolveIp(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        IAuthService authService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        await authService.LogoutAllAsync(userId, ResolveIp(http), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        IAuthService authService,
        IValidator<VerifyEmailRequest> validator,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.VerifyEmailAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new MessageResponse("Email verified."))
            : ToProblem(result);
    }

    private static async Task<IResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        IAuthService authService,
        IValidator<ResendVerificationRequest> validator,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        await authService.ResendVerificationAsync(request, cancellationToken);
        return Results.Ok(new MessageResponse("If the account exists and is unverified, a new email has been sent."));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IAuthService authService,
        IValidator<ForgotPasswordRequest> validator,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        await authService.ForgotPasswordAsync(request, cancellationToken);
        return Results.Ok(new MessageResponse("If the account exists, a password reset email has been sent."));
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IAuthService authService,
        IValidator<ResetPasswordRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.ResetPasswordAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded
            ? Results.Ok(new MessageResponse("Password has been reset."))
            : ToProblem(result);
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        IAuthService authService,
        IValidator<ChangePasswordRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.ChangePasswordAsync(userId, request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.NoContent() : ToProblem(result);
    }

    private static async Task<IResult> HealthAsync(
        HealthCheckService healthCheckService,
        IOptions<AuthMicroserviceOptions> options,
        CancellationToken cancellationToken)
    {
        var hcOpts = options.Value.HealthChecks;
        var report = await healthCheckService.CheckHealthAsync(
            r => r.Tags.Contains("auth")
                 && (r.Name != AuthDbHealthCheck.Name || hcOpts.CheckDatabase)
                 && (r.Name != SmtpHealthCheck.Name || hcOpts.CheckSmtp),
            cancellationToken);

        var statusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        };

        return Results.Json(payload, statusCode: statusCode);
    }

    private static async Task<IResult> ExternalGoogleAsync(
        GoogleExternalLoginRequest request,
        IAuthService authService,
        IValidator<GoogleExternalLoginRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginWithGoogleAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> ExternalMicrosoftAsync(
        MicrosoftExternalLoginRequest request,
        IAuthService authService,
        IValidator<MicrosoftExternalLoginRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginWithMicrosoftAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> ExternalFacebookAsync(
        FacebookExternalLoginRequest request,
        IAuthService authService,
        IValidator<FacebookExternalLoginRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginWithFacebookAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> ExternalLineAsync(
        LineExternalLoginRequest request,
        IAuthService authService,
        IValidator<LineExternalLoginRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginWithLineAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> ExternalThaIdChallengeAsync(
        [FromQuery] string? returnUrl,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.StartThaIdChallengeAsync(returnUrl, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return ToProblem(result);
        }

        return Results.Redirect(result.Value.AuthorizeUrl);
    }

    private static async Task<IResult> ExternalThaIdCallbackAsync(
        [FromQuery] string code,
        [FromQuery] string state,
        [FromQuery(Name = "error")] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        IAuthService authService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Results.Problem(
                title: errorDescription ?? error,
                statusCode: StatusCodes.Status401Unauthorized,
                type: AuthErrorCodes.InvalidThaIdCode);
        }

        var result = await authService.LoginWithThaIdCallbackAsync(code, state, ResolveIp(http), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return ToProblem(result);
        }

        var auth = result.Value.AuthResponse;
        var returnUrl = result.Value.ReturnUrl;
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.Ok(auth);
        }

        var fragment =
            $"#access_token={Uri.EscapeDataString(auth.AccessToken)}" +
            $"&refresh_token={Uri.EscapeDataString(auth.RefreshToken)}" +
            $"&expires_at={Uri.EscapeDataString(auth.ExpiresAt.ToString("O"))}";
        return Results.Redirect(returnUrl + fragment);
    }

    private static async Task<IResult> SendEmailVerificationOtpAsync(
        SendEmailVerificationOtpRequest request,
        IAuthService authService,
        IValidator<SendEmailVerificationOtpRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.SendEmailVerificationOtpAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> VerifyEmailWithOtpAsync(
        VerifyEmailOtpRequest request,
        IAuthService authService,
        IValidator<VerifyEmailOtpRequest> validator,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.VerifyEmailWithOtpAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new MessageResponse("Email verified."))
            : ToProblem(result);
    }

    private static async Task<IResult> LoginTwoFactorVerifyAsync(
        LoginTwoFactorRequest request,
        IAuthService authService,
        IValidator<LoginTwoFactorRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.LoginTwoFactorVerifyAsync(request, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> TwoFactorEnableRequestAsync(
        IAuthService authService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await authService.EnableTwoFactorRequestAsync(userId, ResolveIp(http), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : ToProblem(result);
    }

    private static async Task<IResult> TwoFactorEnableConfirmAsync(
        Enable2FaConfirmRequest request,
        IAuthService authService,
        IValidator<Enable2FaConfirmRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.EnableTwoFactorConfirmAsync(userId, request, cancellationToken);
        return result.Succeeded ? Results.NoContent() : ToProblem(result);
    }

    private static async Task<IResult> TwoFactorDisableAsync(
        Disable2FaRequest request,
        IAuthService authService,
        IValidator<Disable2FaRequest> validator,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (await ValidateAsync(request, validator, cancellationToken) is { } bad)
        {
            return bad;
        }

        var result = await authService.DisableTwoFactorAsync(userId, request, cancellationToken);
        return result.Succeeded ? Results.NoContent() : ToProblem(result);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        IAuthService authService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (ResolveUserId(http) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var user = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(user);
    }

    private static async Task<IResult?> ValidateAsync<T>(T request, IValidator<T> validator, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (validation.IsValid)
        {
            return null;
        }

        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.ValidationProblem(errors);
    }

    private static IResult ToProblem(AuthResult result)
    {
        var statusCode = result.ErrorCode switch
        {
            AuthErrorCodes.InvalidCredentials => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.InvalidRefreshToken => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.InvalidToken => StatusCodes.Status400BadRequest,
            AuthErrorCodes.EmailNotConfirmed => StatusCodes.Status403Forbidden,
            AuthErrorCodes.UserLockedOut => StatusCodes.Status423Locked,
            AuthErrorCodes.UserDeactivated => StatusCodes.Status403Forbidden,
            AuthErrorCodes.EmailAlreadyRegistered => StatusCodes.Status409Conflict,
            AuthErrorCodes.EmailExistsUnverified => StatusCodes.Status409Conflict,
            AuthErrorCodes.InvalidGoogleToken => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.GoogleEmailNotVerified => StatusCodes.Status400BadRequest,
            AuthErrorCodes.GoogleLoginDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.InvalidMicrosoftToken => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.MicrosoftLoginDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.InvalidFacebookToken => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.FacebookEmailRequired => StatusCodes.Status400BadRequest,
            AuthErrorCodes.FacebookLoginDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.InvalidLineToken => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.LineLoginDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.ThaIdLoginDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.InvalidThaIdState => StatusCodes.Status400BadRequest,
            AuthErrorCodes.InvalidThaIdCode => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.ThaIdReturnUrlNotAllowed => StatusCodes.Status400BadRequest,
            AuthErrorCodes.InvalidRole => StatusCodes.Status400BadRequest,
            AuthErrorCodes.WeakPassword => StatusCodes.Status400BadRequest,
            AuthErrorCodes.ValidationFailed => StatusCodes.Status400BadRequest,
            AuthErrorCodes.InvalidOtp => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.OtpExpired => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.OtpAttemptsExceeded => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.OtpCooldownActive => StatusCodes.Status429TooManyRequests,
            AuthErrorCodes.OtpDisabled => StatusCodes.Status404NotFound,
            AuthErrorCodes.TwoFactorNotEnabled => StatusCodes.Status409Conflict,
            AuthErrorCodes.TwoFactorAlreadyEnabled => StatusCodes.Status409Conflict,
            AuthErrorCodes.UserNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        if (result.ValidationErrors is not null)
        {
            var writable = result.ValidationErrors.ToDictionary(kv => kv.Key, kv => kv.Value);
            return Results.ValidationProblem(writable, statusCode: statusCode, detail: result.ErrorMessage);
        }

        return Results.Problem(
            title: result.ErrorMessage ?? result.ErrorCode ?? "Auth error",
            statusCode: statusCode,
            type: result.ErrorCode);
    }

    private static string? ResolveIp(HttpContext http)
    {
        if (http.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded.ToString()))
        {
            return forwarded.ToString().Split(',')[0].Trim();
        }
        return http.Connection.RemoteIpAddress?.ToString();
    }

    private static Guid? ResolveUserId(HttpContext http)
    {
        var raw = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? http.User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string ResolvePrefix(HttpContext http)
    {
        var options = http.RequestServices.GetRequiredService<IOptions<AuthMicroserviceOptions>>().Value;
        return options.RoutePrefix.TrimEnd('/');
    }
}
