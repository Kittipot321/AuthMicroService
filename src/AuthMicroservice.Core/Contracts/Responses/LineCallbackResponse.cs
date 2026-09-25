namespace AuthMicroservice.Core.Contracts.Responses;

public sealed record LineCallbackResponse(AuthResponse AuthResponse, string? ReturnUrl);
