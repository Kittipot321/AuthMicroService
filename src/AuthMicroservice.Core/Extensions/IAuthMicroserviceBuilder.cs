using Microsoft.Extensions.DependencyInjection;

namespace AuthMicroservice.Core.Extensions;

public interface IAuthMicroserviceBuilder
{
    IServiceCollection Services { get; }
}

internal sealed class AuthMicroserviceBuilder : IAuthMicroserviceBuilder
{
    public AuthMicroserviceBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}
