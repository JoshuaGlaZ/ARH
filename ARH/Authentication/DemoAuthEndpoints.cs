using ARH.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ARH.Authentication
{

    public static class DemoAuthEndpoints
    {
        public static IEndpointRouteBuilder MapDemoAuthEndpoints(
            this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/dev/auth");

            group.MapPost("/login", async (
                HttpContext context,
                ArhDbContext db) =>
            {
                var form = await context.Request.ReadFormAsync();

                var email = form["email"].ToString();

                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Email == email &&
                        x.IsActive);

                if (user == null)
                {
                    return Results.BadRequest(
                        "Invalid demo user.");
                }

                var claims = new List<Claim>
                {
                new(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new(
                    ClaimTypes.Name,
                    user.FullName),

                new(
                    ClaimTypes.Email,
                    user.Email),

                new(
                    ClaimTypes.Role,
                    user.Role.ToString())
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal);

                return Results.Redirect("/");
            })
            .DisableAntiforgery();

            group.MapPost("/logout", async (
                HttpContext context) =>
            {
                await context.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.Redirect("/login");
            })
            .DisableAntiforgery();

            return app;
        }
    }
}