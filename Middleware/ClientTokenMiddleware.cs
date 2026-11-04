using Microsoft.AspNetCore.Authorization;
using SankalpAPI.Interface;
using SankalpAPI.Security;

namespace SankalpAPI.Middleware
{
    public class ClientTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientTokenMiddleware> _logger;

        public ClientTokenMiddleware(RequestDelegate next, ILogger<ClientTokenMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IClientTokenRepository tokenRepository)
        {
            // Allow CORS preflight
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Endpoints marked with [AllowAnonymous] do not require ClientToken
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }

            // Public endpoints that do not require ClientToken
            if (context.Request.Path.StartsWithSegments("/api/Auth/Login", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Users/Login", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/User/Insert", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/User/Register", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Portal", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Applicant", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Totp", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Otp", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/devices", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/Message", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Swagger does not require ClientToken
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            // Get ClientToken
            if (!context.Request.Headers.TryGetValue("X-Client-Token", out var token))
            {
                await Unauthorized(context, "ClientToken is required.");

                return;
            }

            string clientToken = token.ToString();

            if (string.IsNullOrWhiteSpace(clientToken))
            {
                await Unauthorized(context, "Invalid ClientToken.");

                return;
            }

            // Hash token
            string tokenHash;

            try
            {
                tokenHash = ClientTokenHasher.Hash(clientToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid ClientToken.");

                await Unauthorized(context, "Invalid ClientToken.");

                return;
            }

            // Validate token from database
            string? userId;

            try
            {
                userId = await tokenRepository.ValidateTokenAsync(tokenHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ClientToken.");

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        status = false,
                        message = "Unable to validate ClientToken."
                    });

                return;
            }

            // Invalid / expired token
            if (string.IsNullOrWhiteSpace(userId))
            {
                await Unauthorized(context, "Invalid or expired ClientToken.");

                return;
            }

            // Store authenticated UserID
            context.Items["UserID"] = userId;

            // Continue request
            await _next(context);
        }

        private static async Task Unauthorized(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    status = false,
                    message = message
                });
        }
    }
}
