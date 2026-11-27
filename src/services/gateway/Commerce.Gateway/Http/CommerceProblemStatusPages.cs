using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Errors;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>Supplies frozen problem bodies for framework authentication/authorization/not-found short circuits.</summary>
public static class CommerceProblemStatusPages
{
    /// <summary>Adds problem documents only when no endpoint body was written.</summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The pipeline.</returns>
    public static IApplicationBuilder UseCommerceProblemStatusPages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseStatusCodePages(async context =>
        {
            var status = context.HttpContext.Response.StatusCode;
            var code = status switch
            {
                StatusCodes.Status401Unauthorized => CommerceErrorCodes.Unauthenticated,
                StatusCodes.Status403Forbidden => CommerceErrorCodes.Forbidden,
                StatusCodes.Status404NotFound => CommerceErrorCodes.NotFound,
                _ => null,
            };
            if (code is not null)
            {
                await CommerceHttpResults.WriteProblem(context.HttpContext, CommerceProblemFactory.FromCode(code))
                    .ExecuteAsync(context.HttpContext)
                    .ConfigureAwait(false);
            }
        });
    }
}
