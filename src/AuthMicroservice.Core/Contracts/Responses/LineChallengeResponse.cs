namespace AuthMicroservice.Core.Contracts.Responses;

public sealed record LineChallengeResponse(string AuthorizeUrl, string State);
