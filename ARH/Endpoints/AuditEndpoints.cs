using System.Security.Claims;
using ARH.DTOs;
using ARH.Services;

namespace ARH.Endpoints
{
    public static class AuditEndpoints
    {
        public static IEndpointRouteBuilder MapAuditEndpoints(
            this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api")
                .RequireAuthorization();

            // Request audit history
            group.MapGet("/requests/{id:guid}/history", async (
                Guid id,
                ClaimsPrincipal user,
                AuditService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                try
                {
                    var logs = await service.GetRequestHistoryAsync(
                        actorId,
                        id,
                        ct);

                    return Results.Ok(
                        logs.Select(AuditLogDto.FromEntity));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new
                    {
                        message = "Request not found."
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

            // All audit logs - Admin/Auditor only
            group.MapGet("/audit-events", async (
                ClaimsPrincipal user,
                AuditService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                try
                {
                    var logs = await service.GetAuditLogsAsync(
                        actorId,
                        ct);

                    return Results.Ok(
                        logs.Select(AuditLogDto.FromEntity));
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

            return app;
        }

            private static bool TryGetActorId(
                ClaimsPrincipal user,
                out Guid actorId)
            {
                var value = user.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                return Guid.TryParse(value, out actorId);
            }
    }
}