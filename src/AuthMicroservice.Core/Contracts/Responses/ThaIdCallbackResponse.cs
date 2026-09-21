namespace AuthMicroservice.Core.Contracts.Responses;

public sealed record ThaIdCallbackResponse(AuthResponse AuthResponse, string? ReturnUrl);
