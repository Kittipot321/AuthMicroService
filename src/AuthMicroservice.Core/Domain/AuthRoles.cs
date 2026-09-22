namespace AuthMicroservice.Core.Domain;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> System = new[] { Admin, User };
}
