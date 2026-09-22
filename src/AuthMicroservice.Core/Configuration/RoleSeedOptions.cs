using AuthMicroservice.Core.Domain;

namespace AuthMicroservice.Core.Configuration;

public sealed class RoleSeedOptions
{
    public string DefaultRegistrationRole { get; set; } = AuthRoles.User;

    public List<string> AllowedSelfRegisterRoles { get; set; } = new();

    public List<RoleDefinition> AdditionalRoles { get; set; } = new();
}

public sealed class RoleDefinition
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
