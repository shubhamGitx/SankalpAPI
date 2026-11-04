using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.Interface;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class DevicesController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public DevicesController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.DeviceId) ||
                string.IsNullOrWhiteSpace(request.FcmToken) ||
                string.IsNullOrWhiteSpace(request.Platform))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "DeviceId, FcmToken and Platform are required."
                });
            }

            // Authenticated via ClientTokenMiddleware (X-Client-Token header)
            // or fallback to request.UserId, X-User-Id header, or DeviceId for initial registration
            var userId = HttpContext.Items["UserID"]?.ToString()
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
            {
                if (!string.IsNullOrWhiteSpace(request.UserId))
                {
                    userId = request.UserId.Trim();
                }
                else if (Request.Headers.TryGetValue("X-User-Id", out var headerUser) && !string.IsNullOrWhiteSpace(headerUser))
                {
                    userId = headerUser.ToString().Trim();
                }
                else
                {
                    userId = request.DeviceId.Trim();
                }
            }

            string deviceId = request.DeviceId.Trim();
            string fcmToken = request.FcmToken.Trim();
            string platform = request.Platform.Trim();

            if (deviceId.Length > 200 ||
                fcmToken.Length > 2048 ||
                platform.Length > 20 ||
                (!string.Equals(platform, "Android", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(platform, "iOS", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(platform, "Web", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid device registration details."
                });
            }

            bool success = await _userRepository.UpsertUserFcmDeviceAsync(
                userId,
                deviceId,
                fcmToken,
                platform,
                cancellationToken);

            if (!success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "Failed to register device token."
                });
            }

            return Ok(new
            {
                success = true,
                message = "Device registered successfully."
            });
        }
    }

    public class RegisterDeviceRequest
    {
        public string? UserId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string FcmToken { get; set; } = string.Empty;
        public string Platform { get; set; } = "Android";
    }
}
