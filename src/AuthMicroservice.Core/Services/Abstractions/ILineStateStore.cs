namespace AuthMicroservice.Core.Services.Abstractions;

public interface ILineStateStore
{
    void Save(string state, LineAuthState value, TimeSpan ttl);

    LineAuthState? Consume(string state);
}

public sealed record LineAuthState(
    string CodeVerifier,
    string Nonce,
    string? ReturnUrl,
    DateTimeOffset CreatedAt);
