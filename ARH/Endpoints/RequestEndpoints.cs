
using System.Security.Claims;
using ARH.DTOs;
using ARH.Domain.Enums;
using ARH.Services;

namespace ARH.Endpoints
{
    public static class RequestEndpoints
    {
        public static IEndpointRouteBuilder MapRequestEndpoints(
            this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/requests")
                .RequireAuthorization();

            // CREATE REQUEST

            group.MapPost("/", async (
                CreateRequestDto command,
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                if (!user.IsInRole(UserRole.Requester.ToString()))
                    return Results.Forbid();

                try
                {
                    var result = await service.CreateAsync(
                        actorId,
                        command,
                        ct);

                    var dto = RequestResponseDto.FromEntity(
                        result.Request);

                    if (result.IsReplay)
                        return Results.Ok(dto);

                    return Results.Created(
                        $"/api/requests/{result.Request.Id}",
                        dto);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new
                    {
                        message = ex.Message
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
            });

            // GET MY REQUESTS

            group.MapGet("/my", async (
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                if (!user.IsInRole(UserRole.Requester.ToString()))
                    return Results.Forbid();

                try
                {
                    var requests = await service.GetMyRequestsAsync(
                        actorId,
                        ct);

                    var response = requests.Select(
                        RequestResponseDto.FromEntity);

                    return Results.Ok(response);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

            // GET APPROVAL INBOX

            group.MapGet("/inbox", async (
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                var isManager = user.IsInRole(
                    UserRole.Manager.ToString());

                var isSystemOwner = user.IsInRole(
                    UserRole.SystemOwner.ToString());

                if (!isManager && !isSystemOwner)
                    return Results.Forbid();

                try
                {
                    var requests = await service.GetInboxAsync(
                        actorId,
                        ct);

                    var response = requests.Select(
                        RequestResponseDto.FromEntity);

                    return Results.Ok(response);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

            // GET REQUEST DETAIL

            group.MapGet("/{id:guid}", async (
                Guid id,
                ClaimsPrincipal user,
                AccessRequestService service,
                CancellationToken ct) =>
            {
                if (!TryGetActorId(user, out var actorId))
                    return Results.Unauthorized();

                try
                {
                    var request = await service.GetRequestAsync(
                        actorId,
                        id,
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
            });

            return app;
        }

        // AUTHENTICATED ACTOR

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