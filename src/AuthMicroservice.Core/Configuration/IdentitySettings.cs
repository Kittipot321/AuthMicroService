namespace AuthMicroservice.Core.Configuration;

public sealed class IdentitySettings
{
    public PasswordSettings Password { get; set; } = new();

    public LockoutSettings Lockout { get; set; } = new();

    public SignInSettings SignIn { get; set; } = new();

    public UserSettings User { get; set; } = new();
}

public sealed class PasswordSettings
{
    public int RequiredLength { get; set; } = 8;

    public bool RequireDigit { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; } = true;

    public int RequiredUniqueChars { get; set; } = 1;
}

public sealed class LockoutSettings
{
    public bool AllowedForNewUsers { get; set; } = true;

    public int MaxFailedAccessAttempts { get; set; } = 5;

    public int DefaultLockoutMinutes { get; set; } = 15;
}

public sealed class SignInSettings
{
    public bool RequireConfirmedEmail { get; set; } = true;

    public bool RequireConfirmedPhoneNumber { get; set; }
}

public sealed class UserSettings
{
    public bool RequireUniqueEmail { get; set; } = true;
}
