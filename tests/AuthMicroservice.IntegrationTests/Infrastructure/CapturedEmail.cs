namespace AuthMicroservice.IntegrationTests.Infrastructure;

public sealed record CapturedEmail(string To, string Subject, string HtmlBody);
