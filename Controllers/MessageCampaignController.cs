using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SankalpAPI.Interface;
using SankalpAPI.Model;

namespace SankalpAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class MessageCampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;
        private readonly IPortalRepository _portalRepository;
        private readonly IMasterDataService _masterDataService;

        public MessageCampaignController(
            ICampaignService campaignService,
            IPortalRepository portalRepository,
            IMasterDataService masterDataService)
        {
            _campaignService = campaignService;
            _portalRepository = portalRepository;
            _masterDataService = masterDataService;
        }

        private string GetCurrentUserId(string? fallbackUserId = null)
        {
            if (HttpContext.Items.TryGetValue("UserID", out var uidObj) && uidObj != null && !string.IsNullOrWhiteSpace(uidObj.ToString()))
            {
                return uidObj.ToString()!;
            }

            if (Request.Headers.TryGetValue("X-User-Id", out var headerUser) && !string.IsNullOrWhiteSpace(headerUser))
            {
                return headerUser.ToString().Trim();
            }

            return !string.IsNullOrWhiteSpace(fallbackUserId) ? fallbackUserId.Trim() : "System";
        }

        private async Task<string?> GetCurrentUserRoleAsync(string? userId = null)
        {
            if (Request.Headers.TryGetValue("X-User-Role", out var headerRole) && !string.IsNullOrWhiteSpace(headerRole))
            {
                return headerRole.ToString().Trim();
            }

            string uid = !string.IsNullOrWhiteSpace(userId) ? userId : GetCurrentUserId();
            if (!string.IsNullOrWhiteSpace(uid) && !string.Equals(uid, "System", StringComparison.OrdinalIgnoreCase))
            {
                var role = await _campaignService.GetUserRoleAsync(uid);
                if (!string.IsNullOrWhiteSpace(role))
                {
                    return role;
                }
            }

            if (HttpContext.Items.TryGetValue("UserRole", out var rObj) && rObj != null)
            {
                return rObj.ToString();
            }

            return null;
        }

        /// <summary>
        /// 1. Create a message campaign
        /// </summary>
        [HttpPost("Create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { status = false, message = "Request body cannot be null." });
            }

            if (string.IsNullOrWhiteSpace(request.CreatedBy))
            {
                request.CreatedBy = GetCurrentUserId("Admin");
            }

            string? role = await GetCurrentUserRoleAsync(request.CreatedBy);
            if (CampaignRoles.IsChecker(role) || CampaignRoles.IsApprover(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Checkers and Approvers cannot create campaigns. Only Makers (MKR) are authorized." });
            }

            var (isSuccess, message, campaignId, data) = await _campaignService.CreateCampaignAsync(request);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new
            {
                status = true,
                message,
                campaignId,
                data
            });
        }

        /// <summary>
        /// 2. Retrieve campaigns with search filters and pagination
        /// </summary>
        [HttpGet("GetAll")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] CampaignListFilter filter)
        {
            filter ??= new CampaignListFilter();
            filter.CurrentUserId = GetCurrentUserId();
            filter.CurrentUserRole = await GetCurrentUserRoleAsync(filter.CurrentUserId);

            var (items, totalCount) = await _campaignService.GetAllCampaignsAsync(filter);

            return Ok(new
            {
                status = true,
                totalCount,
                page = filter.Page,
                pageSize = filter.PageSize,
                data = items
            });
        }

        /// <summary>
        /// 3. Retrieve campaign details by ID
        /// </summary>
        [HttpGet("GetById/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid campaign ID." });
            }

            var campaign = await _campaignService.GetCampaignByIdAsync(id);
            if (campaign == null)
            {
                return NotFound(new { status = false, message = $"Campaign with ID {id} not found." });
            }

            return Ok(new
            {
                status = true,
                data = campaign
            });
        }

        /// <summary>
        /// 4. Update campaign details (only editable in Draft or Rejected state)
        /// </summary>
        [HttpPut("Update/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateCampaignRequest request)
        {
            if (id <= 0 || request == null)
            {
                return BadRequest(new { status = false, message = "Invalid campaign ID or empty request." });
            }

            if (string.IsNullOrWhiteSpace(request.UpdatedBy))
            {
                request.UpdatedBy = GetCurrentUserId("Admin");
            }

            string? role = await GetCurrentUserRoleAsync(request.UpdatedBy);
            if (CampaignRoles.IsChecker(role) || CampaignRoles.IsApprover(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Checkers and Approvers cannot edit campaigns. Only Makers (MKR) are authorized." });
            }

            var (isSuccess, message) = await _campaignService.UpdateCampaignAsync(id, request);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new { status = true, message });
        }

        /// <summary>
        /// 5. Verify the associated message or campaign
        /// </summary>
        [HttpPut("IsVerified")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> IsVerified([FromBody] VerifyCampaignRequest request)
        {
            if (request == null || request.CampaignId <= 0)
            {
                return BadRequest(new { status = false, message = "Valid Campaign ID is required." });
            }

            if (string.IsNullOrWhiteSpace(request.VerifiedBy))
            {
                request.VerifiedBy = GetCurrentUserId("Verifier");
            }

            string? role = await GetCurrentUserRoleAsync(request.VerifiedBy);
            if (CampaignRoles.IsMaker(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Makers (MKR) cannot verify campaigns. Only Checkers (CKR) are authorized." });
            }
            if (CampaignRoles.IsApprover(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Approvers (APR) cannot verify campaigns. Only Checkers (CKR) are authorized." });
            }

            var (isSuccess, message) = await _campaignService.VerifyCampaignAsync(request, role);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new { status = true, message });
        }

        /// <summary>
        /// 6. Approve the associated message or campaign (Enforces that creator cannot approve)
        /// </summary>
        [HttpPut("IsApproved")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> IsApproved([FromBody] ApproveCampaignRequest request)
        {
            if (request == null || request.CampaignId <= 0)
            {
                return BadRequest(new { status = false, message = "Valid Campaign ID is required." });
            }

            if (string.IsNullOrWhiteSpace(request.ApprovedBy))
            {
                request.ApprovedBy = GetCurrentUserId("Approver");
            }

            string? role = await GetCurrentUserRoleAsync(request.ApprovedBy);
            if (CampaignRoles.IsMaker(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Makers (MKR) cannot approve campaigns." });
            }
            if (CampaignRoles.IsChecker(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Checkers (CKR) cannot approve campaigns. Only Approvers (APR) are authorized." });
            }

            var (isSuccess, message) = await _campaignService.ApproveCampaignAsync(request, role);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new { status = true, message });
        }

        /// <summary>
        /// 7. Send an approved campaign
        /// </summary>
        [HttpPost("Send/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Send(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid campaign ID." });
            }

            string sender = GetCurrentUserId("Sender");
            string? role = await GetCurrentUserRoleAsync(sender);
            if (CampaignRoles.IsChecker(role) || CampaignRoles.IsApprover(role))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { status = false, message = "Permission Denied: Checkers and Approvers cannot send campaign messages. Only Makers (MKR) are authorized." });
            }

            var (isSuccess, message) = await _campaignService.SendCampaignAsync(id, sender);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new { status = true, message });
        }

        /// <summary>
        /// 8. Cancel a campaign that has not finished sending
        /// </summary>
        [HttpPost("Cancel/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Cancel(long id, [FromBody] CancelCampaignRequest? request)
        {
            request ??= new CancelCampaignRequest();
            request.CampaignId = id;

            if (string.IsNullOrWhiteSpace(request.CancelledBy))
            {
                request.CancelledBy = GetCurrentUserId("Admin");
            }

            var (isSuccess, message) = await _campaignService.CancelCampaignAsync(request);
            if (!isSuccess)
            {
                return BadRequest(new { status = false, message });
            }

            return Ok(new { status = true, message });
        }

        /// <summary>
        /// 9. Retrieve delivery statistics and recipient-level logs
        /// </summary>
        [HttpGet("DeliveryReport/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeliveryReport(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid campaign ID." });
            }

            var report = await _campaignService.GetDeliveryReportAsync(id);
            if (report == null)
            {
                return NotFound(new { status = false, message = $"Delivery report not found for Campaign {id}." });
            }

            return Ok(new { status = true, data = report });
        }

        /// <summary>
        /// 10. Retrieve complete audit trail history for a campaign
        /// </summary>
        [HttpGet("AuditHistory/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AuditHistory(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid campaign ID." });
            }

            var history = await _campaignService.GetAuditHistoryAsync(id);
            return Ok(new { status = true, data = history });
        }

        /// <summary>
        /// Estimate candidate audience size based on target criteria before sending
        /// </summary>
        [HttpPost("EstimateRecipients")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> EstimateRecipients([FromBody] EstimateRecipientsRequest request)
        {
            request ??= new EstimateRecipientsRequest();
            var result = await _campaignService.EstimateRecipientsAsync(request);
            return Ok(new { status = true, data = result });
        }

        /// <summary>
        /// Helper: Get list of active portals for campaign targeting
        /// </summary>
        [HttpGet("GetPortals")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPortals()
        {
            var portals = await _portalRepository.GetActivePortalsAsync();
            return Ok(new { status = true, data = portals });
        }

        /// <summary>
        /// Helper: Get user types for selected portal
        /// </summary>
        [HttpGet("GetUserTypes/{portalId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserTypes(long portalId)
        {
            var userTypes = await _portalRepository.GetUserTypeByIdAsync(portalId);
            return Ok(new { status = true, data = userTypes });
        }

        /// <summary>
        /// Helper: Get districts for location targeting
        /// </summary>
        [HttpGet("GetDistricts")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetDistricts()
        {
            var districts = _masterDataService.GetDistrictList();
            return Ok(new { status = true, data = districts });
        }

        /// <summary>
        /// Helper: Get blocks by district code
        /// </summary>
        [HttpGet("GetBlocks/{distCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetBlocks(string distCode)
        {
            var blocks = _masterDataService.GetBlockList(distCode);
            return Ok(new { status = true, data = blocks });
        }
    }
}
