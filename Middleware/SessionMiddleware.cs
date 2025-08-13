using System.Security.Claims;
using ChatApp.Services;

namespace ChatApp.Middleware
{
    public class SessionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionMiddleware> _logger;

        public SessionMiddleware(RequestDelegate next, ILogger<SessionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IUserService userService)
        {
            // Authentication kontrolü
            if (context.User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var userId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                    var username = context.User.Identity.Name;
                    
                    if (userId > 0)
                    {
                        // Session activity güncelle
                        context.Session.SetString("LastActivity", DateTime.UtcNow.ToString("O"));
                        context.Session.SetString("UserId", userId.ToString());
                        context.Session.SetString("Username", username ?? "");
                        
                        // Kullanıcının aktif olduğunu belirt
                        await userService.UpdateLastActiveAsync(userId);
                        
                        _logger.LogDebug($"Session activity updated for user {username} (ID: {userId})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating session activity");
                }
            }

            await _next(context);
        }
    }
}
