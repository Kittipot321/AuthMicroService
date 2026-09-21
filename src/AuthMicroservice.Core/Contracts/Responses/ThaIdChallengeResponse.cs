namespace AuthMicroservice.Core.Contracts.Responses;

public sealed record ThaIdChallengeResponse(string AuthorizeUrl, string State);
