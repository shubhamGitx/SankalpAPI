using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.Model;
using SankalpAPI.Services;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class MessageController : ControllerBase
    {
        private readonly MessageService _messageService;

        public MessageController(MessageService messageService)
        {
            _messageService = messageService;
        }

        /// <summary>
        /// Steps 8, 9 & 10: Fetch personalized messages (only unsynced) from web-app using JSON request body.
        /// Unsynced messages are returned in JSON format and automatically marked as synced in the web-app database.
        /// </summary>
        [HttpPost("GetUnsyncedMessages")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(GetUnsyncedMessagesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetUnsyncedMessagesResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUnsyncedMessages([FromBody] GetUnsyncedMessagesRequest request)
        {
            var response = await _messageService.GetUnsyncedMessagesAsync(request);

            if (!response.Status)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Steps 8, 9 & 10: Fetch personalized messages (only unsynced) from web-app via GET query parameters.
        /// </summary>
        //[HttpGet("GetUnsyncedMessages")]
        //[ProducesResponseType(typeof(GetUnsyncedMessagesResponse), StatusCodes.Status200OK)]
        //[ProducesResponseType(typeof(GetUnsyncedMessagesResponse), StatusCodes.Status400BadRequest)]
        //public async Task<IActionResult> GetUnsyncedMessages([FromQuery] string mobile, [FromQuery] long? portalId = null)
        //{
        //    var response = await _messageService.GetUnsyncedMessagesAsync(mobile, portalId);

        //    if (!response.Status)
        //    {
        //        return BadRequest(response);
        //    }

        //    return Ok(response);
        //}

        /// <summary>
        /// Create a new personalized notification in web-app via POST JSON body (marked as unsynced).
        /// </summary>
        [HttpPost("CreateNotification")]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            var result = await _messageService.CreateNotificationAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Explicitly mark specific notification IDs as synced via POST JSON body.
        /// </summary>
        [HttpPost("MarkSynced")]
        [Consumes("application/json")]
        public async Task<IActionResult> MarkSynced([FromBody] MarkSyncedRequest request)
        {
            var result = await _messageService.MarkMessagesAsSyncedAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// View message history (both synced and unsynced) via POST JSON body.
        /// Supports filtering by multiple interest IDs (e.g., interestIds: [1, 2, 3] or interest_id: "1,2,3,4,5").
        /// </summary>
        [HttpPost("GetInterestWiseMessage")]
        [Consumes("application/json")]
        public async Task<IActionResult> GetHistory([FromBody] GetMessageHistoryRequest request)
        {
            var result = await _messageService.GetMessageHistoryAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Fetch all active broadcast messages from tbl_BroadCastMessage.
        /// No request parameters required.
        /// </summary>
        [HttpPost("GetBroadCastMessage")]
        public async Task<IActionResult> GetBroadCastMessage([FromBody] GetBroadCastMessageRequest request)
        {
            var result = await _messageService.GetBroadcastMessagesAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Fetches the message types available when creating a broadcast notice.
        /// </summary>
        [HttpPost("GetMessageType")]
        public async Task<IActionResult> GetMessageType()
        {
            var result = await _messageService.GetMessageTypesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Insert a new broadcast message into tbl_BroadCastMessage via POST JSON body.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Message/InsertBroadcastMessage
        ///     {
        ///       "message_head": "Advisory / Alert Header",
        ///       "message_body": "Full body content of the broadcast notification...",
        ///       "validity_in_minutes": 60,
        ///       "is_active": "Y",
        ///       "photo1path": "https://example.com/images/alert.jpg",
        ///       "entryby": "Admin"
        ///     }
        /// </remarks>
        [HttpPost("InsertBroadcastMessage")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(InsertBroadcastMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InsertBroadcastMessageResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InsertBroadcastMessage([FromBody] InsertBroadcastMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest(new InsertBroadcastMessageResponse
                {
                    Status = false,
                    Message = "Invalid request payload."
                });
            }

            var result = await _messageService.InsertBroadcastMessageAsync(request);

            if (!result.Status)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("InsertBroadcastMessageWithAttachments")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(55 * 1024 * 1024)]
        [ProducesResponseType(typeof(InsertBroadcastMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InsertBroadcastMessageResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InsertBroadcastMessageWithAttachments(
            [FromForm] InsertBroadcastMessageWithAttachmentsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _messageService.InsertBroadcastMessageWithAttachmentsAsync(request, cancellationToken);
            return result.Status ? Ok(result) : BadRequest(result);
        }

        [HttpPost("ForwardDraftMessages")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ForwardDraftMessagesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ForwardDraftMessagesResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForwardDraftMessages([FromBody] ForwardDraftMessagesRequest? request)
        {
            request ??= new ForwardDraftMessagesRequest();

            string headerRole = Request.Headers["X-User-Role"].ToString();
            string headerUserId = Request.Headers["X-User-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(headerRole)) request.UserRole = headerRole;
            if (!string.IsNullOrWhiteSpace(headerUserId)) request.ActionBy = headerUserId;
            request.ActionIP = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _messageService.ForwardDraftMessagesAsync(request);
            return result.Status ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Updates the verification status (IsVerified) of a broadcast message in tbl_BroadCastMessage.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Message/IsVerified
        ///     {
        ///       "Bmsg_id": 101,
        ///       "IsVerified": true,
        ///       "VerifiedBy": "ABC",
        ///       "VerifiedIP": "10.133.20.147"
        ///     }
        /// </remarks>
        [HttpPost("IsVerified")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IsVerified([FromBody] UpdateBroadcastVerifiedRequest request)
        {
            if (request == null)
            {
                return BadRequest(new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "Invalid request payload."
                });
            }

            if (request.Bmsg_id <= 0)
            {
                return BadRequest(new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "A valid Bmsg_id greater than 0 is required."
                });
            }

            string headerUserId = Request.Headers["X-User-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(headerUserId)) request.VerifiedBy = headerUserId;
            if (string.IsNullOrWhiteSpace(request.VerifiedIP))
            {
                request.VerifiedIP = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            var result = await _messageService.UpdateBroadcastVerifiedAsync(request);

            if (!result.Status)
            {
                if (result.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(result);
                }

                if (result.StatusCode == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Updates the approval status (IsApproved) of a broadcast message in tbl_BroadCastMessage.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Message/IsApproved
        ///     {
        ///       "Bmsg_id": 101,
        ///       "IsApproved": true,
        ///       "ApprovedBy": "ABC",
        ///       "ApprovedIP": "10.133.20.147"
        ///     }
        /// </remarks>
        [HttpPost("IsApproved")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(UpdateBroadcastStatusResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IsApproved([FromBody] UpdateBroadcastApprovedRequest request)
        {
            if (request == null)
            {
                return BadRequest(new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "Invalid request payload."
                });
            }

            if (request.Bmsg_id <= 0)
            {
                return BadRequest(new UpdateBroadcastStatusResponse
                {
                    Status = false,
                    StatusCode = StatusCodes.Status400BadRequest,
                    Message = "A valid Bmsg_id greater than 0 is required."
                });
            }

            string headerUserId = Request.Headers["X-User-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(headerUserId)) request.ApprovedBy = headerUserId;
            if (string.IsNullOrWhiteSpace(request.ApprovedIP))
            {
                request.ApprovedIP = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            var result = await _messageService.UpdateBroadcastApprovedAsync(request);

            if (!result.Status)
            {
                if (result.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(result);
                }

                if (result.StatusCode == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Insert a new interest-wise message into tbl_Message (without using portal_id or mobile columns) via POST JSON body.
        /// Targeted to users based on a single interest_id.
        /// </summary>
        /// <remarks>
        /// Sample Request:
        /// 
        ///     POST /api/Message/InsertInterestWiseMessage
        ///     {
        ///       "interest_id": 5,
        ///       "message_head": "Agriculture Advisory: Monsoon Crop Guidelines",
        ///       "message_body": "Important guidelines for paddy farmers regarding sowing and disease prevention...",
        ///       "validity_in_minutes": 1440,
        ///       "is_active": "Y",
        ///       "photo1path": "https://health.bihar.gov.in/images/crop_advisory.jpg",
        ///       "entryby": "Admin"
        ///     }
        /// </remarks>
        [HttpPost("InsertInterestWiseMessage")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(InsertInterestWiseMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InsertInterestWiseMessageResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> InsertInterestWiseMessage([FromBody] InsertInterestWiseMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest(new InsertInterestWiseMessageResponse
                {
                    Status = false,
                    Message = "Invalid request payload."
                });
            }

            var result = await _messageService.InsertInterestWiseMessageAsync(request);

            if (!result.Status)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Fetch all active interest-wise messages from tbl_Message joined with mst_interest.
        /// Optional interestId parameter to filter by interest.
        /// </summary>
        [HttpGet("GetInterestWiseMessages")]
        [HttpPost("GetInterestWiseMessages")]
        public async Task<IActionResult> GetInterestWiseMessages([FromQuery] long? interestId = null)
        {
            var result = await _messageService.GetInterestWiseMessagesAsync(interestId);
            return Ok(new
            {
                status = true,
                message = "Success",
                count = result.Count,
                data = result
            });
        }



        /// <summary>
        /// View message history (both synced and unsynced) for the given mobile number via GET.
        /// </summary>
        //[HttpGet("GetHistory")]
        //public async Task<IActionResult> GetHistory([FromQuery] string mobile, [FromQuery] long? portalId = null)
        //{
        //    var result = await _messageService.GetMessageHistoryAsync(mobile, portalId);
        //    return Ok(result);
        //}
    }
}
