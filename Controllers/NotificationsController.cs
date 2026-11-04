using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.DTO;
using SankalpAPI.Helper;
using SankalpAPI.Interface;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class NotificationsController : ControllerBase
    {
        private readonly IFcmNotificationService _fcmNotificationService;
        private readonly IUserRepository _userRepository;
        private readonly ExceptionLogging _exceptionLogging;

        public NotificationsController(
            IFcmNotificationService fcmNotificationService,
            IUserRepository userRepository,
            ExceptionLogging exceptionLogging)
        {
            _fcmNotificationService = fcmNotificationService;
            _userRepository = userRepository;
            _exceptionLogging = exceptionLogging;
        }

        /// <summary>
        /// Diagnostic endpoint to verify Firebase Admin SDK initialization status and credentials on the server.
        /// Can be accessed anonymously to quickly diagnose deployment issues.
        /// </summary>
        [HttpGet("firebase-status")]
        [AllowAnonymous]
        public IActionResult GetFirebaseStatus([FromServices] IConfiguration configuration)
        {
            var diagnostics = FirebaseInitializer.GetDiagnostics(configuration);
            return Ok(diagnostics);
        }

        /// <summary>
        /// Sends a push notification to all active FCM devices registered for a specific user ID.
        /// Protected endpoint: Requires valid X-Client-Token header and administrator or trusted backend authorization.
        /// </summary>
        /// <param name="request">Notification payload including target UserId, Title, Body, optional ImageUrl and custom Data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Delivery response with device counts and status</returns>
        [HttpPost("send-to-user")]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(typeof(SendUserNotificationResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendToUser(
            [FromBody] SendUserNotificationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Authenticated caller via ClientTokenMiddleware
                var authenticatedUserId = HttpContext.Items["UserID"]?.ToString()
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

                if (string.IsNullOrWhiteSpace(authenticatedUserId))
                {
                    return StatusCode(StatusCodes.Status401Unauthorized, new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "ClientToken is required to access notification services."
                    });
                }

                // Role authorization check (admin or trusted backend only)
                bool isAuthorized = await _userRepository.IsAuthorizedNotificationSenderAsync(
                    authenticatedUserId,
                    cancellationToken);

                if (!isAuthorized)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "Only authorized administrators or trusted backend processes may send notifications."
                    });
                }

                if (request == null || !ModelState.IsValid)
                {
                    return BadRequest(new SendUserNotificationResponse
                    {
                        Success = false,
                        Message = "Invalid notification request data."
                    });
                }

                var result = await _fcmNotificationService.SendToUserAsync(request, cancellationToken);
                return StatusCode(result.StatusCode, result.Data);
            }
            catch (Exception ex)
            {
                _exceptionLogging.SendErrorToText(ex);

                return StatusCode(StatusCodes.Status500InternalServerError, new SendUserNotificationResponse
                {
                    Success = false,
                    Message = "Unable to process notification request."
                });
            }
        }
    }
}
