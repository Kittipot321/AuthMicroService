using System.Text;
using AuthMicroservice.Core.Configuration;
using AuthMicroservice.Core.Data;
using AuthMicroservice.Core.Domain;
using AuthMicroservice.Core.HealthChecks;
using AuthMicroservice.Core.Services;
using AuthMicroservice.Core.Services.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthMicroservice.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Authentication Microservice stack: ASP.NET Core Identity, JWT bearer auth, refresh tokens,
    /// email verification/reset, and FluentValidation validators. Binds the "AuthMicroservice" section from
    /// <paramref name="configuration"/>. The caller MUST chain a database provider extension
    /// (e.g. <c>.UseSqlServer()</c> from AuthMicroservice.Migrations.SqlServer) to register <c>AuthDbContext</c>.
    /// </summary>
    public static IAuthMicroserviceBuilder AddAuthMicroservice(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthMicroserviceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(AuthMicroserviceOptions.SectionName);

        var optionsBuilder = services.AddOptions<AuthMicroserviceOptions>()
            .Bind(section)
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton<IValidateOptions<AuthMicroserviceOptions>, AuthMicroserviceOptionsValidator>();

        services.AddHttpContextAccessor();
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<ICurrentUserService, CurrentUserService>();

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();

        // Bind Identity options lazily against the fully-assembled AuthMicroservice config.
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<AuthMicroserviceOptions>>((o, auth) =>
            {
                var s = auth.Value.Identity;
                o.Password.RequiredLength = s.Password.RequiredLength;
                o.Password.RequireDigit = s.Password.RequireDigit;
                o.Password.RequireLowercase = s.Password.RequireLowercase;
                o.Password.RequireUppercase = s.Password.RequireUppercase;
                o.Password.RequireNonAlphanumeric = s.Password.RequireNonAlphanumeric;
                o.Password.RequiredUniqueChars = s.Password.RequiredUniqueChars;

                o.Lockout.AllowedForNewUsers = s.Lockout.AllowedForNewUsers;
                o.Lockout.MaxFailedAccessAttempts = s.Lockout.MaxFailedAccessAttempts;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(s.Lockout.DefaultLockoutMinutes);

                o.SignIn.RequireConfirmedEmail = s.SignIn.RequireConfirmedEmail;
                o.SignIn.RequireConfirmedPhoneNumber = s.SignIn.RequireConfirmedPhoneNumber;

                o.User.RequireUniqueEmail = s.User.RequireUniqueEmail;
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthMicroserviceOptions>>((o, auth) =>
            {
                var jwt = auth.Value.Jwt;
                var keyBytes = Encoding.UTF8.GetBytes(
                    string.IsNullOrWhiteSpace(jwt.Key) ? new string('x', 32) : jwt.Key);

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddAuthorization();

        services.TryAddScoped<IJwtTokenService, JwtTokenService>();
        services.TryAddScoped<IRefreshTokenService, RefreshTokenService>();
        services.TryAddScoped<IEmailSender, SmtpEmailSender>();
        services.TryAddScoped<IEmailService, EmailService>();
        services.TryAddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.TryAddSingleton<IMicrosoftTokenValidator, MicrosoftTokenValidator>();
        services.AddHttpClient(FacebookTokenValidator.HttpClientName);
        services.TryAddSingleton<IFacebookTokenValidator, FacebookTokenValidator>();
        services.TryAddScoped<IAuthService, AuthService>();

        services.AddHealthChecks()
            .AddCheck<AuthDbHealthCheck>(
                AuthDbHealthCheck.Name,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "auth", "db", "ready" })
            .AddCheck<SmtpHealthCheck>(
                SmtpHealthCheck.Name,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "auth", "smtp", "ready" });

        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "AuthMicroservice API",
                Version = "v1"
            });

            var bearerScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "Enter your JWT access token."
            };
            o.AddSecurityDefinition("Bearer", bearerScheme);
            o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
        });

        return new AuthMicroserviceBuilder(services);
    }
}
