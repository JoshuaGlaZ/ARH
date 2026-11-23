using Microsoft.AspNetCore.Antiforgery;

namespace ARH.Middleware
{
        public class AntiforgeryEndpointFilter : IEndpointFilter
        {
                public async ValueTask<object?> InvokeAsync(
                    EndpointFilterInvocationContext context,
                    EndpointFilterDelegate next)
                {
                    var antiforgery = context.HttpContext.RequestServices
                        .GetRequiredService<IAntiforgery>();

                    try
                    {
                        await antiforgery.ValidateRequestAsync(
                            context.HttpContext);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return Results.BadRequest(new
                        {
                            message = "Invalid or missing antiforgery token."
                        });
                    }

                    return await next(context);
                }
        }
}