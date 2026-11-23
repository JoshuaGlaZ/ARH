
using ARH.DTOs;
using ARH.Middleware;
using ARH.Services;
using System.Security.Claims;

namespace ARH.Endpoints
{
    public static class ApprovalEndpoints
    {
        public static IEndpointRouteBuilder MapApprovalEndpoints(
            this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/requests")
                .RequireAuthorization();

            // APPROVE
            group.MapPost("/{id:guid}/approve", async (
                Guid id,
                ApprovalActionDto command,
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                if (command.ExpectedVersion < 1)
                    return Results.BadRequest(new
                    {
                        message = "ExpectedVersion must be greater than zero."
                    });

                try
                {
                    var request = await service.ApproveAsync(
                        actorId,
                        id,
                        command.ExpectedVersion,
                        ct);

                    return Results.Ok(
                        RequestResponseDto.FromEntity(request));
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
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new
                    {
                        message = ex.Message
                    });
                }
            })
                .AddEndpointFilter<AntiforgeryEndpointFilter>(); 

            // REJECT
            group.MapPost("/{id:guid}/reject", async (
                Guid id,
                ApprovalActionDto command,
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                if (command.ExpectedVersion < 1)
                    return Results.BadRequest(new
                    {
                        message = "ExpectedVersion must be greater than zero."
                    });

                if (string.IsNullOrWhiteSpace(command.Reason))
                    return Results.BadRequest(new
                    {
                        message = "Rejection reason is required."
                    });

                try
                {
                    var request = await service.RejectAsync(
                        actorId,
                        id,
                        command.ExpectedVersion,
                        command.Reason,
                        ct);

                    return Results.Ok(
                        RequestResponseDto.FromEntity(request));
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
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new
                    {
                        message = ex.Message
                    });
                }
            })
                .AddEndpointFilter<AntiforgeryEndpointFilter>(); 

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