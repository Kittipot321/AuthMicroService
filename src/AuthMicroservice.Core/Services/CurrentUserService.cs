using System.Security.Claims;
using AuthMicroservice.Core.Services.Abstractions;
using Microsoft.AspNetCore.Http;

namespace AuthMicroservice.Core.Services;

internal sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
                            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? IpAddress
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null)
            {
                return null;
            }

            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
                !string.IsNullOrWhiteSpace(forwarded.ToString()))
            {
                return forwarded.ToString().Split(',')[0].Trim();
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }
}
