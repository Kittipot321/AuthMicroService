using System.Security.Claims;
using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AuthMicroservice.Core.Services;

internal sealed class AuthService : IAuthService
{
    internal const string GoogleLoginProvider = "Google";
    internal const string MicrosoftLoginProvider = "Microsoft";
    internal const string FacebookLoginProvider = "Facebook";
    internal const string LineLoginProvider = "Line";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IMicrosoftTokenValidator _microsoftTokenValidator;
    private readonly IFacebookTokenValidator _facebookTokenValidator;
    private readonly ILineTokenValidator _lineTokenValidator;
    private readonly IClock _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IEmailService emailService,
        IGoogleTokenValidator googleTokenValidator,
        IMicrosoftTokenValidator microsoftTokenValidator,
        IFacebookTokenValidator facebookTokenValidator,
        ILineTokenValidator lineTokenValidator,
        IClock clock,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _emailService = emailService;
        _googleTokenValidator = googleTokenValidator;
        _microsoftTokenValidator = microsoftTokenValidator;
        _facebookTokenValidator = facebookTokenValidator;
        _lineTokenValidator = lineTokenValidator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResult<AuthResponse>> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (existing is not null)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.EmailAlreadyRegistered, "This email is already registered.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            FullName = request.FullName,
            CreatedAt = _clock.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            return IdentityFailure<AuthResponse>(createResult);
        }

        await _userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);

        var verificationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
        try
        {
            await _emailService.SendEmailVerificationAsync(user, verificationToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email to {Email} — user was still created.", user.Email);
        }

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null || user.IsDeactivated)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true).ConfigureAwait(false);

        if (signIn.IsLockedOut)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserLockedOut, "Account is temporarily locked. Try again later.");
        }

        if (signIn.IsNotAllowed)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.EmailNotConfirmed, "Email address has not been confirmed.");
        }

        if (!signIn.Succeeded)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        user.LastLoginAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult<AuthResponse>> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, "Access token and refresh token are required.");
        }

        if (_jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken) is null)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, "Invalid access token.");
        }

        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, Guid.NewGuid().ToString("N"), ipAddress, cancellationToken).ConfigureAwait(false);
        if (!rotation.Succeeded || rotation.User is null || rotation.NewRawToken is null)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidRefreshToken, rotation.ErrorMessage ?? "Refresh token is invalid.");
        }

        var user = rotation.User;
        if (user.IsDeactivated)
        {
            await _refreshTokenService.RevokeAsync(rotation.NewRawToken, ipAddress, "User is deactivated.", cancellationToken).ConfigureAwait(false);
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserDeactivated, "User account is deactivated.");
        }

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var access = _jwtTokenService.GenerateAccessToken(user, roles);

        var response = new AuthResponse
        {
            AccessToken = access.Token,
            RefreshToken = rotation.NewRawToken,
            ExpiresAt = access.ExpiresAt,
            User = await BuildUserResponseAsync(user, roles).ConfigureAwait(false)
        };

        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult> LogoutAsync(LogoutRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _refreshTokenService.RevokeAsync(request.RefreshToken, ipAddress, "User logout.", cancellationToken).ConfigureAwait(false);
        return AuthResult.Success();
    }

    public async Task<AuthResult> LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _refreshTokenService.RevokeAllForUserAsync(userId, ipAddress, "User logout-all.", cancellationToken).ConfigureAwait(false);
        return AuthResult.Success();
    }

    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return AuthResult.Failure(AuthErrorCodes.UserNotFound, "User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        await _refreshTokenService.RevokeAllForUserAsync(userId, ipAddress, "Password changed.", cancellationToken).ConfigureAwait(false);
        return AuthResult.Success();
    }

    public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null || user.IsDeactivated)
        {
            return AuthResult.Success();
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
            await _emailService.SendPasswordResetAsync(user, token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}.", user.Email);
        }

        return AuthResult.Success();
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null)
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidToken, "Invalid token or email.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return IdentityFailure(result);
        }

        await _refreshTokenService.RevokeAllForUserAsync(user.Id, ipAddress, "Password reset.", cancellationToken).ConfigureAwait(false);
        return AuthResult.Success();
    }

    public async Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidToken, "Invalid user identifier.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return AuthResult.Failure(AuthErrorCodes.InvalidToken, "Invalid token.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Token).ConfigureAwait(false);
        return result.Succeeded
            ? AuthResult.Success()
            : IdentityFailure(result);
    }

    public async Task<AuthResult> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null || user.EmailConfirmed)
        {
            return AuthResult.Success();
        }

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
            await _emailService.SendEmailVerificationAsync(user, token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resend verification email to {Email}.", user.Email);
        }

        return AuthResult.Success();
    }

    public async Task<AuthResult<AuthResponse>> LoginWithGoogleAsync(GoogleExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        GoogleUserInfo googleUser;
        try
        {
            googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken).ConfigureAwait(false);
        }
        catch (GoogleTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Google id_token validation failed.");
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidGoogleToken, "Google id_token is invalid.");
        }

        if (!googleUser.EmailVerified)
        {
            return AuthResult<AuthResponse>.Failure(
                AuthErrorCodes.GoogleEmailNotVerified,
                "Google account email is not verified.");
        }

        var user = await _userManager.FindByLoginAsync(GoogleLoginProvider, googleUser.Subject).ConfigureAwait(false);

        if (user is null)
        {
            var byEmail = await _userManager.FindByEmailAsync(googleUser.Email).ConfigureAwait(false);
            if (byEmail is not null)
            {
                if (!byEmail.EmailConfirmed)
                {
                    return AuthResult<AuthResponse>.Failure(
                        AuthErrorCodes.EmailExistsUnverified,
                        "An unverified local account exists for this email. Verify it before linking a Google login.");
                }

                var linkResult = await _userManager.AddLoginAsync(
                    byEmail,
                    new UserLoginInfo(GoogleLoginProvider, googleUser.Subject, GoogleLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }

                user = byEmail;
            }
            else
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = googleUser.Email,
                    UserName = googleUser.Email,
                    FullName = googleUser.Name,
                    EmailConfirmed = true,
                    CreatedAt = _clock.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!createResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(createResult);
                }

                await _userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);

                var linkResult = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(GoogleLoginProvider, googleUser.Subject, GoogleLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }
            }
        }

        if (user.IsDeactivated)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserDeactivated, "User account is deactivated.");
        }

        user.LastLoginAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult<AuthResponse>> LoginWithMicrosoftAsync(MicrosoftExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        MicrosoftUserInfo microsoftUser;
        try
        {
            microsoftUser = await _microsoftTokenValidator.ValidateAsync(request.IdToken, cancellationToken).ConfigureAwait(false);
        }
        catch (MicrosoftTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Microsoft id_token validation failed.");
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidMicrosoftToken, "Microsoft id_token is invalid.");
        }

        var user = await _userManager.FindByLoginAsync(MicrosoftLoginProvider, microsoftUser.Subject).ConfigureAwait(false);

        if (user is null)
        {
            var byEmail = await _userManager.FindByEmailAsync(microsoftUser.Email).ConfigureAwait(false);
            if (byEmail is not null)
            {
                if (!byEmail.EmailConfirmed)
                {
                    return AuthResult<AuthResponse>.Failure(
                        AuthErrorCodes.EmailExistsUnverified,
                        "An unverified local account exists for this email. Verify it before linking a Microsoft login.");
                }

                var linkResult = await _userManager.AddLoginAsync(
                    byEmail,
                    new UserLoginInfo(MicrosoftLoginProvider, microsoftUser.Subject, MicrosoftLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }

                user = byEmail;
            }
            else
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = microsoftUser.Email,
                    UserName = microsoftUser.Email,
                    FullName = microsoftUser.Name,
                    EmailConfirmed = true,
                    CreatedAt = _clock.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!createResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(createResult);
                }

                await _userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);

                var linkResult = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(MicrosoftLoginProvider, microsoftUser.Subject, MicrosoftLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }
            }
        }

        if (user.IsDeactivated)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserDeactivated, "User account is deactivated.");
        }

        user.LastLoginAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult<AuthResponse>> LoginWithFacebookAsync(FacebookExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        FacebookUserInfo facebookUser;
        try
        {
            facebookUser = await _facebookTokenValidator.ValidateAsync(request.AccessToken, cancellationToken).ConfigureAwait(false);
        }
        catch (FacebookTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Facebook access_token validation failed.");
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidFacebookToken, "Facebook access token is invalid.");
        }

        if (string.IsNullOrWhiteSpace(facebookUser.Email))
        {
            return AuthResult<AuthResponse>.Failure(
                AuthErrorCodes.FacebookEmailRequired,
                "Facebook account does not have an accessible email. Grant email permission or use another provider.");
        }

        var user = await _userManager.FindByLoginAsync(FacebookLoginProvider, facebookUser.Subject).ConfigureAwait(false);

        if (user is null)
        {
            var byEmail = await _userManager.FindByEmailAsync(facebookUser.Email).ConfigureAwait(false);
            if (byEmail is not null)
            {
                if (!byEmail.EmailConfirmed)
                {
                    return AuthResult<AuthResponse>.Failure(
                        AuthErrorCodes.EmailExistsUnverified,
                        "An unverified local account exists for this email. Verify it before linking a Facebook login.");
                }

                var linkResult = await _userManager.AddLoginAsync(
                    byEmail,
                    new UserLoginInfo(FacebookLoginProvider, facebookUser.Subject, FacebookLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }

                user = byEmail;
            }
            else
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = facebookUser.Email,
                    UserName = facebookUser.Email,
                    FullName = facebookUser.Name,
                    EmailConfirmed = true,
                    CreatedAt = _clock.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!createResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(createResult);
                }

                await _userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);

                var linkResult = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(FacebookLoginProvider, facebookUser.Subject, FacebookLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }
            }
        }

        if (user.IsDeactivated)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserDeactivated, "User account is deactivated.");
        }

        user.LastLoginAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<AuthResult<AuthResponse>> LoginWithLineAsync(LineExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        LineUserInfo lineUser;
        try
        {
            lineUser = await _lineTokenValidator.ValidateAsync(request.IdToken, cancellationToken).ConfigureAwait(false);
        }
        catch (LineTokenValidationException ex)
        {
            _logger.LogWarning(ex, "LINE id_token validation failed.");
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.InvalidLineToken, "LINE id_token is invalid.");
        }

        if (string.IsNullOrWhiteSpace(lineUser.Email))
        {
            return AuthResult<AuthResponse>.Failure(
                AuthErrorCodes.LineEmailRequired,
                "LINE account did not return an email. Grant email permission in the LINE Login channel and consent, or use another provider.");
        }

        var user = await _userManager.FindByLoginAsync(LineLoginProvider, lineUser.Subject).ConfigureAwait(false);

        if (user is null)
        {
            var byEmail = await _userManager.FindByEmailAsync(lineUser.Email).ConfigureAwait(false);
            if (byEmail is not null)
            {
                if (!byEmail.EmailConfirmed)
                {
                    return AuthResult<AuthResponse>.Failure(
                        AuthErrorCodes.EmailExistsUnverified,
                        "An unverified local account exists for this email. Verify it before linking a LINE login.");
                }

                var linkResult = await _userManager.AddLoginAsync(
                    byEmail,
                    new UserLoginInfo(LineLoginProvider, lineUser.Subject, LineLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }

                user = byEmail;
            }
            else
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = lineUser.Email,
                    UserName = lineUser.Email,
                    FullName = lineUser.Name,
                    EmailConfirmed = true,
                    CreatedAt = _clock.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!createResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(createResult);
                }

                await _userManager.AddToRoleAsync(user, "User").ConfigureAwait(false);

                var linkResult = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo(LineLoginProvider, lineUser.Subject, LineLoginProvider)).ConfigureAwait(false);
                if (!linkResult.Succeeded)
                {
                    return IdentityFailure<AuthResponse>(linkResult);
                }
            }
        }

        if (user.IsDeactivated)
        {
            return AuthResult<AuthResponse>.Failure(AuthErrorCodes.UserDeactivated, "User account is deactivated.");
        }

        user.LastLoginAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        var response = await BuildAuthResponseAsync(user, ipAddress, cancellationToken).ConfigureAwait(false);
        return AuthResult<AuthResponse>.Success(response);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return await BuildUserResponseAsync(user).ConfigureAwait(false);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var access = _jwtTokenService.GenerateAccessToken(user, roles);
        var refresh = await _refreshTokenService.IssueAsync(user, access.Jti, ipAddress, cancellationToken).ConfigureAwait(false);

        return new AuthResponse
        {
            AccessToken = access.Token,
            RefreshToken = refresh,
            ExpiresAt = access.ExpiresAt,
            User = await BuildUserResponseAsync(user, roles).ConfigureAwait(false)
        };
    }

    private async Task<UserResponse> BuildUserResponseAsync(ApplicationUser user, IList<string>? cachedRoles = null)
    {
        var roles = cachedRoles ?? await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var claims = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);

        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToArray(),
            Claims = claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => string.Join(",", g.Select(c => c.Value)))
        };
    }

    private static AuthResult<T> IdentityFailure<T>(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return AuthResult<T>.Validation(errors);
    }

    private static AuthResult IdentityFailure(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return AuthResult.Validation(errors);
    }
}
