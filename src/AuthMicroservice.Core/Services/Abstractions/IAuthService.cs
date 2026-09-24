using AuthMicroservice.Core.Contracts.Common;
using AuthMicroservice.Core.Contracts.Requests;
using AuthMicroservice.Core.Contracts.Responses;

namespace AuthMicroservice.Core.Services.Abstractions;

public interface IAuthService
{
    Task<AuthResult<AuthResponse>> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> LogoutAsync(LogoutRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginWithGoogleAsync(GoogleExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginWithMicrosoftAsync(MicrosoftExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginWithFacebookAsync(FacebookExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginWithLineAsync(LineExternalLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<ThaIdChallengeResponse>> StartThaIdChallengeAsync(string? returnUrl, CancellationToken cancellationToken = default);

    Task<AuthResult<ThaIdCallbackResponse>> LoginWithThaIdCallbackAsync(string code, string state, string? ipAddress, CancellationToken cancellationToken = default);

    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResult<TwoFactorRequiredResponse>> SendEmailVerificationOtpAsync(SendEmailVerificationOtpRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> VerifyEmailWithOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginTwoFactorVerifyAsync(LoginTwoFactorRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult<TwoFactorRequiredResponse>> EnableTwoFactorRequestAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResult> EnableTwoFactorConfirmAsync(Guid userId, Enable2FaConfirmRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> DisableTwoFactorAsync(Guid userId, Disable2FaRequest request, CancellationToken cancellationToken = default);
}
