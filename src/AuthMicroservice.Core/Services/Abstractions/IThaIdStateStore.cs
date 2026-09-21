namespace AuthMicroservice.Core.Services.Abstractions;

public interface IThaIdStateStore
{
    void Save(string state, ThaIdAuthState value, TimeSpan ttl);

    ThaIdAuthState? Consume(string state);
}

public sealed record ThaIdAuthState(string CodeVerifier, string? ReturnUrl, DateTimeOffset CreatedAt);
