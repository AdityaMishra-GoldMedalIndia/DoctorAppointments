using System.Security.Claims;
using DoctorAppointments.Api.Contracts;

namespace DoctorAppointments.Api.Infrastructure;

public static class CurrentUserExtensions
{
    public static CurrentUserContext ToCurrentUser(this ClaimsPrincipal user)
    {
        return new CurrentUserContext
        {
            Id = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0"),
            Name = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
        };
    }
}
